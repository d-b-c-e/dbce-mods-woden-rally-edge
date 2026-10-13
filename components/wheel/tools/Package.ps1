[CmdletBinding()]
param(
    [ValidatePattern('^[a-z0-9][a-z0-9.-]*$')][string]$ArtifactLabel,
    [string]$PrivateShimPath,
    [string]$AutomationMutedNativePath,
    [string]$AutomationNativeProvenancePath,
    [switch]$HudGuiDiagnostic,
    [switch]$HudDepthDiagnostic,
    [switch]$HudStateTrace
)
$ErrorActionPreference = 'Stop'
if ($HudDepthDiagnostic) { throw 'HudDepthDiagnostic is retired; plane 100/far 110 is ordinary HUD geometry.' }
if ($HudGuiDiagnostic -and $HudStateTrace) { throw 'HUD state trace is for the matched driving depth trial, not the menu GUI trial.' }
if ($HudGuiDiagnostic -and $ArtifactLabel -notmatch '^hudgui-') { throw 'HUD GUI diagnostic packages require a hudgui- artifact label.' }
if ($HudStateTrace -and $ArtifactLabel -notmatch '^hudstate-') { throw 'HUD state trace packages require a hudstate- artifact label.' }
$root = Split-Path $PSScriptRoot -Parent
$privateShimHash = '9B8A036F664CF8F45CCCF82B98E5E4376F93215B60EAC50B6DB965BF498015D8'
$automationNativeHash = '088F1089BEB6D18D478F37582C59330C6BDA6BC37D5E365F6BA7EBF80040F567'
$automationProvenanceHash = '6068B7A848F85FE42AE88EE8A86B10F1EF4C4E307B9C4235BE44C1A84BBEA8E5'
$baseNativeHash = 'F8361A08BBE08EAAB936B37800D308CF1FC7EDAB34E2839DB16A447FE34178C5'
if ($PrivateShimPath) {
    if (-not [IO.Path]::IsPathFullyQualified($PrivateShimPath) -or -not (Test-Path -LiteralPath $PrivateShimPath -PathType Leaf)) { throw 'PrivateShimPath must be an existing absolute file.' }
    $PrivateShimPath = [IO.Path]::GetFullPath($PrivateShimPath)
    if ((Get-FileHash -LiteralPath $PrivateShimPath).Hash -ne $privateShimHash) { throw 'PrivateShimPath does not match the reviewed ABI 2 pin.' }
}
if ($AutomationNativeProvenancePath -and -not $AutomationMutedNativePath) { throw 'Automation native provenance requires the matching native DLL.' }
if ($AutomationMutedNativePath) {
    if (-not $PrivateShimPath -or $ArtifactLabel -notmatch '^automation-muted-[a-z0-9.-]+$') {
        throw 'Automation-muted native requires a private shim and an automation-muted- artifact label.'
    }
    if (-not [IO.Path]::IsPathFullyQualified($AutomationMutedNativePath) -or -not (Test-Path -LiteralPath $AutomationMutedNativePath -PathType Leaf)) {
        throw 'AutomationMutedNativePath must be an existing absolute file.'
    }
    $AutomationMutedNativePath = [IO.Path]::GetFullPath($AutomationMutedNativePath)
    if ((Get-FileHash -LiteralPath $AutomationMutedNativePath).Hash -ne $automationNativeHash) {
        throw 'AutomationMutedNativePath does not match the proven no-force test native pin.'
    }
    if (-not $AutomationNativeProvenancePath -or -not [IO.Path]::IsPathFullyQualified($AutomationNativeProvenancePath) -or
        -not (Test-Path -LiteralPath $AutomationNativeProvenancePath -PathType Leaf)) { throw 'AutomationNativeProvenancePath must be the exact reviewed sidecar.' }
    $AutomationNativeProvenancePath = [IO.Path]::GetFullPath($AutomationNativeProvenancePath)
    if ((Get-FileHash -LiteralPath $AutomationNativeProvenancePath).Hash -ne $automationProvenanceHash) { throw 'Automation native provenance hash differs.' }
    $nativeProvenance = Get-Content -LiteralPath $AutomationNativeProvenancePath -Raw | ConvertFrom-Json
    if ($nativeProvenance.dllSha256 -ne $automationNativeHash -or $nativeProvenance.arch -cne 'x64' -or
        $nativeProvenance.define -cne 'WHEELFFB_NO_FORCE_BUILD' -or $nativeProvenance.marker -cne 'DBCE-WHEELFFB-NO-FORCE-TEST-BUILD' -or
        $nativeProvenance.commit -cne 'eb77b909416b8a6ef70787e4621aaa62f16b9f45' -or $nativeProvenance.sourcesDirty -ne $false -or
        $nativeProvenance.firstCallStatus -notmatch 'armed: no force \(no-force build\)') { throw 'Native sidecar does not attest the qualified load-muted build.' }
    $sourceCommit = (& git -C (Join-Path $root '..\..') rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $sourceCommit -notmatch '^[a-f0-9]{40}$') { throw 'Automation package source commit could not be pinned.' }
    if (@(& git -C (Join-Path $root '..\..') status --porcelain --untracked-files=no).Count -ne 0) {
        throw 'Automation package requires a clean tracked source checkout.'
    }
}
& (Join-Path $PSScriptRoot 'Verify-Dependencies.ps1')
$buildArgs = @('build',(Join-Path $root 'WodenRallyEdgeWheel.sln'),'-c','Release','-warnaserror','-v','q')
if ($PrivateShimPath) { $buildArgs += ('-p:PrivateShimPath=' + $PrivateShimPath) }
$diagnostics = @()
if ($HudGuiDiagnostic) { $diagnostics += 'WODEN_HUD_GUI_DIAGNOSTIC' }
if ($HudStateTrace) { $diagnostics += 'WODEN_HUD_STATE_TRACE' }
if ($diagnostics.Count) { $buildArgs += ('-p:DefineConstants=' + ($diagnostics -join '%3B')) }
& dotnet @buildArgs
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
& dotnet run --project (Join-Path $root 'tests\WodenRallyEdge.Tests') -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw 'Regression failed' }
& dotnet run --project (Join-Path $root 'tests\WodenRallyEdge.UiTests') -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw 'UI fixture failed' }
& dotnet run --project (Join-Path $root 'tests\WodenRallyEdge.InputTests') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Coherent input/profile-hat fixture failed' }
$stage = Join-Path $root ('dist\stage-' + [guid]::NewGuid().ToString('N'))
$plugin = Join-Path $stage 'BepInEx\plugins\WodenRallyEdgeWheel'
New-Item -ItemType Directory -Force $plugin | Out-Null
$bin = Join-Path $root 'src\WodenRallyEdge.Plugin\bin\Release\net6.0'
# Allowlist prevents game/interop/core/analysis assemblies entering a release.
foreach ($name in 'WodenRallyEdgeWheel.dll','WodenRallyEdge.Core.dll','Dbce.Wheel.Telemetry.dll','Dbce.Wheel.Recording.dll','Dbce.Wheel.Ffb.dll','WheelFfb.dll','Dbce.Wheel.Playback.dll') {
    Copy-Item -LiteralPath (Join-Path $bin $name) -Destination $plugin
}
if ($AutomationMutedNativePath) {
    if ((Get-FileHash -LiteralPath (Join-Path $plugin 'WheelFfb.dll')).Hash -ne $baseNativeHash) {
        throw 'Automation package base native differs from the pinned v0.12.0 payload.'
    }
    Copy-Item -LiteralPath $AutomationMutedNativePath -Destination (Join-Path $plugin 'WheelFfb.dll') -Force
    if ((Get-FileHash -LiteralPath (Join-Path $plugin 'WheelFfb.dll')).Hash -ne $automationNativeHash) {
        throw 'Staged automation native differs from the proven no-force pin.'
    }
    Copy-Item -LiteralPath $AutomationNativeProvenancePath -Destination (Join-Path $stage 'WheelFfb.noforce.provenance.json')
    if ((Get-FileHash -LiteralPath (Join-Path $stage 'WheelFfb.noforce.provenance.json')).Hash -ne $automationProvenanceHash) { throw 'Staged native provenance differs.' }
    [ordered]@{
        schema='woden.automation-muted-native@1'; purpose='internal hash-pinned temporary test deployment only; restore the product afterward; never a release or lasting install'
        sourceCommit=$sourceCommit
        baseToolkitVersion='v0.12.0'; baseNativeSha256=$baseNativeHash.ToLowerInvariant()
        nativeSha256=$automationNativeHash.ToLowerInvariant()
        nativeDefaultState='armed'; requiresExplicitArm=$false
        toolkitNativeCommit=$nativeProvenance.commit; toolkitNativeProvenanceSha256=$automationProvenanceHash.ToLowerInvariant()
        provenance='WheelFfb no-force internal build eb77b90; native force gate latched before first export, independently qualified with actual DLL first-call and fake-device checks'
    } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $stage 'automation-native-provenance.json') -Encoding utf8
    'INTERNAL AUTOMATION ONLY. Temporary hash-pinned test deployment; restore the product afterward. Never a release or lasting owner install.' |
        Set-Content -LiteralPath (Join-Path $stage 'AUTOMATION-ONLY.txt') -Encoding utf8
}
Copy-Item -LiteralPath (Join-Path $root 'lib\recording\provenance.json') -Destination (Join-Path $plugin 'recording-provenance.json')
$playback = Join-Path $root '../../vendor/playback'
Copy-Item -LiteralPath (Join-Path $playback 'provenance.json') -Destination (Join-Path $plugin 'playback-provenance.json')
Copy-Item -LiteralPath (Join-Path $playback 'LICENSE') -Destination (Join-Path $plugin 'playback-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $playback 'Stage-Session.ps1') -Destination $plugin
Copy-Item -LiteralPath (Join-Path $playback 'Stage-RigLease.ps1') -Destination $plugin
Copy-Item -LiteralPath (Join-Path $playback 'rig-lease-provenance.json') -Destination $plugin
Copy-Item -LiteralPath (Join-Path $playback 'Stage-RunnerLifecycle.ps1') -Destination $plugin
Copy-Item -LiteralPath (Join-Path $root '../../tools/game/Run-StageSession.ps1') -Destination (Join-Path $plugin 'WodenFullSession.ps1')
Copy-Item -LiteralPath (Join-Path $root '../../tools/game/ReplayEnvironment.ps1') -Destination $plugin
[ordered]@{
    schema='dbce.session-capabilities@1'; game='Super Woden Rally Edge'; protocol='woden.full-session-package@1'
    runner='WodenFullSession.ps1'; gameBuildSha256='f422894d8d2b0df4edb7e5259e5e60cb8c4f8dea2e85ebdfc09dd6766349250c'
    record=[ordered]@{enabled=$false}
    playback=[ordered]@{enabled=$false;adapters=@('woden.native-arcade-v1@1')}
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $plugin 'session-capabilities.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $root 'lib\toolkit\VERSION') -Destination (Join-Path $plugin 'toolkit.version')
foreach ($name in 'Start-RecordedGame.ps1','RecordedStopControl.ps1') {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $plugin
}
$finalizerPublish = Join-Path $root ('dist\finalizer-publish-' + [guid]::NewGuid().ToString('N'))
& dotnet publish (Join-Path $root 'tools\TelemetryInspector\TelemetryInspector.csproj') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $finalizerPublish -v q
if ($LASTEXITCODE -ne 0) { throw 'Self-contained case finalizer publish failed' }
Copy-Item -LiteralPath (Join-Path $finalizerPublish 'TelemetryInspector.exe') -Destination (Join-Path $plugin 'WodenCaseFinalizer.exe')
if ($PrivateShimPath) {
    Copy-Item -LiteralPath $PrivateShimPath -Destination (Join-Path $plugin 'dbce_shim.dll')
    if ((Get-FileHash -LiteralPath (Join-Path $plugin 'dbce_shim.dll')).Hash -ne $privateShimHash) { throw 'Staged private shim differs from the reviewed pin.' }
    $delivered = @('steer','throttle','brake','handbrake','shiftUp','shiftDown','neutral','reverse','confirm','back','start','navUp','navDown','navLeft','navRight','camera','lookBack','reset','horn','modMenu','muteFfb','gear1','gear2','gear3','gear4','gear5','gear6')
    $unsupported = [ordered]@{}
    foreach ($action in 'clutch','select','gear7','gear8') { $unsupported[$action] = 'not delivered by Woden wheel-only shim pilot' }
    $conditions = [ordered]@{ shiftUp='Transmission=Sequential;NativeGearbox=Manual'; shiftDown='Transmission=Sequential;NativeGearbox=Manual' }
    foreach ($action in @('neutral','reverse') + @(1..6 | ForEach-Object { 'gear' + $_ })) { $conditions[$action] = 'Transmission=H-pattern;NativeGearbox=Manual' }
    $candidate = [ordered]@{
        schema=1; adapter='woden-shim-ini-controls-1'; profile='BepInEx/config/dbce-controls.ini'; settings='BepInEx/config/dbce.wodenrallyedgewheel.cfg'; shimAbi=2
        optIn=[ordered]@{ section='Input'; key='Backend'; value='Shim'; default='Native'; appliesAfterRestart=$true }
        gameAssemblySha256='F422894D8D2B0DF4EDB7E5259E5E60CB8C4F8DEA2E85EBDFC09DD6766349250C'
        pluginSha256=(Get-FileHash -LiteralPath (Join-Path $plugin 'WodenRallyEdgeWheel.dll')).Hash.ToLowerInvariant()
        coreSha256=(Get-FileHash -LiteralPath (Join-Path $plugin 'WodenRallyEdge.Core.dll')).Hash.ToLowerInvariant()
        nativeSha256=(Get-FileHash -LiteralPath (Join-Path $plugin 'WheelFfb.dll')).Hash.ToLowerInvariant()
        shimSha256=$privateShimHash.ToLowerInvariant(); delivered=$delivered; unsupported=$unsupported; conditions=$conditions
    }
    [ordered]@{ controls=$candidate } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $stage 'private-shim-capability.json') -Encoding utf8
}
& dotnet run --project (Join-Path $root 'tools\TelemetryInspector') -c Release --no-build -- schema (Join-Path $plugin 'telemetry-schema.json')
if ($LASTEXITCODE -ne 0) { throw 'Schema export failed' }
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $stage
Copy-Item -LiteralPath (Join-Path $root 'docs\DEVELOPMENT-BUILD.md') -Destination (Join-Path $stage 'README.md')
foreach ($name in 'Install.bat','Uninstall.bat','Manage-Install.ps1') { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $stage }
$manifest = @(Get-ChildItem -LiteralPath $stage -File -Recurse | Sort-Object FullName | ForEach-Object { @{ path = [IO.Path]::GetRelativePath($stage, $_.FullName); sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant() } })
$manifest | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $stage 'manifest.json') -Encoding utf8
$version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$identity = if ($ArtifactLabel) { "$version-$ArtifactLabel" } else { $version }
$suffix = if ($PrivateShimPath) { '-private-shim-dev.zip' } else { '-dev.zip' }
$zip = Join-Path $root ("dist\WodenRallyEdgeWheel-$identity" + $suffix)
if (Test-Path -LiteralPath $zip) { throw 'Package already exists. Preserve its identity and choose a distinct version before packaging again.' }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
Write-Host "Development package: $zip"
Write-Host "Stage: $stage"
