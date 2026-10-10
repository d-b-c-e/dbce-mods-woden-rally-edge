#Requires -Version 7.0
# No owner paths/registry/devices. Uses the actual frozen writer and synthetic
# Woden capability/config fixture, with wrong bindings inserted by production helper.
$ErrorActionPreference='Stop'
. "$PSScriptRoot/OwnerFiles.ps1"
. "$PSScriptRoot/Apply-Check.ps1"
$dir=Join-Path ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../results'))) ('controls-apply-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($dir)|Out-Null
$writer=& "$PSScriptRoot/Freeze-Wheelkit.ps1" -Result $dir -Repo 'E:/Source/toolkits/dbce-wheelkit'
$case=Join-Path $dir 'writer-source/tests/fixtures/configuration/woden-controls'
Copy-Item "$case/before/game" "$dir/game" -Recurse
$spec=Get-Content "$case/case.json" -Raw | ConvertFrom-Json
@{Schema=2;ActiveId=$spec.Profile.Id;Profiles=@($spec.Profile)} | ConvertTo-Json -Depth 30 | Set-Content "$dir/input-profiles.json"
Invoke-ControlApplyCheck "$dir/game" $dir $writer.harness $writer.catalog "$dir/input-profiles.json"
$after=Get-Content "$dir/game/BepInEx/config/wheel-bindings.json" -Raw | ConvertFrom-Json
if($after.Steer.Axis -ne 0 -or $after.Buttons.Confirm.Button -ne 31){throw 'Independent expected synthetic bindings differ.'}
if(!(Test-Path "$dir/apply-check.json")){throw 'No completed parity evidence.'}
& dotnet $writer.harness restore-live "$dir/apply"
if($LASTEXITCODE){throw 'Synthetic transaction did not restore.'}
$wrong=Get-Content "$dir/game/BepInEx/config/wheel-bindings.json" -Raw | ConvertFrom-Json
if($wrong.Steer.Axis -ne 1 -or $wrong.Buttons.Confirm.Button -ne 32){throw 'Did not restore staged synthetic input.'}
"PASS: frozen reviewed writer, staged wrong controls, fixture/live preview+bytes parity, independent repaired values and restore. $dir"
