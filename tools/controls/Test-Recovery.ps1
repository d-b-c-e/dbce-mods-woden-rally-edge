#Requires -Version 7.0
# Execute the production Recover branch against isolated trees and a file-only
# registry boundary. No game, live registry, owner file, device or real lease.
$ErrorActionPreference='Stop'
. "$PSScriptRoot/OwnerFiles.ps1"
$base=Join-Path ([IO.Path]::GetTempPath()) ('woden-controls-recovery-'+[guid]::NewGuid().ToString('N'))
$checks=0
function Check([bool]$Ok,[string]$Why){$script:checks++;if(!$Ok){throw $Why}}
function Text([string]$Path,[string]$Value){[IO.Directory]::CreateDirectory((Split-Path -Parent $Path))|Out-Null;[IO.File]::WriteAllText($Path,$Value)}
foreach($case in @('restore','expired','already-restored','bad-backup','bad-metadata','other-lease','other-request','environment-failure')) {
    $dir=Join-Path $base $case; $game=Join-Path $dir 'game'; $result=Join-Path $dir 'result'
    $user=Join-Path $dir 'user';$app=Join-Path $dir 'app';$copy=Join-Path $dir 'tools/controls'
    [IO.Directory]::CreateDirectory($copy)|Out-Null
    Copy-Item "$PSScriptRoot/Run-Controls.ps1","$PSScriptRoot/OwnerFiles.ps1" $copy
    Text "$copy/Environment.ps1" @'
function Registry-Restore([string]$Path) {
    if(Test-Path "$Result/fail-registry"){throw 'simulated registry failure'}
    [IO.File]::WriteAllText("$GameDir/prefs-restored.txt",'exact raw preference bytes')
}
'@
    [IO.Directory]::CreateDirectory("$dir/vendor/playback")|Out-Null
    Copy-Item "$PSScriptRoot/../../vendor/playback/Stage-RigLease.ps1" "$dir/vendor/playback/"
    $plugin=Join-Path $game 'BepInEx/plugins/WodenRallyEdgeWheel'
    $roots=[ordered]@{config=(Join-Path $game 'BepInEx/config');save=(Join-Path $user 'AppData/LocalLow/ViJuDa/Super Woden Rally Edge');dev=(Join-Path $plugin 'dev')}
    $files=[ordered]@{native=(Join-Path $plugin 'WheelFfb.dll');probe=(Join-Path $game 'BepInEx/plugins/DbceControlsTest/Woden.ControlsProbe.dll');log=(Join-Path $game 'BepInEx/LogOutput.log')}
    Text $files.native 'original native';Text "$($roots.config)/owner.cfg" 'owner config'; Text "$($roots.save)/Save/owner.sav" 'owner save'
    Save-ControlFiles "$result/owner-before" $roots $files
    Text "$result/preferences.json" '[]'
    $slot=Join-Path $app 'dbce/test-slot.txt';$nonce='b'*32;Text $slot 'fixture lease'
    $lease=@{path=$slot;token='fixture lease';previous=''}
    @{schema=1;gameDir=$game;lease=$lease;nonce=$nonce;filesHash=(Get-ControlHash "$result/owner-before/manifest.json");prefsHash=(Get-ControlHash "$result/preferences.json")} | ConvertTo-Json -Depth 5 | Set-Content "$result/recovery.json"
    Text $files.native 'temporary native';Text $files.probe 'temporary addon';Text "$($roots.config)/owner.cfg" 'temporary config';Text "$($roots.save)/new.log" 'new log'
    Text "$app/dbce/super-woden-rally-edge/inject.on" $nonce
    switch($case) {
        expired {[IO.File]::SetLastWriteTimeUtc($slot,[DateTime]::UtcNow.AddHours(-3))}
        already-restored {Text "$result/restored.txt" 'previously complete'}
        bad-backup {Text "$result/owner-before/file/native" 'corrupted'}
        bad-metadata {Text "$result/owner-before/manifest.json" '{}'}
        other-lease {Text $slot 'another test lease'}
        other-request {Text "$app/dbce/super-woden-rally-edge/inject.on" ('c'*32)}
        environment-failure {Text "$result/fail-registry" 'fail'}
    }
    $start=[Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path)
    $start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($a in @('-NoProfile','-File',"$copy/Run-Controls.ps1",'-Recover','-Result',$result,'-GameDir',$game)){$start.ArgumentList.Add($a)}
    $start.Environment['USERPROFILE']=$user;$start.Environment['LOCALAPPDATA']=$app
    $p=[Diagnostics.Process]::Start($start);$stdout=$p.StandardOutput.ReadToEndAsync();$stderr=$p.StandardError.ReadToEndAsync()
    if(!$p.WaitForExit(30000)){$p.Kill();throw 'Recovery fixture timed out'}
    $log=$stdout.GetAwaiter().GetResult()+$stderr.GetAwaiter().GetResult();Text "$dir/output.txt" $log
    $success=$case -in @('restore','expired');$recoverFiles=$success -or $case -eq 'environment-failure'
    Check (($p.ExitCode -eq 0) -eq $success) "Wrong exit ($case): $log"
    Check (([IO.File]::ReadAllText($files.native) -ceq 'original native') -eq $recoverFiles) "Wrong native restoration ($case)"
    Check (([IO.File]::ReadAllText("$($roots.config)/owner.cfg") -ceq 'owner config') -eq $recoverFiles) "Wrong config restoration ($case)"
    Check ((Test-Path $files.probe) -ne $recoverFiles) "Wrong temporary addon restoration ($case)"
    Check ((Test-Path $slot) -ne $success) "Wrong lease release ($case)"
    Check ((Test-Path "$result/restored.txt") -eq ($success -or $case -eq 'already-restored')) "Wrong restoration receipt ($case)"
    if($success){Check ((Test-Path "$game/prefs-restored.txt") -and !(Test-Path "$($roots.save)/new.log")) 'Preferences/new logs not restored'}
    $p.Dispose()
}
"PASS: $checks production recovery checks; isolated evidence $base"
