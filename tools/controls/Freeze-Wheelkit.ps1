#Requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Result,[Parameter(Mandatory)][string]$Repo,
      [ValidatePattern('^[a-f0-9]{40}$')][string]$Revision='8c7243ec4c7d11d992b99ba8009bc6d790679595')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/OwnerFiles.ps1"
$archive=Join-Path $Result 'wheelkit-source.zip';$source=Join-Path $Result 'writer-source';$harness=Join-Path $Result 'harness'
foreach($path in @($archive,$source,$harness)){Assert-ControlPlain $path;if(Test-Path $path){throw 'Frozen writer destination must be new.'}}
& git -C $Repo archive --format=zip "--output=$archive" $Revision
if($LASTEXITCODE){throw 'Reviewed Wheelkit source unavailable.'}
[IO.Compression.ZipFile]::ExtractToDirectory($archive,$source)
# Git archive never reads the working tree or its potentially stale bin outputs.
& dotnet build "$source/tools/ConfigurationQualification/ConfigurationQualification.csproj" -c Release -o $harness -warnaserror *> "$Result/writer-build.log"
if($LASTEXITCODE){throw "Frozen writer build failed: $Result/writer-build.log"}
$catalog=Join-Path $source 'src/Wheelkit.App/Data/catalog-seed.json'
$inventory=@(Get-ChildItem $harness -File | Sort-Object Name | ForEach-Object {@{name=$_.Name;sha256=(Get-ControlHash $_.FullName)}})
$receipt=[ordered]@{schema=1;repository='https://github.com/d-b-c-e/dbce-wheelkit';sourceRevision=$Revision;archiveSha256=(Get-ControlHash $archive);catalogSha256=(Get-ControlHash $catalog);files=$inventory}
$receipt | ConvertTo-Json -Depth 5 | Set-Content "$Result/writer.json"
[pscustomobject]@{harness=(Join-Path $harness 'ConfigurationQualification.dll');catalog=$catalog;receiptHash=(Get-ControlHash "$Result/writer.json")}
