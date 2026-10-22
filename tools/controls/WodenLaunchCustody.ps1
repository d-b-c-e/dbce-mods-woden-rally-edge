# Consumer glue over canonical BE13 comparison; definitions only, no OS query.
function Get-WodenOwnedCreationPrecision {
    param([uint32]$IdentityId,$KnownPrecision,[uint32[]]$NativeRootProcessIds=@())
    if($KnownPrecision.ContainsKey($IdentityId)){return $KnownPrecision[$IdentityId]}
    if($NativeRootProcessIds -contains $IdentityId){return 'Tick100ns'}
    # Every other entry is a descendant admitted by the pinned E58A helper from
    # its explicit CimMicrosecond census. Never infer precision from digit count.
    'CimMicrosecond'
}
function Merge-WodenLaunchCustody {
    param([Parameter(Mandatory)][AllowEmptyCollection()]$Owned,[Parameter(Mandatory)][AllowEmptyCollection()]$KnownPrecision,
        [uint32[]]$NativeRootProcessIds=@(),[Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Records,
        [Parameter(Mandatory)]$Snapshot)
    if(!$Snapshot -or $Snapshot.querySucceeded -isnot [bool] -or !$Snapshot.querySucceeded -or $null -eq $Snapshot.rows){throw 'External custody complete census unavailable'}
    $updates=[Collections.Generic.Dictionary[uint32,DateTime]]::new()
    $precisions=[Collections.Generic.Dictionary[uint32,string]]::new()
    $refined=[Collections.Generic.List[uint32]]::new()
    foreach($record in $Records){
        [long]$number=0
        if(![long]::TryParse([string]$record.pid,[ref]$number) -or $number -le 0 -or $number -gt [uint32]::MaxValue){throw 'External custody PID malformed'}
        $identityId=[uint32]$number
        $incoming=ConvertTo-StageCreationStamp $record.createdUtc $record.precision
        if(!$incoming){throw 'External creation stamp/precision malformed'}
        $newRecord=[pscustomobject]@{processId=$identityId;createdUtc=$record.createdUtc;precision=$record.precision}
        $live=Get-StageProcessIdentityDisposition -Recorded $newRecord -Snapshot $Snapshot
        if($live.state -notin 'OriginalAlive','Absent','Reused' -or
            ($live.state -ceq 'Reused' -and ($live.originalAbsent -isnot [bool] -or !$live.originalAbsent))){throw ('External identity refused: '+$live.state)}
        $hasOld=$updates.ContainsKey($identityId) -or $Owned.ContainsKey($identityId)
        if($hasOld){
            $oldTime=if($updates.ContainsKey($identityId)){$updates[$identityId]}else{$Owned[$identityId]}
            $oldPrecision=if($precisions.ContainsKey($identityId)){$precisions[$identityId]}else{Get-WodenOwnedCreationPrecision $identityId $KnownPrecision $NativeRootProcessIds}
            $old=ConvertTo-StageCreationStamp $oldTime $oldPrecision
            if(!$old){throw 'Canonical owned creation stamp/precision unavailable'}
            $oldRecord=[pscustomobject]@{processId=$identityId;createdUtc=$oldTime;precision=$oldPrecision}
            # Pure comparison of the two producer records, using BE13's own
            # coarser-bucket rule; this pair is not claimed as a fresh OS census.
            $pair=[pscustomobject]@{querySucceeded=$true;rows=@($newRecord)}
            $same=Get-StageProcessIdentityDisposition -Recorded $oldRecord -Snapshot $pair
            $oldLive=Get-StageProcessIdentityDisposition -Recorded $oldRecord -Snapshot $Snapshot
            if($same.state -cne 'OriginalAlive' -or $oldLive.state -notin 'OriginalAlive','Absent','Reused' -or
                ($oldLive.state -ceq 'Reused' -and ($oldLive.originalAbsent -isnot [bool] -or !$oldLive.originalAbsent))){throw 'External PID identity changed or unknown'}
            if($incoming.quantum -lt $old.quantum){
                $updates[$identityId]=[DateTime]::new($incoming.ticks,[DateTimeKind]::Utc)
                $precisions[$identityId]=$record.precision;$refined.Add($identityId)
            }else{
                $updates[$identityId]=$oldTime;$precisions[$identityId]=$oldPrecision
            }
        }else{
            $updates[$identityId]=[DateTime]::new($incoming.ticks,[DateTimeKind]::Utc)
            $precisions[$identityId]=$record.precision
        }
    }
    # A canonical Reused comparison proves only original absence; the replacement
    # birth is never admitted to Owned. Unknown timestamps retain custody.
    foreach($identityId in $updates.Keys){$Owned[$identityId]=$updates[$identityId];$KnownPrecision[$identityId]=$precisions[$identityId]}
    [pscustomobject]@{records=$Records.Count;refinedProcessIds=@($refined)}
}
