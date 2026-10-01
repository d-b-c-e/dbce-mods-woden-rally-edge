[CmdletBinding()]
param([string]$Toolkit = (Join-Path (Split-Path $PSScriptRoot -Parent) '..\..\..\dbce-wheel-mod-toolkit'))
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $Toolkit 'dotnet\Dbce.Wheel.Recording'
& dotnet build (Join-Path $project 'Dbce.Wheel.Recording.csproj') -c Release -v q
if ($LASTEXITCODE -ne 0) { throw 'Recording dependency build failed' }
$destination = Join-Path $root 'lib\recording'
New-Item -ItemType Directory -Force $destination | Out-Null
$sourceFiles = @(Get-ChildItem -LiteralPath $project -File -Filter '*.cs') + @(Get-Item (Join-Path $project 'Dbce.Wheel.Recording.csproj'))
$sources = @($sourceFiles | Sort-Object Name | ForEach-Object { @{ path = $_.Name; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() } })
Copy-Item -LiteralPath (Join-Path $project 'bin\Release\netstandard2.0\Dbce.Wheel.Recording.dll') -Destination $destination
$revision = & git -C $Toolkit rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Missing toolkit source revision' }
@{ status = 'unpublished extension, not part of v0.12.0'; repository = 'https://github.com/d-b-c-e/dbce-wheel-mod-toolkit'; baseRevision = $revision; dirty = [bool]@(& git -C $Toolkit status --porcelain).Count; sources = $sources; dllSha256 = (Get-FileHash (Join-Path $destination 'Dbce.Wheel.Recording.dll')).Hash.ToLowerInvariant() } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $destination 'provenance.json') -Encoding utf8
