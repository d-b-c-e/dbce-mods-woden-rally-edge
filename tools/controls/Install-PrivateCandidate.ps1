#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Result,
    [Parameter(Mandatory)][string]$PackageRoot,
    [string]$GameDir='D:/Program Files (x86)/Steam/steamapps/common/Super Woden Rally Edge',
    [string]$LoaderArchive='E:/Source/games/dbce-mods-woden-rally-edge/components/wheel/lib/loader/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788+5b766a3.zip'
)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/OwnerFiles.ps1"
. "$PSScriptRoot/Environment.ps1"
. "$PSScriptRoot/../../vendor/playback/Stage-RigLease.ps1"
function Full([string]$p){$ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($p)}
$Result=Full $Result;$PackageRoot=Full $PackageRoot;$GameDir=Full $GameDir;$LoaderArchive=Full $LoaderArchive
foreach($p in @($Result,$PackageRoot,$GameDir,$LoaderArchive)){Assert-ControlPlain $p}
if(Test-Path -LiteralPath $Result){throw 'Choose a new private install result directory.'}
if(Get-Process -Name 'Super Woden Rally Edge' -ErrorAction SilentlyContinue){throw 'Woden must be closed.'}
if((Get-ControlHash (Join-Path $GameDir 'GameAssembly.dll')) -ne 'F422894D8D2B0DF4EDB7E5259E5E60CB8C4F8DEA2E85EBDFC09DD6766349250C'){throw 'Supported game build differs.'}
$receiptPath=Join-Path $GameDir 'BepInEx/WodenWheel-install.json'
$configPath=Join-Path $GameDir 'BepInEx/config/dbce.wodenrallyedgewheel.cfg'
$text=[IO.File]::ReadAllText($configPath)
$sections=[regex]::Matches($text,'(?ms)^\[Input\][ \t]*\r?\n.*?(?=^\[|\z)')
if($sections.Count -gt 1){throw 'Duplicate Input settings section.'}
if($sections.Count -eq 1){
    $backend=[regex]::Matches($sections[0].Value,'(?m)^\s*Backend\s*=\s*([^\r\n;#]+)')
    if($backend.Count -gt 1 -or $backend.Count -eq 1 -and $backend[0].Groups[1].Value.Trim() -cne 'Native'){throw 'Owner backend is not unambiguously Native.'}
}
$prefsKey='HKCU:\Software\ViJuDa\Super Woden Rally Edge'
$roots=[ordered]@{config=(Join-Path $GameDir 'BepInEx/config');save=(Join-Path $env:USERPROFILE 'AppData/LocalLow/ViJuDa/Super Woden Rally Edge')}
$files=[ordered]@{ownerReceipt=$receiptPath}
$slot=Join-Path $env:LOCALAPPDATA 'dbce/test-slot.txt'
$lease=Enter-StageRigLease -Path $slot -Owner halo-woden-install -Purpose 'closed-game exact private candidate install and owner-state hash verification'
$safe=$false
try {
    & hcom send '@sana' '@nene' '@guru' --intent inform --name halo -- 'ENTER shared rig lease: closed-game Woden exact private candidate install and owner config/save/prefs verification. No launch/device/physical output.'
    [IO.Directory]::CreateDirectory($Result)|Out-Null
    Save-ControlFiles "$Result/owner-before" $roots $files
    Registry-Snapshot "$Result/preferences-before.json"
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PackageRoot 'Manage-Install.ps1') -GameDir $GameDir -PackageRoot $PackageRoot -LoaderArchive $LoaderArchive *> "$Result/install.log"
    if($LASTEXITCODE){throw "Private installer failed: $LASTEXITCODE"}
    if(Get-Process -Name 'Super Woden Rally Edge' -ErrorAction SilentlyContinue){throw 'Game unexpectedly started after install.'}
    $receipt=Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    if($receipt.mode -cne 'Install' -or @($receipt.files).Count -ne 14 -or $receipt.sharedInputCandidate.controls.adapter -cne 'woden-shim-ini-controls-1'){throw 'Installed private receipt differs.'}
    $backupRoot=[IO.Path]::GetFullPath((Join-Path $GameDir 'WodenWheelBackups')).TrimEnd('\')+'\'
    $backupPath=[IO.Path]::GetFullPath([string]$receipt.backup)
    if(!$backupPath.StartsWith($backupRoot,[StringComparison]::OrdinalIgnoreCase) -or !(Test-Path -LiteralPath $backupPath -PathType Container)){throw 'Installer backup is missing or outside WodenWheelBackups.'}
    foreach($entry in $receipt.files){
        $path=Join-Path $GameDir $entry.path
        if((Get-ControlHash $path) -ne $entry.sha256 -or (Get-ControlHash (Join-Path $PackageRoot $entry.path)) -ne $entry.sha256){throw "Installed payload differs: $($entry.path)"}
    }
    $saved=Get-Content -LiteralPath "$Result/owner-before/manifest.json" -Raw | ConvertFrom-Json
    $current=@(Get-ControlInventory $roots ([ordered]@{}))
    $expected=@($saved.entries | Where-Object { $_.key -like 'tree/*' })
    if($current.Count -ne $expected.Count){throw 'Owner config/save file count changed.'}
    $map=@{};foreach($entry in $expected){$map[$entry.key]=$entry.hash}
    foreach($entry in $current){if(!$map.ContainsKey($entry.key) -or (Get-ControlHash $entry.path) -ne $map[$entry.key]){throw "Owner config/save changed: $($entry.key)"}}
    Registry-Snapshot "$Result/preferences-after.json"
    if((Get-ControlHash "$Result/preferences-before.json") -ne (Get-ControlHash "$Result/preferences-after.json")){throw 'Owner preferences changed.'}
    $after=[IO.File]::ReadAllText($configPath)
    if($after -cne $text){throw 'Saved owner Woden configuration changed.'}
    [ordered]@{schema=1;installedReceiptSha256=(Get-ControlHash $receiptPath);priorReceiptSha256=(@($saved.entries | Where-Object key -eq 'file/ownerReceipt')[0].hash);payloads=14;backend='Native (saved setting unchanged)';ownerConfigSaveFiles=$current.Count;preferences='byte-exact';backup=$receipt.backup;gameLaunched=$false} |
        ConvertTo-Json | Set-Content "$Result/install-result.json"
    $safe=$true
} finally {
    if($safe){$released=Exit-StageRigLease -Lease $lease;& hcom send '@sana' '@nene' '@guru' --intent inform --name halo -- "EXIT shared rig lease: Woden private candidate installed Native-default; owner config/save/prefs unchanged; released=$released. Evidence $Result/install-result.json"}
    else {& hcom send '@sana' --intent inform --name halo -- "Woden private install verification incomplete; shared lease retained for recovery. Evidence $Result"}
}
