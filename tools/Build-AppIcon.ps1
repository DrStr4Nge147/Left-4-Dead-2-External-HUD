<#
.SYNOPSIS
    Generates the application icon, overlay-app/OverlayHud/Assets/OverlayHud.ico.

.DESCRIPTION
    The icon is the product: a stack of survivor health bars on the panel's own dark
    background, in the panel's own colours - full green, part amber, nearly empty red -
    so it reads as "the roster the built-in HUD has no room for".

    Drawn from code rather than from a supplied image on purpose. The item-icon masks
    lost their source screenshots and could not be regenerated; this one can always be
    rebuilt by running the script.

    Nothing here is derived from Valve artwork.

.NOTES
    Frames up to 128px are written as 32bpp BMP/DIB, and only 256 as PNG. GDI+ cannot
    decode a PNG-compressed frame through System.Drawing.Icon.ToBitmap, which is exactly
    how the tray icon is loaded - an all-PNG .ico looks correct in Explorer and throws at
    runtime. Verified by writing one and watching it fail.

    Re-run after changing any of the numbers below; the .ico is committed, so the build
    never depends on this script.
#>

[CmdletBinding()]
param(
    [string] $OutputPath = (Join-Path $PSScriptRoot '..\overlay-app\OverlayHud\Assets\OverlayHud.ico')
)

Add-Type -AssemblyName System.Drawing

$sizes = @(16, 24, 32, 48, 64, 128, 256)

# The overlay's own palette - see ViewModel/SurvivorCard.cs and MainWindow.xaml.
$background = [System.Drawing.Color]::FromArgb(255, 10, 13, 17)
$border     = [System.Drawing.Color]::FromArgb(255, 74, 86, 104)
$track      = [System.Drawing.Color]::FromArgb(255, 32, 37, 45)
$bars = @(
    @{ Fill = [System.Drawing.Color]::FromArgb(255, 76, 192, 76);  Portion = 1.00 },
    @{ Fill = [System.Drawing.Color]::FromArgb(255, 224, 168, 48); Portion = 0.62 },
    @{ Fill = [System.Drawing.Color]::FromArgb(255, 200, 60, 60);  Portion = 0.28 }
)

function New-RoundedPath {
    param([single] $X, [single] $Y, [single] $Width, [single] $Height, [single] $Radius)

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $Radius * 2

    if ($d -le 0) {
        $path.AddRectangle((New-Object System.Drawing.RectangleF($X, $Y, $Width, $Height)))
        return $path
    }

    $path.AddArc($X, $Y, $d, $d, 180, 90)
    $path.AddArc($X + $Width - $d, $Y, $d, $d, 270, 90)
    $path.AddArc($X + $Width - $d, $Y + $Height - $d, $d, $d, 0, 90)
    $path.AddArc($X, $Y + $Height - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    return $path
}

function New-IconFrame {
    param([int] $Size)

    $bitmap = New-Object System.Drawing.Bitmap($Size, $Size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # Panel plate.
    $inset  = [single]([Math]::Max(0.5, $Size * 0.03))
    $plateW = [single]($Size - $inset * 2)
    $radius = [single]($Size * 0.17)
    $plate  = New-RoundedPath -X $inset -Y $inset -Width $plateW -Height $plateW -Radius $radius

    $brush = New-Object System.Drawing.SolidBrush($background)
    $g.FillPath($brush, $plate)
    $brush.Dispose()

    # The border is a hairline at every size; below 32px it only muddies the edge.
    if ($Size -ge 32) {
        $pen = New-Object System.Drawing.Pen($border, [single]([Math]::Max(1, $Size * 0.02)))
        $g.DrawPath($pen, $plate)
        $pen.Dispose()
    }
    $plate.Dispose()

    # Three health bars, evenly spaced inside the plate.
    $marginX  = [single]($Size * 0.17)
    $barW     = [single]($Size - $marginX * 2)
    $barH     = [single]($Size * 0.135)
    $gap      = [single]($Size * 0.088)
    $stackH   = [single]($barH * 3 + $gap * 2)
    $y        = [single](($Size - $stackH) / 2)
    $barRadius = [single]([Math]::Min($barH / 2, $Size * 0.045))

    foreach ($bar in $bars) {
        $trackPath = New-RoundedPath -X $marginX -Y $y -Width $barW -Height $barH -Radius $barRadius
        $trackBrush = New-Object System.Drawing.SolidBrush($track)
        $g.FillPath($trackBrush, $trackPath)
        $trackBrush.Dispose()
        $trackPath.Dispose()

        $fillW = [single]($barW * $bar.Portion)
        if ($fillW -gt 0) {
            # Never let the rounded end exceed the fill itself at small sizes.
            $fillRadius = [single]([Math]::Min($barRadius, $fillW / 2))
            $fillPath = New-RoundedPath -X $marginX -Y $y -Width $fillW -Height $barH -Radius $fillRadius
            $fillBrush = New-Object System.Drawing.SolidBrush($bar.Fill)
            $g.FillPath($fillBrush, $fillPath)
            $fillBrush.Dispose()
            $fillPath.Dispose()
        }

        $y = [single]($y + $barH + $gap)
    }

    $g.Dispose()
    return $bitmap
}

# A 32bpp bottom-up DIB with the header the ICO format expects: doubled height, and a
# 1bpp AND mask that stays empty because transparency comes from the alpha channel.
function ConvertTo-IconDib {
    param([System.Drawing.Bitmap] $Bitmap)

    $size = $Bitmap.Width
    $rect = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
    $data = $Bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
                             [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    $pixels = New-Object byte[] ($data.Stride * $size)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixels, 0, $pixels.Length)
    $Bitmap.UnlockBits($data)

    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($stream)

    $writer.Write([uint32] 40)          # biSize
    $writer.Write([int32] $size)        # biWidth
    $writer.Write([int32] ($size * 2))  # biHeight: colour data plus the mask
    $writer.Write([uint16] 1)           # biPlanes
    $writer.Write([uint16] 32)          # biBitCount
    $writer.Write([uint32] 0)           # biCompression: BI_RGB
    $writer.Write([uint32] 0)           # biSizeImage: may be 0 for BI_RGB
    $writer.Write([int32] 0)            # biXPelsPerMeter
    $writer.Write([int32] 0)            # biYPelsPerMeter
    $writer.Write([uint32] 0)           # biClrUsed
    $writer.Write([uint32] 0)           # biClrImportant

    # Bottom-up rows.
    for ($y = $size - 1; $y -ge 0; $y--) {
        $writer.Write($pixels, $y * $data.Stride, $size * 4)
    }

    # AND mask: one bit per pixel, rows padded to 4 bytes, all zero.
    $maskStride = [int]([Math]::Floor(($size + 31) / 32) * 4)
    $writer.Write((New-Object byte[] ($maskStride * $size)))

    $writer.Flush()
    $bytes = $stream.ToArray()
    $writer.Dispose()
    $stream.Dispose()

    return $bytes
}

# Typed, and cast on the way in, because PowerShell unrolls a byte[] returned from a
# function into an Object[] of boxed bytes. Length still reads correctly, so the directory
# entries looked right while BinaryWriter.Write bound to a different overload and emitted
# one byte per frame - a 5 KB .ico whose header claimed 107 KB.
$frames = New-Object 'System.Collections.Generic.List[byte[]]'

foreach ($size in $sizes) {
    $bitmap = New-IconFrame -Size $size

    if ($size -ge 256) {
        $stream = New-Object System.IO.MemoryStream
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        $frames.Add([byte[]] $stream.ToArray())
        $stream.Dispose()
    }
    else {
        $frames.Add([byte[]] (ConvertTo-IconDib -Bitmap $bitmap))
    }

    $bitmap.Dispose()
}

$directory = Split-Path -Parent $OutputPath
if (-not (Test-Path $directory)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }

$file = [System.IO.File]::Create($OutputPath)
$writer = New-Object System.IO.BinaryWriter($file)

# ICONDIR: reserved, type 1 (icon), image count.
$writer.Write([uint16] 0)
$writer.Write([uint16] 1)
$writer.Write([uint16] $frames.Count)

$offset = 6 + 16 * $frames.Count

for ($i = 0; $i -lt $sizes.Count; $i++) {
    $size = $sizes[$i]
    $data = $frames[$i]

    # 256 is stored as 0 in a single byte - that is what the format says.
    $dimension = [byte]($(if ($size -ge 256) { 0 } else { $size }))

    $writer.Write($dimension)          # width
    $writer.Write($dimension)          # height
    $writer.Write([byte] 0)            # palette entries: none, this is true colour
    $writer.Write([byte] 0)            # reserved
    $writer.Write([uint16] 1)          # colour planes
    $writer.Write([uint16] 32)         # bits per pixel
    $writer.Write([uint32] $data.Length)
    $writer.Write([uint32] $offset)

    $offset += $data.Length
}

foreach ($data in $frames) { $writer.Write($data) }

$writer.Flush()
$writer.Dispose()
$file.Dispose()

$resolved = (Resolve-Path $OutputPath).Path
Write-Host "Wrote $resolved ($([System.IO.FileInfo]::new($resolved).Length) bytes, $($sizes.Count) sizes: $($sizes -join ', '))"
