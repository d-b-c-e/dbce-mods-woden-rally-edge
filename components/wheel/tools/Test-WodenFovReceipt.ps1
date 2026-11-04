[CmdletBinding()]
param([string]$Installer, [string]$EmitFixture)
$ErrorActionPreference = 'Stop'
if (-not $Installer) { $Installer=Join-Path $PSScriptRoot 'Manage-Install.ps1' }
$tokens=$null; $errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile($Installer,[ref]$tokens,[ref]$errors)
if (@($errors).Count) { throw 'Installer syntax errors.' }
$functions=@($ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq 'Expected-WodenFovSets'},$false))
if ($functions.Count -ne 1) { throw 'Missing source producer Expected-WodenFovSets.' }
Invoke-Expression $functions[0].Extent.Text
$assignments=@($ast.FindAll({param($n) $n -is [Management.Automation.Language.AssignmentStatementAst] -and $n.Left.Extent.Text -eq '$nextReceipt.wodenFovSets'},$false))
if ($assignments.Count -ne 1 -or $assignments[0].Right.Extent.Text -ne 'Expected-WodenFovSets') { throw 'Receipt does not use the reviewed producer.' }
$parent=$assignments[0].Parent
while ($parent -and $parent -isnot [Management.Automation.Language.IfStatementAst]) { $parent=$parent.Parent }
if (-not $parent -or $parent.Clauses[0].Item1.Extent.Text -ne '$Mode -eq ''Install''') { throw 'Camera declaration must only advertise installation.' }
$nextReceipt=[ordered]@{}
Invoke-Expression $assignments[0].Extent.Text
$cap=$nextReceipt.wodenFovSets
if ($cap.schema -ne 1 -or $cap.config -cne 'BepInEx/config/dbce.wodenrallyedgewheel.cfg' -or $cap.section -cne 'Triple') { throw 'Wrong destination.' }
if (($cap.starts.camera -join ',') -cne 'Bonnet,Bumper,Chase,Isometric' -or ($cap.starts.key -join ',') -cne 'BonnetRealistic,BumperRealistic,ChaseRealistic,IsometricRealistic') { throw 'Wrong slot map.' }
if (($cap.starts.defaultRealistic -join ',') -cne 'False,True,False,False') { throw 'Wrong defaults.' }
if (($cap.stockPresetFamilies -join ',') -cne 'Chase,Isometric' -or $cap.stockPresetIndex -cne 'canonical-int32') { throw 'Wrong preset grammar.' }
if ($cap.offsetsOwnedByApply -isnot [bool] -or $cap.offsetsOwnedByApply -or $cap.runtimeVerified -isnot [bool] -or $cap.runtimeVerified) { throw 'Invalid source-only boundary.' }
$json=$cap | ConvertTo-Json -Depth 8
if ($EmitFixture) { [IO.File]::WriteAllText([IO.Path]::GetFullPath($EmitFixture),$json+[Environment]::NewLine,[Text.UTF8Encoding]::new($false)) }
Write-Host 'PASS: actual receipt producer and install-only emission; four fixed choices, existing stock grammar, no offsets/runtime claim.'
