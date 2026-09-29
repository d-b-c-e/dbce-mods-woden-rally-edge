param(
    [string]$GameDirectory = 'D:\Program Files (x86)\Steam\steamapps\common\Super Woden Rally Edge',
    [string]$CaseId,
    [switch]$AttendedFfb,
    [switch]$PrepareOnly,
    [ValidateRange(15,600)][int]$StartupTimeoutSeconds = 180,
    [ValidateRange(1,30)][int]$SessionTimeoutMinutes = 25
)
$ErrorActionPreference = 'Stop'
$gameRoot = (Resolve-Path -LiteralPath $GameDirectory).Path
$exe = Join-Path $gameRoot 'Super Woden Rally Edge.exe'
if (!(Test-Path -LiteralPath $exe)) { throw 'Woden executable not found.' }
if (Get-Process -Name 'Super Woden Rally Edge' -ErrorAction SilentlyContinue) { throw 'Close Woden normally before preparing a recorded launch.' }
if ((Get-FileHash -LiteralPath (Join-Path $gameRoot 'GameAssembly.dll')).Hash -ne 'F422894D8D2B0DF4EDB7E5259E5E60CB8C4F8DEA2E85EBDFC09DD6766349250C') { throw 'Unsupported game build.' }
$plugin = Join-Path $gameRoot 'BepInEx/plugins/WodenRallyEdgeWheel/WodenRallyEdgeWheel.dll'
if (!(Test-Path -LiteralPath $plugin) -or [version][Diagnostics.FileVersionInfo]::GetVersionInfo($plugin).FileVersion -lt [version]'0.2.11') { throw 'Install Woden Wheel 0.2.11 or later first.' }
$requestId = [guid]::NewGuid()
if (!$CaseId) { $CaseId = 'woden-drive-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') }
if ($CaseId -notmatch '^[A-Za-z0-9][A-Za-z0-9_.:@-]{0,127}$') { throw 'CaseId must follow the shared identifier form: 1-128 ASCII letters, digits, dot, underscore, colon, at-sign or hyphen, beginning with a letter or digit.' }
$requestPath = Join-Path $gameRoot 'BepInEx/config/woden-record-next-launch.json'
if (Test-Path -LiteralPath $requestPath) { throw "A launch request already exists: $requestPath. Inspect it before replacing it." }
$request = [ordered]@{ Version = 2; Id = $requestId.ToString(); ExpiresUtc = [DateTimeOffset]::UtcNow.AddMinutes(15).ToString('o'); DisableForces = !$AttendedFfb; CaseId = $CaseId }
$request | ConvertTo-Json | Set-Content -LiteralPath $requestPath -Encoding utf8
Write-Host "Capture requested for one launch: $($request.Id). Expires in 15 minutes."
Write-Host "Case: $CaseId (signal reprocessing; not deterministic game-driving replay)."
Write-Host "Physical FFB allowed for this diagnostic launch: $([bool]$AttendedFfb). Saved preferences are preserved."
$captureDirectory = Join-Path $gameRoot ('BepInEx/WodenRecordings/request-' + $requestId.ToString('N'))
Write-Host "Expected capture directory: $captureDirectory"
if ($PrepareOnly) { Write-Host 'PrepareOnly: no game launched, input injected, device opened or force actuated.'; return }

Start-Process -FilePath 'steam://rungameid/3218630' -WindowStyle Hidden | Out-Null
$startupDeadline = [DateTimeOffset]::UtcNow.AddSeconds($StartupTimeoutSeconds)
do {
    $running = @(Get-Process -Name 'Super Woden Rally Edge' -ErrorAction SilentlyContinue)
    if ($running.Count -gt 0) { break }
    Start-Sleep -Milliseconds 250
} while ([DateTimeOffset]::UtcNow -lt $startupDeadline)
if ($running.Count -eq 0) { throw "Woden did not appear within $StartupTimeoutSeconds seconds. No process was stopped; the expiring request remains at $requestPath." }
Write-Host "Woden started. Close it normally when the seated drive is complete (20 minutes / 64 MiB capture maximum)."
$sessionDeadline = [DateTimeOffset]::UtcNow.AddMinutes($SessionTimeoutMinutes)
do {
    Start-Sleep -Milliseconds 500
    $running = @(Get-Process -Name 'Super Woden Rally Edge' -ErrorAction SilentlyContinue)
} while ($running.Count -gt 0 -and [DateTimeOffset]::UtcNow -lt $sessionDeadline)
if ($running.Count -gt 0) { throw "Woden is still running after $SessionTimeoutMinutes minutes. No process was stopped. Close it normally; expected capture remains $captureDirectory." }

$source = Join-Path $captureDirectory 'source.jsonl'
$finalizeDeadline = [DateTimeOffset]::UtcNow.AddSeconds(15)
while (!(Test-Path -LiteralPath $source) -and [DateTimeOffset]::UtcNow -lt $finalizeDeadline) { Start-Sleep -Milliseconds 250 }
if (!(Test-Path -LiteralPath $source)) { throw "Woden exited but the correlated source recording was not found: $source" }
$inspector = Join-Path $PSScriptRoot 'TelemetryInspector/TelemetryInspector.csproj'
& dotnet run --project $inspector -c Release -- reprocess $captureDirectory
if ($LASTEXITCODE -ne 0) { throw "Recording finalized but driving readiness/signal reprocessing failed. Source retained unchanged: $source" }
$observation = Join-Path $captureDirectory 'force-observation.jsonl'
Write-Host "Source: $source"
Write-Host "Source SHA-256: $((Get-FileHash -LiteralPath $source).Hash.ToLowerInvariant())"
Write-Host "Observation: $observation"
Write-Host "Observation SHA-256: $((Get-FileHash -LiteralPath $observation).Hash.ToLowerInvariant())"
Write-Host "Case manifest: $(Join-Path $captureDirectory 'case.json')"
