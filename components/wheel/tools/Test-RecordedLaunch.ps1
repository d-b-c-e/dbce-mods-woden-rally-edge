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
    if (!$read.DisableForces -or $read.Version -ne 2 -or $read.CaseId -ne 'fixture-drive' -or [DateTimeOffset]$read.ExpiresUtc -le [DateTimeOffset]::UtcNow -or
        $read.PSObject.Properties['InputOrigin']) { throw 'Incorrect safe/default request or an unstated origin was invented.' }
    if (Test-Path -LiteralPath (Join-Path $fixture ('BepInEx/WodenRecordings/request-' + ([guid]$read.Id).ToString('N')))) { throw 'PrepareOnly created a recording directory.' }
    $hash = (Get-FileHash -LiteralPath $request).Hash
    $refused = $false
    try { & (Join-Path $PSScriptRoot 'Start-RecordedGame.ps1') -GameDirectory $fixture -PrepareOnly -CaseId 'fixture-drive' } catch { if ($_.Exception.Message -notlike '*already exists*') { throw }; $refused = $true }
    if (!$refused -or (Get-FileHash -LiteralPath $request).Hash -ne $hash) { throw 'Duplicate request was not preserved.' }
    Remove-Item -LiteralPath $request
    $invalid = $false
    try { & (Join-Path $PSScriptRoot 'Start-RecordedGame.ps1') -GameDirectory $fixture -PrepareOnly -CaseId '../escape' } catch { if ($_.Exception.Message -notlike '*shared identifier*') { throw }; $invalid = $true }
    if (!$invalid -or (Test-Path -LiteralPath $request)) { throw 'Unsafe case identity was not refused before writing a request.' }
    & (Join-Path $PSScriptRoot 'Start-RecordedGame.ps1') -GameDirectory $fixture -PrepareOnly -AttendedFfb -InputOrigin human -CaseId 'fixture-drive:attended'
    $attended=Get-Content -LiteralPath $request -Raw | ConvertFrom-Json
    if ($attended.DisableForces -or $attended.InputOrigin -cne 'human' -or $attended.Version -ne 2) { throw 'Attended human request did not retain its explicit origin and force mode.' }
    Remove-Item -LiteralPath $request
    & (Join-Path $PSScriptRoot 'Start-RecordedGame.ps1') -GameDirectory $fixture -PrepareOnly -InputOrigin automated -CaseId 'fixture-drive:automated'
    $automated=Get-Content -LiteralPath $request -Raw | ConvertFrom-Json
    if (!$automated.DisableForces -or $automated.InputOrigin -cne 'automated' -or $automated.Version -ne 2) { throw 'Automated request did not retain its explicit origin and no-force mode.' }
    Remove-Item -LiteralPath $request
    $invalidOrigin=$false
    try { & (Join-Path $PSScriptRoot 'Start-RecordedGame.ps1') -GameDirectory $fixture -PrepareOnly -InputOrigin Human -CaseId 'fixture-drive:case' }
    catch { if ($_.Exception.Message -notmatch 'exactly human or automated') { throw }; $invalidOrigin=$true }
    if (!$invalidOrigin -or (Test-Path -LiteralPath $request)) { throw 'Mixed-case origin was not refused before writing a request.' }
    $forceRefused=$false
    try { & (Join-Path $PSScriptRoot 'Start-RecordedGame.ps1') -GameDirectory $fixture -PrepareOnly -AttendedFfb -InputOrigin automated -CaseId 'fixture-drive:unsafe' }
    catch { if ($_.Exception.Message -notmatch 'Automated input cannot request physical FFB') { throw }; $forceRefused=$true }
    if (!$forceRefused -or (Test-Path -LiteralPath $request)) { throw 'Automated input with physical FFB was not refused before writing a request.' }
    $stopRefused=$false
    try { & (Join-Path $PSScriptRoot 'Start-RecordedGame.ps1') -GameDirectory $fixture -Stop -RequestId ([guid]::NewGuid()) -InputOrigin automated }
    catch { if ($_.Exception.Message -notmatch 'Stop requires only an exact RequestId') { throw }; $stopRefused=$true }
    if (!$stopRefused -or (Test-Path -LiteralPath $request)) { throw 'Stop accepted a new input-origin claim.' }
    if ((Get-FileHash -LiteralPath $config).Hash -ne $before) { throw 'Saved configuration changed.' }
    Write-Host 'PASS: v2 case/request identity, omitted/explicit input origin, no-force automated guard, duplicate/unsafe refusal, preserved configuration; no game launched.'
} finally {
    Remove-Item -LiteralPath $exe
    if (Test-Path -LiteralPath $request) { Remove-Item -LiteralPath $request }
}
