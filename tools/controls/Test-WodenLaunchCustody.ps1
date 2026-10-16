#Requires -Version 7.5
# New mixed-precision consumer cases only: pure records, no census/native query.
$ErrorActionPreference='Stop'
$helper='E:/Source/toolkits/dbce-wheel-mod-toolkit/tools/playback/Stage-ProcessIdentity.ps1'
if((Get-FileHash -LiteralPath $helper).Hash -cne 'BE132A68893EE591DD76DE7143D41F5B87C767FEA6EB628952ABFEF477713A29'){throw 'Canonical identity helper drift'}
. $helper
. (Join-Path $PSScriptRoot 'WodenLaunchCustody.ps1')
$count=0
function Check([bool]$Value,[string]$Message){if(!$Value){throw $Message};$script:count++}
function Refuses([scriptblock]$Action){try{& $Action;throw 'Unexpected pass'}catch{if($_.Exception.Message -ceq 'Unexpected pass'){throw};$script:count++}}
$coarse=[DateTime]::Parse('2026-10-16T02:46:59.2693330Z').ToUniversalTime()
$fine=$coarse.AddTicks(4)
function Reset {
 $script:owned=[Collections.Generic.Dictionary[uint32,DateTime]]::new();$owned.Add(67492,$coarse)
 $script:precision=[Collections.Generic.Dictionary[uint32,string]]::new()
 $script:snapshot=[pscustomobject]@{querySucceeded=$true;rows=@([pscustomobject]@{processId=67492;createdUtc=$coarse;precision='CimMicrosecond'})}
}
function Record($Time,[string]$Producer='Tick100ns',[uint32]$IdentityId=67492){[pscustomobject]@{pid=$IdentityId;createdUtc=$Time;precision=$Producer}}
function Merge([object[]]$Records,[uint32[]]$Native=@()){Merge-WodenLaunchCustody -Owned $owned -KnownPrecision $precision -NativeRootProcessIds $Native -Records $Records -Snapshot $snapshot}
Reset;$answer=Merge @(Record $fine)
Check ($owned[67492].Ticks -eq $fine.Ticks -and $precision[67492] -ceq 'Tick100ns' -and $answer.refinedProcessIds[0] -eq 67492) 'Actual coarse2693330/native2693334 refines same known process'
$null=Merge @(Record $coarse 'CimMicrosecond')
Check ($owned[67492].Ticks -eq $fine.Ticks -and $precision[67492] -ceq 'Tick100ns') 'Later coarse row cannot erase native precision'
$null=Merge @(Record $fine)
Check ($owned.Count -eq 1) 'Repeated exact row stays one identity'
Refuses {Merge @(Record ($fine.AddTicks(1)))}
Check ($owned[67492].Ticks -eq $fine.Ticks) 'Two full-precision stamps differ even inside one CIM bucket'
Reset;$owned[67492]=$fine
Refuses {Merge @(Record ($fine.AddTicks(1))) @(67492)}
Check ($owned[67492].Ticks -eq $fine.Ticks) 'Canonical direct root is never mislabeled coarse'
Reset;$later=$coarse.AddMilliseconds(1);$snapshot.rows[0].createdUtc=$later
Refuses {Merge @(Record $later)}
Check ($owned[67492].Ticks -eq $coarse.Ticks -and $precision.Count -eq 0) 'Real PID reuse does not replace original custody'
Reset;$snapshot.querySucceeded=$false
Refuses {Merge @(Record $fine)}
Check ($owned[67492].Ticks -eq $coarse.Ticks) 'Failed census cannot refine'
Reset
Refuses {Merge @(Record $fine 'unknown')}
Refuses {Merge @(Record $fine 'CimMicrosecond')}
Reset;$snapshot.rows=@()
$null=Merge @(Record $fine)
Check ($owned[67492].Ticks -eq $fine.Ticks) 'Compatible closed child remains in exact history'
Reset;$snapshot.rows=@([pscustomobject]@{processId=88;createdUtc=$coarse;precision='CimMicrosecond'},$snapshot.rows[0])
Refuses {Merge @((Record $coarse 'Tick100ns' 88),(Record ($coarse.AddTicks(20))))}
Check (!$owned.ContainsKey(88) -and $precision.Count -eq 0) 'One bad row rolls back entire receipt merge'
Check ((Get-WodenOwnedCreationPrecision 99 $precision @(99)) -ceq 'Tick100ns') 'Explicit native root provenance is retained'
Check ((Get-WodenOwnedCreationPrecision 98 $precision @(99)) -ceq 'CimMicrosecond') 'Only canonical descendant provenance supplies CIM precision'
Write-Output ('New Woden custody precision cases: PASS; assertions='+$count)
