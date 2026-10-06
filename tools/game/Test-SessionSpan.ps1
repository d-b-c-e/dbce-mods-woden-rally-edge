# Device-free temporary-config fixtures. Never reads or modifies the installed game.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ReplayEnvironment.ps1')
$scratch = Join-Path $PSScriptRoot ('../../results/span-config-fixtures-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $scratch
$cases = @(
    "[Triple]`r`nMode = Auto`r`nSpanSeparateMonitors = false`r`n[Other]`r`nSpanSeparateMonitors = false`r`n",
    "[Triple]`nMode = Auto",
    "[Other]`nMode = Auto`n",
    "[Triple]`n  SpanSeparateMonitors = false`n",
    "[Triple]`nSpanSeparateMonitors = true`n"
)
$checks = 0
foreach ($text in $cases) {
    $path = Join-Path $scratch ($checks.ToString() + '.cfg')
    [IO.File]::WriteAllText($path, $text)
    Enable-SessionSpan $path
    $actual = [IO.File]::ReadAllText($path)
    if ([regex]::Matches($actual, '(?m)^SpanSeparateMonitors = true$').Count -ne 1 -and
        [regex]::Matches($actual, '(?m)^SpanSeparateMonitors = true\r?$').Count -ne 1) { throw 'Expected one enabled span key' }
    if ($text.Contains('[Other]') -and !$actual.Contains(($text -split '\[Other\]',2)[1].TrimEnd("`r", "`n"))) { throw 'Unrelated section changed' }
    if ($text.Contains('Mode = Auto') -and !$actual.Contains('Mode = Auto')) { throw 'Unrelated key changed' }
    $checks++
}
foreach ($text in @("[Triple]`nMode = Auto`n[Triple]`nMode = On`n", "[Triple]`nSpanSeparateMonitors = false`nSpanSeparateMonitors = true`n")) {
    $path = Join-Path $scratch ($checks.ToString() + '.cfg')
    [IO.File]::WriteAllText($path, $text)
    $refused = $false
    try { Enable-SessionSpan $path } catch { if ($_.Exception.Message -notlike 'Duplicate*') { throw }; $refused = $true }
    if (!$refused -or [IO.File]::ReadAllText($path) -cne $text) { throw 'Ambiguous input was not preserved and refused' }
    $checks++
}
Write-Output "PASS: $checks temporary span-config cases; no game or device access."
