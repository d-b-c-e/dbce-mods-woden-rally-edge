#Requires -Version 7.0
# Developer-only, restored controls qualification. Raw mode is operator-driven;
# explicit schema 4/5 trials run only their fixed shared-Core workloads.
[CmdletBinding(DefaultParameterSetName='Run')]
param(
    [Parameter(Mandatory)][string]$Result,
    [Parameter(Mandatory,ParameterSetName='Run')][string]$NativeCandidate,
    [Parameter(Mandatory,ParameterSetName='Run')][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$NativeSha256,
    [Parameter(Mandatory,ParameterSetName='Recover')][switch]$Recover,
    [Parameter(ParameterSetName='Run')][switch]$ShimAdmission,
    [Parameter(ParameterSetName='Run')][switch]$ShimMenuTrial,
    [Parameter(ParameterSetName='Run')][switch]$ShimCarTrial,
    [Parameter(ParameterSetName='Run')][string]$StageReference=(Join-Path $env:LOCALAPPDATA 'Dbce/StagePlayback/references/woden-kenya-20261005'),
    [Parameter(ParameterSetName='Run')][string]$OriginalWorkload='E:/Source/games/dbce-mods-woden-rally-edge/results/raw-controls-20261010-07',
    [Parameter(ParameterSetName='Run')][string]$CandidateDirectory,
    [Parameter(ParameterSetName='Run')][string]$ControlsSource,
    [Parameter(ParameterSetName='Run')][string]$ControlsSha256,
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
if($Recover -and (Test-Path "$Result/recovery.json")){
    $savedMode=(Get-Content "$Result/recovery.json" -Raw | ConvertFrom-Json).mode
    $ShimAdmission=$savedMode -in @('shim-admission','shim-menu-trial','shim-car-trial');$ShimMenuTrial=$savedMode -eq 'shim-menu-trial';$ShimCarTrial=$savedMode -eq 'shim-car-trial'
}
if($ShimCarTrial -and $ShimMenuTrial){throw 'Choose one bounded trial mode.'}
if($ShimMenuTrial -or $ShimCarTrial){$ShimAdmission=$true}
if($ShimAdmission) {
    $files.plugin=Join-Path $plugin 'WodenRallyEdgeWheel.dll'
    $files.core=Join-Path $plugin 'WodenRallyEdge.Core.dll'
    $files.shim=Join-Path $plugin 'dbce_shim.dll'
    $roots.shimReports=Join-Path $GameDir 'BepInEx/WodenShim'
}
$mode=if($ShimCarTrial){'shim-car-trial'}elseif($ShimMenuTrial){'shim-menu-trial'}elseif($ShimAdmission){'shim-admission'}else{'raw'}
$blocked=@($request,$marker,(Join-Path $roots.config 'woden-record-next-launch.json'),(Join-Path $env:LOCALAPPDATA 'Dbce/StagePlayback/woden/request.txt'))
$probe=Join-Path $PSScriptRoot 'Probe/bin/Release/net6.0/Woden.ControlsProbe.dll'
$configVerifier=Join-Path $PSScriptRoot 'VerifyConfig/bin/Release/net8.0/VerifyConfig.dll'
$ready=$false; $lease=$null; $game=$null; $ownedGame=$false; $nonce=$null; $restored=$false; $applied=$false; $verificationError=$null
function Closed {if(Get-Process -Name 'Super Woden Rally Edge' -ErrorAction SilentlyContinue){throw 'Woden is running; no preparation or restoration.'}}
function Set-IniFalse([string]$Section,[string]$Key,[string]$Path=$cfg) {
    $text=[IO.File]::ReadAllText($Path)
    $sections=[regex]::Matches($text,'(?ms)^\['+[regex]::Escape($Section)+'\][ \t]*\r?\n.*?(?=^\[|\z)')
    if($sections.Count -ne 1){throw "Expected exactly one $Section section."}
    $sectionText=$sections[0].Value
    $keyPattern='(?m)^[ \t]*'+[regex]::Escape($Key)+'[ \t]*=[^\r\n]*'
    if([regex]::Matches($sectionText,$keyPattern).Count -ne 1){throw "Expected exactly one $Section/$Key key."}
    $updated=[regex]::Replace($sectionText,$keyPattern,$Key+' = false')
    [IO.File]::WriteAllText($Path,$text.Substring(0,$sections[0].Index)+$updated+$text.Substring($sections[0].Index+$sectionText.Length))
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
function Set-ShimBackend {
    $text=[IO.File]::ReadAllText($cfg)
    $sections=[regex]::Matches($text,'(?ms)^\[Input\][ \t]*\r?\n.*?(?=^\[|\z)')
    if($sections.Count -gt 1){throw 'Duplicate Input section.'}
    if($sections.Count -eq 0){$text+="`r`n[Input]`r`nBackend = Shim`r`n"}
    else {
        $s=$sections[0];$pattern='(?m)^[ \t]*Backend[ \t]*=[^\r\n]*'
        if([regex]::Matches($s.Value,$pattern).Count -ne 1){throw 'Expected one Input/Backend key.'}
        $text=$text.Substring(0,$s.Index)+[regex]::Replace($s.Value,$pattern,'Backend = Shim')+$text.Substring($s.Index+$s.Length)
    }
    [IO.File]::WriteAllText($cfg,$text)
}
Closed; Assert-ControlPlain $Result; Assert-ControlPlain $GameDir
if($Recover) {
    if(Test-Path "$Result/restored.txt"){throw 'Already restored; do not overwrite newer owner settings.'}
    $state=Get-Content "$Result/recovery.json" -Raw | ConvertFrom-Json
    if($state.schema -ne 1 -or $state.gameDir -ne $GameDir -or $state.lease.path -ne $slot -or $state.nonce -notmatch '^[a-f0-9]{32}$'){throw 'Recovery scope differs.'}
    if((Get-ControlHash "$Result/owner-before/manifest.json") -ne $state.filesHash -or (Get-ControlHash "$Result/preferences.json") -ne $state.prefsHash){throw 'Recovery metadata changed.'}
    $nonce=$state.nonce
    if($ShimCarTrial){$roots.recording=Join-Path $GameDir "BepInEx/WodenRecordings/request-$nonce"}
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
    if($ShimAdmission) {
        if(!$CandidateDirectory -or !$ControlsSource -or $ControlsSha256 -notmatch '^[A-Fa-f0-9]{64}$'){throw 'Shim admission requires CandidateDirectory, ControlsSource and its exact ControlsSha256.'}
        $CandidateDirectory=Full $CandidateDirectory;$ControlsSource=Full $ControlsSource
        Assert-ControlPlain $CandidateDirectory;Assert-ControlPlain $ControlsSource
        if((Get-ControlHash $ControlsSource) -ne $ControlsSha256){throw 'Accepted Controls source changed.'}
        $shimCandidate=Full "$PSScriptRoot/../../vendor/dbce-shim/dbce_shim.dll"
        $shimHash='9b8a036f664cf8f45cccf82b98e5e4376f93215b60eac50b6db965bf498015d8'
        if((Get-ControlHash $shimCandidate) -ne $shimHash){throw 'Provisional shim pin differs.'}
        $candidateHashes=[ordered]@{plugin=(Get-ControlHash "$CandidateDirectory/WodenRallyEdgeWheel.dll");core=(Get-ControlHash "$CandidateDirectory/WodenRallyEdge.Core.dll");shim=$shimHash;controls=$ControlsSha256}
        if($candidateHashes.Values -contains 'absent'){throw 'Production candidate is incomplete.'}
        if((Get-ControlHash (Join-Path $GameDir 'GameAssembly.dll')) -ne 'f422894d8d2b0df4edb7e5259e5e60cb8c4f8dea2e85ebdfc09dd6766349250c'){throw 'Supported game build differs.'}
        if($ShimCarTrial) {
            $StageReference=Full $StageReference;$OriginalWorkload=Full $OriginalWorkload
            foreach($p in @($StageReference,$OriginalWorkload)){Assert-ControlPlain $p}
            if($ControlsSha256 -ne '5206EDFBD1B400E5D6A41D91AE836485BBE27D63992AD6F7483B3BABB0E72B79' -or
               (Get-ControlHash "$OriginalWorkload/raw-workload.json") -ne '4AB26FDA741482B8E229BF3097A2558B21C27E09D4CA45CF2B3E255A970BA03C' -or
               (Get-ControlHash "$OriginalWorkload/apply/profile.json") -ne '9317FAE5C8B029E71713EB7DD665D650EA1452C0E1CFEDA4C5E1335E33452279' -or
               (Get-ControlHash "$StageReference/stage-context.json") -ne '711747D3021A6E95B600E8386DC149C79102F837D10B981642210F2A634B813F'){throw 'Independent original profile/workload/Kenya context differs.'}
        }
    }
    elseif($CandidateDirectory -or $ControlsSource -or $ControlsSha256){throw 'Shim parameters require ShimAdmission.'}
    if([double](& "$HubRepo/tools/Owner-Input.ps1" -IdleSeconds) -lt 300){throw 'Owner input is recent.'}
}
if(!$lease){$lease=Enter-StageRigLease -Path $slot -Owner romi-woden-controls -Purpose "$mode; no force; exact restoration"}
$harness=Join-Path $Result 'harness/ConfigurationQualification.dll'; $live=Join-Path $Result 'apply'
try {
    Closed
    if($Recover){$state.lease=$lease;$state | ConvertTo-Json -Depth 5 | Set-Content "$Result/recovery.json";return}
    [IO.Directory]::CreateDirectory($Result) | Out-Null
    $writer=$null
    if(!$ShimAdmission){$writer=& "$PSScriptRoot/Freeze-Wheelkit.ps1" -Result $Result -Repo $WheelkitRepo;$harness=$writer.harness}
    Copy-Item -LiteralPath (Split-Path -Parent $configVerifier) -Destination "$Result/config-verifier" -Recurse
    $configVerifier=Join-Path $Result 'config-verifier/VerifyConfig.dll'
    Get-ChildItem "$Result/config-verifier" -File | Sort-Object Name | ForEach-Object {@{name=$_.Name;sha256=(Get-ControlHash $_.FullName)}} | ConvertTo-Json | Set-Content "$Result/config-verifier-hashes.json"
    $nonce=[guid]::NewGuid().ToString('N')
    if($ShimCarTrial) {
        $roots.recording=Join-Path $GameDir "BepInEx/WodenRecordings/request-$nonce"
        Assert-ControlPlain $roots.recording
        if(Test-Path -LiteralPath $roots.recording){throw 'Car trial recording directory must be new.'}
        [IO.Directory]::CreateDirectory("$Result/workload")|Out-Null
        foreach($pair in @(@("$OriginalWorkload/raw-workload.json","$Result/workload/raw-workload.json"),@("$OriginalWorkload/apply/profile.json","$Result/workload/profile.json"))){Copy-ControlExact $pair[0] $pair[1] (Get-ControlHash $pair[0])}
    }
    Save-ControlFiles "$Result/owner-before" $roots $files
    Registry-Snapshot "$Result/preferences.json"
    $state=[ordered]@{schema=1;mode=$mode;gameDir=$GameDir;lease=$lease;nonce=$nonce;filesHash=(Get-ControlHash "$Result/owner-before/manifest.json");prefsHash=(Get-ControlHash "$Result/preferences.json");writerReceiptHash=$writer.receiptHash}
    $state | ConvertTo-Json -Depth 5 | Set-Content "$Result/recovery.json"
    $ready=$true
    foreach($pair in @(@('ForceFeedback','Enabled'),@('Telemetry','Enabled'),@('Diagnostics','RecordSession'),@('Dev','InputCommandFile'))){Set-IniFalse $pair[0] $pair[1]}
    if($ShimAdmission) {
        $candidateHashes | ConvertTo-Json | Set-Content "$Result/candidate-hashes.json"
        Copy-ControlExact "$CandidateDirectory/WodenRallyEdgeWheel.dll" $files.plugin $candidateHashes.plugin
        Copy-ControlExact "$CandidateDirectory/WodenRallyEdge.Core.dll" $files.core $candidateHashes.core
        Copy-ControlExact $shimCandidate $files.shim $shimHash
        Copy-ControlExact $ControlsSource "$Result/accepted-controls.ini" $ControlsSha256
        Copy-ControlExact "$Result/accepted-controls.ini" "$($roots.config)/dbce-controls.ini" $ControlsSha256
        Set-ShimBackend
        # The console previously kept actual Unity focus false. This temporary
        # launch setting is part of the exact config-tree backup/restoration;
        # it does not move focus or bypass the production focus gate.
        if($ShimMenuTrial -or $ShimCarTrial){Set-IniFalse 'Logging.Console' 'Enabled' (Join-Path $roots.config 'BepInEx.cfg')}
    } else {Invoke-ControlApplyCheck $GameDir $Result $harness $writer.catalog $Profiles}
    $applied=$true
    # Verify again after production Apply and before launch. The addon also
    # checks the settings as loaded by the game before it arms raw input.
    Assert-OutputMute
    Copy-ControlExact $cfg "$Result/applied.cfg" (Get-ControlHash $cfg)
    if(!$ShimAdmission){& dotnet $harness raw-workload "$live/profile.json" "$Result/raw-workload.json";if($LASTEXITCODE){throw 'Independent raw workload failed.'}}
    Copy-ControlExact $NativeCandidate $files.native $NativeSha256
    Copy-ControlExact $probe $files.probe (Get-ControlHash $probe)
    $cold=[ordered]@{schema=2;nonce=$nonce;expiresUtc=[DateTime]::UtcNow.AddMinutes(5).ToString('o');pluginSha256=(Get-ControlHash "$plugin/WodenRallyEdgeWheel.dll");coreSha256=(Get-ControlHash "$plugin/WodenRallyEdge.Core.dll");configSha256=(Get-ControlHash "$Result/applied.cfg");bindingsSha256=(Get-ControlHash "$($roots.config)/wheel-bindings.json");nativeSha256=$NativeSha256;outputDirectory=(Join-Path $Result 'trace')}
    if($ShimAdmission){$cold.schema=if($ShimCarTrial){5}elseif($ShimMenuTrial){4}else{3};$cold.mode=$mode;$cold.controlsSha256=$ControlsSha256;$cold.shimSha256=$shimHash}
    if($ShimCarTrial) {
        $cold.stageReference=$StageReference;$cold.workloadDirectory=Join-Path $Result 'workload'
        # Existing ordinary diagnostic producer; schema 5 explicitly permits
        # this exact nonce/case/expiry. No stage replay or pose request is made.
        [ordered]@{Version=2;Id=([guid]::ParseExact($nonce,'N').ToString());ExpiresUtc=$cold.expiresUtc;DisableForces=$true;CaseId="woden-shim-car-$nonce"} |
            ConvertTo-Json | Set-Content (Join-Path $roots.config 'woden-record-next-launch.json')
    }
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
    [ordered]@{schema=1;mode=$mode;process=$game.Id;startedUtc=$game.StartTime.ToUniversalTime().ToString('o');nonce=$nonce;gameDir=$GameDir;lease=$lease} | ConvertTo-Json -Depth 4 | Set-Content "$Result/context.json"
    $deadline=[DateTime]::UtcNow.AddSeconds(60)
    while(!(Test-Path "$Result/trace/identity.json") -and !$game.HasExited -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 500;$game.Refresh()}
    if(!(Test-Path "$Result/trace/identity.json")){throw 'Probe did not confirm native no-force admission.'}
    & "$PSScriptRoot/Send-Command.ps1" -Result $Result -Operation status
    Write-Output "Armed $mode; inspect state before every command. Evidence $Result"
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
            $strict=$null
            if(!$ShimAdmission){& dotnet $harness verify-live $live *> "$Result/verify-live.log";$strict=$LASTEXITCODE;"exit=$strict" | Set-Content "$Result/verify.txt"}
            else {
                if((Get-ControlHash "$($roots.config)/dbce-controls.ini") -ne $ControlsSha256){throw 'Runtime changed accepted Controls bytes.'}
                foreach($key in @('plugin','core','shim')){if((Get-ControlHash $files[$key]) -ne $candidateHashes[$key]){throw "Runtime candidate changed: $key"}}
            }
            # BepInEx can rewrite comments/order and append a short, explicit
            # list of missing defaults during Load. Every original value must
            # survive, and bindings still require exact production-Apply bytes.
            Copy-ControlExact $cfg "$Result/loaded.cfg" (Get-ControlHash $cfg)
            & dotnet $configVerifier "$Result/applied.cfg" "$Result/loaded.cfg" $cold.configSha256 > "$Result/config-verification.json"
            if($LASTEXITCODE){throw 'Loaded configuration changed semantics.'}
            if((Get-ControlHash "$($roots.config)/wheel-bindings.json") -ne $cold.bindingsSha256){throw 'Runtime changed applied binding bytes.'}
            [ordered]@{status='passed';mode=$mode;strictByteCheckExit=$strict;config='values retained; only known defaults allowed';bindings=$(if($ShimAdmission){'exact owner bytes; accepted Controls unchanged'}else{'exact applied bytes'})} | ConvertTo-Json | Set-Content "$Result/runtime-verification.json"
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
