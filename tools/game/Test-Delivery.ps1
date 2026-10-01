[CmdletBinding()]
param([Parameter(Mandatory)][string]$Archive,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Woden-Delivery.ps1')
. (Join-Path $PSScriptRoot 'Delivery-Zip.ps1')
if(Test-Path $OutputDirectory){throw 'Use new fixture directory'}
$null=New-Item -ItemType Directory -Path $OutputDirectory
$base=Expand-WodenDeliveryZip -Archive $Archive -Destination (Join-Path $OutputDirectory 'extracted-original')
$null=Assert-WodenDelivery -Root $base
$script:checks=1;$script:cases=@()
function Check([bool]$condition,[string]$why){if(-not $condition){throw $why};$script:checks++}
function WriteJson($file,$value){$value|ConvertTo-Json -Depth 30|Set-Content -LiteralPath $file -Encoding utf8}
function Rehash($dir){$m=@(Get-ChildItem $dir -File -Recurse|Where-Object Name -ne 'manifest.json'|Sort-Object FullName|ForEach-Object {[ordered]@{path=$_.FullName.Substring($dir.Length+1).Replace('\','/');sha256=(Get-FileHash $_.FullName).Hash.ToLowerInvariant()}});WriteJson (Join-Path $dir 'manifest.json') $m}
function Negative($name,[scriptblock]$mutate){
 $dir=Join-Path $OutputDirectory ('mutation-'+$name);Copy-Item $base $dir -Recurse
 $path=Join-Path $dir 'delivery-manifest.json';$d=Get-Content $path -Raw|ConvertFrom-Json
 & $mutate $dir $d;WriteJson $path $d;Rehash $dir
 $zip=Join-Path $OutputDirectory ($name+'.zip');[IO.Compression.ZipFile]::CreateFromDirectory($dir,$zip)
 $refused=$false;$reason='';try{$extracted=Expand-WodenDeliveryZip $zip (Join-Path $OutputDirectory ('extracted-'+$name));$null=Assert-WodenDelivery $extracted}catch{$refused=$true;$reason=$_.Exception.Message}
 Check $refused ('Accepted negative ZIP '+$name);$script:cases += [ordered]@{name=$name;refused=$refused;reason=$reason;zipSha256=(Get-FileHash $zip).Hash.ToLowerInvariant()}
}
Negative 'unknown-field' {param($r,$d)$d|Add-Member -NotePropertyName unexpected -NotePropertyValue $true}
Negative 'wrong-boolean' {param($r,$d)$d.features[0].implemented='true'}
Negative 'unsupported-schema' {param($r,$d)$d.schemaVersion=2}
Negative 'duplicate-feature' {param($r,$d)$d.features[1].featureId='wheel'}
Negative 'nm-claim' {param($r,$d)($d.features|Where-Object featureId -eq 'ffb').capabilities=@('physical-nm-calibrated')}
Negative 'triple-promotion' {param($r,$d)$f=$d.features|Where-Object featureId -eq 'triple';$f.implemented=$true;$f.packaged=$true;$f.defaultState='on';$f.acceptance.status='pending';$f.capabilities=@('asymmetric-frustum')}
Negative 'gameplay-replay' {param($r,$d)$d.recording.playbackKinds=@('game-input-replay')}
Negative 'device-playback' {param($r,$d)$d.extensions.'dbce.woden'.engineOperations.devicePlayback='supported'}
Negative 'candidate-accepted' {param($r,$d)$d.features[0].acceptance.status='accepted';$d.features[0].acceptance.evidence=@([ordered]@{reference='review:invented';identity='invented';scope='invented'})}
Negative 'tree-mismatch' {param($r,$d)$d.provenance.sources[0].tree=('0'*40)}
Negative 'packaging-commit-mismatch' {param($r,$d)$d.provenance.sources[2].commit=('0'*40)}
Negative 'fresh-runtime-claim' {param($r,$d)($d.provenance.artifacts|Where-Object sourceRoles -contains 'runtime')[0].origin='fresh-build'}
Negative 'invented-dry-run' {param($r,$d)$d.setup.operations.check.support='automated';$d.setup.operations.check.entrypoint='Install.bat';$d.setup.operations.check.arguments=@('-DryRun')}
Negative 'rename-claim' {param($r,$d)$d.repository.mappingStatus='verified'}
Negative 'copied-art-id' {param($r,$d)$d.packageId='dbce-mods-art-of-rally'}
Negative 'unknown-private-path' {param($r,$d)$d.extensions.'dbce.woden'|Add-Member -NotePropertyName ownerPath -NotePropertyValue 'C:/Users/owner/private-capture'}
Negative 'recording-released-claim' {param($r,$d)($d.provenance.dependencies|Where-Object dependencyId -eq 'recording-extension').status='released'}
Negative 'cadence-adoption' {param($r,$d)$d.extensions.'dbce.woden'.engineOperations.cadenceImpactAdoption='adopted'}
Negative 'legacy-triple-communication' {param($r,$d)$p=Join-Path $r 'game-release.json';$l=Get-Content $p -Raw|ConvertFrom-Json;($l.features|Where-Object featureId -eq 'triple')|Add-Member -NotePropertyName communication -NotePropertyValue ([ordered]@{requestPath='desired.json';statusPath='status.json'});WriteJson $p $l}
Negative 'runtime-recatalogned' {param($r,$d)$p=Join-Path $r 'BepInEx/plugins/WodenRallyEdgeWheel/WodenRallyEdge.Core.dll';[IO.File]::AppendAllText($p,'unknown');($d.provenance.artifacts|Where-Object path -like '*WodenRallyEdge.Core.dll').sha256=(Get-FileHash $p).Hash.ToLowerInvariant()}
Negative 'installer-modified' {param($r,$d)[IO.File]::AppendAllText((Join-Path $r 'Manage-Install.ps1'),"`n# unknown installer revision")}
Negative 'broken-local-doc-link' {param($r,$d)[IO.File]::AppendAllText((Join-Path $r 'README.md'),"`n[absent source](../components/wheel/README.md)")}
Negative 'circular-descriptor-artifact' {param($r,$d)$d.provenance.artifacts[0].path='delivery-manifest.json'}
# Duplicate-key grammar must be tested on raw JSON, not a parser that already discarded duplicates.
$dup=Join-Path $OutputDirectory 'duplicate-json';Copy-Item $base $dup -Recurse
$raw=Get-Content (Join-Path $dup 'delivery-manifest.json') -Raw
$raw=$raw.Replace('"schema": "dbce.game-delivery"','"schema": "dbce.game-delivery", "schema": "dbce.game-delivery"')
$raw|Set-Content (Join-Path $dup 'delivery-manifest.json') -Encoding utf8;Rehash $dup
$zip=Join-Path $OutputDirectory 'duplicate-json.zip';[IO.Compression.ZipFile]::CreateFromDirectory($dup,$zip)
$caught=$false;try{$x=Expand-WodenDeliveryZip $zip (Join-Path $OutputDirectory 'extracted-duplicate-json');$null=Assert-WodenDelivery $x}catch{$caught=$true};Check $caught 'Duplicate JSON accepted'
# ZIP-entry traversal and duplicate paths are refused before extraction.
foreach($kind in 'traversal','duplicate','symlink','missing-delivery'){
 $zip=Join-Path $OutputDirectory ($kind+'.zip');$source=[IO.Compression.ZipFile]::OpenRead($Archive);$target=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
 try{foreach($entry in $source.Entries){if($kind -eq 'missing-delivery' -and $entry.FullName -eq 'delivery-manifest.json'){continue};$name=if($kind -eq 'traversal' -and $entry.FullName -eq 'README.md'){'../outside.txt'}elseif($kind -eq 'duplicate' -and $entry.FullName -eq 'GUIDE.md'){'README.md'}else{$entry.FullName};$e=$target.CreateEntry($name);if($kind -eq 'symlink' -and $name -eq 'README.md'){$e.ExternalAttributes=-1610612736};$i=$entry.Open();$o=$e.Open();try{$i.CopyTo($o)}finally{$i.Dispose();$o.Dispose()}}}finally{$target.Dispose();$source.Dispose()}
 $caught=$false;try{$x=Expand-WodenDeliveryZip $zip (Join-Path $OutputDirectory ('extracted-'+$kind));$null=Assert-WodenDelivery $x}catch{$caught=$true};Check $caught ('ZIP '+$kind+' accepted')
}
Check (-not (Test-Path (Join-Path $OutputDirectory 'outside.txt'))) 'ZIP traversal wrote outside extraction'
$null=Assert-WodenDelivery $base;Check $true 'Original ZIP extracted package still valid'
[ordered]@{checks=$script:checks;mutationCases=$script:cases;originalArchiveSha256=(Get-FileHash $Archive).Hash.ToLowerInvariant();extractedOriginal=$base;gameDevicesOrInstall=$false}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $OutputDirectory 'result.json') -Encoding utf8
Write-Host "PASS $script:checks real-ZIP delivery checks; no game, devices or target install. Original extraction: $base"
