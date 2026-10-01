function Expand-WodenDeliveryZip {
 [CmdletBinding()]param([Parameter(Mandatory)][string]$Archive,[Parameter(Mandatory)][string]$Destination)
 $ErrorActionPreference='Stop';Add-Type -AssemblyName System.IO.Compression.FileSystem
 if(Test-Path -LiteralPath $Destination){throw 'Extraction requires a new directory'}
 $expected=@(Get-WodenDeliveryFiles)+@('manifest.json')
 $zip=[IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($Archive))
 try {
  $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
  if($zip.Entries.Count -ne $expected.Count){throw 'ZIP allowlist count differs'}
  foreach($entry in $zip.Entries){$name=$entry.FullName
   if($name -cnotin $expected -or -not $seen.Add($name) -or $name -match '[\\:]|^/' -or @($name.Split('/')|Where-Object {$_ -in @('','.','..')}).Count){throw 'Unsafe/duplicate/unlisted ZIP entry'}
   if((($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000){throw 'ZIP symbolic link refused'}
  }
  $target=[IO.Path]::GetFullPath($Destination).TrimEnd('\','/')
  $walk=$target;while($walk){if((Test-Path -LiteralPath $walk) -and ((Get-Item $walk -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Linked extraction parent'};$walk=Split-Path $walk -Parent}
  $null=New-Item -ItemType Directory -Path $target
  foreach($entry in $zip.Entries){$file=[IO.Path]::GetFullPath((Join-Path $target $entry.FullName));if(-not $file.StartsWith($target+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'ZIP escape'}
   $null=New-Item -ItemType Directory -Path (Split-Path $file -Parent) -Force
   $input=$entry.Open();try{$output=[IO.File]::Open($file,[IO.FileMode]::CreateNew);try{$input.CopyTo($output)}finally{$output.Dispose()}}finally{$input.Dispose()}
  }
 } finally {$zip.Dispose()}
 return $target
}
