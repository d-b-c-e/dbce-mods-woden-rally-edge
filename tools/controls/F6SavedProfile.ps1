# Schema-9 permits only the one recorded production checked Save.
function Assert-F6SavedProfile {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Witness,
          [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{32}$')][string]$Nonce,
          [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$StartupSha256,
          [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ActualSha256)
    $expected=$StartupSha256
    if(Test-Path -LiteralPath $Witness){
        $item=Get-Item -LiteralPath $Witness
        if($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -or $item.Length -gt 4096){throw 'Saved-profile witness shape refused.'}
        $text=[IO.File]::ReadAllText($item.FullName)
        $json=[Text.Json.JsonDocument]::Parse($text)
        try {
            if($json.RootElement.ValueKind -ne [Text.Json.JsonValueKind]::Object){throw 'Saved-profile witness must be an object.'}
            $names=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
            foreach($property in $json.RootElement.EnumerateObject()){if(!$names.Add($property.Name)){throw 'Repeated saved-profile witness property.'}}
            if($json.RootElement.GetProperty('schema').ValueKind -ne [Text.Json.JsonValueKind]::Number -or $json.RootElement.GetProperty('schema').GetInt32() -ne 1){throw 'Saved-profile witness schema differs.'}
            foreach($name in @('nonce','stepId','startupSha256','beforeSha256','savedSha256')){
                if($json.RootElement.GetProperty($name).ValueKind -ne [Text.Json.JsonValueKind]::String){throw 'Saved-profile witness field type differs.'}
            }
        }finally{$json.Dispose()}
        $saved=$text|ConvertFrom-Json
        if($saved.schema -ne 1 -or $saved.nonce -cne $Nonce -or $saved.startupSha256 -ine $StartupSha256 -or
           $saved.beforeSha256 -ine $StartupSha256 -or $saved.savedSha256 -notmatch '^[A-Fa-f0-9]{64}$' -or
           $saved.stepId -notmatch '^[a-f0-9]{32}$'){throw 'Explicit checked-save witness identity differs.'}
        $expected=$saved.savedSha256
    }
    if($ActualSha256 -ine $expected){throw 'Runtime Controls differs from startup or the one witnessed explicit Save.'}
    return $expected
}
