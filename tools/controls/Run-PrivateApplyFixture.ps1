#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Result,
    [Parameter(Mandatory)][string]$PackageRoot,
    [Parameter(Mandatory)][string]$Writer,
    [Parameter(Mandatory)][string]$Catalog,
    [Parameter(Mandatory)][string]$Profiles,
    [ValidateSet('AsIs','Sequential')][string]$Transmission='AsIs',
    [string]$GameAssembly='D:/Program Files (x86)/Steam/steamapps/common/Super Woden Rally Edge/GameAssembly.dll',
    [string]$LoaderArchive='E:/Source/games/dbce-mods-woden-rally-edge/components/wheel/lib/loader/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788+5b766a3.zip'
)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/OwnerFiles.ps1"
function Full([string]$p){$ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($p)}
$Result=Full $Result;$PackageRoot=Full $PackageRoot;$Writer=Full $Writer
$Catalog=Full $Catalog;$Profiles=Full $Profiles;$GameAssembly=Full $GameAssembly;$LoaderArchive=Full $LoaderArchive
foreach($path in @($Result,$PackageRoot,$Writer,$Catalog,$Profiles,$GameAssembly,$LoaderArchive)){Assert-ControlPlain $path}
if(Test-Path -LiteralPath $Result){throw 'Choose a new disposable result directory.'}
if((Get-ControlHash $GameAssembly) -ne 'F422894D8D2B0DF4EDB7E5259E5E60CB8C4F8DEA2E85EBDFC09DD6766349250C'){throw 'GameAssembly pin differs.'}
if((Get-ControlHash (Join-Path $PackageRoot 'BepInEx/plugins/WodenRallyEdgeWheel/dbce_shim.dll')) -ne '9B8A036F664CF8F45CCCF82B98E5E4376F93215B60EAC50B6DB965BF498015D8'){throw 'Private shim pin differs.'}
if(!(Test-Path -LiteralPath $Writer -PathType Leaf)){throw 'Production writer missing.'}
$game=Join-Path $Result 'game';$evidence=Join-Path $Result 'apply'
$qualified=$false
[IO.Directory]::CreateDirectory($game)|Out-Null
'Disposable Woden private Apply fixture; never launch' | Set-Content -LiteralPath (Join-Path $game '.woden-installer-fixture')
Copy-ControlExact $GameAssembly (Join-Path $game 'GameAssembly.dll') (Get-ControlHash $GameAssembly)
try {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PackageRoot 'Manage-Install.ps1') -GameDir $game -PackageRoot $PackageRoot -LoaderArchive $LoaderArchive *> (Join-Path $Result 'install.log')
    if($LASTEXITCODE){throw "Disposable package install failed: $LASTEXITCODE"}
    $receipt=Get-Content -LiteralPath (Join-Path $game 'BepInEx/WodenWheel-install.json') -Raw | ConvertFrom-Json
    if($receipt.mode -cne 'Install' -or @($receipt.files).Count -ne 14 -or $receipt.sharedInputCandidate.controls.adapter -cne 'woden-shim-ini-controls-1'){throw 'Disposable private receipt differs.'}
    $before=Get-ChildItem -LiteralPath $game -Recurse -File | Sort-Object FullName | ForEach-Object { [ordered]@{path=$_.FullName.Substring($game.Length+1).Replace('\','/');sha256=(Get-ControlHash $_.FullName)} }
    Copy-ControlExact $Profiles (Join-Path $Result 'profiles.json') (Get-ControlHash $Profiles)
    if($Transmission -eq 'Sequential'){
        $mapping=Get-Content -LiteralPath (Join-Path $Result 'profiles.json') -Raw | ConvertFrom-Json
        $selected=@($mapping.Profiles | Where-Object Id -eq $mapping.ActiveId)
        if($mapping.Schema -ne 2 -or $selected.Count -ne 1 -or $selected[0].Profile.Transmission -notin @('Sequential','H-pattern')){throw 'Selected profile cannot be cloned for Sequential qualification.'}
        $selected[0].Profile.Transmission='Sequential'
        $selected[0].Profile.Name+=' - private Sequential qualification'
        $mapping | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $Result 'profiles.json')
    }
    & dotnet $Writer prepare-live super-woden-rally-edge $game $Catalog (Join-Path $Result 'profiles.json') $evidence *> (Join-Path $Result 'prepare.log')
    if($LASTEXITCODE){throw "Production Prepare failed: $LASTEXITCODE"}
    $planned=Get-Content -LiteralPath (Join-Path $evidence 'apply.json') -Raw | ConvertFrom-Json
    if($planned.schema -ne 2 -or @($planned.files | Where-Object path -eq 'BepInEx/config/dbce-controls.ini').Count -ne 1 -or @($planned.candidateDependencies.PSObject.Properties).Count -lt 5){throw 'Woden shared profile recovery or candidate pins missing.'}
    & dotnet $Writer apply-live $evidence *> (Join-Path $Result 'apply.log')
    if($LASTEXITCODE){throw "Production Apply failed: $LASTEXITCODE"}
    & dotnet $Writer verify-live $evidence *> (Join-Path $Result 'verify.log')
    if($LASTEXITCODE){throw "Production Verify failed: $LASTEXITCODE"}
    $appliedControls=Join-Path $game 'BepInEx/config/dbce-controls.ini'
    Copy-ControlExact $appliedControls (Join-Path $Result 'applied-controls.ini') (Get-ControlHash $appliedControls)
    $ini=Get-Content -LiteralPath $appliedControls -Raw
    $profile=Get-Content -LiteralPath (Join-Path $evidence 'profile.json') -Raw | ConvertFrom-Json
    & dotnet $Writer raw-workload (Join-Path $evidence 'profile.json') (Join-Path $Result 'raw-workload.json') *> (Join-Path $Result 'raw-workload.log')
    if($LASTEXITCODE){throw "Production independent raw workload failed: $LASTEXITCODE"}
    $workload=Get-Content -LiteralPath (Join-Path $Result 'raw-workload.json') -Raw | ConvertFrom-Json
    if($ini -notmatch '(?m)^Revision = ([A-Fa-f0-9]{64})\s*$' -or ![string]::Equals($Matches[1],$workload.ProfileHash,[StringComparison]::OrdinalIgnoreCase)){throw 'Applied profile revision differs from frozen Wheelkit workload.'}
    if($ini -notmatch '(?m)^Transmission = (Sequential|H-pattern)\s*$'){throw 'Applied private transmission was not recorded.'}
    $revision=$workload.ProfileHash;$appliedTransmission=$Matches[1]
    $applied=Get-ChildItem -LiteralPath $game -Recurse -File | Sort-Object FullName | ForEach-Object { [ordered]@{path=$_.FullName.Substring($game.Length+1).Replace('\','/');sha256=(Get-ControlHash $_.FullName)} }
    [ordered]@{schema=1;packageRoot=$PackageRoot;receiptSha256=(Get-ControlHash (Join-Path $game 'BepInEx/WodenWheel-install.json'));writerSha256=(Get-ControlHash $Writer);profileRevision=$revision;transmission=$appliedTransmission;before=$before;applied=$applied} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $Result 'fixture-result.json')
    $qualified=$true
} finally {
    if(Test-Path -LiteralPath (Join-Path $evidence 'apply.json')) {
        & dotnet $Writer restore-live $evidence *> (Join-Path $Result 'restore.log')
        if($LASTEXITCODE){throw "Production Restore failed: $LASTEXITCODE"}
        $after=Get-ChildItem -LiteralPath $game -Recurse -File | Sort-Object FullName | ForEach-Object { [ordered]@{path=$_.FullName.Substring($game.Length+1).Replace('\','/');sha256=(Get-ControlHash $_.FullName)} }
        if(($before | ConvertTo-Json -Depth 4 -Compress) -cne ($after | ConvertTo-Json -Depth 4 -Compress)){throw 'Disposable fixture did not restore byte-exactly.'}
        if($qualified){'PASS: exact private receipt, production Apply, revision readback, byte-exact restore; no game/device' | Set-Content -LiteralPath (Join-Path $Result 'passed.txt')}
    }
}
