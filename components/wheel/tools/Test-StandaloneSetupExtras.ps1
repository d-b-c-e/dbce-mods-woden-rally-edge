#Requires -Version 7.0
<# Fake-stage fixture for the private setup helper extras: Add-StandaloneSetup.ps1 and the packaged
   Extras\Setup-Controls.ps1 launcher. Needs the pinned helper ZIP and its sidecar (-Cache, default lib\cache). Uses
   disposable folders only: no game, install, device or helper window. The launcher runs under Windows PowerShell 5.1,
   as Set Up Controls.bat starts it, with -PlanOnly. #>
[CmdletBinding()]
param([string]$Cache = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $Cache) { $Cache = Join-Path $root 'lib\cache' }
$work = Join-Path $root ('artifacts\standalone-extras-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $work
$script:checks = 0
function Check([bool]$condition, [string]$message) { if (-not $condition) { throw $message }; $script:checks++ }
function Refuses([scriptblock]$action, [string]$pattern) {
    $message = $null
    try { & $action } catch { $message = $_.Exception.Message }
    Check ($null -ne $message -and $message -match $pattern) "Expected a refusal matching '$pattern', got: $message"
}
# As Package.ps1 writes it: every staged file, relative path and lowercase SHA-256.
function Write-Manifest([string]$stage) {
    Remove-Item -LiteralPath (Join-Path $stage 'manifest.json') -ErrorAction SilentlyContinue
    $manifest = @(Get-ChildItem -LiteralPath $stage -File -Recurse | Sort-Object FullName | ForEach-Object { @{ path = [IO.Path]::GetRelativePath($stage, $_.FullName); sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant() } })
    $manifest | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $stage 'manifest.json') -Encoding utf8
}
function New-Stage([string]$name) {
    $stage = Join-Path $work $name
    $null = New-Item -ItemType Directory -Path $stage
    'Fixture package README' | Set-Content -LiteralPath (Join-Path $stage 'README.md') -Encoding utf8
    'Fixture installer, never run' | Set-Content -LiteralPath (Join-Path $stage 'Manage-Install.ps1') -Encoding utf8
    return $stage
}
function Copy-Stage([string]$from, [string]$name) { $to = Join-Path $work $name; Copy-Item -LiteralPath $from -Destination $to -Recurse; return $to }
# The launcher exactly as the .bat starts it; a refusal becomes this process's exception.
function Launch([string]$stage, [string]$game) {
    $output = & "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File (Join-Path $stage 'Extras\Setup-Controls.ps1') -GameDir $game -PlanOnly 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw $output.Trim() }
    return $output.Trim()
}
$pin = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'standalone-setup.json') -Raw | ConvertFrom-Json
try {
    # Staging: the pinned bundle, its launcher and pin, the root .bat and the README section, before the manifest.
    $stage = New-Stage 'package'
    & (Join-Path $PSScriptRoot 'Add-StandaloneSetup.ps1') -Stage $stage -Cache $Cache | Out-Null
    $helper = Join-Path $stage 'Extras\ModSetup'
    Check (Test-Path -LiteralPath (Join-Path $helper 'DbceModSetup.exe')) 'Helper executable not staged'
    Check ((Get-FileHash -LiteralPath (Join-Path $helper 'bundle-manifest.json')).Hash -eq $pin.bundleManifestSha256) 'Staged bundle manifest differs from the pin'
    Check (@(Get-ChildItem -LiteralPath $helper -Recurse -File).Count -eq $pin.files + 1) 'Staged helper file count differs'
    Check ((Test-Path -LiteralPath (Join-Path $stage 'Set Up Controls.bat')) -and (Test-Path -LiteralPath (Join-Path $stage 'Extras\Setup-Controls.ps1')) -and
        (Test-Path -LiteralPath (Join-Path $stage 'Extras\standalone-setup.json'))) 'Launcher, pin or .bat not staged'
    Check ((Get-Content -LiteralPath (Join-Path $stage 'README.md') -Raw).Contains('Set Up Controls.bat')) 'README section not appended'
    Write-Manifest $stage
    Refuses { & (Join-Path $PSScriptRoot 'Add-StandaloneSetup.ps1') -Stage $stage -Cache $Cache } 'already has an Extras'
    # A ZIP that is not the pinned bytes stages nothing.
    $badCache = Join-Path $work 'bad-cache'; $null = New-Item -ItemType Directory -Path $badCache
    Copy-Item -LiteralPath (Join-Path $Cache $pin.zip), (Join-Path $Cache ($pin.zip + '.sha256')) -Destination $badCache
    [IO.File]::AppendAllText((Join-Path $badCache $pin.zip), 'changed')
    $badStage = New-Stage 'bad-zip'
    Refuses { & (Join-Path $PSScriptRoot 'Add-StandaloneSetup.ps1') -Stage $badStage -Cache $badCache } 'differs from the pin'
    Check (-not (Test-Path -LiteralPath (Join-Path $badStage 'Extras'))) 'A changed ZIP staged files'

    # Launcher: installed mod required; the exact game-locked plan; no helper start.
    $game = Join-Path $work 'Super Woden Rally Edge'; $null = New-Item -ItemType Directory -Path (Join-Path $game 'BepInEx')
    Refuses { Launch $stage $game } 'Install the mod first'
    '{"fixture":true}' | Set-Content -LiteralPath (Join-Path $game 'BepInEx\WodenWheel-install.json') -Encoding utf8
    $plan = Launch $stage $game
    Check ($plan -ceq ('PLAN ONLY: "' + (Join-Path $helper 'DbceModSetup.exe') + '" --game super-woden-rally-edge --game-dir "' + $game + '"')) "Unexpected plan: $plan"
    # A changed managed DLL beside the unchanged executable never starts, with or without a rehashed package manifest.
    $changed = Copy-Stage $stage 'changed-dll'
    [IO.File]::AppendAllText((Join-Path $changed 'Extras\ModSetup\DbceModSetup.dll'), 'changed')
    Refuses { Launch $changed $game } 'Package integrity check failed'
    Write-Manifest $changed
    Refuses { Launch $changed $game } 'differs from its bundle manifest'
    # Nor does an unlisted file beside the bundle, even when the package manifest lists it.
    $extra = Copy-Stage $stage 'unlisted-file'
    'not in the bundle' | Set-Content -LiteralPath (Join-Path $extra 'Extras\ModSetup\unlisted.dll') -Encoding utf8
    Write-Manifest $extra
    Refuses { Launch $extra $game } 'does not list'
    # A pin edited to match a changed bundle manifest is caught by the package manifest.
    $pinEdit = Copy-Stage $stage 'pin-edit'
    $edited = Get-Content -LiteralPath (Join-Path $pinEdit 'Extras\standalone-setup.json') -Raw | ConvertFrom-Json
    $edited.bundleManifestSha256 = '0' * 64
    $edited | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $pinEdit 'Extras\standalone-setup.json') -Encoding utf8
    Refuses { Launch $pinEdit $game } 'Package integrity check failed'
    # The packaging switch needs the private shim; it is refused before any build step.
    Refuses { & (Join-Path $PSScriptRoot 'Package.ps1') -PrivateStandaloneSetup } 'only with -PrivateShimPath'
    Write-Output "PASS: $script:checks setup-helper extras checks ($($pin.sourceCommit.Substring(0, 12))); evidence $work"
} catch { Write-Output "FAIL after $script:checks checks; evidence kept at $work"; throw }
