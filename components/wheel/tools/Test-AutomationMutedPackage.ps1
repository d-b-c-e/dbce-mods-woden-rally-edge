[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$PackageRoot)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$pin = '24A9B624E3F3C474383C64A0177019BFB0837DE70B311854C59EE692D7DB03F5'
$basePin = 'F8361A08BBE08EAAB936B37800D308CF1FC7EDAB34E2839DB16A447FE34178C5'
$package = [IO.Path]::GetFullPath($PackageRoot)
$pluginRelative = 'BepInEx\plugins\WodenRallyEdgeWheel\WheelFfb.dll'
$metadataPath = Join-Path $package 'automation-native-provenance.json'
$noticePath = Join-Path $package 'AUTOMATION-ONLY.txt'
$nativePath = Join-Path $package $pluginRelative
$manifest = Get-Content -LiteralPath (Join-Path $package 'manifest.json') -Raw | ConvertFrom-Json
$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
$script:checks = 0
function Check([bool]$Condition,[string]$Message) { if (-not $Condition) { throw $Message }; $script:checks++ }
function Hash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
foreach ($path in @($pluginRelative,'automation-native-provenance.json','AUTOMATION-ONLY.txt','private-shim-capability.json')) {
    $entries = @($manifest | Where-Object { $_.path.Replace('/','\') -eq $path })
    Check ($entries.Count -eq 1 -and (Hash (Join-Path $package $path)) -eq $entries[0].sha256) ('Manifest pin: ' + $path)
}
Check ((Hash $nativePath) -eq $pin -and $metadata.nativeSha256 -eq $pin.ToLowerInvariant()) 'Automation native pin'
Check ($metadata.schema -eq 'woden.automation-muted-native@1' -and $metadata.baseNativeSha256 -eq $basePin.ToLowerInvariant() -and
    $metadata.sourceCommit -match '^[a-f0-9]{40}$') 'Source and base toolkit provenance'
Check ((Get-Content -LiteralPath $noticePath -Raw) -match 'INTERNAL AUTOMATION ONLY') 'Test-only notice'
$fixture = Join-Path $root ('artifacts\automation-muted-installer-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
'Disposable automation installer fixture' | Set-Content -LiteralPath (Join-Path $fixture '.woden-installer-fixture')
Copy-Item -LiteralPath 'D:\Program Files (x86)\Steam\steamapps\common\Super Woden Rally Edge\GameAssembly.dll' -Destination $fixture
$loader = Join-Path $root 'lib\loader\BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788+5b766a3.zip'
$installer = Join-Path $package 'Manage-Install.ps1'
& $installer -GameDir $fixture -PackageRoot $package -LoaderArchive $loader
$receiptPath = Join-Path $fixture 'BepInEx\WodenWheel-install.json'
$receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
$installedNative = Join-Path $fixture $pluginRelative
$ownedNative = @($receipt.files | Where-Object { $_.path.Replace('/','\') -eq $pluginRelative })
Check ($receipt.installerRevision -eq 4 -and $receipt.files.Count -eq 17) 'Revision-4 private ownership'
Check ($ownedNative.Count -eq 1 -and $ownedNative[0].sha256 -eq $pin -and (Hash $installedNative) -eq $pin) 'Installed native owned by receipt'
Check ($receipt.sharedInputCandidate.controls.nativeSha256 -eq $pin.ToLowerInvariant()) 'Installed private capability binds muted native'
Check ($receipt.ordinarySession.schema -eq 'woden.signal-session@1' -and $receipt.ordinarySession.record -and -not $receipt.ordinarySession.playback) 'Installed ordinary record capability'
$originalReceiptHash = Hash $receiptPath
'fixture tamper' | Set-Content -LiteralPath $installedNative
$refused = $false
try { & $installer -GameDir $fixture -PackageRoot $package -LoaderArchive $loader } catch {
    if ($_.Exception.Message -notmatch 'Prior owned file missing or modified') { throw }
    $refused = $true
}
Check ($refused -and (Hash $receiptPath) -eq $originalReceiptHash) 'Tampered installed native refused without receipt change'
Write-Host "PASS: $script:checks automation package/installer capability checks. Disposable fixture: $fixture"
