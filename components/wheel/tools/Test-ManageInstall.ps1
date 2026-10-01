[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$PackageRoot)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path $root ('artifacts\managed installer test-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $fixture
'Disposable installer regression fixture' | Set-Content -LiteralPath (Join-Path $fixture '.woden-installer-fixture')
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
# Exercise the shipped player entry point from a different working directory,
# with spaces in paths and no PackageRoot argument. NUL only skips batch pause.
$batch = Join-Path ([IO.Path]::GetFullPath($PackageRoot)) 'Install.bat'
Push-Location $fixture
try {
    # Set the raw Windows command line explicitly: PS 5.1 native argument
    # marshaling otherwise rewrites the nested cmd /s /c quotes.
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $env:ComSpec
    $start.Arguments = '/d /s /c ""{0}" -GameDir "{1}" -LoaderArchive "{2}" <NUL"' -f $batch,$fixture,$archive
    $start.WorkingDirectory = $fixture
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        Write-Host $stdout.GetAwaiter().GetResult()
        Write-Host $stderr.GetAwaiter().GetResult()
        Check ($process.ExitCode -eq 0) 'Default-root Install.bat failed'
    } finally { $process.Dispose() }
} finally { Pop-Location }
$first = Get-Content -LiteralPath $receipt -Raw | ConvertFrom-Json
Check ($first.files.Count -eq 9 -and $first.mode -eq 'Install' -and -not $first.gameLaunched) 'Install receipt'
Check ($first.installerRevision -eq 3) 'Default-root player installer revision'
Check ((Hash (Join-Path $plugin 'WodenRallyEdgeWheel.dll')) -eq (Hash (Join-Path $PackageRoot 'BepInEx\plugins\WodenRallyEdgeWheel\WodenRallyEdgeWheel.dll'))) 'Default-root selected wrong package'
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
$validReceiptBytes = [IO.File]::ReadAllBytes($receipt)
Check ($updated.backup -ne $first.backup -and $updated.configurationPreserved) 'Update backup receipt'
foreach ($file in $retained.Keys) { Check ((Hash $file) -eq $retained[$file]) ('Update changed retained file: ' + $file) }
Check ((Hash (Join-Path $updated.backup 'config\wheel-bindings.json')) -eq $retained[$bindings]) 'Binding backup'

# Failure midway through replacement must restore genuinely different old bytes.
$dll = Join-Path $plugin 'WodenRallyEdgeWheel.dll'
$savedDll = Join-Path $fixture 'before-fixture-dll.bin'
Copy-Item -LiteralPath $dll -Destination $savedDll
'old version fixture' | Set-Content -LiteralPath $dll
# Unknown bytes must be refused, not merely backed up.
$unknownHash = Hash $dll; $knownReceiptHash = Hash $receipt
Refuses { Install } 'Prior owned file missing or modified'
Check ((Hash $dll) -eq $unknownHash -and (Hash $receipt) -eq $knownReceiptHash) 'Unknown update mutated payload or receipt'
[IO.File]::WriteAllBytes($receipt,[byte[]]@())
Refuses { Install } 'Invalid prior install ownership receipt'
[IO.File]::WriteAllBytes($receipt,$validReceiptBytes)
Remove-Item -LiteralPath $receipt
Refuses { Install } 'No prior ownership receipt'
Check ((Hash $dll) -eq $unknownHash) 'Receipt-free update changed unknown bytes'
[IO.File]::WriteAllBytes($receipt,$validReceiptBytes)
# Explicit synthetic catalog of old bytes for rollback tests only.
function Catalog-FixturePayload {
    $catalog = [Text.Encoding]::UTF8.GetString($validReceiptBytes).TrimStart([char]0xFEFF) | ConvertFrom-Json
    foreach ($entry in $catalog.files) { $entry.sha256 = Hash (Join-Path $fixture $entry.path) }
    $catalog | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $receipt -Encoding utf8
}
Catalog-FixturePayload
$oldHash = Hash $dll; $oldReceiptHash = Hash $receipt
Refuses { & $installer -GameDir $fixture -PackageRoot $PackageRoot -TestFailAfterWrite 2 } 'rolled back.*injected'
Check ((Hash $dll) -eq $oldHash) 'Update rollback lost original payload'
Check ((Hash $receipt) -eq $oldReceiptHash) 'Update rollback changed receipt'
foreach ($file in $retained.Keys) { Check ((Hash $file) -eq $retained[$file]) ('Rollback changed retained file: ' + $file) }
[IO.File]::WriteAllBytes($receipt,$validReceiptBytes)
Refuses { & $installer -Mode Uninstall -GameDir $fixture } 'Owned file was modified'
Check ((Hash $dll) -eq $oldHash -and (Test-Path -LiteralPath (Join-Path $plugin 'WheelFfb.dll'))) 'Refused uninstall changed payload'
Copy-Item -LiteralPath $savedDll -Destination $dll -Force

# Never roll back while the game is open. The fail-closed simulation does not
# launch a process or weaken the real process check.
$coreDll = Join-Path $plugin 'WodenRallyEdge.Core.dll'
function Restore-FixturePayload {
    foreach ($entry in $first.files) { Copy-Item -LiteralPath (Join-Path $PackageRoot $entry.path) -Destination (Join-Path $fixture $entry.path) -Force }
    [IO.File]::WriteAllBytes($receipt,$validReceiptBytes)
}
function Latest-Recovery {
    $report = Get-ChildItem -LiteralPath (Join-Path $fixture 'WodenWheelBackups') -Filter recovery.json -File -Recurse | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    Get-Content -LiteralPath $report.FullName -Raw | ConvertFrom-Json
}
'old first file' | Set-Content -LiteralPath $dll
'old second file' | Set-Content -LiteralPath $coreDll
Catalog-FixturePayload
$beforeReceipt = Hash $receipt
Refuses { & $installer -GameDir $fixture -PackageRoot $PackageRoot -TestScenario GameStarts } 'Recovery required.*Close'
Check ((Hash $dll) -eq $first.files[0].sha256) 'Running-game recovery changed first written payload'
Check ((Hash $coreDll) -eq $first.files[1].sha256) 'Running-game recovery changed second written payload'
Check ((Hash $receipt) -eq $beforeReceipt) 'Running-game recovery changed receipt'
$recovery = Latest-Recovery
Check ($recovery.recoveryRequired -and $recovery.mutations.Count -eq 2) 'Running-game recovery report'
foreach ($entry in @($first.files | Select-Object -Skip 2)) { Check ((Hash (Join-Path $fixture $entry.path)) -eq $entry.sha256) 'Running-game recovery changed unwritten payload' }
Restore-FixturePayload

# Unknown current bytes survive rollback; other files still recover from their
# verified backups. Receipt never claims the interrupted update completed.
'old first file' | Set-Content -LiteralPath $dll
'old second file' | Set-Content -LiteralPath $coreDll
Catalog-FixturePayload
$beforeReceipt = Hash $receipt
$oldCore = Hash $coreDll
Refuses { & $installer -GameDir $fixture -PackageRoot $PackageRoot -TestScenario ExternalReplacement -TestFailAfterWrite 2 } 'Recovery required.*Unknown current bytes'
Check ((Get-Content -LiteralPath $dll -Raw).Trim() -eq 'external replacement after write') 'Rollback overwrote external replacement'
Check ((Hash $coreDll) -eq $oldCore) 'Rollback failed to restore unchanged second payload'
Check ((Hash $receipt) -eq $beforeReceipt) 'External replacement changed receipt'
Check ((Latest-Recovery).recoveryRequired) 'External replacement recovery report'
Restore-FixturePayload

# Recheck the snapshot immediately before each forward mutation.
'old first file' | Set-Content -LiteralPath $dll
Catalog-FixturePayload
$beforeReceipt = Hash $receipt
$oldFirst = Hash $dll
Refuses { & $installer -GameDir $fixture -PackageRoot $PackageRoot -TestScenario ExternalBeforeWrite } 'Recovery required.*File changed during operation'
Check ((Hash $dll) -eq $oldFirst) 'Forward conflict did not roll back first payload'
Check ((Get-Content -LiteralPath $coreDll -Raw).Trim() -eq 'external change before write') 'Forward write overwrote external bytes'
Check ((Hash $receipt) -eq $beforeReceipt) 'Forward conflict changed receipt'
Check ((Latest-Recovery).recoveryRequired) 'Forward conflict recovery report'
Restore-FixturePayload

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
Write-Host "PASS: $script:checks installer checks (fresh install/rollback, update/rollback, running-game recovery, external changes, integrity, receipt, ownership, uninstall, settings/recording/other-mod preservation). Fixture: $fixture"
