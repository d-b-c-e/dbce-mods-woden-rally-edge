#Requires -Version 7.0
# Closed-game outer transaction for the existing applied-car controls trial.
param(
    [Parameter(Mandatory)][string]$Result,
    [Parameter(Mandatory)][string]$PackageRoot,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$PackageManifestSha256,
    [Parameter(Mandatory)][string]$PrivateWriter,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$PrivateWriterSha256,
    [Parameter(Mandatory)][string]$PrivateCatalog,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$PrivateCatalogSha256,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ProbeSha256,
    [string]$GameDir='D:/Program Files (x86)/Steam/steamapps/common/Super Woden Rally Edge',
    [string]$Profiles=(Join-Path $env:LOCALAPPDATA 'Wheelkit/mapping-profiles.json')
)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/OwnerFiles.ps1"
. "$PSScriptRoot/Environment.ps1"
. "$PSScriptRoot/../../vendor/playback/Stage-RigLease.ps1"
function Full([string]$Path) { [IO.Path]::GetFullPath($Path).TrimEnd('\','/') }
function Closed { if(Get-Process -Name 'Super Woden Rally Edge' -ErrorAction SilentlyContinue){throw 'Woden must be closed; retain owner state and lease.'} }
function Pin([string]$Path,[string]$Hash) { Assert-ControlPlain $Path; if((Get-ControlHash $Path) -ne $Hash){throw "Pinned file differs: $Path"} }
function Assert-Inventory($Before,$Roots,$Files) {
    $actual=@(Get-ControlInventory $Roots $Files | ForEach-Object { [pscustomobject]@{key=$_.key;hash=(Get-ControlHash $_.path)} } | Sort-Object key)
    $expected=@($Before.entries | Sort-Object key)
    if($actual.Count -ne $expected.Count){throw 'Owner file membership differs after restoration.'}
    for($i=0;$i -lt $actual.Count;$i++){
        if($actual[$i].key -cne $expected[$i].key -or $actual[$i].hash -cne $expected[$i].hash){throw "Owner file differs after restoration: $($expected[$i].key)"}
    }
}

$Result=Full $Result; $PackageRoot=Full $PackageRoot; $GameDir=Full $GameDir
$PrivateWriter=Full $PrivateWriter; $PrivateCatalog=Full $PrivateCatalog; $Profiles=Full $Profiles
foreach($path in @($Result,$PackageRoot,$GameDir,$PrivateWriter,$PrivateCatalog,$Profiles)){Assert-ControlPlain $path}
if(Test-Path -LiteralPath $Result){throw 'Choose a new outer result directory.'}
Closed
$manifestPath=Join-Path $PackageRoot 'manifest.json'
$receiptPath=Join-Path $GameDir 'BepInEx/WodenWheel-install.json'
$plugin=Join-Path $GameDir 'BepInEx/plugins/WodenRallyEdgeWheel'
$candidate=Join-Path $PackageRoot 'BepInEx/plugins/WodenRallyEdgeWheel'
$native='E:/Source/dbce-project-mgmt/artifacts/wheelffb-noforce-20261013/x64/WheelFfb.dll'
$provenance='E:/Source/dbce-project-mgmt/artifacts/wheelffb-noforce-20261013/x64/WheelFfb.noforce.provenance.json'
$nativeHash='088F1089BEB6D18D478F37582C59330C6BDA6BC37D5E365F6BA7EBF80040F567'
$provenanceHash='6068B7A848F85FE42AE88EE8A86B10F1EF4C4E307B9C4235BE44C1A84BBEA8E5'
$originalReceipt='F645FE983D7A313DF3C3BC7550A03F4AE9F0B6AC1C3184AF129F0B63FB5EF6B5'
$prefsKey='HKCU:\Software\ViJuDa\Super Woden Rally Edge'
Pin $manifestPath $PackageManifestSha256; Pin $receiptPath $originalReceipt
Pin $PrivateWriter $PrivateWriterSha256; Pin $PrivateCatalog $PrivateCatalogSha256
Pin $native $nativeHash; Pin $provenance $provenanceHash
Pin (Join-Path $GameDir 'BepInEx/core/BepInEx.Unity.IL2CPP.dll') '46CF1EF802BDF8CB6587FC1E4D98F7AF1073A60487B39A64E230E6F6E4C23AEC'
if((Get-ControlHash $Profiles) -eq 'absent'){throw 'Applied profile input is missing.'}
if((Get-ControlHash (Join-Path $GameDir 'GameAssembly.dll')) -ne 'F422894D8D2B0DF4EDB7E5259E5E60CB8C4F8DEA2E85EBDFC09DD6766349250C'){throw 'Unsupported game build.'}
$manifest=@(Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json)
foreach($entry in $manifest){
    if($entry.path -match '(^|[\\/])\.\.([\\/]|$)' -or [IO.Path]::IsPathRooted($entry.path) -or $entry.sha256 -notmatch '^[A-Fa-f0-9]{64}$'){throw 'Unsafe package manifest entry.'}
    Pin (Join-Path $PackageRoot $entry.path) $entry.sha256
}
$owner=Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
if($owner.mode -cne 'Install' -or $owner.installerRevision -ne 3 -or @($owner.files).Count -ne 14 -or $owner.sharedInputCandidate.controls.adapter -cne 'woden-shim-ini-controls-1'){throw 'Original owner receipt differs.'}
$ownerPaths=@($owner.files | ForEach-Object { $_.path.Replace('\','/') })
$pluginPaths=@(Get-ChildItem -LiteralPath $plugin -File -Recurse -Force | ForEach-Object { 'BepInEx/plugins/WodenRallyEdgeWheel/'+$_.FullName.Substring($plugin.Length+1).Replace('\','/') })
if(@($ownerPaths | Select-Object -Unique).Count -ne 14){throw 'Original receipt repeats an owned plugin path.'}
foreach($path in $ownerPaths){if($pluginPaths -cnotcontains $path){throw "Original receipt-owned plugin file is absent: $path"}}
$unknown=@($pluginPaths | Where-Object { $ownerPaths -notcontains $_ })
$candidateOwned=@($manifest | ForEach-Object { $_.path.Replace('\','/') } | Where-Object { $_ -like 'BepInEx/plugins/WodenRallyEdgeWheel/*' })
if($candidateOwned.Count -ne 17 -or @($candidateOwned | Select-Object -Unique).Count -ne 17){throw 'Private package plugin membership differs.'}
foreach($path in $candidateOwned){if($unknown -contains $path){throw "Private candidate would replace a pre-existing unknown plugin file: $path"}}
foreach($entry in $owner.files){Pin (Join-Path $GameDir $entry.path) $entry.sha256}

$roots=[ordered]@{
    plugin=$plugin; config=(Join-Path $GameDir 'BepInEx/config')
    save=(Join-Path $env:USERPROFILE 'AppData/LocalLow/ViJuDa/Super Woden Rally Edge')
    loaderCore=(Join-Path $GameDir 'BepInEx/core'); loaderDotnet=(Join-Path $GameDir 'dotnet')
}
$files=[ordered]@{
    receipt=$receiptPath; log=(Join-Path $GameDir 'BepInEx/LogOutput.log')
    winhttp=(Join-Path $GameDir 'winhttp.dll'); doorstop=(Join-Path $GameDir 'doorstop_config.ini')
    doorstopVersion=(Join-Path $GameDir '.doorstop_version')
}
$slot=Join-Path $env:LOCALAPPDATA 'dbce/test-slot.txt'
$lease=Enter-StageRigLease -Path $slot -Owner halo-woden-forward -Purpose 'temporary candidate install, muted applied forward trial, exact owner restore'
$mutated=$false; $restored=$false; $failure=$null
try {
    Closed; Assert-StageRigLease -Path $slot -Token $lease.token
    [IO.Directory]::CreateDirectory($Result) | Out-Null
    Save-ControlFiles (Join-Path $Result 'owner-before') $roots $files
    Registry-Snapshot (Join-Path $Result 'preferences.json')
    $before=Get-Content -LiteralPath (Join-Path $Result 'owner-before/manifest.json') -Raw | ConvertFrom-Json
    $prefsHash=Get-ControlHash (Join-Path $Result 'preferences.json')
    $backups=Join-Path $GameDir 'WodenWheelBackups'
    $backupBefore=@(Get-ChildItem -LiteralPath $backups -Directory -Force | Select-Object -ExpandProperty Name)
    [ordered]@{schema=1;gameDir=$GameDir;originalReceiptSha256=$originalReceipt;packageManifestSha256=$PackageManifestSha256;ownerManifestSha256=(Get-ControlHash (Join-Path $Result 'owner-before/manifest.json'));preferencesSha256=$prefsHash;leasePath=$slot} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Result 'recovery.json')
    $mutated=$true
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PackageRoot 'Manage-Install.ps1') -Mode Install -PackageRoot $PackageRoot -GameDir $GameDir *> (Join-Path $Result 'install.log')
    if($LASTEXITCODE -ne 0){throw "Candidate installer failed: $LASTEXITCODE"}
    Closed; Assert-StageRigLease -Path $slot -Token $lease.token
    $installed=Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    if($installed.mode -cne 'Install' -or $installed.installerRevision -ne 4 -or @($installed.files).Count -ne 17){throw 'Temporary candidate receipt differs.'}
    foreach($entry in $installed.files){Pin (Join-Path $GameDir $entry.path) $entry.sha256}
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Run-Controls.ps1') -ShimAppliedForwardCapture -PrivateApply -Result (Join-Path $Result 'trial') -GameDir $GameDir -CandidateDirectory $candidate -NativeCandidate $native -NativeSha256 $nativeHash -NativeProvenancePath $provenance -PrivateWriter $PrivateWriter -PrivateCatalog $PrivateCatalog -ProbeSha256 $ProbeSha256 -Profiles $Profiles -RigLeaseToken $lease.token *> (Join-Path $Result 'trial.log')
    if($LASTEXITCODE -ne 0){throw "Applied forward trial failed or was incomplete: $LASTEXITCODE"}
} catch { $failure=$_.Exception.Message; if(Test-Path -LiteralPath $Result){$failure | Set-Content -LiteralPath (Join-Path $Result 'run-error.txt')} }
finally {
    if($mutated){
        try {
            Closed; Assert-StageRigLease -Path $slot -Token $lease.token
            Restore-ControlFiles (Join-Path $Result 'owner-before') (Join-Path $Result 'candidate-after') $roots $files
            if((Get-ControlHash (Join-Path $Result 'preferences.json')) -ne $prefsHash){throw 'Preference snapshot changed.'}
            Registry-Restore (Join-Path $Result 'preferences.json')
            Assert-Inventory $before $roots $files
            Pin $receiptPath $originalReceipt
            Registry-Snapshot (Join-Path $Result 'preferences-after.json')
            if((Get-ControlHash (Join-Path $Result 'preferences-after.json')) -ne $prefsHash){throw 'Owner preferences differ after restoration.'}
            $backupAfter=@(Get-ChildItem -LiteralPath $backups -Directory -Force | Select-Object -ExpandProperty Name)
            $retained=@($backupAfter | Where-Object { $_ -notin $backupBefore })
            [ordered]@{schema=1;ownerReceiptSha256=$originalReceipt;ownerFileCount=@($before.entries).Count;retainedCandidateBackupFolders=$retained;trialExit=$(if($failure){'failed-or-incomplete'}else{'passed'})} |
                ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $Result 'restore-result.json')
            $restored=$true
        } catch { $_.Exception.Message | Set-Content -LiteralPath (Join-Path $Result 'restore-error.txt') }
    }
    if(!$mutated -or $restored){$null=Exit-StageRigLease -Lease $lease}
}
if($mutated -and !$restored){throw "Owner restoration unverified; retain shared lease and inspect $Result"}
if($failure){throw "$failure; owner files/preferences restored; evidence $Result"}
Write-Host "Applied forward trial completed; original owner files/preferences restored. Evidence: $Result"
