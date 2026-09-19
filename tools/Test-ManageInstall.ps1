[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$PackageRoot)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path $root ('artifacts\managed-installer-test-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $fixture
# Identity-only data in an ignored fixture; no executable, game launch or devices.
Copy-Item -LiteralPath 'D:\Program Files (x86)\Steam\steamapps\common\Super Woden Rally Edge\GameAssembly.dll' -Destination $fixture
$installer = Join-Path $PackageRoot 'Manage-Install.ps1'
$archive = Join-Path $root 'lib\loader\BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788+5b766a3.zip'
$plugin = Join-Path $fixture 'BepInEx\plugins\WodenRallyEdgeWheel'
$config = Join-Path $fixture 'BepInEx\config'
$receipt = Join-Path $fixture 'BepInEx\WodenWheel-install.json'
$script:checks = 0
function Check([bool]$value,[string]$message) { if (-not $value) { throw $message }; $script:checks++ }
function Refuses([scriptblock]$action,[string]$pattern) {
    $caught = $false
    try { & $action } catch { if ($_.Exception.Message -notmatch $pattern) { throw }; $caught = $true }
    Check $caught ('Expected refusal: ' + $pattern)
}
function Hash([string]$path) { (Get-FileHash -LiteralPath $path).Hash }
function Install { & $installer -GameDir $fixture -PackageRoot $PackageRoot -LoaderArchive $archive }
Refuses { & $installer -GameDir $fixture -PackageRoot $PackageRoot -LoaderArchive $archive -TestFailAfterWrite 2 } 'rolled back.*injected'
foreach ($name in 'BepInEx','winhttp.dll','doorstop_config.ini','dotnet','.doorstop_version') { Check (-not (Test-Path -LiteralPath (Join-Path $fixture $name))) ('Fresh rollback left ' + $name) }
Install
$first = Get-Content -LiteralPath $receipt -Raw | ConvertFrom-Json
Check ($first.files.Count -eq 9 -and $first.mode -eq 'Install' -and -not $first.gameLaunched) 'Install receipt'
foreach ($entry in $first.files) { Check ((Hash (Join-Path $fixture $entry.path)) -eq $entry.sha256) ('Payload ' + $entry.path) }
Check ((Get-Content -LiteralPath (Join-Path $config 'BepInEx.cfg') -Raw) -match '(?m)^UnityBaseLibrariesSource\s*=\s*\r?$') 'Loader config'
$settings = Join-Path $config 'dbce.wodenrallyedgewheel.cfg'
$bindings = Join-Path $config 'wheel-bindings.json'
$otherConfig = Join-Path $config 'other-mod.cfg'
$unknown = Join-Path $plugin 'other-mod.dat'
'owner FFB/camera settings fixture' | Set-Content -LiteralPath $settings
'owner bindings fixture' | Set-Content -LiteralPath $bindings
'unrelated settings' | Set-Content -LiteralPath $otherConfig
'unrelated plugin file' | Set-Content -LiteralPath $unknown
$recordingDir = Join-Path $fixture 'BepInEx\WodenRecordings'
$null = New-Item -ItemType Directory -Path $recordingDir
$recording = Join-Path $recordingDir 'keep.jsonl'
'private capture fixture' | Set-Content -LiteralPath $recording
$retained = @{}
foreach ($file in @($settings,$bindings,$otherConfig,$unknown,$recording,(Join-Path $fixture 'winhttp.dll'))) { $retained[$file] = Hash $file }
Install
$updated = Get-Content -LiteralPath $receipt -Raw | ConvertFrom-Json
Check ($updated.backup -ne $first.backup -and $updated.configurationPreserved) 'Update backup receipt'
foreach ($file in $retained.Keys) { Check ((Hash $file) -eq $retained[$file]) ('Update changed retained file: ' + $file) }
Check ((Hash (Join-Path $updated.backup 'config\wheel-bindings.json')) -eq $retained[$bindings]) 'Binding backup'

# Failure midway through replacement must restore genuinely different old bytes.
$dll = Join-Path $plugin 'WodenRallyEdgeWheel.dll'
$savedDll = Join-Path $fixture 'before-fixture-dll.bin'
Copy-Item -LiteralPath $dll -Destination $savedDll
'old version fixture' | Set-Content -LiteralPath $dll
$oldHash = Hash $dll; $oldReceiptHash = Hash $receipt
Refuses { & $installer -GameDir $fixture -PackageRoot $PackageRoot -TestFailAfterWrite 2 } 'rolled back.*injected'
Check ((Hash $dll) -eq $oldHash) 'Update rollback lost original payload'
Check ((Hash $receipt) -eq $oldReceiptHash) 'Update rollback changed receipt'
foreach ($file in $retained.Keys) { Check ((Hash $file) -eq $retained[$file]) ('Rollback changed retained file: ' + $file) }
Refuses { & $installer -Mode Uninstall -GameDir $fixture } 'Owned file was modified'
Check ((Hash $dll) -eq $oldHash -and (Test-Path -LiteralPath (Join-Path $plugin 'WheelFfb.dll'))) 'Refused uninstall changed payload'
Copy-Item -LiteralPath $savedDll -Destination $dll -Force

# Receipt allowlist cannot be widened to a nested path or arbitrary sibling file.
$savedReceipt = Get-Content -LiteralPath $receipt -Raw
$badReceipt = $savedReceipt | ConvertFrom-Json
$badReceipt.files[0].path = 'BepInEx\plugins\WodenRallyEdgeWheel\nested\WodenRallyEdgeWheel.dll'
$badReceipt | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $receipt
Refuses { & $installer -Mode Uninstall -GameDir $fixture } 'Unrecognized uninstall'
$savedReceipt | Set-Content -LiteralPath $receipt

# Package corruption is rejected before any installed write.
$packageFile = Join-Path $PackageRoot 'README.md'
$packageBytes = [IO.File]::ReadAllBytes($packageFile)
try {
    'tampered package fixture' | Set-Content -LiteralPath $packageFile
    Refuses { Install } 'Package integrity check failed'
    Check ((Hash $dll) -eq (Hash $savedDll)) 'Bad package changed installed DLL'
} finally { [IO.File]::WriteAllBytes($packageFile,$packageBytes) }

& $installer -Mode Uninstall -GameDir $fixture
foreach ($entry in $first.files) { Check (-not (Test-Path -LiteralPath (Join-Path $fixture $entry.path))) ('Uninstall retained owned payload: ' + $entry.path) }
foreach ($file in $retained.Keys) { Check ((Hash $file) -eq $retained[$file]) ('Uninstall changed retained file: ' + $file) }
Install
& $installer -Mode Uninstall -GameDir $fixture -RemoveUserData
Check (-not (Test-Path -LiteralPath $settings) -and -not (Test-Path -LiteralPath $bindings)) 'Explicit remove settings'
foreach ($file in @($otherConfig,$unknown,$recording)) { Check ((Hash $file) -eq $retained[$file]) ('Remove settings changed unrelated file: ' + $file) }
Check (-not (Test-Path -LiteralPath (Join-Path $fixture 'Super Woden Rally Edge.exe'))) 'Game executable in fixture'
Write-Host "PASS: $script:checks installer checks (fresh install/rollback, update/rollback, integrity, receipt, ownership, uninstall, settings/recording/other-mod preservation). Fixture: $fixture"
