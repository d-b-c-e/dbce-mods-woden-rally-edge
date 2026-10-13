#Requires -Version 7.0
# Source-only contract check: the FullSession runner must use the frozen ordinary probe/verdict mode.
$ErrorActionPreference='Stop'
. "$PSScriptRoot/ControlMode.ps1"
. "$PSScriptRoot/Trial-Result.ps1"
$mode=Resolve-ControlMode -ShimHudStopOnly $false -ShimHudOrdinary $false -ShimForceCapture $false `
    -OrdinarySavedForward $true -OrdinaryFullSession $true -ShimAppliedForwardCapture $true `
    -ShimCarTrial $true -PrivateApply $true -ShimMenuTrial $false -ShimAdmission $true
if($mode -cne 'shim-applied-forward-ordinary'){throw "FullSession selected an unsupported probe mode: $mode"}
$ordinary=Resolve-ControlMode -ShimHudStopOnly $false -ShimHudOrdinary $false -ShimForceCapture $false `
    -OrdinarySavedForward $true -OrdinaryFullSession $false -ShimAppliedForwardCapture $true `
    -ShimCarTrial $true -PrivateApply $true -ShimMenuTrial $false -ShimAdmission $true
if($ordinary -cne $mode){throw 'FullSession changed the frozen ordinary probe mode.'}
$probe=Join-Path $PSScriptRoot 'Probe/bin/Release/net6.0/Woden.ControlsProbe.dll'
if((Get-FileHash -LiteralPath $probe -Algorithm SHA256).Hash -ne '36A198CA26510B921DBEB4FB2979268EA3498661CCA1FC600BB4D59E02D60E22'){
    throw 'The frozen probe differs from the S1 capture pin.'
}
Add-Type -Path $probe
$now=[DateTimeOffset]::UtcNow
$nonce=[guid]::NewGuid().ToString('N')
$hash='a'*64
$root=[IO.Path]::GetFullPath($PSScriptRoot)
# The same schema-7 field set emitted by Run-Controls for the private applied-forward capture.
$request=[ordered]@{schema=7;nonce=$nonce;expiresUtc=$now.AddMinutes(2).ToString('o');
    pluginSha256=$hash;coreSha256=$hash;configSha256=$hash;bindingsSha256=$hash;nativeSha256=$hash;
    outputDirectory=$root;mode=$mode;controlsSha256=$hash;shimSha256=$hash;
    stageReference=$root;workloadDirectory=$root;profileSha256=$hash;workloadSha256=$hash;
    profileRevision=$hash;applyReadbackSha256=$hash}
$bytes=[Text.Encoding]::UTF8.GetBytes(($request | ConvertTo-Json -Compress))
$admitted=[Woden.ControlsTesting.Protocol]::Admit($bytes,$now,$nonce)
if($admitted.Mode -cne $mode -or ![Woden.ControlsTesting.Protocol]::OrdinaryForward($admitted) -or
   ![Woden.ControlsTesting.Protocol]::ForceCapture($admitted)){
    throw 'The frozen probe did not admit the FullSession ordinary request.'
}
$request.mode='shim-applied-forward-full-session'
$unsupported=[Text.Encoding]::UTF8.GetBytes(($request | ConvertTo-Json -Compress))
$refused=$false
try { $null=[Woden.ControlsTesting.Protocol]::Admit($unsupported,$now,$nonce) }
catch [IO.InvalidDataException] { $refused=$true }
if(!$refused){throw 'The frozen probe unexpectedly admitted the unsupported FullSession mode.'}
$missing=Join-Path $root ('missing-full-session-verdict-'+$nonce)
try {
    $null=Assert-ControlTrialResult -Result $missing -Mode $mode
    throw 'The ordinary trial adjudicator silently skipped missing evidence.'
} catch {
    if($_.Exception.Message -cne 'Owner restoration is not complete.'){throw}
}
Write-Output 'PASS FullSession request admitted by frozen probe and routed to the ordinary trial adjudicator.'
