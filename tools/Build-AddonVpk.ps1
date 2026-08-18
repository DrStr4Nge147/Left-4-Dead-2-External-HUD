<#
.SYNOPSIS
    Packs overlay_hud_export/ into compiled vpks/overlay_hud_export_v<version>.vpk.

.DESCRIPTION
    VPK format version 1, single file, contents embedded after the directory tree. The
    target L4D2 build rejects VPK v2 with "Unknown version 2", and the vpk.exe shipped with
    the game writes v2, so the pack is written here instead of shelled out.

    The version comes from addoninfo.txt's addonversion, which is the one place it is
    authored; the output name follows it.

.NOTES
    Every file under the source tree is packed. Keep editable working files - PSDs, notes -
    out of that folder rather than filtering them here.
#>

[CmdletBinding()]
param(
    [string] $Source = (Join-Path $PSScriptRoot '..\overlay_hud_export'),
    [string] $OutputRoot = (Join-Path $PSScriptRoot '..\compiled vpks'),
    [switch] $Force
)

$ErrorActionPreference = 'Stop'

$Source = (Resolve-Path $Source).Path
$addonInfo = Join-Path $Source 'addoninfo.txt'
if (-not (Test-Path $addonInfo)) { throw "No addoninfo.txt under $Source" }

$match = [regex]::Match((Get-Content $addonInfo -Raw), 'addonversion\s+"([^"]+)"')
if (-not $match.Success) { throw "addoninfo.txt carries no addonversion" }
$version = $match.Groups[1].Value

$target = Join-Path $OutputRoot "overlay_hud_export_v$version.vpk"
if ((Test-Path $target) -and -not $Force) {
    Write-Host "Overwriting $target"
}

# CRC32, which the VPK directory carries per file. The game does not verify it on mount,
# but a pack whose CRCs are wrong is a pack no other tool will trust.
# Written in [long] and masked back to 32 bits: Windows PowerShell parses a hex literal
# wider than Int32 as a negative Int32, so the polynomial has to be a decimal constant and
# the intermediates cannot be [uint32] without underflowing.
$polynomial = 3988292384      # 0xEDB88320
$mask = 4294967295            # 0xFFFFFFFF

$crcTable = New-Object 'System.UInt32[]' 256
for ($i = 0; $i -lt 256; $i++) {
    $c = [long] $i
    for ($k = 0; $k -lt 8; $k++) {
        if ($c -band 1) { $c = $polynomial -bxor ($c -shr 1) }
        else            { $c = $c -shr 1 }
        $c = $c -band $mask
    }
    $crcTable[$i] = [uint32] $c
}

function Get-Crc32 {
    param([byte[]] $Bytes)

    $crc = [long] 4294967295
    foreach ($b in $Bytes) {
        $index = [int] (($crc -bxor $b) -band 0xFF)
        $crc = ([long] $crcTable[$index] -bxor ($crc -shr 8)) -band 4294967295
    }
    return [uint32] ($crc -bxor 4294967295)
}

# Grouped the way a VPK directory is: extension, then path, then file. A file with no
# extension or at the root uses the single space Source packs use for "none".
$entries = @()
foreach ($file in Get-ChildItem $Source -Recurse -File) {
    $relative = $file.FullName.Substring($Source.Length + 1) -replace '\\', '/'
    $extension = if ($file.Extension) { $file.Extension.TrimStart('.').ToLowerInvariant() } else { ' ' }
    $directory = if ($relative.Contains('/')) { $relative.Substring(0, $relative.LastIndexOf('/')) } else { ' ' }
    $name = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)

    $entries += [pscustomobject]@{
        Extension = $extension
        Directory = $directory
        Name      = $name
        Relative  = $relative
        Bytes     = [System.IO.File]::ReadAllBytes($file.FullName)
    }
}

if ($entries.Count -eq 0) { throw "No files under $Source" }

# Data offsets are relative to the end of the tree, so the layout is decided before the
# tree is written: one pass to place the bytes, a second to write the directory.
$offset = [uint32] 0
foreach ($entry in $entries) {
    $entry | Add-Member -NotePropertyName Offset -NotePropertyValue $offset
    $entry | Add-Member -NotePropertyName Crc -NotePropertyValue (Get-Crc32 $entry.Bytes)
    $offset += [uint32] $entry.Bytes.Length
}

$tree = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($tree, [System.Text.Encoding]::ASCII)

function Write-Cstring {
    param([System.IO.BinaryWriter] $Writer, [string] $Value)

    $Writer.Write([System.Text.Encoding]::ASCII.GetBytes($Value))
    $Writer.Write([byte] 0)
}

foreach ($extensionGroup in $entries | Group-Object Extension) {
    Write-Cstring $writer $extensionGroup.Name

    foreach ($directoryGroup in $extensionGroup.Group | Group-Object Directory) {
        Write-Cstring $writer $directoryGroup.Name

        foreach ($entry in $directoryGroup.Group) {
            Write-Cstring $writer $entry.Name
            $writer.Write([uint32] $entry.Crc)
            $writer.Write([uint16] 0)        # no preload bytes
            $writer.Write([uint16] 0x7FFF)   # contents live in this file, after the tree
            $writer.Write([uint32] $entry.Offset)
            $writer.Write([uint32] $entry.Bytes.Length)
            $writer.Write([uint16] 0xFFFF)   # terminator
        }

        $writer.Write([byte] 0)              # end of files in this path
    }

    $writer.Write([byte] 0)                  # end of paths for this extension
}

$writer.Write([byte] 0)                      # end of extensions
$writer.Flush()
$treeBytes = $tree.ToArray()

if (-not (Test-Path $OutputRoot)) { New-Item -ItemType Directory -Path $OutputRoot | Out-Null }

$output = [System.IO.File]::Create($target)
try {
    $header = New-Object System.IO.BinaryWriter($output, [System.Text.Encoding]::ASCII, $true)
    $header.Write([uint32] 0x55AA1234)
    $header.Write([uint32] 1)                # format version 1
    $header.Write([uint32] $treeBytes.Length)
    $header.Flush()

    $output.Write($treeBytes, 0, $treeBytes.Length)
    foreach ($entry in $entries) { $output.Write($entry.Bytes, 0, $entry.Bytes.Length) }
}
finally {
    $output.Dispose()
}

Write-Host "wrote $target"
foreach ($entry in $entries) {
    Write-Host ("  {0,-44} {1,7} bytes" -f $entry.Relative, $entry.Bytes.Length)
}
