# File-only fixture for the production runner's one additional setting edit.
# Does not invoke the runner, Steam, registry, lease or devices.
$ErrorActionPreference='Stop'
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile("$PSScriptRoot/Run-Controls.ps1",[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Runner does not parse.'}
$fn=$ast.FindAll({param($a) $a -is [Management.Automation.Language.FunctionDefinitionAst] -and $a.Name -eq 'Set-ShimBackend'},$true)
if($fn.Count -ne 1){throw 'Backend staging seam differs.'}
. ([scriptblock]::Create($fn[0].Extent.Text))
$folder=Join-Path ([IO.Path]::GetTempPath()) ('woden-shim-staging-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($folder)|Out-Null
$cfg=Join-Path $folder 'candidate.cfg';$checks=0
function Check([bool]$Ok,[string]$Why){$script:checks++;if(!$Ok){throw $Why}}
foreach($case in @('absent','native','shim','duplicate-section','duplicate-key','missing-key')) {
    $owner="[ForceFeedback]`r`nEnabled = true`r`nStrengthPercent = 49.583332`r`n`r`n[Triple]`r`nEnabled = true`r`n"
    $suffix=switch($case) {
        absent {''}
        native {"`r`n[Input]`r`n# retain this comment`r`nBackend = Native`r`n"}
        shim {"`r`n[Input]`r`nBackend = Shim`r`n"}
        duplicate-section {"`r`n[Input]`r`nBackend = Native`r`n[Input]`r`nBackend = Native`r`n"}
        duplicate-key {"`r`n[Input]`r`nBackend = Native`r`nBackend = Shim`r`n"}
        missing-key {"`r`n[Input]`r`nUnknown = Native`r`n"}
    }
    [IO.File]::WriteAllText($cfg,$owner+$suffix)
    $before=[IO.File]::ReadAllBytes($cfg);$failed=$false
    try {Set-ShimBackend}catch{$failed=$true}
    $refuse=$case -in @('duplicate-section','duplicate-key','missing-key')
    Check ($failed -eq $refuse) "Wrong admission ($case)"
    if($refuse){Check ([Convert]::ToBase64String([IO.File]::ReadAllBytes($cfg)) -ceq [Convert]::ToBase64String($before)) "Refusal changed bytes ($case)"}
    else {
        $after=[IO.File]::ReadAllText($cfg)
        Check ($after.StartsWith($owner,[StringComparison]::Ordinal)) "Force/triple values changed ($case)"
        Check ([regex]::Matches($after,'(?m)^Backend = Shim\r?$').Count -eq 1) "Backend not staged exactly ($case)"
        if($case -eq 'native'){Check ($after.Contains('# retain this comment')) 'Comment lost'}
    }
}
"PASS: $checks production backend staging checks; isolated evidence $folder"
