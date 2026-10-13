#Requires -Version 7.0
# Offline check (halo 73104): Run-StageSession.ps1 resolves -AdmittedPackage against the caller's PowerShell location before
# any use, as it does -Recording. The native inspector and .NET Path.GetFullPath resolve against the process directory,
# which can differ (Start-Job, Push-Location). No game, device, lease or owner file.
$ErrorActionPreference = 'Stop'
$runner = Join-Path $PSScriptRoot 'Run-StageSession.ps1'
$errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($runner, [ref]$null, [ref]$errors)
if ($errors.Count) { throw "Run-StageSession.ps1 has $($errors.Count) parse errors" }

# 1. Order: the resolution assignment comes before every other use of $AdmittedPackage outside the param block.
$assignments = @($ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.AssignmentStatementAst] -and
    $n.Left.Extent.Text -eq '$AdmittedPackage' -and $n.Right.Extent.Text -match 'GetUnresolvedProviderPathFromPSPath\(\$AdmittedPackage\)' }, $true))
if ($assignments.Count -ne 1) { throw "Expected one caller-location resolution of AdmittedPackage, found $($assignments.Count)" }
$guard = $assignments[0]; while ($guard -and $guard -isnot [System.Management.Automation.Language.IfStatementAst]) { $guard = $guard.Parent }
if (-not $guard -or $guard.Clauses[0].Item1.Extent.Text -ne '$AdmittedPackage') { throw 'The resolution must be guarded by if ($AdmittedPackage)' }
$paramEnd = $ast.ParamBlock.Extent.EndOffset
$early = @($ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.VariableExpressionAst] -and $n.VariablePath.UserPath -eq 'AdmittedPackage' }, $true) |
    Where-Object { $_.Extent.StartOffset -gt $paramEnd -and $_.Extent.StartOffset -lt $guard.Extent.StartOffset })
if ($early.Count) { throw "AdmittedPackage is used before it is resolved, at line $($early[0].Extent.StartLineNumber)" }
$recording = @($ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.AssignmentStatementAst] -and $n.Left.Extent.Text -eq '$Recording' -and
    $n.Right.Extent.Text -match 'GetUnresolvedProviderPathFromPSPath' }, $true))
if ($recording.Count -ne 1 -or [Math]::Abs($recording[0].Extent.StartLineNumber - $assignments[0].Extent.StartLineNumber) -gt 2) {
    throw 'AdmittedPackage is not resolved alongside Recording'
}

# 2. The divergence the resolution guards against: PowerShell location versus process directory.
$location = Join-Path ([IO.Path]::GetTempPath()) ('admitted-path-ps-' + [guid]::NewGuid().ToString('N'))
$process = Join-Path ([IO.Path]::GetTempPath()) ('admitted-path-cwd-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $location, $process | Out-Null
$savedCwd = [Environment]::CurrentDirectory
Push-Location $location
try {
    [Environment]::CurrentDirectory = $process
    $caller = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath('package')
    $native = [IO.Path]::GetFullPath('package')
    if ($caller -ne (Join-Path $location 'package')) { throw "Caller-location resolution differs: $caller" }
    if ($native -ne (Join-Path $process 'package')) { throw "Process-directory resolution differs: $native" }
} finally {
    Pop-Location
    [Environment]::CurrentDirectory = $savedCwd
    foreach ($folder in $location, $process) { if (Test-Path -LiteralPath $folder) { [IO.Directory]::Delete($folder) } }
}
"PASS admitted package path: resolved at line $($assignments[0].Extent.StartLineNumber) beside Recording, before its first use; a relative package follows the caller's location ($location), not the process directory."
