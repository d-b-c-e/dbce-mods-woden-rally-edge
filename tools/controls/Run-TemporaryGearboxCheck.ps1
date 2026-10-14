#Requires -Version 7.0
# Closed-game outer transaction for one bounded check of [Input] NativeGearbox (sana 96307/96352): one ordinary launch
# with a temporary load-muted candidate, the dev channel's own pad A presses up to the first main menu (MapScreen),
# where the plugin sets and reads back the game's saved transmission choice once; then close from MapScreen and restore
# the owner's files, preferences and receipt exactly. No StageStartup, replay or stage: those are tooling and skipped.
# Evidence: the plugin's "Native gearbox" log line, Supersave0.sav/BackUp state before launch, after the line and after
# exit, and a hash of Steam's local remotecache.vdf (a local witness only, not the remote Cloud state).
param(
    [Parameter(Mandatory)][string]$Result,
    [Parameter(Mandatory)][string]$PackageRoot,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$PackageManifestSha256,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$OwnerReceiptSha256,
    [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{40}$')][string]$SourceCommit,
    [Parameter(Mandatory)][ValidateSet('Manual','Automatic')][string]$Setting,
    [int]$Presses=15,
    [switch]$VerifyOnly,
    [string]$GameDir='D:/Program Files (x86)/Steam/steamapps/common/Super Woden Rally Edge',
    [string]$RemoteCache='D:/Program Files (x86)/Steam/userdata/22623167/3218630/remotecache.vdf'
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
# Sets one key in one section of a BepInEx cfg (exactly one section; the key replaced or appended). The config root is
# snapshot and restored as a whole, so this edit never outlives the run.
function Set-CfgValue([string]$Path,[string]$Section,[string]$Key,[string]$Value) {
    $lines=[Collections.Generic.List[string]]::new([IO.File]::ReadAllLines($Path))
    $starts=@(for($i=0;$i -lt $lines.Count;$i++){if($lines[$i].Trim() -ceq "[$Section]"){$i}})
    if($starts.Count -gt 1){throw "Duplicate [$Section] section."}
    if($starts.Count -eq 0){$lines.Add(''); $lines.Add("[$Section]"); $lines.Add("$Key = $Value"); [IO.File]::WriteAllLines($Path,$lines); return}
    $end=$lines.Count; for($i=$starts[0]+1;$i -lt $lines.Count;$i++){if($lines[$i].TrimStart().StartsWith('[')){$end=$i;break}}
    $hits=@(for($i=$starts[0]+1;$i -lt $end;$i++){if($lines[$i] -match "^\s*$([regex]::Escape($Key))\s*="){$i}})
    if($hits.Count -gt 1){throw "Duplicate [$Section] $Key."}
    if($hits.Count -eq 1){$lines[$hits[0]]="$Key = $Value"}else{$lines.Insert($end,"$Key = $Value")}
    [IO.File]::WriteAllLines($Path,$lines)
}
function Save-State([string]$Name) {
    $save=Join-Path $env:USERPROFILE 'AppData/LocalLow/ViJuDa/Super Woden Rally Edge/Save'
    $main=Join-Path $save 'Supersave0.sav'
    [ordered]@{name=$Name;utc=[DateTime]::UtcNow.ToString('o')
        supersave=[ordered]@{sha256=(Get-ControlHash $main);lastWriteUtc=$(if(Test-Path -LiteralPath $main){(Get-Item -LiteralPath $main).LastWriteTimeUtc.ToString('o')})}
        backups=@(Get-ChildItem -LiteralPath (Join-Path $save 'BackUp') -File -Force -ErrorAction SilentlyContinue | Sort-Object Name | ForEach-Object { $_.Name })
        remoteCache=(Get-ControlHash $RemoteCache)}
}
function Gearbox-Line([string]$Log) {
    if(!(Test-Path -LiteralPath $Log)){return $null}
    $s=[IO.File]::Open($Log,'Open','Read','ReadWrite, Delete'); try{$text=[IO.StreamReader]::new($s).ReadToEnd()}finally{$s.Dispose()}
    @($text -split "`r?`n" | Where-Object { $_ -match 'Native gearbox ' }) | Select-Object -First 1
}

$Result=Full $Result; $PackageRoot=Full $PackageRoot; $GameDir=Full $GameDir
foreach($path in @($Result,$PackageRoot,$GameDir)){Assert-ControlPlain $path}
if(Test-Path -LiteralPath $Result){throw 'Choose a new result directory.'}
Closed
$manifestPath=Join-Path $PackageRoot 'manifest.json'
$receiptPath=Join-Path $GameDir 'BepInEx/WodenWheel-install.json'
$configPath=Join-Path $GameDir 'BepInEx/config/dbce.wodenrallyedgewheel.cfg'
$logPath=Join-Path $GameDir 'BepInEx/LogOutput.log'
$plugin=Join-Path $GameDir 'BepInEx/plugins/WodenRallyEdgeWheel'
$candidate=Join-Path $PackageRoot 'BepInEx/plugins/WodenRallyEdgeWheel'
$nativeHash='088F1089BEB6D18D478F37582C59330C6BDA6BC37D5E365F6BA7EBF80040F567'
$prefsKey='HKCU:\Software\ViJuDa\Super Woden Rally Edge'
Pin $manifestPath $PackageManifestSha256; Pin $receiptPath $OwnerReceiptSha256
Pin (Join-Path $GameDir 'BepInEx/core/BepInEx.Unity.IL2CPP.dll') '46CF1EF802BDF8CB6587FC1E4D98F7AF1073A60487B39A64E230E6F6E4C23AEC'
if((Get-ControlHash (Join-Path $GameDir 'GameAssembly.dll')) -ne 'F422894D8D2B0DF4EDB7E5259E5E60CB8C4F8DEA2E85EBDFC09DD6766349250C'){throw 'Unsupported game build.'}
$manifest=@(Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json)
foreach($entry in $manifest){
    if($entry.path -match '(^|[\\/])\.\.([\\/]|$)' -or [IO.Path]::IsPathRooted($entry.path) -or $entry.sha256 -notmatch '^[A-Fa-f0-9]{64}$'){throw 'Unsafe package manifest entry.'}
    Pin (Join-Path $PackageRoot $entry.path) $entry.sha256
}
$owner=Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
if($owner.mode -cne 'Install'){throw 'The owner receipt is not an install.'}
foreach($entry in $owner.files){Pin (Join-Path $GameDir $entry.path) $entry.sha256}
# The candidate may replace receipt-owned plugin files only, never a pre-existing file the owner receipt does not own.
$ownerPaths=@($owner.files | ForEach-Object { $_.path.Replace('\','/') })
$pluginPaths=@(Get-ChildItem -LiteralPath $plugin -File -Recurse -Force | ForEach-Object { 'BepInEx/plugins/WodenRallyEdgeWheel/'+$_.FullName.Substring($plugin.Length+1).Replace('\','/') })
$unknown=@($pluginPaths | Where-Object { $ownerPaths -notcontains $_ })
foreach($path in @($manifest | ForEach-Object { $_.path.Replace('\','/') } | Where-Object { $_ -like 'BepInEx/plugins/WodenRallyEdgeWheel/*' })){
    if($unknown -contains $path){throw "Candidate would replace a pre-existing unknown plugin file: $path"}
}
$automation=Get-Content -LiteralPath (Join-Path $PackageRoot 'automation-native-provenance.json') -Raw | ConvertFrom-Json
if($automation.schema -cne 'woden.automation-muted-native@1' -or $automation.nativeSha256 -ne $nativeHash -or $automation.sourceCommit -cne $SourceCommit -or
   !(Test-Path -LiteralPath (Join-Path $PackageRoot 'AUTOMATION-ONLY.txt') -PathType Leaf)){throw 'Candidate is not the reviewed load-muted automation flavor of this source.'}
Pin (Join-Path $candidate 'WheelFfb.dll') $nativeHash
if($VerifyOnly){
    [pscustomobject]@{status='passed';setting=$Setting;ownerReceiptSha256=$OwnerReceiptSha256;packageManifestSha256=$PackageManifestSha256;
        source=$SourceCommit;mutedNativeSha256=$nativeHash;ownerFiles=@($owner.files).Count;noGameOrInstall=$true}
    return
}

$roots=[ordered]@{
    plugin=$plugin; config=(Join-Path $GameDir 'BepInEx/config')
    save=(Join-Path $env:USERPROFILE 'AppData/LocalLow/ViJuDa/Super Woden Rally Edge')
    loaderCore=(Join-Path $GameDir 'BepInEx/core'); loaderDotnet=(Join-Path $GameDir 'dotnet')
}
$files=[ordered]@{
    receipt=$receiptPath; log=$logPath
    winhttp=(Join-Path $GameDir 'winhttp.dll'); doorstop=(Join-Path $GameDir 'doorstop_config.ini')
    doorstopVersion=(Join-Path $GameDir '.doorstop_version')
}
$slot=Join-Path $env:LOCALAPPDATA 'dbce/test-slot.txt'
$lease=Enter-StageRigLease -Path $slot -Owner deli-woden-gearbox -Purpose "one ordinary NativeGearbox=$Setting check to MapScreen and exact owner restore"
$mutated=$false; $restored=$false; $failure=$null; $game=$null; $states=[Collections.Generic.List[object]]::new(); $line=$null
try {
    Closed; Assert-StageRigLease -Path $slot -Token $lease.token
    [IO.Directory]::CreateDirectory($Result) | Out-Null
    Save-ControlFiles (Join-Path $Result 'owner-before') $roots $files
    Registry-Snapshot (Join-Path $Result 'preferences.json')
    $before=Get-Content -LiteralPath (Join-Path $Result 'owner-before/manifest.json') -Raw | ConvertFrom-Json
    $prefsHash=Get-ControlHash (Join-Path $Result 'preferences.json')
    $backups=Join-Path $GameDir 'WodenWheelBackups'
    $backupBefore=@(Get-ChildItem -LiteralPath $backups -Directory -Force | Select-Object -ExpandProperty Name)
    [ordered]@{schema=1;gameDir=$GameDir;ownerReceiptSha256=$OwnerReceiptSha256;packageManifestSha256=$PackageManifestSha256;setting=$Setting;
        ownerManifestSha256=(Get-ControlHash (Join-Path $Result 'owner-before/manifest.json'));preferencesSha256=$prefsHash;leasePath=$slot} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Result 'recovery.json')
    $states.Add((Save-State 'before-launch'))
    $mutated=$true
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PackageRoot 'Manage-Install.ps1') -Mode Install -PackageRoot $PackageRoot -GameDir $GameDir *> (Join-Path $Result 'install.log')
    if($LASTEXITCODE -ne 0){throw "Candidate installer failed: $LASTEXITCODE"}
    Closed; Assert-StageRigLease -Path $slot -Token $lease.token
    $installed=Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    if($installed.mode -cne 'Install' -or $installed.nativeGearboxPreference -ne $true){throw 'Temporary candidate receipt does not declare nativeGearboxPreference.'}
    foreach($entry in $installed.files){Pin (Join-Path $GameDir $entry.path) $entry.sha256}
    Pin (Join-Path $plugin 'WheelFfb.dll') $nativeHash
    Set-CfgValue $configPath 'Input' 'NativeGearbox' $Setting
    Set-CfgValue $configPath 'ForceFeedback' 'Enabled' 'false'
    Set-CfgValue $configPath 'Dev' 'InputCommandFile' 'true'
    if(Test-Path -LiteralPath (Join-Path $plugin 'request.txt')){throw 'A stage request would make this tooling; refusing.'}
    $logStart=if(Test-Path -LiteralPath $logPath){(Get-Item -LiteralPath $logPath).LastWriteTimeUtc}else{[DateTime]::MinValue}
    Start-Process 'steam://rungameid/3218630'
    $deadline=[DateTime]::UtcNow.AddSeconds(90)
    while(!$game -and [DateTime]::UtcNow -lt $deadline) {
        $games=@(Get-Process -Name 'Super Woden Rally Edge' -ErrorAction SilentlyContinue)
        if($games.Count -gt 1){throw 'Multiple Woden processes.'}
        if($games.Count -eq 1){
            try { if(!$games[0].MainModule.FileName){throw [ComponentModel.Win32Exception]::new(299)}; $game=$games[0] }
            catch [ComponentModel.Win32Exception] {if($_.Exception.NativeErrorCode -ne 299){throw};$games[0].Dispose()}
        }
        Start-Sleep -Milliseconds 500
    }
    if(!$game){throw 'No game launched; leave Steam session prompts untouched.'}
    if(![string]::Equals($game.MainModule.FileName,(Join-Path $GameDir 'Super Woden Rally Edge.exe'),[StringComparison]::OrdinalIgnoreCase)){throw 'Wrong executable launched.'}
    $handle=$game.Handle
    # The dev channel writes its heartbeat once it reads commands.
    $dev=Join-Path $plugin 'dev'; $beat=Join-Path $dev 'heartbeat.txt'; $cmd=Join-Path $dev 'cmd.txt'
    $deadline=[DateTime]::UtcNow.AddSeconds(90)
    while(!((Test-Path -LiteralPath $beat) -and (Get-Item -LiteralPath $beat).LastWriteTimeUtc -gt $logStart) -and [DateTime]::UtcNow -lt $deadline){if($game.HasExited){throw 'The game exited before the dev channel started.'};Start-Sleep 1}
    if([DateTime]::UtcNow -ge $deadline){throw 'The dev channel did not start.'}
    # One pad A per step until the plugin reports at MapScreen; each step is preceded by an in-game screenshot.
    for($i=1;$i -le $Presses -and !$line;$i++){
        if($game.HasExited){throw 'The game exited during navigation.'}
        $line=Gearbox-Line $logPath; if($line){break}
        if(Test-Path -LiteralPath $cmd){throw 'The previous dev command was not read; not sending another.'}
        [IO.File]::WriteAllText($cmd,"shot nav-$i`npad A 150`n")
        Start-Sleep 4
        $line=Gearbox-Line $logPath
    }
    if(!$line){throw "No 'Native gearbox' line after $Presses pad A steps."}
    $line | Set-Content -LiteralPath (Join-Path $Result 'gearbox-line.txt')
    Start-Sleep 5
    $states.Add((Save-State 'after-gearbox-line'))
    if(!(Test-Path -LiteralPath $cmd)){[IO.File]::WriteAllText($cmd,"shot mapscreen`n"); Start-Sleep 2}
} catch { $failure=$_.Exception.Message; if(Test-Path -LiteralPath $Result){$failure | Set-Content -LiteralPath (Join-Path $Result 'run-error.txt')} }
finally {
    if($game -and !$game.HasExited){
        $null=$game.CloseMainWindow()
        if(!$game.WaitForExit(60000)){ $failure="$failure; the game did not close within 60 s" }
    }
    if($game -and $game.HasExited){ try{ "exitCode=$($game.ExitCode)" | Set-Content -LiteralPath (Join-Path $Result 'game-exit.txt') } catch {} }
    if($mutated){
        try {
            Closed; Assert-StageRigLease -Path $slot -Token $lease.token
            $states.Add((Save-State 'after-exit'))
            $states | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $Result 'save-states.json')
            if(Test-Path -LiteralPath $logPath){Copy-Item -LiteralPath $logPath -Destination (Join-Path $Result 'LogOutput.log')}
            Restore-ControlFiles (Join-Path $Result 'owner-before') (Join-Path $Result 'candidate-after') $roots $files
            if((Get-ControlHash (Join-Path $Result 'preferences.json')) -ne $prefsHash){throw 'Preference snapshot changed.'}
            Registry-Restore (Join-Path $Result 'preferences.json')
            Assert-Inventory $before $roots $files
            Pin $receiptPath $OwnerReceiptSha256
            Registry-Snapshot (Join-Path $Result 'preferences-after.json')
            if((Get-ControlHash (Join-Path $Result 'preferences-after.json')) -ne $prefsHash){throw 'Owner preferences differ after restoration.'}
            $retained=@(Get-ChildItem -LiteralPath $backups -Directory -Force | Select-Object -ExpandProperty Name | Where-Object { $_ -notin $backupBefore })
            [ordered]@{schema=1;setting=$Setting;ownerReceiptSha256=$OwnerReceiptSha256;ownerFileCount=@($before.entries).Count;
                gearboxLine=$line;retainedCandidateBackupFolders=$retained;runExit=$(if($failure){'failed-or-incomplete'}else{'passed'})} |
                ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $Result 'restore-result.json')
            $restored=$true
        } catch { $_.Exception.Message | Set-Content -LiteralPath (Join-Path $Result 'restore-error.txt') }
    }
    if(!$mutated -or $restored){$null=Exit-StageRigLease -Lease $lease}
}
if($mutated -and !$restored){throw "Owner restoration unverified; retain shared lease and inspect $Result"}
if($failure){throw "$failure; owner files/preferences restored; evidence $Result"}
Write-Host "NativeGearbox $Setting check completed: $line. Owner files/preferences restored. Evidence: $Result"
