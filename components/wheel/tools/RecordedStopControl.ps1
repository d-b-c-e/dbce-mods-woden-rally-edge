param(
    [Parameter(Mandatory)][guid]$RequestId,
    [Parameter(Mandatory)][string]$ControlRoot,
    [Parameter(Mandatory)][datetimeoffset]$ExpiresUtc
)
$ErrorActionPreference = 'Stop'
if ($RequestId -eq [guid]::Empty -or !(Test-Path -LiteralPath $ControlRoot -PathType Container) -or
    $ExpiresUtc -le [DateTimeOffset]::UtcNow -or $ExpiresUtc -gt [DateTimeOffset]::UtcNow.AddMinutes(5)) {
    throw 'Invalid ordinary Stop request identity, root or deadline.'
}
$requestFile = Join-Path $ControlRoot 'request.txt'
if (Test-Path -LiteralPath $requestFile) { throw 'A recording control request is already pending.' }
$temporary = "$requestFile.tmp-$([guid]::NewGuid().ToString('N'))"
try {
    [string[]]$lines = @(
        ('id=' + $RequestId.ToString('N')),
        ('expiresUtc=' + $ExpiresUtc.UtcDateTime.ToString('O')),
        'action=stop'
    )
    [IO.File]::WriteAllLines($temporary, $lines, [Text.UTF8Encoding]::new($false))
    [IO.File]::Move($temporary, $requestFile)
} finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary } }
