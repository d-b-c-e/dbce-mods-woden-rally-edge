#Requires -Version 7.5
# New Woden consumer cases only. Source-linked functions, fake processes/shell;
# no shared helper qualification, native/token/device/window API or game launch.
$ErrorActionPreference='Stop'
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'Run-OrdinaryStopSmoke.ps1'),[ref]$tokens,[ref]$errors)
if ($errors.Count) { throw 'Consumer parse failed' }
$names=@('Save-OrdinaryLaunchCustody','Register-OrdinaryLaunchProcess','Assert-OrdinaryLaunchClosed','Start-OrdinarySteamGame')
foreach ($name in $names) {
    $nodes=@($ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $name},$false))
    if ($nodes.Count -ne 1) { throw ('Missing exact production function '+$name) }
    . ([scriptblock]::Create($nodes[0].Extent.Text))
}
$count=0
function Check([bool]$Value,[string]$Message) { if (!$Value) { throw $Message }; $script:count++ }
function Refuses([scriptblock]$Action,[string]$Message) {
    try { & $Action; throw 'Unexpected pass' } catch { Check ($_.Exception.Message -like ('*'+$Message+'*')) $Message }
}
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('woden-shared-consumer-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixture)|Out-Null
$outputRoot=$fixture;$GameDir='C:/fixture/woden';$exe=$GameDir+'/Super Woden Rally Edge.exe';$SteamExecutable='C:/fixture/steam.exe'
function Reset-Case {
    $script:result=[ordered]@{identities=@();launchAttempted=$false;launchAdopted=$false;launchOutcome='not-requested';childrenQuiescent=$false}
    $script:owned=[Collections.Generic.Dictionary[uint32,DateTime]]::new()
    $script:ownedRoots=[Collections.Generic.List[object]]::new()
    $script:events=[Collections.Generic.List[string]]::new()
    $script:decision='broker';$script:tokenMatches=$true;$script:requestFailure='';$script:quiescent=$true
    $script:launched=$null
}
function Closed { $events.Add('closed-game-check') }
function Get-SteamLaunchContext { param($SteamExecutable) [pscustomobject]@{decision=$decision;reason='fixture'} }
function New-ChildEnvironmentBroker {
    param($Path,$Executable,$WorkingDirectory,$AppId,$Environment,$AllowedEnvironmentNames)
    Check ($Executable -ceq $exe -and $WorkingDirectory -ceq $GameDir -and $AppId -ceq '3218630') 'Consumer supplies exact Woden image/cwd/AppID'
    $events.Add('new-broker');'A'*64
}
function Fake-Process([int]$Id,[string]$Role) {
    $p=[pscustomobject]@{Id=$Id;Role=$Role}
    $p|Add-Member ScriptProperty SafeHandle {
        $receipt=Get-Content -LiteralPath (Join-Path $outputRoot 'custody.json') -Raw|ConvertFrom-Json
        Check (@($receipt.identities|Where-Object {$_.pid -eq $this.Id}).Count -gt 0) 'Exact identity is atomically published before native handle/token refusal'
        $events.Add('handle-'+$this.Role);[IntPtr]1
    }
    $p
}
function Start-MediumBroker {
    param($Path,$BrokerSha256,$Context)
    $receipt=Get-Content -LiteralPath (Join-Path $outputRoot 'custody.json') -Raw|ConvertFrom-Json
    Check ($receipt.launchAttempted -and $receipt.launchOutcome -ceq 'unknown') 'Attempt/unknown latched before shell request'
    if ($requestFailure) {
        $failure=[InvalidOperationException]::new('fake shell refusal')
        $failure.Data['SteamShellRequestAttempted']=$requestFailure -ceq 'after'
        throw $failure
    }
    $stamp=[DateTime]::new(638900000000000001,[DateTimeKind]::Utc)
    [pscustomobject]@{record=[pscustomobject]@{pid=12;startedUtcTicks=$stamp.Ticks;executable='C:/Windows/explorer.exe'};process=(Fake-Process 12 'shell')}
}
function Wait-SteamLaunchProcess {
    param($Executable,$LaunchStartedUtc,$Context,$OnAdopted)
    Check ($LaunchStartedUtc -is [DateTimeOffset]) 'Consumer keeps typed launch timestamp'
    Check ($result.identities[0].role -ceq 'shell-request') 'Shell request enters custody before game wait'
    $record=[pscustomobject]@{pid=34;startedUtcTicks=638900000000000003;executable=$exe;integrityMatchesSteam=$tokenMatches;integrityError='fake-token-refusal'}
    $process=Fake-Process 34 'game'
    & $OnAdopted $record $process
    $events.Add('token-result')
    [pscustomobject]@{record=$record;process=$process;launchOutcome='exact-child-adopted'}
}
function Complete-Owned { $result.childrenQuiescent=$quiescent }
try {
    Reset-Case;Start-OrdinarySteamGame
    Check ($result.launchAdopted -and $owned.Count -eq 2 -and $ownedRoots.Count -eq 2 -and $launched.Id -eq 34) 'Successful adoption retains game and shell identities/objects'
    Check ($events.IndexOf('handle-game') -lt $events.IndexOf('token-result')) 'Game handle captured before token result admission'
    Assert-OrdinaryLaunchClosed
    Reset-Case;$script:tokenMatches=$false
    Refuses { Start-OrdinarySteamGame } 'Game token did not match Steam'
    Check ($result.launchAdopted -and $owned.ContainsKey(34) -and $launched.Id -eq 34) 'Token refusal retains exact adopted game custody'
    Assert-OrdinaryLaunchClosed
    Reset-Case;$script:requestFailure='after'
    Refuses { Start-OrdinarySteamGame } 'fake shell refusal'
    Refuses { Assert-OrdinaryLaunchClosed } 'retain lease and owner state'
    Check ($result.launchAttempted -and !$result.launchAdopted) 'Unknown shell outcome stays unknown even with empty scope'
    Reset-Case;$script:requestFailure='before'
    Refuses { Start-OrdinarySteamGame } 'fake shell refusal'
    Check (!$result.launchAttempted -and $result.launchOutcome -ceq 'not-requested') 'Pre-request validation refusal is recorded separately'
    Assert-OrdinaryLaunchClosed
    Reset-Case;$script:decision='refuse'
    Refuses { Start-OrdinarySteamGame } 'Shared Steam context refused'
    Check (!$result.launchAttempted -and $owned.Count -eq 0) 'Context refusal creates no child'
    Reset-Case;$script:quiescent=$false
    Refuses { Assert-OrdinaryLaunchClosed } 'retain lease and owner state'
    $text=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../components/wheel/tools/Start-RecordedGame.ps1'))
    Check ($text.Contains('if ($ExternalLaunch)') -and $text.Contains('ExternalLaunch requires the explicit automated no-force Native observation companion.')) 'External companion launch stays explicitly restricted'
    Check ($text.Contains('if ($PrepareOnly) {')) 'PrepareOnly remains a distinct return-before-launch path'
    Write-Output ("New Woden shared consumer: PASS; assertions="+$count)
} finally {
    foreach ($file in Get-ChildItem -LiteralPath $fixture -File) { Remove-Item -LiteralPath $file.FullName }
    [IO.Directory]::Delete($fixture,$false)
}
