param([Parameter(Mandatory)][string]$InstallerFixture)
$ErrorActionPreference = 'Stop'
$fixture = (Resolve-Path -LiteralPath $InstallerFixture).Path
$artifacts = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../artifacts')).TrimEnd('\')
if (!$fixture.StartsWith($artifacts + '\', [StringComparison]::OrdinalIgnoreCase) -or (Split-Path $fixture -Leaf) -notlike 'installer-test-*') { throw 'Only a repository installer-test fixture is allowed.' }
$exe = Join-Path $fixture 'Super Woden Rally Edge.exe'
if (Test-Path -LiteralPath $exe) { throw 'Fixture must not contain an executable.' }
# A text placeholder is only used for discovery. PrepareOnly never executes it.
Set-Content -LiteralPath $exe -Value 'Non-executable test placeholder.'
$config = Join-Path $fixture 'BepInEx/config/recording-launch-fixture.txt'
Set-Content -LiteralPath $config -Value 'Saved FFB On and owner settings must be untouched.'
$before = (Get-FileHash -LiteralPath $config).Hash
$request = Join-Path $fixture 'BepInEx/config/woden-record-next-launch.json'
try {
    & (Join-Path $PSScriptRoot 'Start-RecordedGame.ps1') -GameDirectory $fixture -PrepareOnly -CaseId 'fixture-drive'
    $read = Get-Content -LiteralPath $request -Raw | ConvertFrom-Json
    if (!$read.DisableForces -or $read.Version -ne 2 -or $read.CaseId -ne 'fixture-drive' -or [DateTimeOffset]$read.ExpiresUtc -le [DateTimeOffset]::UtcNow) { throw 'Incorrect safe/default request.' }
    if (Test-Path -LiteralPath (Join-Path $fixture ('BepInEx/WodenRecordings/request-' + ([guid]$read.Id).ToString('N')))) { throw 'PrepareOnly created a recording directory.' }
    $hash = (Get-FileHash -LiteralPath $request).Hash
    $refused = $false
    try { & (Join-Path $PSScriptRoot 'Start-RecordedGame.ps1') -GameDirectory $fixture -PrepareOnly -CaseId 'fixture-drive' } catch { if ($_.Exception.Message -notlike '*already exists*') { throw }; $refused = $true }
    if (!$refused -or (Get-FileHash -LiteralPath $request).Hash -ne $hash) { throw 'Duplicate request was not preserved.' }
    Remove-Item -LiteralPath $request
    $invalid = $false
    try { & (Join-Path $PSScriptRoot 'Start-RecordedGame.ps1') -GameDirectory $fixture -PrepareOnly -CaseId '../escape' } catch { if ($_.Exception.Message -notlike '*shared identifier*') { throw }; $invalid = $true }
    if (!$invalid -or (Test-Path -LiteralPath $request)) { throw 'Unsafe case identity was not refused before writing a request.' }
    & (Join-Path $PSScriptRoot 'Start-RecordedGame.ps1') -GameDirectory $fixture -PrepareOnly -AttendedFfb -CaseId 'fixture-drive:attended'
    if ((Get-Content -LiteralPath $request -Raw | ConvertFrom-Json).DisableForces) { throw 'Attended request did not retain requested mode.' }
    if ((Get-FileHash -LiteralPath $config).Hash -ne $before) { throw 'Saved configuration changed.' }
    Write-Host 'PASS: v2 case/request identity, default force suppression, duplicate/unsafe refusal, attended flag, preserved configuration; no game launched.'
} finally {
    Remove-Item -LiteralPath $exe
    if (Test-Path -LiteralPath $request) { Remove-Item -LiteralPath $request }
}
