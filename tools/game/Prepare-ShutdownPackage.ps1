[CmdletBinding()]
param([Parameter(Mandatory)][string]$BaselineStage,[Parameter(Mandatory)][string]$NativeRoot,
 [Parameter(Mandatory)][string]$NativeProvenance,[Parameter(Mandatory)][string]$OutputPath)
$ErrorActionPreference='Stop';$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if(Test-Path -LiteralPath $OutputPath){throw 'Preparation evidence exists; preserve its identity'}
. (Join-Path $PSScriptRoot 'Woden-Delivery.ps1')
$null=Assert-WodenDelivery -Root $BaselineStage
$commit=(& git -c "safe.directory=$root" -C $root rev-parse HEAD).Trim();if($LASTEXITCODE){throw 'Runtime source identity unavailable'}
if(@(& git -c "safe.directory=$root" -C $root status --porcelain).Count){throw 'Commit consumer source before preparation'}
$n=Get-Content -LiteralPath $NativeProvenance -Raw|ConvertFrom-Json
if($n.sourceCommit -ne 'a51bed99ee1f9e02cc92a397c47f9461e225bf4b' -or $n.dirty -or $n.nativeVersion -ne 502 -or $n.inputCapabilities -ne 1){throw 'Held or incorrect native source; no adoption'}
$nativeHead=(& git -c "safe.directory=$NativeRoot" -C $NativeRoot rev-parse HEAD).Trim();if($LASTEXITCODE -or $nativeHead -ne $n.sourceCommit){throw 'Native checkout identity differs'}
$native=@($n.artifacts|Where-Object machine -eq '8664');if($native.Count -ne 1 -or $native[0].exports.Count -ne 39 -or $native[0].exports -notcontains 'GetInputCapabilities'){throw 'Required HAT39 x64 ABI unavailable'}
$nativeFile=Join-Path $NativeRoot $native[0].path
if((Get-FileHash -LiteralPath $nativeFile).Hash.ToLowerInvariant() -cne $native[0].sha256){throw 'Native build changed'}
$bin=Join-Path $root 'components/wheel/src/WodenRallyEdge.Plugin/bin/Release/net6.0'
$payload=@();foreach($name in 'WodenRallyEdgeWheel.dll','WodenRallyEdge.Core.dll'){
 $file=Join-Path $bin $name;$version=(Get-Item -LiteralPath $file).VersionInfo.ProductVersion
 if($version -ne ('0.2.14+'+$commit)){throw 'Build clean consumer commit before preparation'}
 $payload += [ordered]@{path=('BepInEx/plugins/WodenRallyEdgeWheel/'+$name);sha256=(Get-FileHash $file).Hash.ToLowerInvariant();origin='fresh-build';sourceCommit=$commit}
}
$payload += [ordered]@{path='BepInEx/plugins/WodenRallyEdgeWheel/WheelFfb.dll';sha256=$native[0].sha256;origin='fresh-build';sourceCommit=$n.sourceCommit}
$planned=@(Get-WodenDeliveryFiles)+@('native-build-provenance.json','manifest.json')
$result=[ordered]@{schema='dbce.woden.shutdown-package-preparation@1';candidateVersion='0.2.14-shutdown.1';
 packageId='dbce-mods-super-woden-rally-edge';repositoryUrl='https://github.com/d-b-c-e/dbce-mods-woden-rally-edge';
 consumerCommit=$commit;nativeCommit=$n.sourceCommit;nativeVersion=502;nativeExports=39;inputCapabilities=1;
 preparedPayload=$payload;plannedFiles=$planned;legacyInstallerOwnedFileCount=9;
 baselineManifestSha256=(Get-FileHash (Join-Path $BaselineStage 'manifest.json')).Hash.ToLowerInvariant();
 packageReady=$false;zipCreated=$false;installed=$false;devicesUsed=$false;cadenceIncluded=$false;
 gates=@('independent-native-successor-review','consumer-lifecycle-review','new-delivery-integrity-and-provenance-verifier','fake-installer-and-rollback-on-final-zip','Unity-and-attended-rig-acceptance');
 notes='Metadata-only preparation: no held DLL is packaged; old ZIP/installer/settings remain unchanged. Native catalog must be package-relative and retain managed input override separately.'}
$result|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Host 'Prepared exact source/artifact inventory; packageReady=false, no ZIP or installation.'
