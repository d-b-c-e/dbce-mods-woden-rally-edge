[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location $PSScriptRoot
try {
 $sdk=(Get-Content (Join-Path $PSScriptRoot 'global.json') -Raw|ConvertFrom-Json).sdk.version
 if((& dotnet --version).Trim() -ne $sdk){throw 'Use the pinned CI SDK'}
 $pins=@{
  'components/wheel/lib/toolkit/dotnet/Dbce.Wheel.Ffb.dll'='f5217f81b5bd305e0efca3aacc459b9066c4366cd220c3e4299c511e23b5431a'
  'components/wheel/lib/toolkit/dotnet/Dbce.Wheel.Telemetry.dll'='765f5c77814f200b8e2407d141e352e0c9225dcbf2f92fd3f64e69b79c7436e2'
 }
 $recording=Get-Content (Join-Path $root 'components/wheel/lib/recording/provenance.json') -Raw|ConvertFrom-Json
 $pins['components/wheel/lib/recording/Dbce.Wheel.Recording.dll']=$recording.dllSha256
 $delivery=Join-Path $root 'tools/delivery/v1'
 $contract=Get-Content (Join-Path $delivery 'PIN.json') -Raw|ConvertFrom-Json
 foreach($p in $contract.files.PSObject.Properties){$pins['tools/delivery/v1/'+$p.Name]=$p.Value}
 foreach($p in $pins.Keys){if((Get-FileHash -LiteralPath (Join-Path $root $p)).Hash.ToLowerInvariant() -ne $pins[$p].ToLowerInvariant()){throw "Pinned bytes changed: $p"}}
 foreach($project in 'ManagedChecks','GeometryChecks'){
  & dotnet build (Join-Path $PSScriptRoot "$project/$project.csproj") -c Release -warnaserror "-p:RestoreConfigFile=$PSScriptRoot/NuGet.Config" -p:UseAppHost=false
  if($LASTEXITCODE -ne 0){throw "Asset-free $project build failed"}
  & dotnet (Join-Path $PSScriptRoot "$project/bin/Release/net10.0/$project.dll")
  if($LASTEXITCODE -ne 0){throw "Asset-free $project checks failed"}
 }
 & (Join-Path $PSScriptRoot 'Test-SummaryCli.ps1') -Assembly (Join-Path $PSScriptRoot 'ManagedChecks/bin/Release/net10.0/ManagedChecks.dll')
 & (Join-Path $PSScriptRoot 'Test-AssetFreeSetup.ps1')
 Write-Host 'PASS asset-free CI subset; no private references, game/native devices, installation or artifact publication.'
} finally {Pop-Location}
