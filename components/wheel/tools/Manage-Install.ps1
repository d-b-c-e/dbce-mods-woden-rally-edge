[CmdletBinding()]
param(
    [ValidateSet('Install','Uninstall')][string]$Mode = 'Install',
    [string]$GameDir,
    [string]$PackageRoot,
    [string]$LoaderArchive,
    [switch]$RemoveUserData,
    [Parameter(DontShow=$true)][int]$TestFailAfterWrite = 0,
    [Parameter(DontShow=$true)][ValidateSet('None','GameStarts','ExternalReplacement','ExternalBeforeWrite')][string]$TestScenario = 'None'
)
$ErrorActionPreference = 'Stop'
# Windows PowerShell 5.1 advanced-script parameter binding can evaluate
# $PSScriptRoot before it is populated. Resolve this default in the body.
if (-not $PackageRoot) { $PackageRoot = $PSScriptRoot }
$gameName = 'Super Woden Rally Edge'
$gameHash = 'F422894D8D2B0DF4EDB7E5259E5E60CB8C4F8DEA2E85EBDFC09DD6766349250C'
$loaderHash = 'F4CC496BD098A0DF4164B81E3737297707F13A47C2478DBA2F60EEFAB784817A'
$loaderName = 'BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788+5b766a3.zip'
$payloadNames = @('WodenRallyEdgeWheel.dll','WodenRallyEdge.Core.dll','Dbce.Wheel.Telemetry.dll','Dbce.Wheel.Recording.dll','Dbce.Wheel.Ffb.dll','WheelFfb.dll','recording-provenance.json','toolkit.version','telemetry-schema.json','Dbce.Wheel.Playback.dll','playback-provenance.json','playback-LICENSE.txt','Stage-Session.ps1')
$shimPath = 'BepInEx\plugins\WodenRallyEdgeWheel\dbce_shim.dll'
$capabilityPath = 'private-shim-capability.json'
$privateShimHash = '9B8A036F664CF8F45CCCF82B98E5E4376F93215B60EAC50B6DB965BF498015D8'
$basePaths = @($payloadNames | ForEach-Object { 'BepInEx\plugins\WodenRallyEdgeWheel\' + $_ })
function Expected-Candidate($Entries) {
    $hashes = @{}
    foreach ($entry in @($Entries)) { $hashes[$entry.path.Replace('/','\').ToLowerInvariant()] = $entry.sha256.ToLowerInvariant() }
    foreach ($name in 'WodenRallyEdgeWheel.dll','WodenRallyEdge.Core.dll','WheelFfb.dll','dbce_shim.dll') {
        if (-not $hashes.ContainsKey(('BepInEx\plugins\WodenRallyEdgeWheel\' + $name).ToLowerInvariant())) { throw "Private capability lacks owned payload: $name" }
    }
    if ($hashes[$shimPath.ToLowerInvariant()] -ne $privateShimHash.ToLowerInvariant()) { throw 'Private shim payload does not match the reviewed ABI 2 pin.' }
    $unsupported = [ordered]@{}
    foreach ($action in 'clutch','select','gear7','gear8') { $unsupported[$action] = 'not delivered by Woden wheel-only shim pilot' }
    $conditions = [ordered]@{ shiftUp='Transmission=Sequential;NativeGearbox=Manual'; shiftDown='Transmission=Sequential;NativeGearbox=Manual' }
    foreach ($action in @('neutral','reverse') + @(1..6 | ForEach-Object { 'gear' + $_ })) { $conditions[$action] = 'Transmission=H-pattern;NativeGearbox=Manual' }
    return [ordered]@{
        schema=1; adapter='woden-shim-ini-controls-1'; profile='BepInEx/config/dbce-controls.ini'; settings='BepInEx/config/dbce.wodenrallyedgewheel.cfg'; shimAbi=2
        optIn=[ordered]@{ section='Input'; key='Backend'; value='Shim'; default='Native'; appliesAfterRestart=$true }
        gameAssemblySha256=$gameHash
        pluginSha256=$hashes['bepinex\plugins\wodenrallyedgewheel\wodenrallyedgewheel.dll']
        coreSha256=$hashes['bepinex\plugins\wodenrallyedgewheel\wodenrallyedge.core.dll']
        nativeSha256=$hashes['bepinex\plugins\wodenrallyedgewheel\wheelffb.dll']
        shimSha256=$privateShimHash.ToLowerInvariant()
        delivered=@('steer','throttle','brake','handbrake','shiftUp','shiftDown','neutral','reverse','confirm','back','start','navUp','navDown','navLeft','navRight','camera','lookBack','reset','horn','modMenu','muteFfb','gear1','gear2','gear3','gear4','gear5','gear6')
        unsupported=$unsupported; conditions=$conditions
    }
}
function Assert-Candidate($Actual,$Entries) {
    if ($null -eq $Actual -or @($Actual.PSObject.Properties).Count -ne 1 -or $null -eq $Actual.controls) { throw 'Invalid private shim capability metadata.' }
    $expected = Expected-Candidate $Entries
    if (($Actual.controls | ConvertTo-Json -Depth 8 -Compress) -cne ($expected | ConvertTo-Json -Depth 8 -Compress)) { throw 'Private shim capability does not match owned payloads and reviewed contract.' }
}
function Assert-ReceiptFiles($Receipt) {
    $paths = @($Receipt.files | ForEach-Object { $_.path.Replace('/','\').ToLowerInvariant() })
    $private = $shimPath.ToLowerInvariant() -in $paths
    $expected = if ($private) { @($basePaths + $shimPath) } else { $basePaths }
    $expected = @($expected | ForEach-Object { $_.ToLowerInvariant() })
    if (@($Receipt.files).Count -ne $expected.Count -or @($paths | Select-Object -Unique).Count -ne $expected.Count) { throw 'Invalid install ownership entries or payload count.' }
    foreach ($entry in @($Receipt.files)) {
        if ($entry.path.Replace('/','\').ToLowerInvariant() -notin $expected -or $entry.sha256 -notmatch '^[a-fA-F0-9]{64}$') { throw 'Unrecognized install ownership entry.' }
    }
    if ($private -and $Receipt.mode -eq 'Install') { Assert-Candidate $Receipt.sharedInputCandidate $Receipt.files }
    elseif ($null -ne $Receipt.PSObject.Properties['sharedInputCandidate']) { throw 'Native or uninstalled receipt must not advertise private Shim capability.' }
    return $private
}
function Assert-NativeBackend([string]$ConfigPath) {
    if (-not (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) { return }
    $section = ''; $values = @()
    foreach ($line in @(Get-Content -LiteralPath $ConfigPath)) {
        if ($line -match '^\s*\[([^\]]+)\]\s*(?:[;#].*)?$') { $section = $Matches[1].Trim(); continue }
        if ($section -eq 'Input' -and $line -match '^\s*Backend\s*=\s*([^;#\r\n]*)') { $values += $Matches[1].Trim() }
    }
    if ($values.Count -gt 1 -or ($values.Count -eq 1 -and $values[0] -notin @('Native','Shim'))) { throw 'Native package refused: ambiguous saved [Input] Backend. Review the Woden configuration first.' }
    if ($values.Count -eq 1 -and $values[0] -eq 'Shim') { throw 'Native package refused while [Input] Backend=Shim. Select Native and restart Woden before removing the private shim DLL.' }
}
function Assert-Closed {
    if ((Get-Process -Name $gameName -ErrorAction SilentlyContinue) -or $script:fixtureGameRunning) { throw "Close $gameName normally, then retry. No process will be stopped." }
}
function File-Hash([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Expected a file: $Path" }
    return (Get-FileHash -LiteralPath $Path).Hash
}
function Snapshot-File([string]$Path,[string]$Saved) {
    $target = Assert-Path $Path $game
    $originals[$target] = [pscustomobject]@{ hash=(File-Hash $target); saved=$Saved }
}
function Assert-Unchanged([string]$Path,$Expected) {
    $null = Assert-Path $Path $game
    if ((File-Hash $Path) -ne $Expected) { throw "File changed during operation; retained for review: $Path" }
}
function Write-Owned([string]$Source,[string]$Target,[string]$Hash) {
    Ensure-Directory (Split-Path $Target -Parent)
    Assert-Closed
    Assert-Unchanged $Target $originals[$Target].hash
    $mutations.Add([pscustomobject]@{ path=$Target; originalHash=$originals[$Target].hash; writtenHash=$Hash; saved=$originals[$Target].saved })
    Copy-Item -LiteralPath $Source -Destination $Target -Force
    if ((File-Hash $Target) -ne $Hash) { throw "Copy verification failed: $Target" }
}
function Remove-Owned([string]$Target) {
    Assert-Closed
    Assert-Unchanged $Target $originals[$Target].hash
    if ($null -ne $originals[$Target].hash) {
        $mutations.Add([pscustomobject]@{ path=$Target; originalHash=$originals[$Target].hash; writtenHash=$null; saved=$originals[$Target].saved })
        Remove-Item -LiteralPath $Target
        if ($null -ne (File-Hash $Target)) { throw "Removal verification failed: $Target" }
    }
}
function Assert-Path([string]$Path,[string]$Root) {
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($Root.TrimEnd('\') + '\',[StringComparison]::OrdinalIgnoreCase)) { throw "Path escapes intended directory: $full" }
    $cursor = $full
    while ($cursor) {
        if ((Test-Path -LiteralPath $cursor) -and ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Linked path refused: $cursor" }
        $cursor = Split-Path $cursor -Parent
    }
    return $full
}
function Assert-Tree([string]$Path) {
    if (Test-Path -LiteralPath $Path) {
        foreach ($item in Get-ChildItem -LiteralPath $Path -Force -Recurse) { $null = Assert-Path $item.FullName $game }
    }
}
function Ensure-Directory([string]$Path) {
    if ([IO.Path]::GetFullPath($Path).TrimEnd('\') -eq $game) { return }
    $target = Assert-Path $Path $game
    $missing = [Collections.Generic.List[string]]::new()
    while (-not (Test-Path -LiteralPath $target)) { $missing.Add($target); $target = Split-Path $target -Parent }
    for ($i = $missing.Count - 1; $i -ge 0; $i--) { Assert-Closed; $null = Assert-Path $missing[$i] $game; $createdDirectories.Add($missing[$i]); New-Item -ItemType Directory -Path $missing[$i] | Out-Null }
}
function Find-Game {
    $libraries = [Collections.Generic.List[string]]::new()
    foreach ($key in 'HKCU:\Software\Valve\Steam','HKLM:\SOFTWARE\WOW6432Node\Valve\Steam') {
        $entry = Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue
        if ($entry.SteamPath) { $libraries.Add($entry.SteamPath) }
        if ($entry.InstallPath) { $libraries.Add($entry.InstallPath) }
    }
    foreach ($steam in @($libraries.ToArray())) {
        $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf) {
            foreach ($match in [regex]::Matches((Get-Content -LiteralPath $vdf -Raw),'"path"\s+"([^"]+)"')) { $libraries.Add($match.Groups[1].Value.Replace('\\','\')) }
        }
    }
    $found = @($libraries | Select-Object -Unique | ForEach-Object { Join-Path $_ ('steamapps\common\' + $gameName) } | Where-Object { Test-Path -LiteralPath (Join-Path $_ 'GameAssembly.dll') })
    if ($found.Count -eq 1) { return $found[0] }
    Add-Type -AssemblyName System.Windows.Forms
    $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $dialog.Description = 'Choose the Super Woden Rally Edge game folder'; $dialog.ShowNewFolderButton = $false
    if ($dialog.ShowDialog() -ne [Windows.Forms.DialogResult]::OK) { throw 'Folder selection cancelled. No installation change.' }
    return $dialog.SelectedPath
}
Assert-Closed
if (-not $GameDir) { $GameDir = Find-Game }
$game = [IO.Path]::GetFullPath($GameDir).TrimEnd('\')
$script:fixtureGameRunning = $false
if ($TestScenario -ne 'None' -and ((Test-Path -LiteralPath (Join-Path $game ($gameName + '.exe'))) -or -not (Test-Path -LiteralPath (Join-Path $game '.woden-installer-fixture') -PathType Leaf))) { throw 'Test scenarios require a marked fixture without a game executable.' }
if (-not (Test-Path -LiteralPath (Join-Path $game 'GameAssembly.dll') -PathType Leaf)) { throw 'Choose the game folder containing GameAssembly.dll.' }
$destination = Assert-Path (Join-Path $game 'BepInEx\plugins\WodenRallyEdgeWheel') $game
$config = Assert-Path (Join-Path $game 'BepInEx\config') $game
$receiptPath = Assert-Path (Join-Path $game 'BepInEx\WodenWheel-install.json') $game
Assert-Tree $config
Assert-Tree $destination
Write-Host "$Mode $gameName wheel mod: $game"
$beforeConfig = @{}
if (Test-Path -LiteralPath $config) { Get-ChildItem -LiteralPath $config -File -Recurse | ForEach-Object { $beforeConfig[$_.FullName] = (Get-FileHash -LiteralPath $_.FullName).Hash } }
$owned = @(); $retired = @(); $loaderStage = $null
if ($Mode -eq 'Install') {
    if ((Get-FileHash -LiteralPath (Join-Path $game 'GameAssembly.dll')).Hash -ne $gameHash) { throw 'Unsupported game build. Existing files were left in place.' }
    $package = [IO.Path]::GetFullPath($PackageRoot).TrimEnd('\')
    $manifest = Get-Content -LiteralPath (Join-Path $package 'manifest.json') -Raw | ConvertFrom-Json
    if (@($manifest | ForEach-Object { $_.path.Replace('/','\').ToLowerInvariant() } | Select-Object -Unique).Count -ne @($manifest).Count) { throw 'Package manifest has duplicate paths.' }
    foreach ($entry in $manifest) {
        $file = Assert-Path (Join-Path $package $entry.path) $package
        if ($entry.sha256 -notmatch '^[a-fA-F0-9]{64}$' -or (Get-FileHash -LiteralPath $file).Hash -ne $entry.sha256) { throw "Package integrity check failed: $($entry.path)" }
    }
    $shimEntries = @($manifest | Where-Object { $_.path.Replace('/','\') -eq $shimPath })
    $capabilityEntries = @($manifest | Where-Object { $_.path.Replace('/','\') -eq $capabilityPath })
    $private = $shimEntries.Count -eq 1 -and $capabilityEntries.Count -eq 1
    if (($shimEntries.Count -ne $capabilityEntries.Count) -or $shimEntries.Count -gt 1 -or
        ((Test-Path -LiteralPath (Join-Path $package $shimPath)) -ne $private) -or
        ((Test-Path -LiteralPath (Join-Path $package $capabilityPath)) -ne $private)) { throw 'Private shim package requires one manifest-owned DLL and capability file together.' }
    if ($private) {
        $privateMetadata = Get-Content -LiteralPath (Join-Path $package $capabilityPath) -Raw | ConvertFrom-Json
        Assert-Candidate $privateMetadata $manifest
    }
    foreach ($name in $payloadNames) {
        $relative = 'BepInEx\plugins\WodenRallyEdgeWheel\' + $name
        $entry = @($manifest | Where-Object { $_.path.Replace('/','\') -eq $relative })
        if ($entry.Count -ne 1) { throw "Package is missing a unique owned payload: $name" }
        $owned += [ordered]@{ path=$relative; sha256=$entry[0].sha256 }
    }
    if ($private) { $owned += [ordered]@{ path=$shimPath; sha256=$shimEntries[0].sha256 } }
    # A filename and a backup do not establish ownership. Refuse unknown or
    # modified payloads before any loader preparation, backup or replacement.
    $priorPayloadHashes = @{}
    $priorReceiptHash = File-Hash $receiptPath
    if ($null -ne $priorReceiptHash) {
        $prior = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
        if ($prior.mode -notin @('Install','Uninstall') -or $prior.installerRevision -notin @(1,2,3) -or
            [string]::IsNullOrWhiteSpace($prior.version) -or $prior.gameDirectory -ne $game) { throw 'Invalid prior install ownership receipt; separate adoption review required.' }
        try { $priorPrivate = Assert-ReceiptFiles $prior } catch { throw 'Invalid prior install ownership receipt; separate adoption review required. ' + $_.Exception.Message }
        foreach ($entry in @($prior.files)) {
            $target = Assert-Path (Join-Path $game $entry.path) $game
            $expected = if ($prior.mode -eq 'Install') { $entry.sha256 } else { $null }
            if ((File-Hash $target) -ne $expected) { throw "Prior owned file missing or modified; update refused: $target" }
            $priorPayloadHashes[$target] = $expected
        }
        if ($priorPrivate -and -not $private) { $retired = @($prior.files | Where-Object { $_.path.Replace('/','\') -eq $shimPath }) }
        if (-not $priorPrivate -and $null -ne (File-Hash (Assert-Path (Join-Path $game $shimPath) $game))) { throw 'Unowned private shim DLL refused; separate adoption review required.' }
    } else {
        foreach ($relative in @($basePaths + $shimPath)) {
            $target = Assert-Path (Join-Path $game $relative) $game
            if ($null -ne (File-Hash $target)) { throw "No prior ownership receipt; unknown payload refused. Separate adoption review required: $target" }
            $priorPayloadHashes[$target] = $null
        }
    }
    if (-not $private) { Assert-NativeBackend (Join-Path $config 'dbce.wodenrallyedgewheel.cfg') }
    $core = Join-Path $game 'BepInEx\core\BepInEx.Unity.IL2CPP.dll'
    if (Test-Path -LiteralPath $core) {
        # Pin copied from the verified BE #788 archive, not a name/version guess.
        $expectedCore = '46CF1EF802BDF8CB6587FC1E4D98F7AF1073A60487B39A64E230E6F6E4C23AEC'
        if ((Get-FileHash -LiteralPath $core).Hash -ne $expectedCore) { throw 'Existing BepInEx is a different version. Shared loader left unchanged.' }
        $loaderConfig = Join-Path $config 'BepInEx.cfg'
        if (-not (Test-Path -LiteralPath $loaderConfig) -or -not ((Get-Content -LiteralPath $loaderConfig -Raw) -match '(?m)^UnityBaseLibrariesSource\s*=\s*\r?$')) { throw 'Existing loader needs its Woden IL2CPP configuration reviewed; no changes made.' }
    } else {
        foreach ($name in 'winhttp.dll','doorstop_config.ini','.doorstop_version','dotnet','BepInEx') {
            $target = Assert-Path (Join-Path $game $name) $game
            if (Test-Path -LiteralPath $target) { throw "Existing loader/proxy requires review: $target" }
        }
        $scratch = Join-Path ([IO.Path]::GetTempPath()) ('WodenWheel-install-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $scratch | Out-Null
        if (-not $LoaderArchive) {
            $LoaderArchive = Join-Path $scratch $loaderName
            Write-Host 'Downloading the pinned BepInEx prerequisite...'
            [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
            Invoke-WebRequest -UseBasicParsing -Uri ('https://builds.bepinex.dev/projects/bepinex_be/788/' + [uri]::EscapeDataString($loaderName)) -OutFile $LoaderArchive
        }
        if ((Get-FileHash -LiteralPath $LoaderArchive).Hash -ne $loaderHash) { throw 'Loader download hash mismatch; game unchanged.' }
        $loaderStage = Join-Path $scratch 'loader'; Expand-Archive -LiteralPath $LoaderArchive -DestinationPath $loaderStage
    }
} else {
    if (-not (Test-Path -LiteralPath $receiptPath)) { throw 'No owned-file receipt. Separate adoption review required before update or uninstall.' }
    $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    if ($receipt.mode -notin @('Install','Uninstall') -or $receipt.installerRevision -notin @(1,2,3) -or
        [string]::IsNullOrWhiteSpace($receipt.version) -or $receipt.gameDirectory -ne $game) { throw 'Invalid uninstall ownership receipt.' }
    try { $null = Assert-ReceiptFiles $receipt } catch { throw 'Unrecognized uninstall ownership entry or invalid receipt. ' + $_.Exception.Message }
    foreach ($entry in @($receipt.files)) {
        $target = Assert-Path (Join-Path $game $entry.path) $game
        if ((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target).Hash -ne $entry.sha256) { throw "Owned file was modified; uninstall left files in place: $target" }
        $owned += $entry
    }
}
Assert-Closed
$backup = Assert-Path (Join-Path $game ('WodenWheelBackups\before-' + $Mode.ToLowerInvariant() + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8))) $game
$originals = @{}
foreach ($entry in @($owned) + @($retired)) {
    $target = Assert-Path (Join-Path $game $entry.path) $game
    Snapshot-File $target (Join-Path $backup ('WodenRallyEdgeWheel\' + [IO.Path]::GetFileName($target)))
    if ($Mode -eq 'Install' -and $originals[$target].hash -ne $priorPayloadHashes[$target]) { throw 'Prior owned file changed after receipt validation; update refused.' }
    if ($Mode -eq 'Uninstall' -and $null -ne $originals[$target].hash -and $originals[$target].hash -ne $entry.sha256) { throw "Owned file was modified; uninstall left files in place: $target" }
}
foreach ($name in 'dbce.wodenrallyedgewheel.cfg','wheel-bindings.json','wheel-bindings.json.bak','woden-record-next-launch.json') { Snapshot-File (Join-Path $config $name) (Join-Path $backup ('config\' + $name)) }
Snapshot-File $receiptPath (Join-Path $backup 'WodenWheel-install.json')
if ($Mode -eq 'Install' -and $originals[$receiptPath].hash -ne $priorReceiptHash) { throw 'Prior receipt changed after ownership validation; update refused.' }
New-Item -ItemType Directory -Path $backup | Out-Null
if (Test-Path -LiteralPath $destination) { Copy-Item -LiteralPath $destination -Destination $backup -Recurse }
if (Test-Path -LiteralPath $config) { Copy-Item -LiteralPath $config -Destination $backup -Recurse }
if (Test-Path -LiteralPath $receiptPath) { Copy-Item -LiteralPath $receiptPath -Destination $backup }
foreach ($original in $originals.Values) { if ($null -ne $original.hash -and (File-Hash $original.saved) -ne $original.hash) { throw "Backup verification failed; no installed writes performed. Backup: $backup" } }
$mutations = [Collections.Generic.List[object]]::new()
$createdDirectories = [Collections.Generic.List[string]]::new()
$payloadWrites = 0
try {
    Assert-Closed
    if ($Mode -eq 'Install') {
        if ($loaderStage) {
            foreach ($file in Get-ChildItem -LiteralPath $loaderStage -File -Recurse) {
                $relative = $file.FullName.Substring($loaderStage.Length).TrimStart('\')
                $target = Assert-Path (Join-Path $game $relative) $game
                if (Test-Path -LiteralPath $target) { throw "Loader target appeared during install: $target" }
                Snapshot-File $target $null
                if ($null -ne $originals[$target].hash) { throw "Loader target appeared during install: $target" }
                Write-Owned $file.FullName $target (File-Hash $file.FullName)
            }
            $loaderConfig = Join-Path $config 'BepInEx.cfg'
            Snapshot-File $loaderConfig $null
            if ($null -ne $originals[$loaderConfig].hash) { throw 'Loader configuration appeared during install' }
            $stagedConfig = Join-Path $backup 'new-loader-config.txt'
            "[IL2CPP]`nUnityBaseLibrariesSource = `n" | Set-Content -LiteralPath $stagedConfig -Encoding utf8
            Write-Owned $stagedConfig $loaderConfig (File-Hash $stagedConfig)
        }
        Ensure-Directory $destination
        foreach ($entry in $retired) { Remove-Owned (Assert-Path (Join-Path $game $entry.path) $game) }
        foreach ($entry in $owned) {
            Assert-Closed
            $target = Assert-Path (Join-Path $game $entry.path) $game
            if ($TestScenario -eq 'ExternalBeforeWrite' -and $payloadWrites -eq 1) { 'external change before write' | Set-Content -LiteralPath $target }
            Write-Owned (Join-Path $package $entry.path) $target $entry.sha256
            $payloadWrites++
            if ($TestScenario -eq 'ExternalReplacement' -and $payloadWrites -eq 1) { 'external replacement after write' | Set-Content -LiteralPath $target }
            if ($TestScenario -eq 'GameStarts' -and $payloadWrites -eq 2) { $script:fixtureGameRunning = $true; Assert-Closed }
            if ($TestFailAfterWrite -gt 0 -and $payloadWrites -ge $TestFailAfterWrite) { throw 'Fixture injected write failure' }
        }
    } else {
        foreach ($entry in $owned) {
            $target = Assert-Path (Join-Path $game $entry.path) $game
            Remove-Owned $target
        }
    }
    foreach ($path in $beforeConfig.Keys) { if ((Get-FileHash -LiteralPath $path).Hash -ne $beforeConfig[$path]) { throw 'Configuration changed unexpectedly' } }
    if ($Mode -eq 'Uninstall' -and $RemoveUserData) {
        foreach ($name in 'dbce.wodenrallyedgewheel.cfg','wheel-bindings.json','wheel-bindings.json.bak','woden-record-next-launch.json') {
            $target = Assert-Path (Join-Path $config $name) $game
            Remove-Owned $target
        }
    }
    $version = if ($Mode -eq 'Install') { (Get-Item -LiteralPath (Join-Path $destination 'WodenRallyEdgeWheel.dll')).VersionInfo.ProductVersion } else { $receipt.version }
    foreach ($entry in $owned) { $expected = if ($Mode -eq 'Install') { $entry.sha256 } else { $null }; Assert-Unchanged (Join-Path $game $entry.path) $expected }
    $stagedReceipt = Join-Path $backup 'new-receipt.json'
    $nextReceipt = [ordered]@{ version=$version; installerRevision=3; mode=$Mode; gameDirectory=$game; utc=[DateTime]::UtcNow.ToString('o'); backup=$backup; files=$owned; configurationPreserved=($Mode -ne 'Uninstall' -or -not $RemoveUserData); sharedLoaderRetained=$true; gameLaunched=$false }
    if ($Mode -eq 'Install') {
        # This package consumes the same v1 JSON bindings as F6. Wheelkit must
        # verify the receipt's core/plugin hashes before trusting this capability.
        $nextReceipt.controlsProfileSchema = 1
        $nextReceipt.adapter = 'woden-bindings-1'
        $nextReceipt.controlsProfileHatNeighbours = $true
        if ($private) { $nextReceipt.sharedInputCandidate = $privateMetadata }
    }
    $nextReceipt | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $stagedReceipt -Encoding utf8
    Write-Owned $stagedReceipt $receiptPath (File-Hash $stagedReceipt)
} catch {
    $failure = $_.Exception.Message
    $reasons = [Collections.Generic.List[string]]::new()
    if ($failure.StartsWith('File changed during operation;')) { $reasons.Add($failure) }
    try {
        Assert-Closed
        for ($i = $mutations.Count - 1; $i -ge 0; $i--) {
            Assert-Closed
            $change = $mutations[$i]; $target = Assert-Path $change.path $game
            $current = File-Hash $target
            if ($current -eq $change.originalHash) { continue }
            if ($current -ne $change.writtenHash) { $reasons.Add("Unknown current bytes retained: $target"); continue }
            if ($null -ne $change.originalHash -and (File-Hash $change.saved) -ne $change.originalHash) { $reasons.Add("Recovery backup changed: $($change.saved)"); continue }
            Assert-Closed
            Assert-Unchanged $target $change.writtenHash
            if ($null -ne $change.originalHash) { Copy-Item -LiteralPath $change.saved -Destination $target -Force }
            else { Remove-Item -LiteralPath $target }
            Assert-Unchanged $target $change.originalHash
        }
        if ($reasons.Count -eq 0) {
            for ($i = $createdDirectories.Count - 1; $i -ge 0; $i--) {
                Assert-Closed
                $target = Assert-Path $createdDirectories[$i] $game
                if ((Test-Path -LiteralPath $target) -and @(Get-ChildItem -LiteralPath $target -Force).Count -eq 0) { Remove-Item -LiteralPath $target }
            }
        }
    } catch { $reasons.Add($_.Exception.Message) }
    [ordered]@{ failure=$failure; recoveryRequired=($reasons.Count -gt 0); reasons=@($reasons.ToArray()); mutations=@($mutations.ToArray()) } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $backup 'recovery.json') -Encoding utf8
    if ($reasons.Count -gt 0) { throw "Recovery required; stop using this installation and review backup: $backup. No unsafe rollback attempted. $failure $($reasons -join ' ')" }
    throw "Operation rolled back; recovery backup: $backup. $failure"
}
if ($Mode -eq 'Uninstall' -and $RemoveUserData) {
    Write-Host 'Named Woden settings removed. Recordings are retained; remove selected files from BepInEx\WodenRecordings separately if desired.'
}
Write-Host "Completed $Mode. Version: $version. Backup: $backup"
if ($Mode -eq 'Install') { Write-Host 'Launch normally, then press F6 for Wheel settings. No game was launched.' }
else { Write-Host 'Owned plugin payload removed. Shared loader, unknown files, settings (unless explicitly requested) and recordings are retained.' }
