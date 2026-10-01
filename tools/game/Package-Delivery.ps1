[CmdletBinding()]
param([Parameter(Mandatory)][string]$BaselineStage,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop';$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$baselineHash=(Get-FileHash (Join-Path $BaselineStage 'manifest.json')).Hash.ToLowerInvariant()
if($baselineHash -cne '20e9c9beed5458f81c183e582a34067535a946c6a948a96d91817db013741a23'){throw 'Not the reviewed immutable 14740fd baseline stage'}
& (Join-Path $PSScriptRoot 'Verify-SourceStage.ps1') -PackageRoot $BaselineStage
$source=& git -c "safe.directory=$root" -C $root rev-parse HEAD;if($LASTEXITCODE){throw 'Cannot identify packaging commit'}
$tree=& git -c "safe.directory=$root" -C $root rev-parse 'HEAD^{tree}';if($LASTEXITCODE){throw 'Cannot identify actual packaging tree'}
$dirty=@(& git -c "safe.directory=$root" -C $root status --porcelain);if($LASTEXITCODE -or $dirty.Count){throw 'Commit source before freezing delivery ZIP'}
$runtime='14740fdcba4a7e9c490a7e26d94f54614c75e5f7';$rtree=& git -c "safe.directory=$root" -C $root rev-parse ($runtime+'^{tree}');if($LASTEXITCODE -or $rtree -cne '461155fc74da937288ba09d42aae8fdeff11190b'){throw 'Retained runtime tree mismatch'}
$version='0.2.13-delivery.1';$stage=Join-Path $OutputDirectory ('stage-'+$version);$zip=Join-Path $OutputDirectory ('dbce-mods-super-woden-rally-edge-'+$version+'.zip')
if((Test-Path $stage) -or (Test-Path $zip)){throw 'Immutable candidate exists; choose a new output directory'}
$null=New-Item -ItemType Directory -Path $OutputDirectory -Force
Copy-Item -LiteralPath $BaselineStage -Destination $stage -Recurse
foreach($name in 'README.md','GUIDE.md'){Copy-Item (Join-Path $PSScriptRoot ('player/'+$name)) (Join-Path $stage $name) -Force}
foreach($name in 'Verify-Package.ps1','Woden-Delivery.ps1'){Copy-Item (Join-Path $PSScriptRoot $name) (Join-Path $stage $name)}
$common=Join-Path $root 'tools/delivery/v1';foreach($name in 'SPEC.md','ACCEPTANCE-FIXTURES.md','delivery-validator.ps1','delivery-parser.cs'){Copy-Item (Join-Path $common $name) (Join-Path $stage $name)}
Copy-Item (Join-Path $common 'PIN.json') (Join-Path $stage 'delivery-pin.json')
$sources=@();foreach($role in 'runtime','installer','packaging'){$sources += [ordered]@{role=$role;repositoryUrl='https://github.com/d-b-c-e/woden-rally-edge-wheel';commit=$(if($role -eq 'packaging'){$source}else{$runtime});tree=$(if($role -eq 'packaging'){$tree}else{$rtree});dirty=$false}}
$p=[ordered]@{schema='dbce.woden.package-provenance.v1';runtimeVersion='0.2.13+'+$runtime;baselineManifestSha256=$baselineHash;sources=$sources}
$p|ConvertTo-Json -Depth 8|Set-Content (Join-Path $stage 'package-provenance.json') -Encoding utf8
$legacy=Get-Content (Join-Path $stage 'game-release.json') -Raw|ConvertFrom-Json
$legacy.version=$version;$legacy|Add-Member -NotePropertyName runtimeVersion -NotePropertyValue $p.runtimeVersion
$legacy|Add-Member -NotePropertyName sourceTree -NotePropertyValue $rtree
$legacy|ConvertTo-Json -Depth 8|Set-Content (Join-Path $stage 'game-release.json') -Encoding utf8
& (Join-Path $PSScriptRoot 'New-DeliveryManifest.ps1') -PackageRoot $stage -Version $version -Provenance $p
# Integrity inventory hashes descriptor bytes, not itself. External ZIP digest stays outside archive.
$manifest=@(Get-ChildItem $stage -File -Recurse|Where-Object Name -ne 'manifest.json'|Sort-Object FullName|ForEach-Object {[ordered]@{path=$_.FullName.Substring(([IO.Path]::GetFullPath($stage).TrimEnd('\','/')).Length+1).Replace('\','/');sha256=(Get-FileHash $_.FullName).Hash.ToLowerInvariant()}})
$manifest|ConvertTo-Json -Depth 5|Set-Content (Join-Path $stage 'manifest.json') -Encoding utf8
& (Join-Path $PSScriptRoot 'Verify-Package.ps1') -PackageRoot $stage
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory([IO.Path]::GetFullPath($stage),[IO.Path]::GetFullPath($zip))
. (Join-Path $PSScriptRoot 'Woden-Delivery.ps1')
. (Join-Path $PSScriptRoot 'Delivery-Zip.ps1')
$extracted=Expand-WodenDeliveryZip -Archive $zip -Destination (Join-Path $OutputDirectory 'verified-extraction')
$null=Assert-WodenDelivery -Root $extracted
foreach($file in @(Get-WodenDeliveryFiles)+@('manifest.json')){if((Get-FileHash (Join-Path $stage $file)).Hash -ne (Get-FileHash (Join-Path $extracted $file)).Hash){throw 'ZIP differs from staged package bytes'}}
$zipHash=(Get-FileHash $zip).Hash.ToLowerInvariant()
[ordered]@{version=$version;commit=$source;tree=$tree;dirty=$false;runtimeCommit=$runtime;runtimeTree=$rtree;stage=$stage;archive=$zip;archiveSha256=$zipHash;manifestSha256=(Get-FileHash (Join-Path $stage 'manifest.json')).Hash.ToLowerInvariant();zipStageBytesMatch=$true;gameDevicesOrDeployment=$false}|ConvertTo-Json -Depth 5|Set-Content (Join-Path $OutputDirectory 'result.json') -Encoding utf8
Write-Host "Frozen delivery ZIP $zip; SHA256 $zipHash. No publication or installation."
