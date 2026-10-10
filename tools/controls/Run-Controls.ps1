#Requires -Version 7.0
# Developer-only production Apply -> native raw reader qualification. No inputs
# are sent automatically. Use Send-Command after inspecting each game state.
[CmdletBinding(DefaultParameterSetName='Run')]
param(
    [Parameter(Mandatory)][string]$Result,
    [Parameter(Mandatory,ParameterSetName='Run')][string]$NativeCandidate,
    [Parameter(Mandatory,ParameterSetName='Run')][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$NativeSha256,
    [Parameter(Mandatory,ParameterSetName='Recover')][switch]$Recover,
    [string]$GameDir='D:/Program Files (x86)/Steam/steamapps/common/Super Woden Rally Edge',
    [string]$WheelkitRepo='E:/Source/toolkits/dbce-wheelkit',
    [string]$HubRepo='E:/Source/dbce-project-mgmt',
    [string]$Profiles=(Join-Path $env:LOCALAPPDATA 'Wheelkit/mapping-profiles.json'),
    [ValidateRange(30,270)][int]$Seconds=240
)
$ErrorActionPreference='Stop'
function Full([string]$p){$ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($p)}
$Result=Full $Result; $GameDir=Full $GameDir
. "$PSScriptRoot/OwnerFiles.ps1"
. "$PSScriptRoot/Environment.ps1"
. "$PSScriptRoot/Apply-Check.ps1"
. "$PSScriptRoot/../../vendor/playback/Stage-RigLease.ps1"
$slot=Join-Path $env:LOCALAPPDATA 'dbce/test-slot.txt'
$prefsKey='HKCU:\Software\ViJuDa\Super Woden Rally Edge'
$control=Join-Path $env:LOCALAPPDATA 'dbce/super-woden-rally-edge'
$request=Join-Path $control 'controls-request.json'; $marker=Join-Path $control 'inject.on'
$plugin=Join-Path $GameDir 'BepInEx/plugins/WodenRallyEdgeWheel'
$cfg=Join-Path $GameDir 'BepInEx/config/dbce.wodenrallyedgewheel.cfg'
$roots=[ordered]@{config=(Join-Path $GameDir 'BepInEx/config');save=(Join-Path $env:USERPROFILE 'AppData/LocalLow/ViJuDa/Super Woden Rally Edge');dev=(Join-Path $plugin 'dev')}
$files=[ordered]@{native=(Join-Path $plugin 'WheelFfb.dll');probe=(Join-Path $GameDir 'BepInEx/plugins/DbceControlsTest/Woden.ControlsProbe.dll');log=(Join-Path $GameDir 'BepInEx/LogOutput.log')}
$blocked=@($request,$marker,(Join-Path $roots.config 'woden-record-next-launch.json'),(Join-Path $env:LOCALAPPDATA 'Dbce/StagePlayback/woden/request.txt'))
$probe=Join-Path $PSScriptRoot 'Probe/bin/Release/net6.0/Woden.ControlsProbe.dll'
$configVerifier=Join-Path $PSScriptRoot 'VerifyConfig/bin/Release/net8.0/VerifyConfig.dll'
$ready=$false; $lease=$null; $game=$null; $ownedGame=$false; $nonce=$null; $restored=$false; $applied=$false; $verificationError=$null
function Closed {if(Get-Process -Name 'Super Woden Rally Edge' -ErrorAction SilentlyContinue){throw 'Woden is running; no preparation or restoration.'}}
function Set-IniFalse([string]$Section,[string]$Key) {
    $text=[IO.File]::ReadAllText($cfg)
    $sections=[regex]::Matches($text,'(?ms)^\['+[regex]::Escape($Section)+'\][ \t]*\r?\n.*?(?=^\[|\z)')
    if($sections.Count -ne 1){throw "Expected exactly one $Section section."}
    $sectionText=$sections[0].Value
    $keyPattern='(?m)^[ \t]*'+[regex]::Escape($Key)+'[ \t]*=[^\r\n]*'
    if([regex]::Matches($sectionText,$keyPattern).Count -ne 1){throw "Expected exactly one $Section/$Key key."}
    $updated=[regex]::Replace($sectionText,$keyPattern,$Key+' = false')
    [IO.File]::WriteAllText($cfg,$text.Substring(0,$sections[0].Index)+$updated+$text.Substring($sections[0].Index+$sectionText.Length))
}
function Assert-OutputMute {
    $text=[IO.File]::ReadAllText($cfg)
    foreach($pair in @(@('ForceFeedback','Enabled'),@('Telemetry','Enabled'),@('Diagnostics','RecordSession'),@('Dev','InputCommandFile'))) {
        $section=[regex]::Matches($text,'(?ms)^\['+[regex]::Escape($pair[0])+'\][ \t]*\r?\n.*?(?=^\[|\z)')
        if($section.Count -ne 1){throw "Mute section differs: $($pair[0])"}
        $key=[regex]::Matches($section[0].Value,'(?m)^[ \t]*'+[regex]::Escape($pair[1])+'[ \t]*=[ \t]*([^\r\n]*)')
        if($key.Count -ne 1 -or $key[0].Groups[1].Value.Trim() -cne 'false'){throw "Mute key differs: $($pair -join '/')"}
    }
}
Closed; Assert-ControlPlain $Result; Assert-ControlPlain $GameDir
if($Recover) {
    if(Test-Path "$Result/restored.txt"){throw 'Already restored; do not overwrite newer owner settings.'}
    $state=Get-Content "$Result/recovery.json" -Raw | ConvertFrom-Json
    if($state.schema -ne 1 -or $state.gameDir -ne $GameDir -or $state.lease.path -ne $slot -or $state.nonce -notmatch '^[a-f0-9]{32}$'){throw 'Recovery scope differs.'}
    if((Get-ControlHash "$Result/owner-before/manifest.json") -ne $state.filesHash -or (Get-ControlHash "$Result/preferences.json") -ne $state.prefsHash){throw 'Recovery metadata changed.'}
    $nonce=$state.nonce
    foreach($p in @($request,$marker)) {if(Test-Path -LiteralPath $p){if(!(Get-Content -LiteralPath $p -Raw).Contains($nonce)){throw 'Another controls request exists.'}}}
    if(Test-Path -LiteralPath $slot) {
        $token=(Get-Content -LiteralPath $slot -Raw).TrimEnd("`r","`n")
        if($token -and $token -cne $state.lease.token){throw 'Another test owns the slot.'}
        if($token -and [IO.File]::GetLastWriteTimeUtc($slot) -gt [DateTime]::UtcNow.AddHours(-2)){$lease=$state.lease;Assert-StageRigLease -Path $slot -Token $lease.token}
    }
    $ready=$true
} else {
    if(Test-Path -LiteralPath $Result){throw 'Choose a new result directory.'}
    foreach($p in $blocked){if(Test-Path -LiteralPath $p){throw "Existing request needs resolution: $p"}}
    $NativeCandidate=Full $NativeCandidate
    if((Get-ControlHash $NativeCandidate) -ne $NativeSha256 -or !(Test-Path -LiteralPath $probe) -or !(Test-Path -LiteralPath $configVerifier)){throw 'Build/hash preflight failed.'}
    if([double](& "$HubRepo/tools/Owner-Input.ps1" -IdleSeconds) -lt 300){throw 'Owner input is recent.'}
}
if(!$lease){$lease=Enter-StageRigLease -Path $slot -Owner hula-woden-controls -Purpose 'Production Apply and native raw test; no force; exact restoration'}
$harness=Join-Path $Result 'harness/ConfigurationQualification.dll'; $live=Join-Path $Result 'apply'
try {
    Closed
    if($Recover){$state.lease=$lease;$state | ConvertTo-Json -Depth 5 | Set-Content "$Result/recovery.json";return}
    [IO.Directory]::CreateDirectory($Result) | Out-Null
    $writer=& "$PSScriptRoot/Freeze-Wheelkit.ps1" -Result $Result -Repo $WheelkitRepo
    $harness=$writer.harness
    Copy-Item -LiteralPath (Split-Path -Parent $configVerifier) -Destination "$Result/config-verifier" -Recurse
    $configVerifier=Join-Path $Result 'config-verifier/VerifyConfig.dll'
    Get-ChildItem "$Result/config-verifier" -File | Sort-Object Name | ForEach-Object {@{name=$_.Name;sha256=(Get-ControlHash $_.FullName)}} | ConvertTo-Json | Set-Content "$Result/config-verifier-hashes.json"
    Save-ControlFiles "$Result/owner-before" $roots $files
    Registry-Snapshot "$Result/preferences.json"
    $nonce=[guid]::NewGuid().ToString('N')
    $state=[ordered]@{schema=1;gameDir=$GameDir;lease=$lease;nonce=$nonce;filesHash=(Get-ControlHash "$Result/owner-before/manifest.json");prefsHash=(Get-ControlHash "$Result/preferences.json");writerReceiptHash=$writer.receiptHash}
    $state | ConvertTo-Json -Depth 5 | Set-Content "$Result/recovery.json"
    $ready=$true
    foreach($pair in @(@('ForceFeedback','Enabled'),@('Telemetry','Enabled'),@('Diagnostics','RecordSession'),@('Dev','InputCommandFile'))){Set-IniFalse $pair[0] $pair[1]}
    Invoke-ControlApplyCheck $GameDir $Result $harness $writer.catalog $Profiles
    $applied=$true
    # Verify again after production Apply and before launch. The addon also
    # checks the settings as loaded by the game before it arms raw input.
    Assert-OutputMute
    Copy-ControlExact $cfg "$Result/applied.cfg" (Get-ControlHash $cfg)
    & dotnet $harness raw-workload "$live/profile.json" "$Result/raw-workload.json"
    if($LASTEXITCODE){throw 'Independent raw workload failed.'}
    Copy-ControlExact $NativeCandidate $files.native $NativeSha256
    Copy-ControlExact $probe $files.probe (Get-ControlHash $probe)
    $cold=[ordered]@{schema=2;nonce=$nonce;expiresUtc=[DateTime]::UtcNow.AddMinutes(5).ToString('o');pluginSha256=(Get-ControlHash "$plugin/WodenRallyEdgeWheel.dll");coreSha256=(Get-ControlHash "$plugin/WodenRallyEdge.Core.dll");configSha256=(Get-ControlHash "$Result/applied.cfg");bindingsSha256=(Get-ControlHash "$($roots.config)/wheel-bindings.json");nativeSha256=$NativeSha256;outputDirectory=(Join-Path $Result 'trace')}
    [IO.Directory]::CreateDirectory($control) | Out-Null
    [IO.File]::WriteAllText($marker,$nonce); $cold | ConvertTo-Json | Set-Content -LiteralPath $request
    Copy-Item -LiteralPath $request -Destination "$Result/request.json"
    Assert-StageRigLease -Path $slot -Token $lease.token
    Assert-OutputMute
    Start-Process 'steam://rungameid/3218630'
    $deadline=[DateTime]::UtcNow.AddSeconds(90)
    while(!$game -and [DateTime]::UtcNow -lt $deadline) {
        $games=@(Get-Process -Name 'Super Woden Rally Edge' -ErrorAction SilentlyContinue)
        if($games.Count -gt 1){throw 'Multiple Woden processes.'}
        if($games.Count -eq 1){
            try {
                if(!$games[0].MainModule.FileName){throw [ComponentModel.Win32Exception]::new(299)}
                $game=$games[0]
            } catch [ComponentModel.Win32Exception] {if($_.Exception.NativeErrorCode -ne 299){throw};$games[0].Dispose()}
        }
        Start-Sleep -Milliseconds 500
    }
    if(!$game){throw 'No game launched; leave Steam session prompts untouched.'}
    if(![string]::Equals($game.MainModule.FileName,(Join-Path $GameDir 'Super Woden Rally Edge.exe'),[StringComparison]::OrdinalIgnoreCase)){throw 'Wrong executable launched.'}
    $ownedGame=$true
    [ordered]@{schema=1;process=$game.Id;startedUtc=$game.StartTime.ToUniversalTime().ToString('o');nonce=$nonce;gameDir=$GameDir;lease=$lease} | ConvertTo-Json -Depth 4 | Set-Content "$Result/context.json"
    $deadline=[DateTime]::UtcNow.AddSeconds(60)
    while(!(Test-Path "$Result/trace/identity.json") -and !$game.HasExited -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 500;$game.Refresh()}
    if(!(Test-Path "$Result/trace/identity.json")){throw 'Probe did not confirm native no-force admission.'}
    & "$PSScriptRoot/Send-Command.ps1" -Result $Result -Operation status
    Write-Output "Armed; inspect state before every raw sample. Evidence $Result"
    $deadline=[DateTime]::UtcNow.AddSeconds($Seconds)
    $coldDeadline=([DateTimeOffset]$cold.expiresUtc).UtcDateTime
    if($deadline -gt $coldDeadline){$deadline=$coldDeadline}
    while(!$game.HasExited -and !(Test-Path "$Result/trace/result.json") -and [DateTime]::UtcNow -lt $deadline) {
        Assert-StageRigLease -Path $slot -Token $lease.token
        if([double](& "$HubRepo/tools/Owner-Input.ps1" -IdleSeconds) -lt 5){'Owner returned' | Set-Content "$Result/interrupted.txt";break}
        Start-Sleep -Seconds 2;$game.Refresh()
    }
    if(!$game.HasExited -and !(Test-Path "$Result/trace/result.json")){& "$PSScriptRoot/Send-Command.ps1" -Result $Result -Operation stop}
} finally {
    if($ownedGame -and $game -and !$game.HasExited){$null=$game.CloseMainWindow();$null=$game.WaitForExit(20000)}
    Closed # If any process remains, retain lease/recovery and never write into it.
    $errors=[Collections.Generic.List[string]]::new()
    function Attempt([scriptblock]$Work){try{& $Work}catch{$errors.Add($_.Exception.Message);Write-Warning $_.Exception.Message}}
    if($ready) {
        $after=Join-Path $Result ('after-'+[guid]::NewGuid().ToString('N'))
        [IO.Directory]::CreateDirectory($after) | Out-Null
        foreach($p in @($request,$marker)) {Attempt {if(Test-Path -LiteralPath $p){if(!(Get-Content -LiteralPath $p -Raw).Contains($nonce)){throw 'Different request; not removed.'};Copy-Item -LiteralPath $p -Destination $after;Remove-Item -LiteralPath $p}}}
        foreach($taken in Get-ChildItem -LiteralPath $control -Filter 'controls-request.json.*.taken' -ErrorAction SilentlyContinue){Attempt {if((Get-Content -LiteralPath $taken.FullName -Raw).Contains($nonce)){Copy-Item -LiteralPath $taken.FullName -Destination $after;Remove-Item -LiteralPath $taken.FullName}}}
        if($applied){try{
            & dotnet $harness verify-live $live *> "$Result/verify-live.log"
            $strict=$LASTEXITCODE;"exit=$strict" | Set-Content "$Result/verify.txt"
            # BepInEx can rewrite comments/order and append a short, explicit
            # list of missing defaults during Load. Every original value must
            # survive, and bindings still require exact production-Apply bytes.
            Copy-ControlExact $cfg "$Result/loaded.cfg" (Get-ControlHash $cfg)
            & dotnet $configVerifier "$Result/applied.cfg" "$Result/loaded.cfg" $cold.configSha256 > "$Result/config-verification.json"
            if($LASTEXITCODE){throw 'Loaded configuration changed semantics.'}
            if((Get-ControlHash "$($roots.config)/wheel-bindings.json") -ne $cold.bindingsSha256){throw 'Runtime changed applied binding bytes.'}
            [ordered]@{status='passed';strictByteCheckExit=$strict;config='values retained; only known defaults allowed';bindings='exact applied bytes'} | ConvertTo-Json | Set-Content "$Result/runtime-verification.json"
        }catch{$verificationError=$_.Exception.Message;$verificationError | Set-Content "$Result/verification-error.txt"}}
        if(Test-Path "$live/apply.json"){Attempt {& dotnet $harness restore-live $live;if($LASTEXITCODE){throw 'Production transaction restoration failed.'}}}
        Attempt {Restore-ControlFiles "$Result/owner-before" $after $roots $files}
        Attempt {if((Get-ControlHash "$Result/preferences.json") -ne $state.prefsHash){throw 'Preference backup changed.'};Registry-Restore "$Result/preferences.json"}
    }
    if($errors.Count){$errors | Set-Content "$Result/restore-errors.txt";throw "Independent recovery attempted; lease retained: $Result"}
    $restored=$true
    if(Test-Path -LiteralPath $Result){[DateTime]::UtcNow.ToString('o') | Set-Content "$Result/restored.txt"}
    $null=Exit-StageRigLease -Lease $lease
    if($verificationError){throw "$verificationError Owner state restored and lease released."}
}
