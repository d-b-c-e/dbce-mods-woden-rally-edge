#Requires -Version 7.0
# One closed-game, load-muted Kenya replay against a temporary HUD-speed candidate, with exact owner restoration.
param(
    [Parameter(Mandatory)][string]$Result,
    [Parameter(Mandatory)][string]$ProductionStage,
    [Parameter(Mandatory)][string]$MutedStage,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$MutedManifestSha256,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ReplayRunnerSha256,
    [Parameter(Mandatory)][string]$Recording,
    [switch]$VerifyOnly,
    [string]$GameDir='D:/Program Files (x86)/Steam/steamapps/common/Super Woden Rally Edge'
)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/../controls/OwnerFiles.ps1"
. "$PSScriptRoot/../controls/Environment.ps1"
. "$PSScriptRoot/../../vendor/playback/Stage-RigLease.ps1"
function Full([string]$Path) { [IO.Path]::GetFullPath($Path).TrimEnd('\','/') }
function Closed { if(Get-Process -Name 'Super Woden Rally Edge' -ErrorAction SilentlyContinue){throw 'Woden must be closed; retain owner state and lease.'} }
function Pin([string]$Path,[string]$Hash) { Assert-ControlPlain $Path; if((Get-ControlHash $Path) -ne $Hash){throw "Pinned file differs: $Path"} }
function Assert-Inventory($Before,$Roots,$Files) {
    $actual=@(Get-ControlInventory $Roots $Files | ForEach-Object {[pscustomobject]@{key=$_.key;hash=(Get-ControlHash $_.path)}} | Sort-Object key)
    $expected=@($Before.entries | Sort-Object key)
    if($actual.Count -ne $expected.Count){throw 'Owner file membership differs after restoration.'}
    for($i=0;$i -lt $actual.Count;$i++){
        if($actual[$i].key -cne $expected[$i].key -or $actual[$i].hash -cne $expected[$i].hash){throw "Owner file differs after restoration: $($expected[$i].key)"}
    }
}
$Result=Full $Result;$ProductionStage=Full $ProductionStage;$MutedStage=Full $MutedStage
$Recording=Full $Recording;$GameDir=Full $GameDir
foreach($path in @($Result,$ProductionStage,$MutedStage,$Recording,$GameDir)){Assert-ControlPlain $path}
if(Test-Path -LiteralPath $Result){throw 'Choose a new replay result directory.'}
Closed
$receiptPath=Join-Path $GameDir 'BepInEx/WodenWheel-install.json'
$configPath=Join-Path $GameDir 'BepInEx/config/dbce.wodenrallyedgewheel.cfg'
$plugin=Join-Path $GameDir 'BepInEx/plugins/WodenRallyEdgeWheel'
$runner=Join-Path $PSScriptRoot 'Run-StageSession.ps1'
$productionManifest='10770DE7355F820440FA391303A8EA8DD1C38133B3EED664195B80A186EB63CE'
$originalReceipt='FE2BABFA962467EC82C31C0D677476828AAA4C1125FB4882DB1AE8892D9BA440'
$sourceCommit='0ed0292888f29d2152e3a0a77c39e23e43b9d72c'
$nativeHash='088F1089BEB6D18D478F37582C59330C6BDA6BC37D5E365F6BA7EBF80040F567'
$baseNative='F8361A08BBE08EAAB936B37800D308CF1FC7EDAB34E2839DB16A447FE34178C5'
$pluginHash='59C21ECE3A52B8C467D19D478E602FBB90DF3C2059CDCE97B1894A09560FEA0C'
$coreHash='BD6A6A8B7E8318725A510644168B8565D81E6A38FABA364B9BAEF9BD78D17929'
$prefsKey='HKCU:\Software\ViJuDa\Super Woden Rally Edge'
Pin (Join-Path $ProductionStage 'manifest.json') $productionManifest
Pin (Join-Path $MutedStage 'manifest.json') $MutedManifestSha256
Pin $runner $ReplayRunnerSha256
Pin $receiptPath $originalReceipt
Pin $configPath '20C0BC9AEEEA9AB10824D793D3B8894636ED667CB18DFF34C68665FBF77F45A5'
Pin (Join-Path $GameDir 'GameAssembly.dll') 'F422894D8D2B0DF4EDB7E5259E5E60CB8C4F8DEA2E85EBDFC09DD6766349250C'
Pin (Join-Path $GameDir 'BepInEx/core/BepInEx.Unity.IL2CPP.dll') '46CF1EF802BDF8CB6587FC1E4D98F7AF1073A60487B39A64E230E6F6E4C23AEC'
Pin (Join-Path $Recording 'complete.tsv') '57208F75133FFE852AD2BB25038675036650BDCE1877BBF521AC25B6D32C2177'
Pin (Join-Path $Recording 'source.jsonl') '10BDADDD0D950290D5381D71D79CD377B4D3E813822FF10C50CBBBC951AD2458'
Pin (Join-Path $Recording 'trajectory.tsv') 'EA870D11DB387641BB236617E2AF4A9A6E804698CBF5C5B15B1A26F671A8C90B'
$owner=Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
if($owner.mode -cne 'Install' -or $owner.installerRevision -ne 4 -or @($owner.files).Count -ne 17 -or
   $owner.sharedInputCandidate.controls.adapter -cne 'woden-shim-ini-controls-1'){throw 'Current owner receipt differs.'}
$ownerPaths=@($owner.files | ForEach-Object {$_.path.Replace('\','/')})
if(@($ownerPaths | Select-Object -Unique).Count -ne 17){throw 'Owner receipt repeats a path.'}
foreach($entry in $owner.files){Pin (Join-Path $GameDir $entry.path) $entry.sha256}
$pluginPaths=@(Get-ChildItem -LiteralPath $plugin -File -Recurse -Force | ForEach-Object {'BepInEx/plugins/WodenRallyEdgeWheel/'+$_.FullName.Substring($plugin.Length+1).Replace('\','/')})
foreach($path in $ownerPaths){if($pluginPaths -cnotcontains $path){throw "Owner plugin path missing: $path"}}
$unknown=@($pluginPaths | Where-Object {$ownerPaths -cnotcontains $_})
$base=@(Get-Content -LiteralPath (Join-Path $ProductionStage 'manifest.json') -Raw | ConvertFrom-Json)
$muted=@(Get-Content -LiteralPath (Join-Path $MutedStage 'manifest.json') -Raw | ConvertFrom-Json)
foreach($package in @([pscustomobject]@{root=$ProductionStage;entries=$base},[pscustomobject]@{root=$MutedStage;entries=$muted})){
    $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($entry in $package.entries){
        if($entry.path -match '(^|[\\/])\.\.([\\/]|$)' -or [IO.Path]::IsPathRooted($entry.path) -or
           !$seen.Add([string]$entry.path) -or $entry.sha256 -notmatch '^[A-Fa-f0-9]{64}$'){throw 'Unsafe or repeated manifest path.'}
        Pin (Join-Path $package.root $entry.path) $entry.sha256
    }
    $items=@(Get-ChildItem -LiteralPath $package.root -Recurse -Force)
    if(@($items | Where-Object {-not $_.PSIsContainer}).Count -ne $package.entries.Count+1 -or
       @($items | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count){throw 'Candidate stage has unlisted or linked files.'}
}
$baseOwned=@($base | Where-Object {$_.path.Replace('\','/') -like 'BepInEx/plugins/WodenRallyEdgeWheel/*'})
$mutedOwned=@($muted | Where-Object {$_.path.Replace('\','/') -like 'BepInEx/plugins/WodenRallyEdgeWheel/*'})
if($baseOwned.Count -ne 17 -or $mutedOwned.Count -ne 17){throw 'Candidate plugin membership differs.'}
foreach($entry in $baseOwned){
    $relative=$entry.path.Replace('\','/')
    if($ownerPaths -cnotcontains $relative -or $unknown -ccontains $relative){throw "Candidate would replace an unknown plugin file: $relative"}
    $testEntry=@($mutedOwned | Where-Object {$_.path.Replace('\','/') -ceq $relative})
    if($testEntry.Count -ne 1){throw "Muted candidate omits an owned path: $relative"}
    if($relative -ceq 'BepInEx/plugins/WodenRallyEdgeWheel/WheelFfb.dll'){
        if($entry.sha256 -ne $baseNative -or $testEntry[0].sha256 -ne $nativeHash){throw 'Native production/muted pin differs.'}
    } elseif($entry.sha256 -ne $testEntry[0].sha256){throw "Muted candidate changes a managed owner payload: $relative"}
}
Pin (Join-Path $MutedStage 'BepInEx/plugins/WodenRallyEdgeWheel/WodenRallyEdgeWheel.dll') $pluginHash
Pin (Join-Path $MutedStage 'BepInEx/plugins/WodenRallyEdgeWheel/WodenRallyEdge.Core.dll') $coreHash
$meta=Get-Content -LiteralPath (Join-Path $MutedStage 'automation-native-provenance.json') -Raw | ConvertFrom-Json
if($meta.schema -cne 'woden.automation-muted-native@1' -or $meta.sourceCommit -cne $sourceCommit -or
   $meta.managedPayloadSourceCommit -cne $sourceCommit -or $meta.baseManifestSha256 -ne $productionManifest -or
   $meta.ownerReceiptSha256 -ne $originalReceipt -or $meta.nativeSha256 -ne $nativeHash -or
   $meta.baseNativeSha256 -ne $baseNative -or $meta.nativeDefaultState -cne 'armed' -or
   $meta.requiresExplicitArm -ne $false -or !(Test-Path -LiteralPath (Join-Path $MutedStage 'AUTOMATION-ONLY.txt') -PathType Leaf)){
    throw 'Muted candidate provenance differs.'
}
$config=[IO.File]::ReadAllText($configPath)
$inputSection=[regex]::Matches($config,'(?ms)^\[Input\][ \t]*\r?\n.*?(?=^\[|\z)')
if($inputSection.Count -gt 1){throw 'Owner has duplicate Input sections.'}
if($inputSection.Count -eq 1){
    $backend=[regex]::Matches($inputSection[0].Value,'(?m)^\s*Backend\s*=\s*([^\r\n;#]+)')
    if($backend.Count -gt 1 -or ($backend.Count -eq 1 -and $backend[0].Groups[1].Value.Trim() -cne 'Native')){throw 'Owner Backend differs from Native.'}
}
$stageControl=Join-Path $env:LOCALAPPDATA 'Dbce/StagePlayback/woden'
if(Test-Path -LiteralPath (Join-Path $stageControl 'request.txt')){throw 'An unconsumed stage request exists before temporary installation.'}
$roots=[ordered]@{
    plugin=$plugin;config=(Join-Path $GameDir 'BepInEx/config')
    save=(Join-Path $env:USERPROFILE 'AppData/LocalLow/ViJuDa/Super Woden Rally Edge')
    loaderCore=(Join-Path $GameDir 'BepInEx/core');loaderDotnet=(Join-Path $GameDir 'dotnet')
    stageControl=$stageControl
}
$files=[ordered]@{
    receipt=$receiptPath;log=(Join-Path $GameDir 'BepInEx/LogOutput.log')
    winhttp=(Join-Path $GameDir 'winhttp.dll');doorstop=(Join-Path $GameDir 'doorstop_config.ini')
    doorstopVersion=(Join-Path $GameDir '.doorstop_version')
}
if($VerifyOnly){
    Write-Output "PASS muted HUD replay preflight: production manifest $productionManifest; muted manifest $MutedManifestSha256; 17 candidate owner paths, sealed Kenya source, current owner receipt and Native config. No lease, install or game."
    return
}
$slot=Join-Path $env:LOCALAPPDATA 'dbce/test-slot.txt'
$lease=Enter-StageRigLease -Path $slot -Owner halo-woden-hudspeed-replay -Purpose 'one load-muted Kenya HUD-speed replay and exact owner restore'
$mutated=$false;$restored=$false;$failure=$null
try {
    Closed;Assert-StageRigLease -Path $slot -Token $lease.token
    [IO.Directory]::CreateDirectory($Result) | Out-Null
    Save-ControlFiles (Join-Path $Result 'owner-before') $roots $files
    Registry-Snapshot (Join-Path $Result 'preferences.json')
    $before=Get-Content -LiteralPath (Join-Path $Result 'owner-before/manifest.json') -Raw | ConvertFrom-Json
    $prefsHash=Get-ControlHash (Join-Path $Result 'preferences.json')
    $backupRoot=Join-Path $GameDir 'WodenWheelBackups'
    $backupBefore=@(Get-ChildItem -LiteralPath $backupRoot -Directory -Force | Select-Object -ExpandProperty Name)
    [ordered]@{schema=1;ownerReceiptSha256=$originalReceipt;productionManifestSha256=$productionManifest;
        mutedManifestSha256=$MutedManifestSha256;ownerManifestSha256=(Get-ControlHash (Join-Path $Result 'owner-before/manifest.json'));
        preferencesSha256=$prefsHash;leasePath=$slot} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Result 'recovery.json')
    $mutated=$true
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $MutedStage 'Manage-Install.ps1') -Mode Install -PackageRoot $MutedStage -GameDir $GameDir *> (Join-Path $Result 'install.log')
    if($LASTEXITCODE -ne 0){throw "Muted candidate installer failed: $LASTEXITCODE"}
    Closed;Assert-StageRigLease -Path $slot -Token $lease.token
    $installed=Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    if($installed.mode -cne 'Install' -or $installed.installerRevision -ne 4 -or @($installed.files).Count -ne 17){throw 'Muted candidate receipt differs.'}
    foreach($entry in $installed.files){Pin (Join-Path $GameDir $entry.path) $entry.sha256}
    & pwsh -NoProfile -File $runner -Recording $Recording -Result (Join-Path $Result 'trial') -GameDir $GameDir -RigLeaseToken $lease.token *> (Join-Path $Result 'trial.log')
    if($LASTEXITCODE -ne 0){throw "Kenya replay failed or was incomplete: $LASTEXITCODE"}
    Closed;Assert-StageRigLease -Path $slot -Token $lease.token
    $trial=Join-Path $Result 'trial'
    if(!(Test-Path -LiteralPath (Join-Path $trial 'restored.txt') -PathType Leaf)){throw 'Inner replay did not restore its owner environment.'}
    $status=Get-Content -LiteralPath (Join-Path $trial 'status.txt')
    if(!(@($status | Where-Object {$_ -match '^status=passed: 3601 poses;'}).Count)){throw 'Kenya replay did not pass all 3601 poses.'}
    $log=Get-Content -LiteralPath (Join-Path $trial 'LogOutput.log') -Raw
    if($log -notmatch 'Replay HUD speed: the recorded game\.trueSpeed for 3601 rows' -or
       $log -notmatch 'Stage replay HUD speed: recorded game\.trueSpeed per row' -or
       $log -notmatch 'Stage tooling latched physical force and network output off' -or
       $log -notmatch 'Stage replay released:'){throw 'Replay log lacks recorded HUD speed, mute, or release evidence.'}
    Pin (Join-Path $Recording 'complete.tsv') '57208F75133FFE852AD2BB25038675036650BDCE1877BBF521AC25B6D32C2177'
    Pin (Join-Path $Recording 'source.jsonl') '10BDADDD0D950290D5381D71D79CD377B4D3E813822FF10C50CBBBC951AD2458'
    Pin (Join-Path $Recording 'trajectory.tsv') 'EA870D11DB387641BB236617E2AF4A9A6E804698CBF5C5B15B1A26F671A8C90B'
    [ordered]@{schema=1;status='passed';sourceCommit=$sourceCommit;mode='muted sealed Kenya replay';
        mutedManifestSha256=$MutedManifestSha256;recordedHudSpeedRows=3601;poses=3601;
        physicalOutput=$false;gameClosed=$true;ownerRestore='pending outer restore'} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Result 'replay-readback.json')
} catch {$failure=$_.Exception.Message;if(Test-Path -LiteralPath $Result){$failure | Set-Content -LiteralPath (Join-Path $Result 'run-error.txt')}}
finally {
    if($mutated){
        try {
            Closed;Assert-StageRigLease -Path $slot -Token $lease.token
            Restore-ControlFiles (Join-Path $Result 'owner-before') (Join-Path $Result 'candidate-after') $roots $files
            if((Get-ControlHash (Join-Path $Result 'preferences.json')) -ne $prefsHash){throw 'Preference snapshot changed.'}
            Registry-Restore (Join-Path $Result 'preferences.json')
            Assert-Inventory $before $roots $files
            Pin $receiptPath $originalReceipt
            Pin $configPath '20C0BC9AEEEA9AB10824D793D3B8894636ED667CB18DFF34C68665FBF77F45A5'
            Registry-Snapshot (Join-Path $Result 'preferences-after.json')
            if((Get-ControlHash (Join-Path $Result 'preferences-after.json')) -ne $prefsHash){throw 'Owner preferences differ after restoration.'}
            $backupAfter=@(Get-ChildItem -LiteralPath $backupRoot -Directory -Force | Select-Object -ExpandProperty Name)
            $retained=@($backupAfter | Where-Object {$_ -notin $backupBefore})
            [ordered]@{schema=1;ownerReceiptSha256=$originalReceipt;ownerFileCount=@($before.entries).Count;
                retainedCandidateBackupFolders=$retained;trialExit=$(if($failure){'failed-or-incomplete'}else{'passed'})} |
                ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $Result 'restore-result.json')
            $restored=$true
        } catch {$_.Exception.Message | Set-Content -LiteralPath (Join-Path $Result 'restore-error.txt')}
    }
    if(!$mutated -or $restored){$null=Exit-StageRigLease -Lease $lease}
}
if($mutated -and !$restored){throw "Owner restoration unverified; retain shared lease and inspect $Result"}
if($failure){throw "$failure; owner files/preferences restored; evidence $Result"}
Write-Host "Muted Kenya HUD-speed replay passed; original owner state restored. Evidence: $Result"
