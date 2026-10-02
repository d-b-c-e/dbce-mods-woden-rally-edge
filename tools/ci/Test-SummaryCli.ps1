[CmdletBinding()]
param([Parameter(Mandatory)][string]$Assembly)
$ErrorActionPreference='Stop'
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('woden-ci-cli-'+[guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path $fixture
$script:checks=0
function Check([bool]$value,[string]$why){if(-not $value){throw $why};$script:checks++}
function Run([string[]]$Arguments){
 $start=[Diagnostics.ProcessStartInfo]::new('dotnet');$start.UseShellExecute=$false;$start.CreateNoWindow=$true
 $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
 $start.ArgumentList.Add([IO.Path]::GetFullPath($Assembly));foreach($a in $Arguments){$start.ArgumentList.Add($a)}
 $p=[Diagnostics.Process]::Start($start)
 try{
  $out=$p.StandardOutput.ReadToEndAsync();$err=$p.StandardError.ReadToEndAsync()
  if(-not $p.WaitForExit(45000)){$p.Kill($true);$p.WaitForExit();throw 'Owned managed CLI fixture timed out'}
  $result=[pscustomobject]@{exit=$p.ExitCode;stdout=$out.GetAwaiter().GetResult();stderr=$err.GetAwaiter().GetResult()}
  Check ($result.stderr -notmatch 'Unhandled exception|Program\.<Main>| at WodenRallyEdge') 'Unexpected unhandled managed CLI failure'
  Check ($result.stderr.Length -lt 256) 'CLI diagnostic is not bounded'
  return $result
 }finally{$p.Dispose()}
}
$output=Join-Path $fixture 'summary with spaces.json'
$result=Run @('--ffb-summary',$output)
Check ($result.exit -eq 0 -and (Test-Path $output)) 'Fixed CLI success path failed'
$original=(Get-FileHash $output).Hash
$result=Run @('--ffb-summary',$output)
Check ($result.exit -eq 2 -and $result.stderr -match 'ffb-summary-artifact-refused') 'Overwrite must exit2 with bounded refusal'
Check ((Get-FileHash $output).Hash -eq $original) 'Existing summary bytes changed'
$tampered=Get-Content $output -Raw|ConvertFrom-Json;$tampered.Rows[0].Rms=.1
$baseline=Join-Path $fixture 'tampered.json';$tampered|ConvertTo-Json -Depth 30|Set-Content -LiteralPath $baseline
$baselineHash=(Get-FileHash $baseline).Hash;$rejected=Join-Path $fixture 'rejected.json'
$result=Run @('--ffb-summary',$rejected,$baseline)
Check ($result.exit -eq 1 -and $result.stderr -match 'ffb-summary-regression-failed') 'Tampered baseline must exit1'
Check (-not (Test-Path $rejected)) 'Rejected comparison created output'
Check ((Get-FileHash $baseline).Hash -eq $baselineHash -and (Get-FileHash $output).Hash -eq $original) 'Input or reference bytes changed'
$result=Run @('--ffb-summary')
Check ($result.exit -eq 2 -and $result.stderr -match 'ffb-summary-usage' -and $result.stdout -notmatch 'PASS') 'Malformed request fell through into full harness'
$matched=Join-Path $fixture 'matched.json';$result=Run @('--ffb-summary',$matched,$output)
Check ($result.exit -eq 0 -and (Get-FileHash $matched).Hash -eq $original) 'Untampered golden comparison differs'
Write-Host "PASS $script:checks fixed managed CLI exit/preservation checks; ephemeral synthetic fixtures only."
