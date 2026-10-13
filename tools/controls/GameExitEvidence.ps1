#Requires -Version 7.0
# Keep process closure separate from a CloseMainWindow request and from a known numeric exit code.
function New-GameExitEvidence {
    [CmdletBinding()]
    param([bool]$CloseMainWindowRequested,[bool]$Exited,[AllowNull()][object]$ExitCode,[int]$ProcessId)
    if($null -ne $ExitCode -and $ExitCode -isnot [int]){throw 'Game exit code must be a numeric Int32 or null.'}
    if(!$Exited -and $null -ne $ExitCode){throw 'A running game cannot have an observed exit code.'}
    $pass=$Exited -and $null -ne $ExitCode -and $ExitCode -eq 0
    return [pscustomobject]@{schema=1;closeMainWindowRequested=$CloseMainWindowRequested;exited=$Exited;
        exitCode=$ExitCode;shutdownPass=$pass;processId=$ProcessId}
}
