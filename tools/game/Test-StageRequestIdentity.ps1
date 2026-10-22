#Requires -Version 7.0
# Focused actual-source request propagation fixture. Writes only a disposable directory.
param([string]$Runner=(Join-Path $PSScriptRoot 'Run-StageSession.ps1'))
$ErrorActionPreference='Stop'
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile($runner,[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Runner parse failed'}
$function=$ast.Find({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq 'Set-PreparedReplayIdentity'},$true)
if(!$function){throw 'Production request identity function absent'}
Invoke-Expression $function.Extent.Text
$branch=@($ast.FindAll({param($n)$n -is [Management.Automation.Language.IfStatementAst] -and $n.Extent.Text.Contains('$preparedLines=@(Set-PreparedReplayIdentity')},$true))
if($branch.Count -ne 1){throw 'Expected one production identity staging branch'}
$old='3ccf6ef19fb342eab2ff2a77b1ab4ca5';$RequestId='ad2187d99e2d4728a6cc305e2332c1ce'
$sample=@(('id='+$old),'action=replay','expiresUtc=2026-10-15T11:59:51.6841555+00:00','path=E:\Fixture\source.stage','output=E:\Fixture\playback')
$fixed=@(Set-PreparedReplayIdentity $sample $RequestId)
if($fixed.Count -ne $sample.Count -or $fixed[0] -cne ('id='+$RequestId) -or ($fixed[1..4] -join "`n") -cne ($sample[1..4] -join "`n")){throw 'Nonidentity fields changed'}
$checks=1
foreach($case in @(
    @{lines=$sample;id='bad'},
    @{lines=@($sample[1..4]);id=$RequestId},
    @{lines=@($sample+('id='+$old));id=$RequestId},
    @{lines=@($sample|ForEach-Object {if($_ -ceq 'action=replay'){'action=record'}else{$_}});id=$RequestId},
    @{lines=@($sample|ForEach-Object {if($_.StartsWith('path=')){'path=relative'}else{$_}});id=$RequestId},
    @{lines=@($sample|ForEach-Object {if($_.StartsWith('output=')){'output=relative'}else{$_}});id=$RequestId},
    @{lines=@($sample+"extra=value`nforged=true");id=$RequestId}
)){
    $refused=$false;try{$null=Set-PreparedReplayIdentity $case.lines $case.id}catch{$refused=$true}
    if(!$refused){throw 'Malformed request identity accepted'};$checks++
}
$upper=@(Set-PreparedReplayIdentity $sample $RequestId.ToUpperInvariant());if($upper[0] -cne ('id='+$RequestId)){throw 'Requested ID not canonical'};$checks++
$control=Join-Path ([IO.Path]::GetTempPath()) ('woden-request-id-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($control)|Out-Null
$slot='synthetic-only';$rigLease=@{token='synthetic-only'}
$script:closedChecks=0;$script:leaseChecks=0
function Assert-Closed {$script:closedChecks++}
function Assert-StageRigLease {param($Path,$Token)if($Path -cne $slot -or $Token -cne $rigLease.token){throw 'Wrong synthetic lease'};$script:leaseChecks++}
try{
    foreach($scenario in @('requested','ordinary','foreign-temp')){
        $preparedPath=Join-Path $control 'request.txt';[IO.File]::WriteAllLines($preparedPath,[string[]]$sample,[Text.UTF8Encoding]::new($false))
        $id=$old;$RequestId=if($scenario -eq 'ordinary'){''}else{'ad2187d99e2d4728a6cc305e2332c1ce'}
        if($scenario -eq 'foreign-temp'){[IO.File]::WriteAllText((Join-Path $control ($RequestId+'.identity.tmp')),'foreign sentinel')}
        $caught=$false;try{Invoke-Expression $branch[0].Extent.Text}catch{$caught=$true}
        $actual=[IO.File]::ReadAllLines($preparedPath)
        if($scenario -eq 'requested'){
            if($caught -or $id -cne $RequestId -or ($actual -join "`n") -cne ($fixed -join "`n")){throw 'Production atomic staging failed'}
            if(@(Get-ChildItem -LiteralPath $control -Filter '*.tmp').Count){throw 'Own temporary file left behind'}
        }elseif(($actual -join "`n") -cne ($sample -join "`n") -or $id -cne $old -or $caught -ne ($scenario -eq 'foreign-temp')){throw ('Ordinary/refusal path changed: '+$scenario)}
        $checks++
    }
    if($script:closedChecks -ne 2 -or $script:leaseChecks -ne 2){throw 'Closed-game/lease checks missing'};$checks++
    $idRead=$ast.FindAll({param($n)$n -is [Management.Automation.Language.AssignmentStatementAst] -and $n.Left.Extent.Text -ceq '$id'},$true)|Where-Object {$_.Extent.StartOffset -lt $branch[0].Extent.StartOffset}|Select-Object -Last 1
    if(!$idRead -or !$idRead.Extent.Text.Contains('$preparedPath')){throw 'Generated ID not retained before repair failures'};$checks++
    "PASS $checks focused request-ID checks; actual production function and staging branch; no game/device/owner files."
}finally{
    # Known fixture files only; no recursive deletion.
    foreach($file in @(Get-ChildItem -LiteralPath $control -File)){
        if($file.Name -notin @('request.txt','ad2187d99e2d4728a6cc305e2332c1ce.identity.tmp')){throw 'Unexpected disposable fixture file'}
        Remove-Item -LiteralPath $file.FullName
    }
    [IO.Directory]::Delete($control,$false)
}
