#Requires -Version 7.0
$ErrorActionPreference='Stop'
. "$PSScriptRoot/F6SavedProfile.ps1"
$root=Join-Path ([IO.Path]::GetTempPath()) ('woden-f6-save-fixture-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($root)|Out-Null
$path=Join-Path $root 'witness.json';$nonce='a'*32;$startup='B'*64;$savedHash='C'*64;$checks=0
function Verify([string]$actual){Assert-F6SavedProfile -Witness $path -Nonce $nonce -StartupSha256 $startup -ActualSha256 $actual}
function Reject([scriptblock]$work){$script:checks++;$refused=$false;try{& $work|Out-Null}catch{$refused=$true};if(!$refused){throw 'Accepted invalid witness'}}
function Write-Witness($data){[IO.File]::WriteAllText($path,($data|ConvertTo-Json -Compress))}
if((Verify $startup) -ine $startup){throw 'Unchanged startup refusal'};$checks++
Reject {Verify $savedHash}
$valid=[ordered]@{schema=1;nonce=$nonce;stepId=('d'*32);startupSha256=$startup;beforeSha256=$startup;savedSha256=$savedHash;activeSource='fixture'}
Write-Witness $valid;if((Verify $savedHash) -ine $savedHash){throw 'Valid checked Save refusal'};$checks++
foreach($key in @('nonce','stepId','startupSha256','beforeSha256','savedSha256')){
    $bad=[ordered]@{};foreach($entry in $valid.GetEnumerator()){$bad[$entry.Key]=$entry.Value};$bad[$key]='wrong';Write-Witness $bad;Reject {Verify $savedHash}
}
Write-Witness $valid;Reject {Verify $startup}
[IO.File]::WriteAllText($path,'{"schema":1,"schema":1}');Reject {Verify $savedHash}
[IO.File]::WriteAllText($path,('x'*4097));Reject {Verify $savedHash}
Write-Output "PASS $checks schema-9 saved-profile witness checks; retained temp fixture: $root"
