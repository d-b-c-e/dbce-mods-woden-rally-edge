#Requires -Version 7.0
# Read-only S1 package check for the one muted ordinary forward capture.
function Assert-OrdinaryFullSession {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Directory)
    $ErrorActionPreference='Stop'
    $root=[IO.Path]::GetFullPath($Directory).TrimEnd('\','/')
    $stage=Join-Path $root 'source.stage'
    $outer=@('session.json','source.jsonl','source.session.jsonl','source.stage-session.json','source.stage/complete.tsv')
    $inner=@('trajectory.meta','trajectory.tsv','source.jsonl','force-config.json','channels.json','stage-context.json')
    function CheckSeal([string]$Base,[string[]]$Names) {
        $seal=Join-Path $Base 'complete.tsv'
        $lines=@([IO.File]::ReadAllLines($seal))
        if($lines.Count -ne $Names.Count+1 -or $lines[0] -cne 'dbce-playback-seal-v1'){throw "Full-session seal shape differs: $seal"}
        $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach($line in $lines[1..($lines.Count-1)]){
            $fields=$line.Split("`t")
            if($fields.Count -ne 3 -or $Names -cnotcontains $fields[0] -or !$seen.Add($fields[0]) -or
               $fields[1] -notmatch '^[0-9]+$' -or $fields[2] -notmatch '^[A-Fa-f0-9]{64}$'){throw "Full-session seal row differs: $seal"}
            $path=Join-Path $Base $fields[0]
            $item=Get-Item -LiteralPath $path -ErrorAction Stop
            if($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
               $item.Length -ne [long]$fields[1] -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ine $fields[2]){
                throw "Full-session sealed payload differs: $path"
            }
        }
        if($seen.Count -ne $Names.Count){throw "Full-session seal membership differs: $seal"}
        return (Get-FileHash -LiteralPath $seal -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    if(Test-Path -LiteralPath (Join-Path $root 'full-session-incomplete.txt')){throw 'S1 package has an incomplete marker.'}
    $nested=CheckSeal $stage $inner
    $outerHash=CheckSeal $root $outer
    $descriptor=Get-Content -LiteralPath (Join-Path $root 'session.json') -Raw | ConvertFrom-Json
    if($descriptor.schema -cne 'woden.full-session-package@1' -or $descriptor.game -cne 'Super Woden Rally Edge' -or
       $descriptor.gameBuildSha256 -cne 'f422894d8d2b0df4edb7e5259e5e60cb8c4f8dea2e85ebdfc09dd6766349250c' -or
       $descriptor.origin -cne 'diagnostic' -or $descriptor.PSObject.Properties.Name -contains 'playbackAdapter' -or
       $descriptor.signals -cne 'source.jsonl' -or $descriptor.observation -cne 'source.session.jsonl' -or
       $descriptor.timeline -cne 'source.stage-session.json' -or $descriptor.stage -cne 'source.stage' -or
       [string]::IsNullOrWhiteSpace($descriptor.playbackUnavailableReason)){
        throw 'S1 descriptor identity or playback refusal differs.'
    }
    $timeline=Get-Content -LiteralPath (Join-Path $root 'source.stage-session.json') -Raw | ConvertFrom-Json
    if($timeline.Schema -cne 'woden.full-session@1' -or $timeline.GameBuild -cne $descriptor.gameBuildSha256 -or
       $timeline.Stage.Directory -cne 'source.stage' -or $timeline.Stage.SealSha256 -ine $nested -or
       $timeline.Stage.PoseCount -lt 2 -or $timeline.Stage.PoseCount -gt 180000){throw 'S1 timeline stage reference differs.'}
    $poses=0;$driving=$false
    foreach($step in $timeline.Steps){
        if($step.Kind -eq 0 -and $step.State.Phase -eq 3){$driving=$true}
        if($step.Kind -eq 2){
            if(!$driving -or $step.FirstPose -ne $poses -or $step.PoseCount -le 0){throw 'S1 timeline pose interval differs.'}
            $poses+=[int]$step.PoseCount
        }
    }
    if($poses -ne $timeline.Stage.PoseCount){throw 'S1 timeline pose count differs.'}
    $context=Get-Content -LiteralPath (Join-Path $stage 'stage-context.json') -Raw | ConvertFrom-Json
    if($context.schema -cne 'woden.stage-context@1' -or $context.gameAssemblySha256 -cne $descriptor.gameBuildSha256){throw 'S1 stage context identity differs.'}
    $meta=@{}
    foreach($line in [IO.File]::ReadLines((Join-Path $stage 'trajectory.meta'))){
        $at=$line.IndexOf('=')
        if($at -le 0){throw 'S1 trajectory metadata row differs.'}
        $meta[$line.Substring(0,$at)]=$line.Substring($at+1)
    }
    if($meta.physicalOutput -cne 'false' -or $meta.gameSha256 -cne $descriptor.gameBuildSha256){throw 'S1 trajectory metadata differs or was not marked physically muted.'}
    $trajectoryRows=0
    foreach($line in [IO.File]::ReadLines((Join-Path $stage 'trajectory.tsv'))){$trajectoryRows++}
    if($trajectoryRows -ne $poses+1){throw 'S1 trajectory row count differs.'}
    $signalRows=0;$stageCompleted=$false
    foreach($line in [IO.File]::ReadLines((Join-Path $stage 'source.jsonl'))){
        $record=$line | ConvertFrom-Json
        if($record.kind -ceq 'sample'){
            if($record.sample.channels.'capture.trajectoryIndex' -ne $signalRows -or
               $null -eq $record.sample.channels.'game.trueSpeed'){throw 'S1 signal row is not aligned to its pose or lacks recorded HUD speed.'}
            $signalRows++
        }
        if($record.kind -ceq 'footer'){$stageCompleted=($record.footer.completed -eq $true)}
    }
    if(!$stageCompleted -or $signalRows -ne $poses){throw 'S1 stage signals are incomplete or misaligned.'}
    $journal=@([IO.File]::ReadLines((Join-Path $root 'source.session.jsonl')) | ForEach-Object { $_ | ConvertFrom-Json })
    $opened=@($journal | Where-Object kind -CEQ 'stage-open')
    $sealed=@($journal | Where-Object kind -CEQ 'stage-sealed')
    if($opened.Count -ne 1 -or $sealed.Count -ne 1 -or @($journal | Where-Object kind -CEQ 'end').Count -ne 1){throw 'S1 observation lifecycle differs.'}
    $openIndex=[array]::IndexOf($journal,$opened[0])
    if(@($journal[0..$openIndex] | Where-Object { $_.kind -ceq 'scene' -and $_.detail.name -ceq $opened[0].detail.selection.Scene }).Count -eq 0){throw 'S1 stage scene was not observed before the first stage-open.'}
    if($sealed[0].detail.reason -ceq ('scene changed to '+$opened[0].detail.selection.Scene)){throw 'S1 stage was truncated by a late same-scene notification.'}
    return [pscustomobject]@{schema=1;status='passed';poseCount=$poses;signalRows=$signalRows;outerSealSha256=$outerHash;nestedSealSha256=$nested;stageSealReason=$sealed[0].detail.reason;physicalOutput=$false;origin='diagnostic'}
}
