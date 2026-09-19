param(
    [string]$GameDirectory = 'D:\Program Files (x86)\Steam\steamapps\common\Super Woden Rally Edge',
    [switch]$AttendedFfb,
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
$gameRoot = (Resolve-Path -LiteralPath $GameDirectory).Path
$exe = Join-Path $gameRoot 'Super Woden Rally Edge.exe'
if (!(Test-Path -LiteralPath $exe)) { throw 'Woden executable not found.' }
if (Get-Process -Name 'Super Woden Rally Edge' -ErrorAction SilentlyContinue) { throw 'Close Woden normally before preparing a recorded launch.' }
if ((Get-FileHash -LiteralPath (Join-Path $gameRoot 'GameAssembly.dll')).Hash -ne 'F422894D8D2B0DF4EDB7E5259E5E60CB8C4F8DEA2E85EBDFC09DD6766349250C') { throw 'Unsupported game build.' }
$plugin = Join-Path $gameRoot 'BepInEx/plugins/WodenRallyEdgeWheel/WodenRallyEdgeWheel.dll'
if (!(Test-Path -LiteralPath $plugin) -or [version][Diagnostics.FileVersionInfo]::GetVersionInfo($plugin).FileVersion -lt [version]'0.2.3') { throw 'Install Woden Wheel 0.2.3 or later first.' }
$requestPath = Join-Path $gameRoot 'BepInEx/config/woden-record-next-launch.json'
if (Test-Path -LiteralPath $requestPath) { throw "A launch request already exists: $requestPath. Inspect it before replacing it." }
$request = [ordered]@{ Version = 1; Id = [guid]::NewGuid().ToString(); ExpiresUtc = [DateTimeOffset]::UtcNow.AddMinutes(15).ToString('o'); DisableForces = !$AttendedFfb }
$request | ConvertTo-Json | Set-Content -LiteralPath $requestPath -Encoding utf8
if (!$PrepareOnly) { Start-Process -FilePath 'steam://rungameid/3218630' -WindowStyle Hidden }
Write-Host "Capture requested for one launch: $($request.Id). Expires in 15 minutes."
Write-Host "Physical FFB allowed for this diagnostic launch: $([bool]$AttendedFfb). Saved preferences are preserved."
Write-Host "Close the game normally to finalize BepInEx/WodenRecordings (20 minutes / 64 MiB maximum)."
