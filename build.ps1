# Builds the wheel solution and runs the offline tests. No arguments needed: `pwsh build.ps1`.
# Run components/wheel/tools/Initialize-Dependencies.ps1 once first on a fresh checkout.
# Not run here, because each needs inputs prepared by hand: PresentationBatch (two arguments), and
# WodenRallyEdge.TripleFovTests (prepare.py copies the plugin's camera methods, then -p:TripleMethods=<file>).
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath (Join-Path $PSScriptRoot 'components\wheel')
$failed = [Collections.Generic.List[string]]::new()
$scratch = [Collections.Generic.List[string]]::new()
function Step([string]$Name, [scriptblock]$Run) {
    $out = & $Run 2>&1
    if ($LASTEXITCODE) { $failed.Add($Name); "FAIL $Name"; $out | Select-Object -Last 15 } else { "ok   $Name" }
}
# A test that writes files gets a new folder under artifacts\build-check, removed afterwards.
function Scratch([string]$Name) {
    $p = Join-Path $PSScriptRoot "artifacts\build-check\$Name-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
    $scratch.Add($p); $p
}

Step 'WodenRallyEdgeWheel.sln' { dotnet build WodenRallyEdgeWheel.sln -c Release -warnaserror --nologo -v q }
foreach ($t in 'WodenRallyEdge.Tests', 'WodenRallyEdge.InputTests', 'WodenRallyEdge.UiTests') {
    Step $t { dotnet run --project "tests/$t" -c Release -v q }
}
Step 'CameraSets' { dotnet run --project tests/CameraSets -c Release -v q -- (Scratch 'camera-sets') }
# These two compile the production Bindings.cs against small stand-ins; building them catches a stale stand-in.
foreach ($t in 'BindingsPublication', 'DeadzoneBytes') { Step "$t (compiles)" { dotnet build "tests/$t" -c Release --nologo -v q } }

foreach ($p in $scratch) { if ([IO.Directory]::Exists($p)) { [IO.Directory]::Delete($p, $true) } }
if ($failed.Count) { throw "$($failed.Count) step(s) failed: $($failed -join ', ')" }
'build and offline tests passed'
