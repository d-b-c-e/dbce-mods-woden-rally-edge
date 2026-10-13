#Requires -Version 7.0
$ErrorActionPreference='Stop'
. "$PSScriptRoot/GameExitEvidence.ps1"
$zero=New-GameExitEvidence -CloseMainWindowRequested $true -Exited $true -ExitCode ([int]0) -ProcessId 1
$nonzero=New-GameExitEvidence -CloseMainWindowRequested $true -Exited $true -ExitCode ([int]-1073741819) -ProcessId 1
$unknown=New-GameExitEvidence -CloseMainWindowRequested $true -Exited $true -ExitCode $null -ProcessId 1
$running=New-GameExitEvidence -CloseMainWindowRequested $false -Exited $false -ExitCode $null -ProcessId 1
if(!$zero.shutdownPass -or $zero.exitCode -isnot [int] -or
   $nonzero.shutdownPass -or $nonzero.exitCode -ne -1073741819 -or
   $unknown.shutdownPass -or $null -ne $unknown.exitCode -or
   $running.shutdownPass -or $null -ne $running.exitCode){throw 'Game exit evidence classified zero, nonzero or null incorrectly.'}
$json=$unknown | ConvertTo-Json -Compress
if($json -notmatch '"exitCode":null' -or $json -notmatch '"shutdownPass":false'){throw 'Unknown exit code was not serialized as null.'}
Write-Output 'PASS zero/nonzero/null/running exit evidence and null JSON serialization.'
