#Requires -Version 7.0
# Pure supervision record shared by the inner runner and outer owner restore.
function Write-OrdinaryCompanionGuard {
    param([Parameter(Mandatory)][string]$Path,[Parameter(Mandatory)][string]$State,
        [Parameter(Mandatory)][string]$RequestId,[Parameter(Mandatory)][string]$Executable,
        [Parameter(Mandatory)][string]$Script,[int]$ProcessId=0,[long]$StartedUtcTicks=0,
        [bool]$GameObserved=$false,[int]$ExitCode=-1)
    if($State -notin @('pending','running','exited') -or $RequestId -notmatch '^[a-f0-9]{32}$'){
        throw 'Invalid ordinary companion supervision state.'
    }
    $row=[ordered]@{schema=1;state=$State;requestId=$RequestId;executable=[IO.Path]::GetFullPath($Executable);
        script=[IO.Path]::GetFullPath($Script);processId=$ProcessId;startedUtcTicks=$StartedUtcTicks;
        gameObserved=$GameObserved;exitCode=$ExitCode}
    $temp=$Path+'.tmp-'+[guid]::NewGuid().ToString('N')
    try {
        [IO.File]::WriteAllText($temp,($row | ConvertTo-Json -Compress),[Text.UTF8Encoding]::new($false))
        [IO.File]::Move($temp,$Path,$true)
    } finally {if(Test-Path -LiteralPath $temp){Remove-Item -LiteralPath $temp}}
}
function Assert-OrdinaryCompanionQuiescent {
    param([Parameter(Mandatory)][string]$Path,[Parameter(Mandatory)][string]$RequestId,
        [Parameter(Mandatory)][string]$ExpectedExecutable,[Parameter(Mandatory)][string]$ExpectedScript)
    if(!(Test-Path -LiteralPath $Path -PathType Leaf)){return}
    if((Get-Item -LiteralPath $Path).Length -gt 8192){throw 'Ordinary companion guard exceeds its bound; retain lease and owner state.'}
    $row=Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    $exitCode=0
    if($row.schema -ne 1 -or $row.requestId -cne $RequestId -or
       $row.state -cne 'exited' -or $row.gameObserved -ne $true -or
       $row.processId -le 0 -or $row.startedUtcTicks -le 0 -or
       ![int]::TryParse([string]$row.exitCode,[ref]$exitCode) -or
       ![IO.Path]::IsPathFullyQualified([string]$row.executable) -or
       ![IO.Path]::IsPathFullyQualified([string]$row.script) -or
       ![string]::Equals([IO.Path]::GetFullPath([string]$row.executable),[IO.Path]::GetFullPath($ExpectedExecutable),[StringComparison]::OrdinalIgnoreCase) -or
       ![string]::Equals([IO.Path]::GetFullPath([string]$row.script),[IO.Path]::GetFullPath($ExpectedScript),[StringComparison]::OrdinalIgnoreCase)){
        throw 'Ordinary companion can still launch or was never observed with its game; retain lease and owner state.'
    }
    $live=Get-Process -Id ([int]$row.processId) -ErrorAction SilentlyContinue
    if($live -and $live.StartTime.ToUniversalTime().Ticks -eq [long]$row.startedUtcTicks){
        throw 'Exact ordinary companion is still running; retain lease and owner state.'
    }
}
