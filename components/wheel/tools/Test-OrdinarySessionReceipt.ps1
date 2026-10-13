[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$installer = Join-Path $PSScriptRoot 'Manage-Install.ps1'
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($installer,[ref]$tokens,[ref]$errors)
if (@($errors).Count -ne 0) { throw 'Installer did not parse.' }
foreach ($name in 'Expected-OrdinarySession','Assert-OrdinarySession') {
    $definition = @($ast.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name},$false))
    if ($definition.Count -ne 1) { throw "Expected one installer function: $name" }
    Invoke-Expression $definition[0].Extent.Text
}
$gameHash = 'F422894D8D2B0DF4EDB7E5259E5E60CB8C4F8DEA2E85EBDFC09DD6766349250C'
$names = @('WodenRallyEdgeWheel.dll','WodenRallyEdge.Core.dll','Dbce.Wheel.Telemetry.dll','Dbce.Wheel.Recording.dll','Dbce.Wheel.Ffb.dll','WheelFfb.dll','recording-provenance.json','toolkit.version','telemetry-schema.json','Dbce.Wheel.Playback.dll','playback-provenance.json','playback-LICENSE.txt','Stage-Session.ps1','Start-RecordedGame.ps1','RecordedStopControl.ps1','WodenCaseFinalizer.exe')
$fixture = Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts\ordinary-receipt-test-' + [guid]::NewGuid().ToString('N'))
$plugin = Join-Path $fixture 'BepInEx\plugins\WodenRallyEdgeWheel'
$null = New-Item -ItemType Directory -Path $plugin
'identity-only dummy; never launched' | Set-Content -LiteralPath (Join-Path $fixture 'GameAssembly.dll')
$entries = foreach ($name in $names) {
    $path = Join-Path $plugin $name
    "dummy $name A" | Set-Content -LiteralPath $path
    [ordered]@{path=('BepInEx\plugins\WodenRallyEdgeWheel\' + $name);sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
}
$a = Expected-OrdinarySession $entries $true | ConvertTo-Json -Depth 8 | ConvertFrom-Json
Assert-OrdinarySession $a $entries
$old = Expected-OrdinarySession $entries | ConvertTo-Json -Depth 8 | ConvertFrom-Json
Assert-OrdinarySession $old $entries
$malformed = $a | ConvertTo-Json -Depth 8 | ConvertFrom-Json
$malformed.inputOrigin = @('automated','human')
try { Assert-OrdinarySession $malformed $entries; throw 'Malformed origin declaration was accepted.' }
catch { if ($_.Exception.Message -eq 'Malformed origin declaration was accepted.') { throw } }
$tool = Join-Path $plugin 'Start-RecordedGame.ps1'
'dummy changed companion B' | Set-Content -LiteralPath $tool
$bEntries = foreach ($entry in $entries) {
    if ($entry.path -match 'Start-RecordedGame\.ps1$') { [ordered]@{path=$entry.path;sha256=(Get-FileHash -LiteralPath $tool -Algorithm SHA256).Hash} }
    else { $entry }
}
try { Assert-OrdinarySession $a $bEntries; throw 'A receipt matched B payload hashes.' }
catch { if ($_.Exception.Message -eq 'A receipt matched B payload hashes.') { throw } }
$b = Expected-OrdinarySession $bEntries $true | ConvertTo-Json -Depth 8 | ConvertFrom-Json
Assert-OrdinarySession $b $bEntries
$receiptPath = Join-Path $fixture 'BepInEx\WodenWheel-install.json'
[ordered]@{version='dummy';installerRevision=4;mode='Install';gameDirectory=$fixture;files=@($bEntries);ordinarySession=$b} |
    ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $receiptPath
$detached = Join-Path $fixture 'detached'
$null = New-Item -ItemType Directory -Path $detached
$detachedInstaller = Join-Path $detached 'Manage-Install.ps1'
Copy-Item -LiteralPath $installer -Destination $detachedInstaller
if (Test-Path -LiteralPath (Join-Path $detached 'Start-RecordedGame.ps1')) { throw 'Detached installer unexpectedly has a local companion.' }
& $detachedInstaller -Mode Uninstall -GameDir $fixture
$uninstalled = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
if ($uninstalled.mode -ne 'Uninstall' -or (Test-Path -LiteralPath $tool)) { throw 'B dummy uninstall failed.' }
Write-Host "PASS: legacy receipt, origin-capable A, changed B hash, forged declaration refusal, detached B uninstall. Fixture: $fixture"
