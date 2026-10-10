# Developer qualification only. Caller has snapshotted owner files and holds the
# rig lease. Mutates two bindings deliberately, then exercises production Apply
# on a copied fixture and on the live closed-game files. No device or game launch.
function Invoke-ControlApplyCheck([string]$GameDir,[string]$Result,[string]$Harness,[string]$Catalog,[string]$Profiles) {
    $profilesObject=Get-Content -LiteralPath $Profiles -Raw | ConvertFrom-Json
    if($profilesObject.Schema -ne 2){throw 'Explicit named profile required.'}
    $selected=@($profilesObject.Profiles | Where-Object Id -eq $profilesObject.ActiveId)
    if($selected.Count -ne 1){throw 'Ambiguous selected profile.'}
    $steer=$selected[0].Profile.Bindings.steer;$confirm=$selected[0].Profile.Bindings.confirm
    if($steer.Kind -ne 'Axis' -or $steer.Index -notin (0..7) -or $confirm.Kind -ne 'Button' -or $confirm.Index -notin (0..127)){throw 'This qualification needs axis steering and button confirm.'}
    $bindingPath=Join-Path $GameDir 'BepInEx/config/wheel-bindings.json'
    $bindings=Get-Content -LiteralPath $bindingPath -Raw | ConvertFrom-Json -AsHashtable
    if($bindings.Version -ne 1 -or !$bindings.Steer -or !$bindings.Buttons){throw 'Existing Woden binding schema required.'}
    $wrongSteer=([int]$steer.Index+1)%8;$wrongConfirm=([int]$confirm.Index+1)%128
    $bindings.Steer.Axis=$wrongSteer
    if(!$bindings.Buttons.Confirm){$bindings.Buttons.Confirm=@{DeviceGuid=$confirm.InstanceGuid}}
    $bindings.Buttons.Confirm.Button=$wrongConfirm
    $bindings | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $bindingPath
    $stage=[ordered]@{steer=@{before=$wrongSteer;expected=$steer.Index};confirm=@{before=$wrongConfirm;expected=$confirm.Index};bindingsSha256=(Get-ControlHash $bindingPath)}
    $stage | ConvertTo-Json -Depth 5 | Set-Content "$Result/staged-faults.json"
    # Freeze the actual named-profile bytes, including the owner's selected id.
    Copy-ControlExact $Profiles "$Result/profiles.json" (Get-ControlHash $Profiles)
    $fixture=Join-Path $Result 'fixture/game'; $fixtureRun=Join-Path $Result 'fixture/apply';$live=Join-Path $Result 'apply'
    $paths=@('BepInEx/config/dbce.wodenrallyedgewheel.cfg','BepInEx/config/wheel-bindings.json','BepInEx/WodenWheel-install.json')
    foreach($name in @('WheelFfb.dll','WodenRallyEdgeWheel.dll','WodenRallyEdge.Core.dll')){$paths+=@('BepInEx/plugins/WodenRallyEdgeWheel/'+$name)}
    foreach($p in $paths){$from=Join-Path $GameDir $p;Copy-ControlExact $from (Join-Path $fixture $p) (Get-ControlHash $from)}
    & dotnet $Harness prepare-live super-woden-rally-edge $fixture $Catalog "$Result/profiles.json" $fixtureRun
    if($LASTEXITCODE){throw 'Fixture preview failed.'}
    & dotnet $Harness apply-live $fixtureRun
    if($LASTEXITCODE){throw 'Fixture production Apply failed.'}
    & dotnet $Harness verify-live $fixtureRun
    if($LASTEXITCODE){throw 'Fixture readback failed.'}
    & dotnet $Harness prepare-live super-woden-rally-edge $GameDir $Catalog "$Result/profiles.json" $live
    if($LASTEXITCODE){throw 'Live preview failed.'}
    if((Get-ControlHash "$live/preview.json") -ne (Get-ControlHash "$fixtureRun/preview.json")){throw 'Live preview differs from isolated dry run.'}
    $preview=Get-Content "$live/preview.json" -Raw | ConvertFrom-Json
    foreach($name in @('Steer','Buttons')){if(!@($preview | Where-Object {$_.File -eq 'BepInEx/config/wheel-bindings.json' -and $_.What -eq ('mod: controls: '+$name)}).Count){throw "Staged $name was not previewed for repair."}}
    & dotnet $Harness apply-live $live
    if($LASTEXITCODE){throw 'Live production Apply failed.'}
    foreach($p in @('BepInEx/config/dbce.wodenrallyedgewheel.cfg','BepInEx/config/wheel-bindings.json')) {
        if((Get-ControlHash (Join-Path $GameDir $p)) -ne (Get-ControlHash (Join-Path $fixture $p))){throw 'Applied bytes differ from isolated dry run.'}
    }
    $after=Get-Content -LiteralPath $bindingPath -Raw | ConvertFrom-Json
    if($after.Steer.Axis -ne $steer.Index -or $after.Buttons.Confirm.Button -ne $confirm.Index -or $after.Buttons.Confirm.DeviceGuid -ne $confirm.InstanceGuid){throw 'Staged faults were not independently repaired.'}
    [ordered]@{stagedFaults='repaired';preview='matches isolated dry run';appliedBytes='matches isolated production Apply'} | ConvertTo-Json | Set-Content "$Result/apply-check.json"
}
