#Requires -Version 7.0
# Internal replay test stage: exact production candidate bytes except the qualified load-muted native.
param(
    [Parameter(Mandatory)][string]$BaseStage,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$BaseManifestSha256,
    [Parameter(Mandatory)][string]$ResultStage,
    [Parameter(Mandatory)][string]$OwnerReceiptPath,
    [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{40}$')][string]$ManagedSourceCommit,
    [string]$NativeDll='E:/Source/dbce-project-mgmt/artifacts/wheelffb-noforce-20261013/x64/WheelFfb.dll',
    [string]$NativeProvenance='E:/Source/dbce-project-mgmt/artifacts/wheelffb-noforce-20261013/x64/WheelFfb.noforce.provenance.json'
)
$ErrorActionPreference='Stop'
$ownerReceiptHash='FE2BABFA962467EC82C31C0D677476828AAA4C1125FB4882DB1AE8892D9BA440'
$baseNative='F8361A08BBE08EAAB936B37800D308CF1FC7EDAB34E2839DB16A447FE34178C5'
$testNative='088F1089BEB6D18D478F37582C59330C6BDA6BC37D5E365F6BA7EBF80040F567'
$provenanceHash='6068B7A848F85FE42AE88EE8A86B10F1EF4C4E307B9C4235BE44C1A84BBEA8E5'
function Full([string]$Path) { [IO.Path]::GetFullPath($Path).TrimEnd('\','/') }
function Pin([string]$Path,[string]$Hash) {
    if(!(Test-Path -LiteralPath $Path -PathType Leaf) -or (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Hash){throw "Pinned input differs: $Path"}
}
function Child([string]$Root,[string]$Relative) {
    if(!$Relative -or [IO.Path]::IsPathRooted($Relative) -or $Relative -match '(^|[\\/])\.\.([\\/]|$)'){throw 'Unsafe package path.'}
    $path=Full (Join-Path $Root $Relative)
    if(!$path.StartsWith($Root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Package path escapes stage.'}
    $path
}
$BaseStage=Full $BaseStage;$ResultStage=Full $ResultStage;$OwnerReceiptPath=Full $OwnerReceiptPath
$NativeDll=Full $NativeDll;$NativeProvenance=Full $NativeProvenance
$dist=Full (Join-Path $PSScriptRoot '../../components/wheel/dist')
if(!$ResultStage.StartsWith($dist+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
   [IO.Path]::GetFileName($ResultStage) -notmatch '^stage-automation-hudspeed-[a-z0-9-]+$' -or
   (Test-Path -LiteralPath $ResultStage)){throw 'Choose a new stage-automation-hudspeed-* child of components/wheel/dist.'}
foreach($root in @($BaseStage,(Split-Path $ResultStage -Parent))){
    if(!(Test-Path -LiteralPath $root -PathType Container) -or ((Get-Item -LiteralPath $root).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Stage root is missing or linked.'}
}
Pin (Join-Path $BaseStage 'manifest.json') $BaseManifestSha256
Pin $OwnerReceiptPath $ownerReceiptHash
Pin $NativeDll $testNative;Pin $NativeProvenance $provenanceHash
$owner=Get-Content -LiteralPath $OwnerReceiptPath -Raw | ConvertFrom-Json
if($owner.mode -cne 'Install' -or $owner.installerRevision -ne 4 -or @($owner.files).Count -ne 17){throw 'Owner receipt identity differs.'}
$manifest=@(Get-Content -LiteralPath (Join-Path $BaseStage 'manifest.json') -Raw | ConvertFrom-Json)
$seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($entry in $manifest){
    $path=Child $BaseStage ([string]$entry.path)
    if(!$seen.Add([string]$entry.path) -or $entry.sha256 -notmatch '^[a-fA-F0-9]{64}$'){throw 'Production manifest repeats or malforms a path.'}
    Pin $path $entry.sha256
}
$items=@(Get-ChildItem -LiteralPath $BaseStage -Recurse -Force)
if(@($items | Where-Object {-not $_.PSIsContainer}).Count -ne $manifest.Count+1 -or
   @($items | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count){throw 'Production stage has unlisted or linked files.'}
$prefix='BepInEx/plugins/WodenRallyEdgeWheel/'
$owned=@($manifest | Where-Object {$_.path.Replace('\','/') -like "$prefix*"})
$ownerPaths=@($owner.files | ForEach-Object {$_.path.Replace('\','/')})
if($owned.Count -ne 17 -or @($owned | Where-Object {$ownerPaths -cnotcontains $_.path.Replace('\','/')}).Count){throw 'Production candidate plugin membership differs from owner.'}
$nativeRelative=$prefix+'WheelFfb.dll'
$nativeEntry=@($manifest | Where-Object {$_.path.Replace('\','/') -ceq $nativeRelative})
if($nativeEntry.Count -ne 1 -or $nativeEntry[0].sha256 -ne $baseNative){throw 'Production native payload differs.'}
$plugin=Child $BaseStage ($prefix+'WodenRallyEdgeWheel.dll')
if((Get-Item -LiteralPath $plugin).VersionInfo.ProductVersion -notmatch [regex]::Escape($ManagedSourceCommit)){throw 'Managed candidate embeds a different source revision.'}
$compositionCommit=(& git -C (Join-Path $PSScriptRoot '../..') rev-parse HEAD).Trim()
if($LASTEXITCODE -ne 0 -or $compositionCommit -notmatch '^[a-f0-9]{40}$' -or
   @(& git -C (Join-Path $PSScriptRoot '../..') status --porcelain --untracked-files=no).Count){throw 'Composition requires clean committed source.'}
[IO.Directory]::CreateDirectory($ResultStage) | Out-Null
foreach($entry in $manifest){
    $source=Child $BaseStage ([string]$entry.path)
    $target=Child $ResultStage ([string]$entry.path)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
    [IO.File]::Copy($source,$target)
}
$nativeTarget=Child $ResultStage $nativeRelative
[IO.File]::Copy($NativeDll,$nativeTarget,$true)
$capPath=Child $ResultStage 'private-shim-capability.json'
$cap=Get-Content -LiteralPath $capPath -Raw | ConvertFrom-Json
if($cap.controls.nativeSha256 -ne $baseNative.ToLowerInvariant()){throw 'Production private capability native pin differs.'}
$cap.controls.nativeSha256=$testNative.ToLowerInvariant()
$cap | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $capPath -Encoding utf8NoBOM
[IO.File]::Copy($NativeProvenance,(Child $ResultStage 'WheelFfb.noforce.provenance.json'))
[ordered]@{
    schema='woden.automation-muted-native@1';purpose='internal HUD replay test only; restore owner afterward; never release or retain as owner install'
    sourceCommit=$ManagedSourceCommit;managedPayloadSourceCommit=$ManagedSourceCommit;compositionCommit=$compositionCommit
    baseManifestSha256=$BaseManifestSha256.ToLowerInvariant();ownerReceiptSha256=$ownerReceiptHash.ToLowerInvariant()
    baseNativeSha256=$baseNative.ToLowerInvariant();nativeSha256=$testNative.ToLowerInvariant()
    nativeDefaultState='armed';requiresExplicitArm=$false;toolkitNativeCommit='eb77b909416b8a6ef70787e4621aaa62f16b9f45'
    toolkitNativeProvenanceSha256=$provenanceHash.ToLowerInvariant();compositionScriptSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash.ToLowerInvariant()
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Child $ResultStage 'automation-native-provenance.json') -Encoding utf8NoBOM
'INTERNAL AUTOMATION ONLY. Qualified load-muted native with exact production managed HUD-speed payloads; restore owner after test.' |
    Set-Content -LiteralPath (Child $ResultStage 'AUTOMATION-ONLY.txt') -Encoding utf8NoBOM
$output=@(Get-ChildItem -LiteralPath $ResultStage -Recurse -File | Sort-Object FullName | ForEach-Object {
    [ordered]@{path=[IO.Path]::GetRelativePath($ResultStage,$_.FullName);sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
})
$output | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $ResultStage 'manifest.json') -Encoding utf8NoBOM
foreach($entry in $manifest){
    $relative=$entry.path.Replace('\','/')
    if($relative -in @($nativeRelative,'private-shim-capability.json')){continue}
    Pin (Child $ResultStage $relative) $entry.sha256
}
Pin $nativeTarget $testNative
Write-Output "Internal HUD replay stage: $ResultStage"
Write-Output "Manifest SHA-256: $((Get-FileHash -LiteralPath (Join-Path $ResultStage 'manifest.json') -Algorithm SHA256).Hash)"
