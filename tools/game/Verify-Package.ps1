[CmdletBinding()]
param([string]$PackageRoot=$PSScriptRoot)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Woden-Delivery.ps1')
$null=Assert-WodenDelivery -Root $PackageRoot
Write-Host 'PASS Woden delivery: pinned common v1 schema, exact integrity, retained provenance and six-feature semantics; no target writes or devices.'
