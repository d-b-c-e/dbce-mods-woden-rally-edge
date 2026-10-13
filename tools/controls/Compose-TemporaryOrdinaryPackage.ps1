#Requires -Version 7.0
# Compose an internal test stage from the exact frozen a9 owner package.
# Managed payloads and their embedded source revision remain byte-identical.
param(
    [Parameter(Mandatory)][string]$BaseStage,
    [Parameter(Mandatory)][string]$ResultStage,
    [Parameter(Mandatory)][string]$OwnerReceiptPath,
    [switch]$VerifyOnly,
    [string]$NativeDll='E:/Source/dbce-project-mgmt/artifacts/wheelffb-noforce-20261013/x64/WheelFfb.dll',
    [string]$NativeProvenance='E:/Source/dbce-project-mgmt/artifacts/wheelffb-noforce-20261013/x64/WheelFfb.noforce.provenance.json'
)
$ErrorActionPreference='Stop'
$baseManifestHash='60397A0FDF7E26BF56A5ECE2F166A19FF5C94BEFCBBE914EB8B4527091CF1C2C'
$ownerReceiptHash='FE2BABFA962467EC82C31C0D677476828AAA4C1125FB4882DB1AE8892D9BA440'
$baseSource='a9a255c3277cb0ae6e8ec11f18b2f0c17b3c22a9'
$baseNative='F8361A08BBE08EAAB936B37800D308CF1FC7EDAB34E2839DB16A447FE34178C5'
$testNative='088F1089BEB6D18D478F37582C59330C6BDA6BC37D5E365F6BA7EBF80040F567'
$provenanceHash='6068B7A848F85FE42AE88EE8A86B10F1EF4C4E307B9C4235BE44C1A84BBEA8E5'
function Full([string]$Path) { [IO.Path]::GetFullPath($Path).TrimEnd('\','/') }
function Pin([string]$Path,[string]$Hash) {
    if(!(Test-Path -LiteralPath $Path -PathType Leaf) -or (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Hash){throw "Frozen package input differs: $Path"}
}
function StagePath([string]$Root,[string]$Relative) {
    if(!$Relative -or [IO.Path]::IsPathRooted($Relative) -or $Relative -match '(^|[\\/])\.\.([\\/]|$)') {throw 'Unsafe package path.'}
    $path=[IO.Path]::GetFullPath((Join-Path $Root $Relative))
    if(!$path.StartsWith($Root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Package path escapes stage.'}
    return $path
}
$BaseStage=Full $BaseStage;$ResultStage=Full $ResultStage;$OwnerReceiptPath=Full $OwnerReceiptPath
$NativeDll=Full $NativeDll;$NativeProvenance=Full $NativeProvenance
$dist=Full (Join-Path $PSScriptRoot '../../components/wheel/dist')
if(!$ResultStage.StartsWith($dist+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
   [IO.Path]::GetFileName($ResultStage) -notmatch '^stage-automation-[a-z0-9-]+$' -or
   (Test-Path -LiteralPath $ResultStage)){throw 'Choose a new stage-automation-* child of components/wheel/dist.'}
foreach($root in @($BaseStage,(Split-Path $ResultStage -Parent))){
    if(!(Test-Path -LiteralPath $root -PathType Container) -or ((Get-Item -LiteralPath $root).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Stage root is missing or linked.'}
}
Pin (Join-Path $BaseStage 'manifest.json') $baseManifestHash
Pin $OwnerReceiptPath $ownerReceiptHash
Pin $NativeDll $testNative;Pin $NativeProvenance $provenanceHash
$receipt=Get-Content -LiteralPath $OwnerReceiptPath -Raw | ConvertFrom-Json
if($receipt.mode -cne 'Install' -or $receipt.installerRevision -ne 4 -or @($receipt.files).Count -ne 17 -or
   $receipt.version -notmatch [regex]::Escape($baseSource)){throw 'Frozen owner receipt identity differs.'}
$manifest=@(Get-Content -LiteralPath (Join-Path $BaseStage 'manifest.json') -Raw | ConvertFrom-Json)
$seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($entry in $manifest){
    $path=StagePath $BaseStage ([string]$entry.path)
    if(!$seen.Add([string]$entry.path) -or $entry.sha256 -notmatch '^[a-fA-F0-9]{64}$'){throw 'Frozen package manifest repeats or malforms a path.'}
    Pin $path $entry.sha256
}
$items=@(Get-ChildItem -LiteralPath $BaseStage -Recurse -Force)
$actual=@($items | Where-Object { -not $_.PSIsContainer })
if($actual.Count -ne $manifest.Count+1 -or @($items | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count){
    throw 'Frozen package stage has unlisted or linked files.'
}
$prefix='BepInEx/plugins/WodenRallyEdgeWheel/'
foreach($owned in $receipt.files){
    $relative=$owned.path.Replace('\','/')
    $matches=@($manifest | Where-Object { $_.path.Replace('\','/') -ceq $relative })
    if($matches.Count -ne 1 -or $matches[0].sha256 -ne $owned.sha256){throw "Frozen owner payload differs from base stage: $relative"}
}
$nativeRelative=$prefix+'WheelFfb.dll'
$nativeEntry=@($manifest | Where-Object { $_.path.Replace('\','/') -ceq $nativeRelative })
if($nativeEntry.Count -ne 1 -or $nativeEntry[0].sha256 -ne $baseNative){throw 'Frozen stage native payload differs.'}
$plugin=StagePath $BaseStage ($prefix+'WodenRallyEdgeWheel.dll')
if((Get-Item -LiteralPath $plugin).VersionInfo.ProductVersion -notmatch [regex]::Escape($baseSource)){
    throw 'Frozen managed payload has a different embedded source revision.'
}
if($VerifyOnly){
    Write-Output "PASS frozen-a9 base: $($manifest.Count) manifest files, 17 owner payloads, exact embedded managed revision; no stage created."
    return
}
$compositionCommit=(& git -C (Join-Path $PSScriptRoot '../..') rev-parse HEAD).Trim()
if($LASTEXITCODE -ne 0 -or $compositionCommit -notmatch '^[a-f0-9]{40}$' -or
   @(& git -C (Join-Path $PSScriptRoot '../..') status --porcelain --untracked-files=no).Count){
    throw 'Composition requires a clean, commit-identified review script.'
}
[IO.Directory]::CreateDirectory($ResultStage) | Out-Null
foreach($entry in $manifest){
    $source=StagePath $BaseStage ([string]$entry.path)
    $target=StagePath $ResultStage ([string]$entry.path)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
    [IO.File]::Copy($source,$target)
}
$nativeTarget=StagePath $ResultStage $nativeRelative
[IO.File]::Copy($NativeDll,$nativeTarget,$true)
$capPath=StagePath $ResultStage 'private-shim-capability.json'
$cap=Get-Content -LiteralPath $capPath -Raw | ConvertFrom-Json
if($cap.controls.nativeSha256 -ne $baseNative.ToLowerInvariant()){throw 'Frozen private capability native pin differs.'}
$cap.controls.nativeSha256=$testNative.ToLowerInvariant()
$cap | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $capPath -Encoding utf8NoBOM
[IO.File]::Copy($NativeProvenance,(StagePath $ResultStage 'WheelFfb.noforce.provenance.json'))
[ordered]@{
    schema='woden.automation-muted-native@1';purpose='internal hash-pinned temporary test deployment only; restore the product afterward; never a release or lasting owner install'
    sourceCommit=$baseSource;managedPayloadSourceCommit=$baseSource;compositionCommit=$compositionCommit
    baseManifestSha256=$baseManifestHash.ToLowerInvariant();ownerReceiptSha256=$ownerReceiptHash.ToLowerInvariant()
    baseNativeSha256=$baseNative.ToLowerInvariant();nativeSha256=$testNative.ToLowerInvariant()
    nativeDefaultState='armed';requiresExplicitArm=$false;toolkitNativeCommit='eb77b909416b8a6ef70787e4621aaa62f16b9f45'
    toolkitNativeProvenanceSha256=$provenanceHash.ToLowerInvariant();compositionScriptSha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (StagePath $ResultStage 'automation-native-provenance.json') -Encoding utf8NoBOM
'INTERNAL AUTOMATION ONLY. Frozen a9 managed payloads plus the qualified load-muted native; restore the owner package after the trial. Never a release or lasting install.' |
    Set-Content -LiteralPath (StagePath $ResultStage 'AUTOMATION-ONLY.txt') -Encoding utf8NoBOM
$output=@(Get-ChildItem -LiteralPath $ResultStage -Recurse -File | Sort-Object FullName | ForEach-Object {
    [ordered]@{path=[IO.Path]::GetRelativePath($ResultStage,$_.FullName);sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
})
$output | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $ResultStage 'manifest.json') -Encoding utf8NoBOM
foreach($owned in $receipt.files){
    $relative=$owned.path.Replace('\','/')
    if($relative -cne $nativeRelative){Pin (StagePath $ResultStage $relative) $owned.sha256}
}
Pin $nativeTarget $testNative
Write-Output "Frozen-a9 internal automation stage: $ResultStage"
Write-Output "Manifest SHA-256: $((Get-FileHash -LiteralPath (Join-Path $ResultStage 'manifest.json') -Algorithm SHA256).Hash)"
