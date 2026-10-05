[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('woden-ci-setup-'+[guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path $fixture
$script:checks=0
function Check([bool]$value,[string]$why){if(-not $value){throw $why};$script:checks++}
function Refused([scriptblock]$action,[string]$pattern){$caught=$false;try{& $action|Out-Null}catch{if($_.Exception.Message -notmatch $pattern){throw};$caught=$true};Check $caught ('Expected refusal '+$pattern)}
function Json($path,$data){$data|ConvertTo-Json -Depth 30|Set-Content -LiteralPath $path}
. (Join-Path $root 'tools/delivery/v1/delivery-validator.ps1')
. (Join-Path $root 'tools/game/Woden-Delivery.ps1')
. (Join-Path $root 'tools/game/Delivery-Zip.ps1')
$metadata=Join-Path $fixture 'metadata';$null=New-Item -ItemType Directory -Path $metadata
'[]'|Set-Content (Join-Path $metadata 'manifest.json');'# inert metadata fixture; never execute'|Set-Content (Join-Path $metadata 'inert.ps1')
$operations=[ordered]@{};foreach($name in 'check','install','update','uninstall','rollback'){$operations[$name]=[ordered]@{support='unsupported';entrypoint=$null;arguments=@();notes='synthetic fixture only'}}
$descriptor=[ordered]@{
 schema='dbce.game-delivery';schemaVersion=1;kind='package-delivery';packageId='synthetic-ci';gameId='synthetic-ci';displayName='Inert synthetic metadata';version='0.0.0-ci';channel='candidate'
 platform=@{os='windows';architecture='x64'};repository=@{canonicalUrl='https://example.invalid/ci';visibility='private';mappingStatus='proposed';previousUrls=@()}
 provenance=@{sources=@(foreach($role in 'runtime','installer','packaging'){@{role=$role;repositoryUrl='https://example.invalid/ci';commit=('1'*40);tree=('2'*40);dirty=$false}});artifacts=@();dependencies=@()}
 integrity=@{manifestPath='manifest.json';format='synthetic-metadata-only'};loadOwners=@()
 features=@(foreach($id in 'wheel','ffb','telemetry','triple','recording','playback'){@{featureId=$id;implemented=$false;packaged=$false;acceptance=@{status='not-applicable';scope='synthetic fixture';evidence=@()};defaultState='unavailable';capabilities=@();limitations=@('inert synthetic fixture')}})
 setup=@{deliveryMode='fresh-folder';entrypoints=@(@{path='inert.ps1';purpose='primary';argumentStyle='none';startsGame=$false;opensDevices=$false});target=@{kind='new-folder';legacyRootIdentity='synthetic-ci'};operations=$operations;recovery=@{ownershipModel='none-fresh-folder';changedOrUnownedPayload='not-applicable';caughtFailure='not-applicable';interruption='not-applicable';backupVerification='not-applicable'};preservation=@{settings='byte-preserved-existing';captures='retained';otherMods='retained';sharedLoader='retained-by-default';explicitExceptions=@()}}
 compatibility=@{legacyDescriptors=@();retainedIdentities=@();settingsPolicy='preserve-existing';capturePolicy='preserve-existing';optimizerDiscovery='not-adopted'}
 recording=@{captureKinds=@();playbackKinds=@();formatIds=@();bounded=$true;completionPolicy='synthetic fixture';identityPolicy='synthetic fixture';privacy='local-private-by-default';physicalOutput='forbidden-during-offline-playback'}
}
$path=Join-Path $metadata 'delivery-manifest.json';Json $path $descriptor
$null=Assert-DeliveryManifest $metadata -MetadataOnly;Check $true 'Positive pinned metadata validator fixture'
$descriptor.features[0].implemented='false';Json $path $descriptor;Refused {Assert-DeliveryManifest $metadata -MetadataOnly} 'Expected JSON boolean';$descriptor.features[0].implemented=$false
$descriptor.features[0].featureId='ffb';Json $path $descriptor;Refused {Assert-DeliveryManifest $metadata -MetadataOnly} 'Duplicate feature';$descriptor.features[0].featureId='wheel'
$descriptor.setup.entrypoints[0].path='../outside.ps1';Json $path $descriptor;Refused {Assert-DeliveryManifest $metadata -MetadataOnly} 'Unsafe package path';$descriptor.setup.entrypoints[0].path='inert.ps1'
Json $path $descriptor;$raw=Get-Content $path -Raw;$raw.Replace('"schemaVersion": 1','"schemaVersion": 1, "schemaVersion": 1')|Set-Content $path
Refused {Assert-DeliveryManifest $metadata -MetadataOnly} 'duplicate';Json $path $descriptor
Refused {Assert-DeliveryManifest $metadata} 'Unsupported integrity adapter'

# Exercise only the real Woden ZIP inventory/extraction guard, with inert text entries.
# This is not an Assert-WodenDelivery positive package or a player release archive.
$names=@(Get-WodenDeliveryFiles)+@('manifest.json')
function Zip($name,$mode){
 $archive=Join-Path $fixture ($name+'.zip');$zip=[IO.Compression.ZipFile]::Open($archive,[IO.Compression.ZipArchiveMode]::Create)
 try{foreach($entry in $names){$actual=$entry;if($entry -eq 'README.md' -and $mode -eq 'traversal'){$actual='../outside.txt'};if($entry -eq 'GUIDE.md' -and $mode -eq 'duplicate'){$actual='README.md'};$e=$zip.CreateEntry($actual);if($entry -eq 'README.md' -and $mode -eq 'symlink'){$e.ExternalAttributes=-1610612736};$w=[IO.StreamWriter]::new($e.Open());try{$w.Write('INERT SYNTHETIC CI ENTRY; NOT A DLL OR EXECUTABLE')}finally{$w.Dispose()}}}finally{$zip.Dispose()};return $archive
}
$original=Zip 'inventory' 'valid';$expanded=Expand-WodenDeliveryZip $original (Join-Path $fixture 'inventory-extracted')
Check ((Get-ChildItem $expanded -Recurse -File).Count -eq $names.Count) 'Positive Woden inventory extraction count'
foreach($kind in 'traversal','duplicate','symlink'){$archive=Zip $kind $kind;Refused {Expand-WodenDeliveryZip $archive (Join-Path $fixture ('extract-'+$kind))} 'Unsafe|duplicate|unlisted|symbolic link';Check (-not (Test-Path (Join-Path $fixture ('extract-'+$kind)))) 'Rejected inventory created extraction directory'}
Check (-not (Test-Path (Join-Path $fixture 'outside.txt'))) 'Traversal wrote outside extraction'

# Actual setup refusal gates, never override the production game identity hash.
$game=Join-Path $fixture 'unsupported synthetic game';$null=New-Item -ItemType Directory -Path $game
$sentinel=Join-Path $game 'owner-settings-sentinel.cfg';'inert synthetic settings'|Set-Content $sentinel;$before=(Get-FileHash $sentinel).Hash
$installer=Join-Path $root 'components/wheel/tools/Manage-Install.ps1'
Refused {& $installer -GameDir $game -PackageRoot $metadata} 'Choose the game folder containing GameAssembly'
'INERT TEXT; NO GAME ASSEMBLY'|Set-Content (Join-Path $game 'GameAssembly.dll')
Refused {& $installer -GameDir $game -PackageRoot $metadata} 'Unsupported game build'
Check ((Get-FileHash $sentinel).Hash -eq $before -and (Get-ChildItem $game -Force).Count -eq 2) 'Setup refusal mutated synthetic settings or created installation/backup'
Write-Host "PASS $script:checks asset-free metadata, ZIP inventory and setup-refusal checks; not full package/install/rollback coverage."
