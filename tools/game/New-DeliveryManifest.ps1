[CmdletBinding()]
param([Parameter(Mandatory)][string]$PackageRoot,[Parameter(Mandatory)][string]$Version,[Parameter(Mandatory)]$Provenance)
$ErrorActionPreference='Stop'
$prefix='BepInEx/plugins/WodenRallyEdgeWheel/'
$base='6f9c662e330af1ab790ca291a799265e0a50ef8a'
$hashes=@{}
Get-ChildItem -LiteralPath (Join-Path $PackageRoot $prefix) -File | ForEach-Object {$hashes[$_.Name]=(Get-FileHash $_.FullName).Hash.ToLowerInvariant()}
$artifactRoles=@{
 'WodenRallyEdgeWheel.dll'='runtime';'WodenRallyEdge.Core.dll'='runtime'
 'Dbce.Wheel.Telemetry.dll'='toolkit-base';'Dbce.Wheel.Ffb.dll'='toolkit-input-override'
 'WheelFfb.dll'='toolkit-input-override';'Dbce.Wheel.Recording.dll'='recording-extension'
}
$artifacts=@();foreach($name in @($artifactRoles.Keys | Sort-Object)) {
 $role=$artifactRoles[$name]
 $artifacts += [ordered]@{path=$prefix+$name;sha256=$hashes[$name];origin=$(if($role -eq 'runtime'){'retained-binary'}else{'upstream-binary'});sourceRoles=@($role)}
}
function DepFiles([string[]]$names){@($names | ForEach-Object {[ordered]@{path=$prefix+$_;sha256=$hashes[$_]}})}
$dependencies=@(
 [ordered]@{dependencyId='toolkit-base';version='0.12.0';status='released';sourceCommit=$base;overrideOf=$null;reason='Base release is separate from input and recording overrides; only this telemetry binary is unchanged released output.';distribution='included';files=@(DepFiles @('Dbce.Wheel.Telemetry.dll'))},
 [ordered]@{dependencyId='toolkit-input-override';version='0.12.0-input-9ad1f640-native-0.5.1';status='unpublished-override';sourceCommit='9ad1f64044b57fa05c119697518a47f2a86ba0de';overrideOf='toolkit-base';distribution='included';files=@(DepFiles @('Dbce.Wheel.Ffb.dll','WheelFfb.dll'))},
 [ordered]@{dependencyId='recording-extension';version='unpublished-pinned-source-catalog';status='unpublished-override';sourceCommit=$null;overrideOf='toolkit-base';reason='Retained reviewed Recording DLL: original dirty source catalog and exact artifact hash are in recording-provenance.json. BaseRevision is not an exact clean build commit. No clean source commit or released-toolkit membership is invented.';distribution='included';files=@(DepFiles @('Dbce.Wheel.Recording.dll'))}
)
$caps=@{wheel=@('calibrated-wheel-input','shared-button-bindings','pov-hat-input');ffb=@('bounded-directinput-force-requests');telemetry=@('forza-udp','detailed-json-udp');triple=@();recording=@('request-driven-diagnostic-capture');playback=@('offline-force-analysis')}
$features=@();foreach($id in 'wheel','ffb','telemetry','triple','recording','playback') {
 $shipped=$id -in @('wheel','ffb','telemetry','recording');$implemented=$id -ne 'triple'
 $f=[ordered]@{featureId=$id;implemented=$implemented;packaged=$shipped;acceptance=[ordered]@{status=$(if($shipped){'pending'}else{'not-applicable'});scope=$(if($shipped){'This delivery repack with retained runtime 14740fd: exact-candidate rig/game/build acceptance pending; historical 0.2.12 evidence is not candidate acceptance.'}else{'No player triple renderer or playback entrypoint in this ZIP.'});evidence=@()};defaultState=$(if($shipped){'preserve-existing'}else{'unavailable'});capabilities=@($caps[$id]);limitations=@('Offline source/package checks do not establish physical force, rig acceptance or rendered triple presentation.')}
 if($shipped){$f.freshInstallDefault=$(if($id -in @('ffb','telemetry')){'on'}else{'off'})}
 if($id -eq 'ffb'){$f.limitations += 'Legacy FFB preference defaults On, wheel override defaults Off and calibration is required. F8 saves Off; metadata/setup never arms, initializes or resumes force. Shared cadence impacts are not adopted.'}
 if($id -eq 'recording'){$f.limitations += 'Consumer capture implementation is retained, but owner Start-RecordedGame helper is source-only and absent from ZIP. No automatic capture or launch; existing coordinated one-launch request required.'}
 if($id -eq 'playback'){$f.limitations += 'Actual ForceSignal reprocessing exists in Core and source-only TelemetryInspector; no standalone analysis command is distributed. No game-input replay or device playback.'}
 if($id -eq 'triple'){$f.limitations += 'Default-off research source is excluded; no validated native bindings, render pipeline, views or communication advertised.'}
 $features+=$f
}
function Op($support,$entry,$arguments,$notes){[ordered]@{support=$support;entrypoint=$entry;arguments=@($arguments);notes=$notes}}
$m=[ordered]@{
 schema='dbce.game-delivery';schemaVersion=1;kind='package-delivery';packageId='dbce-mods-super-woden-rally-edge';gameId='super-woden-rally-edge';displayName='DBCE mods for Super Woden Rally Edge';version=$Version;channel='candidate'
 platform=[ordered]@{os='windows';architecture='x64'}
 repository=[ordered]@{canonicalUrl='https://github.com/d-b-c-e/dbce-mods-woden-rally-edge';visibility='private';mappingStatus='proposed';previousUrls=@('https://github.com/d-b-c-e/woden-rally-edge-wheel')}
 provenance=[ordered]@{sources=@($Provenance.sources);artifacts=$artifacts;dependencies=$dependencies}
 integrity=[ordered]@{manifestPath='manifest.json';format='dbce.woden-package.sha256-array.v1'}
 loadOwners=@([ordered]@{ownerId='dbce.wodenrallyedgewheel';mechanism='BepInEx IL2CPP single WodenRallyEdgeWheel plugin';role='mod-loader'})
 features=$features
 setup=[ordered]@{deliveryMode='transactional-install';entrypoints=@(
  [ordered]@{path='Install.bat';purpose='primary';argumentStyle='-GameDir <game-directory> [-LoaderArchive <pinned-loader-archive>]';startsGame=$false;opensDevices=$false},
  [ordered]@{path='Uninstall.bat';purpose='recovery';argumentStyle='-GameDir <game-directory> [-RemoveUserData]';startsGame=$false;opensDevices=$false},
  [ordered]@{path='Verify-Package.ps1';purpose='legacy-compatible';argumentStyle='PowerShell -File Verify-Package.ps1 -PackageRoot <extracted-package>';startsGame=$false;opensDevices=$false}
 );target=[ordered]@{kind='game-directory';legacyRootIdentity='Super Woden Rally Edge game folder; BepInEx/plugins/WodenRallyEdgeWheel and legacy BepInEx/config'}
 operations=[ordered]@{
  check=(Op 'manual' $null @() 'No standalone no-target-write target preflight CLI. Verify-Package.ps1 checks extracted package only; target checks are part of install.');install=(Op 'automated' 'Install.bat' @('-GameDir','<game-directory>') 'Closed supported game, recognized loader and receipt-hash ownership; fresh prerequisite download may be needed.');update=(Op 'automated' 'Install.bat' @('-GameDir','<game-directory>') 'Same retained installer; modified/missing/unowned target payload refused before replacement.');uninstall=(Op 'automated' 'Uninstall.bat' @('-GameDir','<game-directory>') 'Receipt-owned removal only; explicit RemoveUserData is separate.');rollback=(Op 'manual' $null @() 'No rollback CLI. Caught failures restore verified bytes only while closed; interruption/conflicts require manual backup/recovery report review.')
 };recovery=[ordered]@{ownershipModel='receipt-hashes';changedOrUnownedPayload='refuse';caughtFailure='automatic-verified-rollback';interruption='manual-backup-recovery';backupVerification='hashes'}
 preservation=[ordered]@{settings='byte-preserved-existing';captures='retained';otherMods='retained';sharedLoader='retained-by-default';explicitExceptions=@('Fresh missing pinned loader setup seeds BepInEx.cfg with empty UnityBaseLibrariesSource; existing loader configuration is verified, not normalized.','Uninstall -RemoveUserData explicitly removes only four named Woden config/request files; recordings and other mods remain.','Unknown existing payload without ownership receipt requires separate manual adoption review.')}
 }
 compatibility=[ordered]@{legacyDescriptors=@('game-release.json','input-override.json',$prefix+'recording-provenance.json');retainedIdentities=@([ordered]@{kind='loader';value='dbce.wodenrallyedgewheel'},[ordered]@{kind='adapter';value='dbce.wodenrallyedgewheel'},[ordered]@{kind='config';value='BepInEx/config/dbce.wodenrallyedgewheel.cfg'},[ordered]@{kind='config';value='BepInEx/config/wheel-bindings.json'},[ordered]@{kind='receipt';value='BepInEx/WodenWheel-install.json'});settingsPolicy='preserve-existing';capturePolicy='preserve-existing';optimizerDiscovery='not-adopted';notes=@('game-release available means retained wheel/input/FFB/telemetry source implementation, not rig acceptance. Delivery separates shipped implementation from acceptance.','Runtime/plugin and install receipt retain version 0.2.13+14740fd; delivery.1 is a separately versioned metadata/docs repack, not a new runtime build.','No dual-renderer migration or optimizer discovery is introduced.')}
 recording=[ordered]@{captureKinds=@('input','telemetry','configuration','state','force-requests');playbackKinds=@();formatIds=@([ordered]@{id='dbce.wheel.session';version='1';adapterId='dbce.wodenrallyedgewheel'});bounded=$true;limits=@('Existing consumer queue 512, duration 1200 seconds, file size 64 MiB and 512 channels per sample; capture drops/limits are reported, not accepted as complete.');completionPolicy='Incomplete/dropped/limited captures are diagnostic only; force analysis requires completed original source and ordered model/reset/gate semantics.';identityPolicy='Original source/case/config/profile and baseline hashes, game/plugin/build/request identity must match; trials use new hashed configs and exclusive output paths. No silent source mismatch.';privacy='local-private-by-default';physicalOutput='forbidden-during-offline-playback';notes=@('Raw input/force requests are diagnostic channels, not a deterministic gameplay input tape.','Offline ForceSignal analysis is source-only tooling, not player playback shipped in this ZIP. No game-input replay, device playback, corpus upload or automatic capture.')}
 extensions=[ordered]@{'dbce.woden'=[ordered]@{contractVersion='1.0.0';packageRepositoryMapping=[ordered]@{packageId='dbce-mods-super-woden-rally-edge';repositoryName='dbce-mods-woden-rally-edge';status='approved-name-not-renamed'};engineOperations=[ordered]@{gameInputReplay='unsupported';devicePlayback='unsupported';offlineAnalysis='source-only';triplePresentation='unavailable';cadenceImpactAdoption='not-adopted'};retainedRuntimeVersion=$Provenance.runtimeVersion;retainedBaselineManifestSha256=$Provenance.baselineManifestSha256;provenanceMeaning='Retained reviewed stage provenance and cross-record consistency, not compiler attestation or physical acceptance.'}}
}
$m | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $PackageRoot 'delivery-manifest.json') -Encoding utf8
