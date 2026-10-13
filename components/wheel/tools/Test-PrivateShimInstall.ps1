[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$NativePackageRoot,
    [Parameter(Mandatory=$true)][string]$PrivatePackageRoot
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path $root ('artifacts\private-shim-installer-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $fixture
'Disposable private installer regression fixture' | Set-Content -LiteralPath (Join-Path $fixture '.woden-installer-fixture')
Copy-Item -LiteralPath 'D:\Program Files (x86)\Steam\steamapps\common\Super Woden Rally Edge\GameAssembly.dll' -Destination $fixture
$archive = Join-Path $root 'lib\loader\BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788+5b766a3.zip'
$nativeInstaller = Join-Path $NativePackageRoot 'Manage-Install.ps1'
$privateInstaller = Join-Path $PrivatePackageRoot 'Manage-Install.ps1'
$receiptPath = Join-Path $fixture 'BepInEx\WodenWheel-install.json'
$shimPath = Join-Path $fixture 'BepInEx\plugins\WodenRallyEdgeWheel\dbce_shim.dll'
$config = Join-Path $fixture 'BepInEx\config'
$script:checks = 0
function Check([bool]$condition,[string]$message) { if (-not $condition) { throw $message }; $script:checks++ }
function Hash([string]$path) { return (Get-FileHash -LiteralPath $path).Hash }
function Receipt { return (Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json) }
function Native { & $nativeInstaller -GameDir $fixture -PackageRoot $NativePackageRoot -LoaderArchive $archive }
function Private { & $privateInstaller -GameDir $fixture -PackageRoot $PrivatePackageRoot -LoaderArchive $archive }
function Refuses([scriptblock]$action,[string]$pattern) {
    $failed = $false
    try { & $action } catch { if ($_.Exception.Message -notmatch $pattern) { throw }; $failed = $true }
    Check $failed ('Expected refusal: ' + $pattern)
}

Native
$native = Receipt
Check ($native.files.Count -eq 16 -and -not $native.PSObject.Properties['sharedInputCandidate']) 'Ordinary Native receipt owns installed tools'
Check (-not (Test-Path -LiteralPath $shimPath)) 'Ordinary Native package staged a private DLL'
$settings = Join-Path $config 'dbce.wodenrallyedgewheel.cfg'
$controls = Join-Path $config 'dbce-controls.ini'
"[Input]`r`nBackend = Native" | Set-Content -LiteralPath $settings
"[Input]`r`nBackend = Native" | Set-Content -LiteralPath $controls
$settingsHash = Hash $settings; $controlsHash = Hash $controls
$tampered = Join-Path $root ('artifacts\private-shim-bad-package-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $tampered
Copy-Item -Path (Join-Path $PrivatePackageRoot '*') -Destination $tampered -Recurse
$badCapabilityPath = Join-Path $tampered 'private-shim-capability.json'
$badCapability = Get-Content -LiteralPath $badCapabilityPath -Raw | ConvertFrom-Json
$badCapability.controls.shimAbi = 3
$badCapability | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $badCapabilityPath -Encoding utf8
$badManifestPath = Join-Path $tampered 'manifest.json'
$badManifest = Get-Content -LiteralPath $badManifestPath -Raw | ConvertFrom-Json
$capabilityEntry = @($badManifest | Where-Object { $_.path -eq 'private-shim-capability.json' })
Check ($capabilityEntry.Count -eq 1) 'Private manifest capability entry is not unique'
$capabilityEntry[0].sha256 = Hash $badCapabilityPath
$badManifest | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $badManifestPath -Encoding utf8
$beforeBadPackage = Hash $receiptPath
Refuses { & $privateInstaller -GameDir $fixture -PackageRoot $tampered -LoaderArchive $archive } 'Private shim capability does not match'
Check ((Hash $receiptPath) -eq $beforeBadPackage -and -not (Test-Path -LiteralPath $shimPath)) 'Self-consistent forged package changed Native install'

Refuses { & $privateInstaller -GameDir $fixture -PackageRoot $PrivatePackageRoot -TestFailAfterWrite 14 } 'rolled back.*injected'
Check (-not (Test-Path -LiteralPath $shimPath)) 'Native-to-private rollback orphaned the DLL'
Check ((Receipt).files.Count -eq 16) 'Native-to-private rollback changed ownership'
Private
$private = Receipt
Check ($private.files.Count -eq 17 -and $private.mode -eq 'Install') 'Private receipt must own 17 payloads'
Check (($private.ordinarySession.inputOrigin -join ',') -ceq 'human,automated') 'Private companion declares exact input-origin capability'
# A legitimate revision-3 private receipt carries 14 historical files and its
# private Shim metadata. Upgrade must preserve the Shim pin while adding tools.
$legacyPrivate = Receipt
$legacyPrivate.installerRevision = 3
$legacyPrivate.PSObject.Properties.Remove('ordinarySession')
$legacyPrivate.files = @($legacyPrivate.files | Where-Object { $_.path -notmatch '(Start-RecordedGame\.ps1|RecordedStopControl\.ps1|WodenCaseFinalizer\.exe)$' })
foreach ($name in 'Start-RecordedGame.ps1','RecordedStopControl.ps1','WodenCaseFinalizer.exe') { Remove-Item -LiteralPath (Join-Path (Split-Path $shimPath -Parent) $name) }
$legacyPrivate | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $receiptPath -Encoding utf8
Check ($legacyPrivate.files.Count -eq 14) 'Legacy private fixture owns exactly 14 files'
Private
$private = Receipt
Check ($private.installerRevision -eq 4 -and $private.files.Count -eq 17 -and $private.ordinarySession.schema -eq 'woden.signal-session@1') 'Legacy private receipt upgrades to revision-4 ordinary ownership'
Check (($private.ordinarySession.inputOrigin -join ',') -ceq 'human,automated') 'Legacy private upgrade gains explicit-origin capability'
Copy-Item -LiteralPath $receiptPath -Destination (Join-Path $fixture 'private-install-receipt.json')
Check ((Hash $shimPath) -eq '9B8A036F664CF8F45CCCF82B98E5E4376F93215B60EAC50B6DB965BF498015D8') 'Installed shim pin'
Check (@($private.files | Where-Object { $_.path -like '*dbce_shim.dll' }).Count -eq 1) 'Private shim ownership entry'
Check ($private.controlsProfileSchema -eq 1 -and $private.adapter -eq 'woden-bindings-1') 'Native capability changed'
Check ($private.sharedInputCandidate.controls.adapter -eq 'woden-shim-ini-controls-1' -and $private.sharedInputCandidate.controls.delivered.Count -eq 27) 'Shared candidate omitted'
Check ($private.sharedInputCandidate.controls.optIn.default -eq 'Native') 'Private capability changed default backend'
Private
Check ((Receipt).files.Count -eq 17 -and (Hash $shimPath) -eq $private.sharedInputCandidate.controls.shimSha256) 'Private update lost ownership'
$nativeSettingsBytes = [IO.File]::ReadAllBytes($settings)
"[Input]`r`nBackend = Shim" | Set-Content -LiteralPath $settings
$activeShimConfigHash = Hash $settings; $activeShimReceiptHash = Hash $receiptPath; $activeShimDllHash = Hash $shimPath
Refuses { Native } 'Native package refused while.*Backend=Shim'
Check ((Hash $settings) -eq $activeShimConfigHash -and (Hash $receiptPath) -eq $activeShimReceiptHash -and (Hash $shimPath) -eq $activeShimDllHash) 'Active-Shim downgrade refusal changed owner state'
[IO.File]::WriteAllBytes($settings,$nativeSettingsBytes)

Refuses { & $nativeInstaller -GameDir $fixture -PackageRoot $NativePackageRoot -TestFailAfterWrite 2 } 'rolled back.*injected'
Check ((Receipt).files.Count -eq 17 -and (Hash $shimPath) -eq $private.sharedInputCandidate.controls.shimSha256) 'Private-to-Native rollback lost shim ownership'
'externally modified DLL' | Set-Content -LiteralPath $shimPath
$changed = Hash $shimPath; $receiptHash = Hash $receiptPath
Refuses { Native } 'Prior owned file missing or modified'
Check ((Hash $shimPath) -eq $changed -and (Hash $receiptPath) -eq $receiptHash) 'Modified shim refusal changed install'
Copy-Item -LiteralPath (Join-Path $PrivatePackageRoot 'BepInEx\plugins\WodenRallyEdgeWheel\dbce_shim.dll') -Destination $shimPath -Force
Native
Check ((Receipt).files.Count -eq 16 -and -not (Receipt).PSObject.Properties['sharedInputCandidate']) 'Private-to-Native receipt retained candidate'
Check (-not (Test-Path -LiteralPath $shimPath)) 'Private-to-Native update orphaned DLL'
Check ((Hash $settings) -eq $settingsHash -and (Hash $controls) -eq $controlsHash) 'Transition changed saved Native backend'

Private
& $privateInstaller -Mode Uninstall -GameDir $fixture
Check (-not (Test-Path -LiteralPath $shimPath)) 'Private uninstall left DLL'
Check (-not (Receipt).PSObject.Properties['sharedInputCandidate']) 'Uninstall retained candidate'
Check ((Hash $settings) -eq $settingsHash -and (Hash $controls) -eq $controlsHash) 'Uninstall changed owner config'
Native
Check ((Receipt).files.Count -eq 16 -and -not (Test-Path -LiteralPath $shimPath)) 'Native reinstall after private uninstall orphaned DLL'
Check (-not (Test-Path -LiteralPath (Join-Path $fixture 'Super Woden Rally Edge.exe'))) 'Fixture launched or staged game executable'
Write-Host "PASS: $script:checks private installer checks (Native/private update, rollback, ownership refusal, uninstall, config preservation). Fixture: $fixture; retained private receipt: $(Join-Path $fixture 'private-install-receipt.json')"
