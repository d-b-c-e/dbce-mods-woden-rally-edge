#Requires -Version 7.0
# Production companion reader and unbound Stop parameter, isolated from the game.
$ErrorActionPreference='Stop'
$companion=Join-Path $PSScriptRoot 'Start-RecordedGame.ps1'
$tokens=$null; $errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile($companion,[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Companion source does not parse.'}
$functions=@($ast.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Read-ControlStatus'},$true))
if($functions.Count -ne 1){throw 'Expected one production Read-ControlStatus function.'}
. ([scriptblock]::Create($functions[0].Extent.Text))
# Call the same .NET overload from C#: PowerShell converts the null optional
# backup string to an empty path before File.Replace reaches Windows.
Add-Type -TypeDefinition 'using System.IO; public static class WodenStatusFixtureReplace { public static void Run(string from, string to) { File.Replace(from, to, null); } }'
$base=Join-Path ([IO.Path]::GetTempPath()) ('woden-companion-status-'+[guid]::NewGuid().ToString('N'))
$base=[IO.Path]::GetFullPath($base)
if(!$base.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()),[StringComparison]::OrdinalIgnoreCase)){throw 'Fixture path escapes Temp.'}
[IO.Directory]::CreateDirectory($base)|Out-Null
$checks=0
function Check([bool]$Ok,[string]$Why){$script:checks++;if(!$Ok){throw $Why}}
function Refuses([scriptblock]$Action){
    $caught=$false
    try {& $Action} catch {if($_.Exception.Message -ne 'Malformed recording status.'){throw};$caught=$true}
    Check $caught 'Malformed production status was accepted.'
}
try {
    $status=Join-Path $base 'status.txt'
    $old="id=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa`r`nstatus=Recording`r`nactive=True`r`nerror=`r`n"
    [IO.File]::WriteAllText($status,$old,[Text.UTF8Encoding]::new($false))
    $held=[IO.FileStream]::new($status,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    try {
        $temp=Join-Path $base 'status.tmp'
        [IO.File]::WriteAllText($temp,"id=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa`r`nstatus=Completed`r`nactive=False`r`nerror=`r`n",[Text.UTF8Encoding]::new($false))
        [WodenStatusFixtureReplace]::Run($temp,$status)
        $fields=Read-ControlStatus $status
        Check ($fields.id -ceq 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa' -and $fields.status -ceq 'Completed' -and $fields.active -ceq 'False' -and $fields.error -ceq '') 'Shared reader did not read complete replaced status.'
        $bytes=[byte[]]::new([int]$held.Length)
        Check ($held.Read($bytes,0,$bytes.Length) -eq $bytes.Length -and [Text.Encoding]::UTF8.GetString($bytes) -ceq $old) 'Held handle did not retain complete old status.'
    } finally {$held.Dispose()}
    [IO.File]::WriteAllText($status,"id=x`nstatus=Recording`nid=y`n")
    Refuses {Read-ControlStatus $status|Out-Null}
    [IO.File]::WriteAllBytes($status,([byte[]](0xFF,0xFF)))
    Refuses {Read-ControlStatus $status|Out-Null}
    [IO.File]::WriteAllText($status,('x='+'a'*8192))
    Refuses {Read-ControlStatus $status|Out-Null}
    $game=Join-Path $base 'fake-game';[IO.Directory]::CreateDirectory($game)|Out-Null
    [IO.File]::WriteAllText((Join-Path $game 'Super Woden Rally Edge.exe'),'fixture only; never execute')
    $stopOutput=(& pwsh -NoProfile -File $companion -GameDirectory $game -Stop 2>&1 | Out-String)
    Check ($LASTEXITCODE -ne 0 -and $stopOutput -match 'Stop requires only an exact RequestId\.') 'Unbound RequestId did not refuse Stop at the parameter gate.'
    Check (!(Test-Path -LiteralPath (Join-Path $game 'BepInEx')) ) 'Unbound Stop wrote a game request.'
    Write-Host "PASS: $checks production companion status/default-RequestId checks; no game or device."
} finally {Remove-Item -LiteralPath $base -Recurse -Force}
