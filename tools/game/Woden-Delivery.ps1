# Game-specific adapter; pinned common validator/parser are never edited.
function Get-WodenDeliveryFiles {
 $prefix='BepInEx/plugins/WodenRallyEdgeWheel/'
 $roots=@('LICENSE','README.md','GUIDE.md','SPEC.md','ACCEPTANCE-FIXTURES.md','Install.bat','Uninstall.bat','Manage-Install.ps1','game-release.json','input-override.json','delivery-manifest.json','package-provenance.json','Verify-Package.ps1','Woden-Delivery.ps1','delivery-validator.ps1','delivery-parser.cs','delivery-pin.json')
 return $roots+@('WodenRallyEdgeWheel.dll','WodenRallyEdge.Core.dll','Dbce.Wheel.Telemetry.dll','Dbce.Wheel.Recording.dll','Dbce.Wheel.Ffb.dll','WheelFfb.dll','recording-provenance.json','toolkit.version','telemetry-schema.json' | ForEach-Object {$prefix+$_})
}
function Assert-WodenDelivery {
 [CmdletBinding()]param([Parameter(Mandatory)][string]$Root)
 $ErrorActionPreference='Stop';$Root=[IO.Path]::GetFullPath($Root).TrimEnd('\','/')
 function Same($a,$b,$why){if(@(Compare-Object (@($a)|Sort-Object) (@($b)|Sort-Object)).Count){throw $why}}
 function File([string]$path){
  if($path -match '[\\:]|^/' -or @($path.Split('/')|Where-Object {$_ -in @('','.','..')}).Count){throw 'Unsafe Woden package path'}
  $full=[IO.Path]::GetFullPath((Join-Path $Root $path));if(-not $full.StartsWith($Root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Escaping package path'}
  $walk=$full;while($walk.Length -ge $Root.Length){if((Test-Path -LiteralPath $walk) -and ((Get-Item $walk -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Linked package path'};$walk=Split-Path $walk -Parent}
  if(-not (Test-Path $full -PathType Leaf)){throw "Missing package file $path"};return $full
 }
 $common=@{'delivery-validator.ps1'='892fe8f539d9bad52ee5a2bb5e981ca3d737bb90814be61291380714df4acae1';'delivery-parser.cs'='cb66ef4470d409b12e03ce922aa08d8b6691bf589483d71acbe11d05fe8259a0';'SPEC.md'='fdf570d896e4c1dc1988673a14c840abf771fd67ad5dca83fb51f41b5492a71c';'ACCEPTANCE-FIXTURES.md'='dfc2a266e9a545a38610c1a406185506f5ff58e4b9201ba4426719dd53b4acc5'}
 foreach($name in $common.Keys){if((Get-FileHash (File $name)).Hash.ToLowerInvariant() -cne $common[$name]){throw 'Generic delivery code pin mismatch'}}
 . (File 'delivery-validator.ps1')
 $d=Assert-DeliveryManifest -PackageRoot $Root -MetadataOnly
 function Json([string]$path){$raw=[IO.File]::ReadAllText((File $path));[DbceDeliveryJsonV1]::Check($raw);return ($raw|ConvertFrom-Json)}
 function NoPrivate($o){if($o -is [string]){if($o -match '(?i)(?<![a-z0-9])[a-z]:[\\/]|^\\\\'){throw 'Private absolute owner path in metadata'}}elseif($o -is [pscustomobject]){foreach($f in $o.PSObject.Properties){NoPrivate $f.Value}}elseif($o -is [array]){foreach($v in $o){NoPrivate $v}}}
 NoPrivate $d
 $pin=Json 'delivery-pin.json';if($pin.version -ne '1.0.0' -or $pin.acceptedPilotCommit -ne '41d0378ab6c3fdcfe19ddd601f1c4f51c4f81849'){throw 'Contract version pin conflict'}
 foreach($name in $common.Keys){if($pin.files.$name -cne $common[$name]){throw 'Contract byte pin conflict'}}
 $expected=@(Get-WodenDeliveryFiles);$inventory=@(Json 'manifest.json')
 Same @($inventory.path) $expected 'Inventory allowlist differs'
 if($inventory.Count -ne $expected.Count -or @($inventory.path|Select-Object -Unique).Count -ne $expected.Count){throw 'Duplicate/incomplete inventory'}
 $hashes=@{};foreach($entry in $inventory){Same @($entry.PSObject.Properties.Name) @('path','sha256') 'Unknown integrity entry field';if($entry.sha256 -cnotmatch '^[a-f0-9]{64}$' -or (Get-FileHash (File $entry.path)).Hash.ToLowerInvariant() -cne $entry.sha256){throw "Integrity mismatch $($entry.path)"};$hashes[$entry.path]=$entry.sha256}
 $actual=@(Get-ChildItem $Root -Force -Recurse -File | ForEach-Object {$_.FullName.Substring($Root.Length+1).Replace('\','/')})
 Same $actual ($expected+@('manifest.json')) 'Uninventoried package bytes'
 foreach($item in Get-ChildItem $Root -Force -Recurse){if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked package entry'}}
 Same @($d.provenance.artifacts.path) @($expected|Where-Object {$_ -match '\.(dll|exe)$'}) 'Binary provenance incomplete'
 foreach($a in $d.provenance.artifacts){if($a.sha256 -cne $hashes[$a.path]){throw 'Artifact integrity conflict'}}
 foreach($dep in $d.provenance.dependencies){foreach($f in $dep.files){if($f.path -notin $expected -or $f.sha256 -cne $hashes[$f.path]){throw 'Dependency integrity conflict'}}}
 if($d.packageId -cne 'dbce-mods-super-woden-rally-edge' -or $d.gameId -cne 'super-woden-rally-edge' -or $d.version -cne '0.2.13-delivery.1' -or $d.channel -ne 'candidate' -or $d.platform.architecture -ne 'x64' -or $d.integrity.format -cne 'dbce.woden-package.sha256-array.v1' -or $d.integrity.manifestPath -cne 'manifest.json'){throw 'Delivery identity conflict'}
 if($d.repository.canonicalUrl -cne 'https://github.com/d-b-c-e/dbce-mods-woden-rally-edge' -or $d.repository.visibility -ne 'private' -or $d.repository.mappingStatus -ne 'proposed' -or ($d.repository.previousUrls -join '|') -cne 'https://github.com/d-b-c-e/woden-rally-edge-wheel'){throw 'Repository mapping conflict'}
 $x=$d.extensions.'dbce.woden';$mapping=$x.packageRepositoryMapping
 if($mapping.packageId -ne $d.packageId -or $mapping.repositoryName -ne 'dbce-mods-woden-rally-edge' -or $mapping.status -ne 'approved-name-not-renamed' -or $x.contractVersion -ne '1.0.0'){throw 'Explicit package/repository mapping lost'}
 $engine=@{gameInputReplay='unsupported';devicePlayback='unsupported';offlineAnalysis='source-only';triplePresentation='unavailable';cadenceImpactAdoption='not-adopted'}
 Same @($x.engineOperations.PSObject.Properties.Name) @($engine.Keys) 'Engine operation shape conflict'
 foreach($key in $engine.Keys){if($x.engineOperations.$key -cne $engine[$key]){throw 'Unsupported engine operation claim'}}
 if($d.loadOwners.Count -ne 1 -or $d.loadOwners[0].ownerId -ne 'dbce.wodenrallyedgewheel' -or $d.loadOwners[0].role -ne 'mod-loader'){throw 'Load owner conflict'}
 $caps=@{wheel=@('calibrated-wheel-input','shared-button-bindings','pov-hat-input');ffb=@('bounded-directinput-force-requests');telemetry=@('forza-udp','detailed-json-udp');triple=@();recording=@('request-driven-diagnostic-capture');playback=@('offline-force-analysis')}
 foreach($f in $d.features){$shipping=$f.featureId -in @('wheel','ffb','telemetry','recording');if($f.implemented -ne ($f.featureId -ne 'triple') -or $f.packaged -ne $shipping -or ($f.capabilities -join '|') -cne ($caps[$f.featureId] -join '|')){throw 'Unsupported feature/capability claim'}
  if($shipping){if($f.defaultState -ne 'preserve-existing' -or $f.freshInstallDefault -ne $(if($f.featureId -in @('ffb','telemetry')){'on'}else{'off'}) -or $f.acceptance.status -ne 'pending' -or $f.acceptance.evidence.Count){throw 'Candidate acceptance/default promotion'}}
  elseif($f.defaultState -ne 'unavailable' -or $f.acceptance.status -ne 'not-applicable'){throw 'Unshipped feature promoted'}
 }
 if(($d.recording.captureKinds -join '|') -cne 'input|telemetry|configuration|state|force-requests' -or $d.recording.playbackKinds.Count -or -not $d.recording.bounded -or $d.recording.formatIds.Count -ne 1 -or $d.recording.formatIds[0].id -cne 'dbce.wheel.session' -or $d.recording.formatIds[0].version -cne '1' -or $d.recording.formatIds[0].adapterId -ne 'dbce.wodenrallyedgewheel'){throw 'Recording/analysis semantics conflict'}
 if($d.setup.deliveryMode -ne 'transactional-install' -or $d.setup.target.kind -ne 'game-directory' -or $d.setup.recovery.ownershipModel -ne 'receipt-hashes' -or $d.setup.recovery.changedOrUnownedPayload -ne 'refuse' -or $d.setup.recovery.caughtFailure -ne 'automatic-verified-rollback' -or $d.setup.recovery.interruption -ne 'manual-backup-recovery' -or $d.setup.recovery.backupVerification -ne 'hashes'){throw 'Installer safety declaration conflict'}
 Same @($d.setup.entrypoints.path) @('Install.bat','Uninstall.bat','Verify-Package.ps1') 'Invented entrypoint'
 foreach($e in $d.setup.entrypoints){if($e.startsGame -or $e.opensDevices){throw 'Setup side effect conflict'}}
 foreach($name in 'check','rollback'){$op=$d.setup.operations.$name;if($op.support -ne 'manual' -or $null -ne $op.entrypoint -or $op.arguments.Count){throw 'Invented check/rollback CLI'}}
 foreach($name in 'install','update','uninstall'){$op=$d.setup.operations.$name;if($op.support -ne 'automated' -or $op.entrypoint -ne $(if($name -eq 'uninstall'){'Uninstall.bat'}else{'Install.bat'}) -or ($op.arguments -join '|') -cne '-GameDir|<game-directory>'){throw 'Invented install operation'}}
 Same @($d.compatibility.retainedIdentities|ForEach-Object {$_.kind+'|'+$_.value}) @('loader|dbce.wodenrallyedgewheel','adapter|dbce.wodenrallyedgewheel','config|BepInEx/config/dbce.wodenrallyedgewheel.cfg','config|BepInEx/config/wheel-bindings.json','receipt|BepInEx/WodenWheel-install.json') 'Legacy identities changed'
 $release=Json 'game-release.json';$p=Json 'package-provenance.json'
 NoPrivate $release;NoPrivate $p;NoPrivate $pin
 Same @($p.PSObject.Properties.Name) @('schema','runtimeVersion','baselineManifestSha256','sources') 'Unknown package provenance field'
 if($release.installedDiscovery -isnot [bool] -or $release.sourceDirty -isnot [bool]){throw 'Legacy descriptor boolean type conflict'}
 if($release.packageId -cne $d.packageId -or $release.gameId -cne $d.gameId -or $release.version -cne $d.version -or $release.installedDiscovery -ne $false -or $release.runtimeVersion -cne $p.runtimeVersion){throw 'Legacy descriptor identity conflict'}
 $lt=@($release.features|Where-Object featureId -eq 'triple');if($lt.Count -ne 1 -or $lt[0].available -isnot [bool] -or $lt[0].available -ne $false -or $lt[0].capabilities -isnot [array] -or $lt[0].capabilities.Count -or $lt[0].reason -isnot [string]){throw 'Legacy triple promoted or wrong type'}
 Same @($lt[0].PSObject.Properties.Name) @('available','reason','featureId','capabilities') 'Legacy triple communication advertised'
 Same @($release.features.featureId) @('wheel-input','ffb','telemetry','triple') 'Legacy feature mapping conflict'
 foreach($id in 'wheel-input','ffb','telemetry'){$f=@($release.features|Where-Object featureId -eq $id);if($f.Count -ne 1 -or $f[0].available -isnot [bool] -or $f[0].rigVerified -isnot [bool] -or $f[0].available -ne $true -or $f[0].rigVerified -ne $false -or $f[0].adapterId -ne 'dbce.wodenrallyedgewheel'){throw 'Legacy available/acceptance conflict'}}
 $runtime='14740fdcba4a7e9c490a7e26d94f54614c75e5f7';$tree='461155fc74da937288ba09d42aae8fdeff11190b';$rv='0.2.13+'+$runtime
 if($p.schema -ne 'dbce.woden.package-provenance.v1' -or $p.baselineManifestSha256 -cne '20e9c9beed5458f81c183e582a34067535a946c6a948a96d91817db013741a23' -or $p.runtimeVersion -cne $rv -or $release.sourceRevision -cne $runtime -or $release.sourceTree -cne $tree -or $release.sourceDirty -ne $false -or $x.retainedRuntimeVersion -cne $rv -or $x.retainedBaselineManifestSha256 -cne $p.baselineManifestSha256){throw 'Retained runtime provenance conflict'}
 if((Get-Item (File 'BepInEx/plugins/WodenRallyEdgeWheel/WodenRallyEdgeWheel.dll')).VersionInfo.ProductVersion -cne $rv){throw 'Retained plugin version conflict'}
 if($p.sources.Count -ne 3 -or $d.provenance.sources.Count -ne 3){throw 'Unexpected source roles'}
 foreach($s in $d.provenance.sources){$a=@($p.sources|Where-Object role -eq $s.role);if($a.Count -ne 1 -or $s.commit -cne $a[0].commit -or $s.tree -cne $a[0].tree -or $s.dirty -ne $a[0].dirty -or $s.repositoryUrl -cne $a[0].repositoryUrl -or $s.dirty){throw 'Delivery/package source anchor conflict'};if($s.role -in @('runtime','installer') -and ($s.commit -cne $runtime -or $s.tree -cne $tree)){throw 'Retained source role relabeled'}}
 $pinned=@{'WodenRallyEdgeWheel.dll'='2909680deddeb3776cd2844b289123fbf5c3b09851d083f48c3d0e7d66540bb6';'WodenRallyEdge.Core.dll'='5b09fec968b6fc03c118a0c01a470880dca7ad8a11157d9811958e2349117815';'Dbce.Wheel.Telemetry.dll'='765f5c77814f200b8e2407d141e352e0c9225dcbf2f92fd3f64e69b79c7436e2';'Dbce.Wheel.Ffb.dll'='f5217f81b5bd305e0efca3aacc459b9066c4366cd220c3e4299c511e23b5431a';'WheelFfb.dll'='a5ed124dd49b323e99821e26155c0f2b52901510ed894e4c8abe3c101cd84782';'Dbce.Wheel.Recording.dll'='af0c081721e817f3e7e22c446d4c24589277d28ba8024f3b9a1c3daa9733fb12'}
 $roles=@{'WodenRallyEdgeWheel.dll'='runtime';'WodenRallyEdge.Core.dll'='runtime';'Dbce.Wheel.Telemetry.dll'='toolkit-base';'Dbce.Wheel.Ffb.dll'='toolkit-input-override';'WheelFfb.dll'='toolkit-input-override';'Dbce.Wheel.Recording.dll'='recording-extension'}
 foreach($a in $d.provenance.artifacts){$name=[IO.Path]::GetFileName($a.path);$role=$roles[$name];if($a.sha256 -cne $pinned[$name] -or ($a.sourceRoles -join '|') -cne $role -or $a.origin -cne $(if($role -eq 'runtime'){'retained-binary'}else{'upstream-binary'})){throw 'Retained artifact relabeled or modified'}}
 if($hashes['Manage-Install.ps1'] -cne 'a4fb6527f9b87b6fb75952203b4fea36f8635e70f726a0e686fb453d62fb70fe'){throw 'Reviewed installer protections changed'}
 Same @($d.provenance.dependencies.dependencyId) @('toolkit-base','toolkit-input-override','recording-extension') 'Dependency classifications changed'
 foreach($dep in $d.provenance.dependencies){if($dep.distribution -ne 'included'){throw 'Dependency distribution changed'};switch($dep.dependencyId){
 'toolkit-base' {if($dep.status -ne 'released' -or $dep.version -ne '0.12.0' -or $dep.sourceCommit -ne '6f9c662e330af1ab790ca291a799265e0a50ef8a' -or $dep.files.Count -ne 1){throw 'Released toolkit identity conflict'}}
 'toolkit-input-override' {if($dep.status -ne 'unpublished-override' -or $dep.sourceCommit -ne '9ad1f64044b57fa05c119697518a47f2a86ba0de' -or $dep.overrideOf -ne 'toolkit-base' -or $dep.files.Count -ne 2){throw 'Input override conflict'}}
 'recording-extension' {if($dep.status -ne 'unpublished-override' -or $null -ne $dep.sourceCommit -or $dep.overrideOf -ne 'toolkit-base' -or $dep.files.Count -ne 1){throw 'Recording extension falsely promoted'}}
 }}
 if($hashes['BepInEx/plugins/WodenRallyEdgeWheel/recording-provenance.json'] -cne '48df059f5c84866f0ae1126c2d9e2817c3f67cd240b47131c5b779e6f4988a10' -or $hashes['input-override.json'] -cne '3dedd6a00c3f66f6cc00d85ce41ad8aa7754537649be14f2da57c2e479fd6a1f'){throw 'Retained dependency source catalog changed'}
 $rec=Json 'BepInEx/plugins/WodenRallyEdgeWheel/recording-provenance.json';if(-not $rec.dirty -or $rec.dllSha256 -ne $pinned['Dbce.Wheel.Recording.dll'] -or $rec.sources.Count -ne 8){throw 'Recording source catalog changed'}
 $override=Json 'input-override.json';if($override.sourceRevision -ne '9ad1f64044b57fa05c119697518a47f2a86ba0de' -or $override.inputCapabilities -ne 1){throw 'Input provenance changed'}
 foreach($doc in 'README.md','GUIDE.md'){$text=[IO.File]::ReadAllText((File $doc));foreach($match in [regex]::Matches($text,'\]\(([^)]+)\)')){$link=$match.Groups[1].Value;if($link -notmatch '^https://|^#'){$null=File $link}}}
 return $d
}
