#Requires -Version 7.0
# Dummy directories only; no installed game, registry, device, lease or launch.
$ErrorActionPreference='Stop'
. "$PSScriptRoot/OwnerFiles.ps1"
$base=Join-Path ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../results'))) ('controls-recovery-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($base) | Out-Null
$checks=0
function Check([bool]$ok,[string]$why){$script:checks++;if(!$ok){throw $why}}
function Fixture([string]$name) {
    $dir=Join-Path $base $name
    [IO.Directory]::CreateDirectory("$dir/game/config") | Out-Null
    [IO.Directory]::CreateDirectory("$dir/game/config/owner-empty") | Out-Null
    [IO.File]::WriteAllText("$dir/game/config/bindings.json",'owner bindings')
    [IO.File]::WriteAllText("$dir/game/native.dll",'owner native')
    $roots=[ordered]@{config="$dir/game/config"}
    $files=[ordered]@{native="$dir/game/native.dll";probe="$dir/game/addon/probe.dll"}
    Save-ControlFiles "$dir/before" $roots $files
    [IO.Directory]::Delete("$dir/game/config/owner-empty",$false)
    [IO.File]::WriteAllText("$dir/game/config/bindings.json",'test bindings')
    [IO.File]::WriteAllText("$dir/game/native.dll",'test native')
    [IO.Directory]::CreateDirectory("$dir/game/addon") | Out-Null
    [IO.Directory]::CreateDirectory("$dir/game/config/new-empty/child") | Out-Null
    [IO.File]::WriteAllText("$dir/game/addon/probe.dll",'temporary addon')
    [IO.File]::WriteAllText("$dir/game/config/new.log",'test log')
    @{dir=$dir;roots=$roots;files=$files}
}
$f=Fixture 'roundtrip'
Remove-Item -LiteralPath "$($f.dir)/game/config/bindings.json"
Restore-ControlFiles "$($f.dir)/before" "$($f.dir)/after" $f.roots $f.files
Check ([IO.File]::ReadAllText("$($f.dir)/game/config/bindings.json") -ceq 'owner bindings') 'Deleted owner file not restored'
Check ([IO.File]::ReadAllText("$($f.dir)/game/native.dll") -ceq 'owner native') 'Payload not restored'
Check (!(Test-Path "$($f.dir)/game/addon")) 'New addon directory remained'
Check (!(Test-Path "$($f.dir)/game/config/new-empty")) 'New empty directory tree remained'
Check (!(Test-Path "$($f.dir)/game/config/new.log")) 'New config file remained'
Check (Test-Path "$($f.dir)/game/config/owner-empty" -PathType Container) 'Deleted empty owner directory not restored'
Check ([IO.File]::ReadAllText("$($f.dir)/after/tree/config/new.log") -ceq 'test log') 'New file not archived'
foreach($case in @('corrupt','scope','traversal','duplicate','missing-fixed','bad-hash')) {
    $f=Fixture $case; $mPath="$($f.dir)/before/manifest.json"
    $m=Get-Content $mPath -Raw | ConvertFrom-Json -AsHashtable
    switch($case) {
        corrupt {[IO.File]::WriteAllText("$($f.dir)/before/file/native",'corrupt')}
        scope {$m.roots.config="$base/outside"}
        traversal {$m.entries[0].key='tree/config/../../outside.txt'}
        duplicate {$m.entries+=@($m.entries[0])}
        missing-fixed {$m.entries=@($m.entries | Where-Object key -ne 'file/native')}
        bad-hash {$m.entries[0].hash='invalid'}
    }
    $m | ConvertTo-Json -Depth 6 | Set-Content $mPath
    $refused=$false
    try {Restore-ControlFiles "$($f.dir)/before" "$($f.dir)/after" $f.roots $f.files} catch {$refused=$true}
    Check $refused "$case was not refused"
    Check ([IO.File]::ReadAllText("$($f.dir)/game/native.dll") -ceq 'test native') "$case mutated payload before validation"
    Check ([IO.File]::ReadAllText("$($f.dir)/game/config/bindings.json") -ceq 'test bindings') "$case mutated config before validation"
}
$f=Fixture 'locked'
$lock=[IO.File]::Open("$($f.dir)/game/native.dll",[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
$refused=$false
try {Restore-ControlFiles "$($f.dir)/before" "$($f.dir)/after" $f.roots $f.files} catch {$refused=$true} finally {$lock.Dispose()}
Check $refused 'Locked payload did not fail restoration'
Check ([IO.File]::ReadAllText("$($f.dir)/game/config/bindings.json") -ceq 'owner bindings') 'Locked payload prevented independent config restore'
Restore-ControlFiles "$($f.dir)/before" "$($f.dir)/retry-after" $f.roots $f.files
Check ([IO.File]::ReadAllText("$($f.dir)/game/native.dll") -ceq 'owner native') 'Retry did not recover payload'
Write-Output "PASS: $checks file-recovery checks; dummy evidence $base"
