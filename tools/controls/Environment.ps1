# Woden-only platform boundary. File/lease recovery lives in the runner.
. "$PSScriptRoot/../game/ReplayEnvironment.ps1"
function Registry-Snapshot([string]$Path) {
    $values=@((Get-Item -LiteralPath $prefsKey).GetValueNames() | ForEach-Object {Get-SessionRegistryValue $_})
    ConvertTo-Json -InputObject $values -Depth 4 | Set-Content -LiteralPath $Path
}
function Registry-Restore([string]$Path) {
    $values=@(Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json)
    foreach($v in $values){$null=[Convert]::FromBase64String($v.data)}
    foreach($name in (Get-Item -LiteralPath $prefsKey).GetValueNames()){if($name -notin $values.name){Remove-ItemProperty -LiteralPath $prefsKey -Name $name}}
    foreach($v in $values){Set-SessionRegistryValue $v}
    foreach($v in $values){$actual=Get-SessionRegistryValue $v.name;if($actual.type -ne $v.type -or $actual.data -cne $v.data){throw 'Preference readback differs.'}}
    if(@((Get-Item -LiteralPath $prefsKey).GetValueNames()).Count -ne $values.Count){throw 'Preference inventory differs.'}
}
