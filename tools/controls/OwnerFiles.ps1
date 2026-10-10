# File-only recovery primitives. The caller owns process/lease and registry gates.
# Roots are supplied by the adapter, never accepted from a recovery manifest.
function Get-ControlHash([string]$Path) {
    if(Test-Path -LiteralPath $Path -PathType Leaf){ return (Get-FileHash -LiteralPath $Path).Hash }
    if(Test-Path -LiteralPath $Path){ throw "Expected a file: $Path" }
    'absent'
}
function Assert-ControlPlain([string]$Path) {
    for($p=[IO.Path]::GetFullPath($Path);$p;$p=Split-Path -Parent $p) {
        if((Test-Path -LiteralPath $p) -and ((Get-Item -LiteralPath $p -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Linked recovery path refused.' }
    }
}
function Get-ControlChild([string]$Root,[string]$Relative) {
    $rootPath=[IO.Path]::GetFullPath($Root).TrimEnd('\','/')
    if([IO.Path]::IsPathRooted($Relative) -or $Relative.Contains(':')){throw 'Invalid relative recovery path.'}
    $path=[IO.Path]::GetFullPath((Join-Path $rootPath $Relative))
    if(!$path.StartsWith($rootPath+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Recovery path escapes its root.'}
    Assert-ControlPlain $path
    $path
}
function Get-ControlInventory($Roots,$Files) {
    foreach($key in $Roots.Keys) {
        $rootPath=[IO.Path]::GetFullPath($Roots[$key]); Assert-ControlPlain $rootPath
        if(!(Test-Path -LiteralPath $rootPath)){continue}
        foreach($item in Get-ChildItem -LiteralPath $rootPath -Recurse -Force) {
            if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked owner subtree refused.'}
            if(!$item.PSIsContainer) {
                [pscustomobject]@{key='tree/'+$key+'/'+$item.FullName.Substring($rootPath.TrimEnd('\','/').Length+1).Replace('\','/');path=$item.FullName}
            }
        }
    }
    foreach($key in $Files.Keys) {
        Assert-ControlPlain $Files[$key]
        [pscustomobject]@{key='file/'+$key;path=[IO.Path]::GetFullPath($Files[$key])}
    }
}
function Resolve-ControlEntry([string]$Key,$Roots,$Files) {
    $parts=$Key -split '/',3
    if($parts.Count -eq 2 -and $parts[0] -eq 'file' -and $Files.Contains($parts[1])) {
        Assert-ControlPlain $Files[$parts[1]]; return [IO.Path]::GetFullPath($Files[$parts[1]])
    }
    if($parts.Count -eq 3 -and $parts[0] -eq 'tree' -and $Roots.Contains($parts[1])) { return Get-ControlChild $Roots[$parts[1]] $parts[2] }
    throw 'Unknown recovery scope.'
}
function Get-ControlDirectories($Roots,$Files) {
    $result=@{}
    foreach($key in $Roots.Keys) {
        $path=[IO.Path]::GetFullPath($Roots[$key]); Assert-ControlPlain $path
        if(Test-Path -LiteralPath $path -PathType Container) {
            $result[$path]=$true
            foreach($d in Get-ChildItem -LiteralPath $path -Directory -Recurse -Force){Assert-ControlPlain $d.FullName;$result[$d.FullName]=$true}
        }
    }
    foreach($key in $Files.Keys) {
        $path=Split-Path -Parent ([IO.Path]::GetFullPath($Files[$key]));Assert-ControlPlain $path
        if(Test-Path -LiteralPath $path -PathType Container){$result[$path]=$true}
    }
    @($result.Keys | Sort-Object)
}
function Copy-ControlExact([string]$From,[string]$To,[string]$Expected) {
    if((Get-ControlHash $From) -ne $Expected){throw 'Source changed before copy.'}
    Assert-ControlPlain $To
    [IO.Directory]::CreateDirectory((Split-Path -Parent $To)) | Out-Null
    Copy-Item -LiteralPath $From -Destination $To -Force
    if((Get-ControlHash $To) -ne $Expected -or (Get-ControlHash $From) -ne $Expected){throw 'Copy readback failed.'}
}
function Save-ControlFiles([string]$Directory,$Roots,$Files) {
    Assert-ControlPlain $Directory
    if(Test-Path -LiteralPath $Directory){throw 'Backup directory must be new.'}
    $entries=@(Get-ControlInventory $Roots $Files)
    if(@($entries.path | Sort-Object -Unique).Count -ne $entries.Count){throw 'Overlapping recovery scopes.'}
    [IO.Directory]::CreateDirectory($Directory) | Out-Null
    $saved=@(foreach($entry in $entries) {
        $hash=Get-ControlHash $entry.path
        if($hash -ne 'absent'){Copy-ControlExact $entry.path (Get-ControlChild $Directory $entry.key) $hash}
        [ordered]@{key=$entry.key;hash=$hash}
    })
    [ordered]@{schema=1;roots=$Roots;files=$Files;entries=$saved;directories=@(Get-ControlDirectories $Roots $Files)} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath "$Directory/manifest.json"
}
function Restore-ControlFiles([string]$Directory,[string]$After,$Roots,$Files) {
    Assert-ControlPlain $Directory; Assert-ControlPlain $After
    $manifest=Get-Content -LiteralPath "$Directory/manifest.json" -Raw | ConvertFrom-Json -AsHashtable
    if($manifest.schema -ne 1 -or $manifest.entries.Count -gt 10000){throw 'Unknown recovery manifest.'}
    foreach($pair in @(@($manifest.roots,$Roots),@($manifest.files,$Files))) {
        if($pair[0].Count -ne $pair[1].Count){throw 'Recovery scope changed.'}
        foreach($key in $pair[1].Keys) {
            if(!$pair[0].ContainsKey($key) -or ![string]::Equals([IO.Path]::GetFullPath($pair[0][$key]),[IO.Path]::GetFullPath($pair[1][$key]),[StringComparison]::OrdinalIgnoreCase)){throw 'Recovery root differs.'}
        }
    }
    $destinations=@{}; $paths=@{}
    foreach($entry in $manifest.entries) {
        $target=Resolve-ControlEntry $entry.key $Roots $Files
        if($destinations.ContainsKey($entry.key) -or $paths.ContainsKey($target)){throw 'Duplicate recovery entry.'}
        if($entry.hash -ne 'absent' -and $entry.hash -notmatch '^[A-Fa-f0-9]{64}$'){throw 'Invalid backup hash.'}
        if($entry.hash -ne 'absent' -and (Get-ControlHash (Get-ControlChild $Directory $entry.key)) -ne $entry.hash){throw 'Backup hash mismatch.'}
        $destinations[$entry.key]=$target; $paths[$target]=$true
    }
    foreach($key in $Files.Keys){if(!$destinations.ContainsKey('file/'+$key)){throw 'Fixed recovery file omitted.'}}
    if(!$manifest.ContainsKey('directories')){throw 'Missing directory inventory.'}
    foreach($d in $manifest.directories) {
        $path=[IO.Path]::GetFullPath($d);Assert-ControlPlain $path
        $allowed=$false
        foreach($root in $Roots.Values){$root=[IO.Path]::GetFullPath($root).TrimEnd('\','/');if($path -eq $root -or $path.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){$allowed=$true}}
        foreach($file in $Files.Values){if($path -eq (Split-Path -Parent ([IO.Path]::GetFullPath($file)))){$allowed=$true}}
        if(!$allowed){throw 'Directory inventory escapes recovery scope.'}
    }
    $current=@(Get-ControlInventory $Roots $Files) # Validate every current path before any mutation.
    $errors=[Collections.Generic.List[string]]::new()
    foreach($d in $manifest.directories) {
        try {Assert-ControlPlain $d; [IO.Directory]::CreateDirectory($d) | Out-Null}
        catch {$errors.Add($_.Exception.Message)}
    }
    foreach($entry in $current) {
        try {
            $hash=Get-ControlHash $entry.path
            if($hash -ne 'absent') {
                Copy-ControlExact $entry.path (Get-ControlChild $After $entry.key) $hash
                if(!$destinations.ContainsKey($entry.key)){Remove-Item -LiteralPath $entry.path}
            }
        } catch {$errors.Add($_.Exception.Message)}
    }
    foreach($entry in $manifest.entries) {
        try {
            $target=$destinations[$entry.key]; Assert-ControlPlain $target
            if($entry.hash -eq 'absent') {if(Test-Path -LiteralPath $target){Remove-Item -LiteralPath $target}}
            else {Copy-ControlExact (Get-ControlChild $Directory $entry.key) $target $entry.hash}
            if((Get-ControlHash $target) -ne $entry.hash){throw 'Restoration readback failed.'}
        } catch {$errors.Add($_.Exception.Message)}
    }
    # Delete only newly created, empty directories under the adapter's known
    # roots (or exact fixed-file parents). No recursive directory deletion.
    foreach($d in (Get-ControlDirectories $Roots $Files | Sort-Object Length -Descending)) {
        try {
            if($d -notin $manifest.directories) {
                Assert-ControlPlain $d
                if(@(Get-ChildItem -LiteralPath $d -Force).Count -eq 0){[IO.Directory]::Delete($d,$false)}
            }
        } catch {$errors.Add($_.Exception.Message)}
    }
    if($errors.Count){throw ('Independent restores attempted; recovery incomplete: '+($errors -join '; '))}
}
