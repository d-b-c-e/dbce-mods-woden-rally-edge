[CmdletBinding()]
param([Parameter(Mandatory)][string]$BaselineStage,[Parameter(Mandatory)][string]$NativeRoot,[Parameter(Mandatory)][string]$NativeProvenance,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
. (Join-Path $PSScriptRoot 'Woden-Delivery.ps1')
$null=Assert-WodenDelivery $BaselineStage
if(Test-Path $OutputDirectory){throw 'Preserve immutable output; use new directory'}
$null=New-Item -ItemType Directory $OutputDirectory
$prep=Join-Path $OutputDirectory 'preparation.json'
& (Join-Path $PSScriptRoot 'Prepare-ShutdownPackage.ps1') -BaselineStage $BaselineStage -NativeRoot $NativeRoot -NativeProvenance $NativeProvenance -OutputPath $prep
$i=Get-Content $prep -Raw|ConvertFrom-Json
$stage=Join-Path $OutputDirectory 'stage-0.2.14-shutdown.1';Copy-Item $BaselineStage $stage -Recurse
$bin=Join-Path $root 'components/wheel/src/WodenRallyEdge.Plugin/bin/Release/net6.0'
foreach($a in $i.preparedPayload){$file=if($a.path -like '*WheelFfb.dll'){Join-Path $NativeRoot 'native/wheelffb/build/WheelFfb.dll'}else{Join-Path $bin ([IO.Path]::GetFileName($a.path))};Copy-Item $file (Join-Path $stage $a.path) -Force}
$native=Get-Content $NativeProvenance -Raw|ConvertFrom-Json
$native.compilerToolchain='MSVC 19.44.35229; VCTools 14.44.35207; SDK 10.0.26100.0; x64 and x86. Full diagnostic paths retained in private external evidence.'
$native|ConvertTo-Json -Depth 20|Set-Content (Join-Path $stage 'native-build-provenance.json') -Encoding utf8
Copy-Item (Join-Path $PSScriptRoot 'Woden-ShutdownDelivery.ps1') (Join-Path $stage 'Woden-Delivery.ps1') -Force
$p=Get-Content (Join-Path $stage 'package-provenance.json') -Raw|ConvertFrom-Json
$tree=(& git -c "safe.directory=$root" -C $root rev-parse ($i.consumerCommit+'^{tree}')).Trim()
$p.runtimeVersion='0.2.14+'+$i.consumerCommit;$p.baselineManifestSha256=$i.baselineManifestSha256
$p.sources[0].commit=$i.consumerCommit;$p.sources[0].tree=$tree
$pack=(& git -c "safe.directory=$root" -C $root rev-parse HEAD).Trim();$ptree=(& git -c "safe.directory=$root" -C $root rev-parse 'HEAD^{tree}').Trim()
$p.sources[2].commit=$pack;$p.sources[2].tree=$ptree
$p|ConvertTo-Json -Depth 20|Set-Content (Join-Path $stage 'package-provenance.json') -Encoding utf8
$release=Get-Content (Join-Path $stage 'game-release.json') -Raw|ConvertFrom-Json
$release.version='0.2.14-shutdown.1';$release.runtimeVersion=$p.runtimeVersion;$release.sourceRevision=$i.consumerCommit;$release.sourceTree=$tree
$release|ConvertTo-Json -Depth 20|Set-Content (Join-Path $stage 'game-release.json') -Encoding utf8
$d=Get-Content (Join-Path $stage 'delivery-manifest.json') -Raw|ConvertFrom-Json
$d.version=$release.version;$d.provenance.sources=$p.sources
foreach($a in $d.provenance.artifacts){$new=@($i.preparedPayload|Where-Object path -eq $a.path);if($new.Count){$a.sha256=$new[0].sha256;$a.origin='fresh-build';if($a.path -like '*WheelFfb.dll'){$a.sourceRoles=@('toolkit-native-shutdown')}}}
$input=$d.provenance.dependencies|Where-Object dependencyId -eq 'toolkit-input-override'
$input.files=@($input.files|Where-Object path -notlike '*WheelFfb.dll');$input.version='0.12.0-input-9ad1f640-managed';$input|Add-Member -NotePropertyName reason -NotePropertyValue 'Managed binary retained. input-override.json remains the historical build catalog; its native 501 entry is superseded by the explicit native shutdown dependency.' -Force
$d.provenance.dependencies+= [pscustomobject]@{dependencyId='toolkit-native-shutdown';version='0.5.2-hat39-a51bed9';status='unpublished-override';sourceCommit=$native.sourceCommit;overrideOf='toolkit-base';reason='Fresh isolated HAT39 shutdown build; review and rig acceptance pending. Full source/header/DLL pinning in native-build-provenance.json.';distribution='included';files=@([pscustomobject]@{path='BepInEx/plugins/WodenRallyEdgeWheel/WheelFfb.dll';sha256=$i.preparedPayload[2].sha256})}
foreach($f in $d.features){if($f.packaged){$f.acceptance.scope='Exact 0.2.14-shutdown.1 candidate: independent reviews and attended runtime/rig acceptance pending.'}}
$d.compatibility.notes[1]='Runtime/plugin and receipt use 0.2.14+'+$i.consumerCommit+'; native 502 is a fresh unpublished shutdown backport, separate from retained managed HAT input binaries.'
$x=$d.extensions.'dbce.woden';$x.retainedRuntimeVersion=$p.runtimeVersion;$x.retainedBaselineManifestSha256=$p.baselineManifestSha256;$x.provenanceMeaning='Fresh runtime/native builds with exact source and artifact catalogs; retained installer and managed dependencies. Review pending; no physical acceptance.'
$d|ConvertTo-Json -Depth 30|Set-Content (Join-Path $stage 'delivery-manifest.json') -Encoding utf8
foreach($name in 'README.md','GUIDE.md'){$doc=Get-Content (Join-Path $stage $name) -Raw;$doc=$doc.Replace('0.2.13-delivery.1','0.2.14-shutdown.1').Replace('0.2.13+14740fd','0.2.14+e3d118e');$doc+="`nShutdown candidate: fresh consumer e3d118e and shared native a51bed9 (502, HAT39). Review and attended acceptance pending. Existing settings are preserved; no cadence/collision activation or force calibration claim.`n";$doc|Set-Content (Join-Path $stage $name) -Encoding utf8}
$manifest=@(Get-ChildItem $stage -File -Recurse|Where-Object Name -ne 'manifest.json'|Sort-Object FullName|ForEach-Object {[ordered]@{path=[IO.Path]::GetRelativePath($stage,$_.FullName).Replace('\','/');sha256=(Get-FileHash $_.FullName).Hash.ToLowerInvariant()}})
$manifest|ConvertTo-Json -Depth 5|Set-Content (Join-Path $stage 'manifest.json') -Encoding utf8
. (Join-Path $PSScriptRoot 'Woden-ShutdownDelivery.ps1');$null=Assert-WodenDelivery $stage
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=Join-Path $OutputDirectory 'dbce-mods-super-woden-rally-edge-0.2.14-shutdown.1.zip';[IO.Compression.ZipFile]::CreateFromDirectory([IO.Path]::GetFullPath($stage),[IO.Path]::GetFullPath($zip))
. (Join-Path $PSScriptRoot 'Delivery-Zip.ps1');$extracted=Expand-WodenDeliveryZip $zip (Join-Path $OutputDirectory 'verified-extraction');$null=Assert-WodenDelivery $extracted
foreach($f in @(Get-WodenDeliveryFiles)+@('manifest.json')){if((Get-FileHash (Join-Path $stage $f)).Hash -ne (Get-FileHash (Join-Path $extracted $f)).Hash){throw 'ZIP bytes differ'}}
[ordered]@{archiveSha256=(Get-FileHash $zip).Hash.ToLowerInvariant();archive=$zip;consumerCommit=$i.consumerCommit;nativeCommit=$i.nativeCommit;packagingCommit=$pack;stageZipBytesMatch=$true;published=$false;installedIntoGame=$false;devicesUsed=$false;reviewPending=$true}|ConvertTo-Json|Set-Content (Join-Path $OutputDirectory 'result.json') -Encoding utf8
Write-Host "Frozen isolated shutdown ZIP: $zip"
