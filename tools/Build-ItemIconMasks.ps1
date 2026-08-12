param(
    [string] $SourceDirectory = (Join-Path $PSScriptRoot '..\workshop assets\item-icon-references'),
    [string] $OutputDirectory = (Join-Path $PSScriptRoot '..\workshop assets\item-icons'),
    [int] $Threshold = 48
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$names = @(
    'bile', 'pipebomb', 'molotov', 'medkit', 'pills', 'defib',
    'explosive_ammo', 'incendiary_ammo', 'adrenaline'
)

foreach ($name in $names) {
    $sourcePath = Join-Path $SourceDirectory "$name-source.png"
    $outputPath = Join-Path $OutputDirectory "$name.png"

    $source = [System.Drawing.Bitmap]::FromFile($sourcePath)
    try {
        $mask = New-Object System.Drawing.Bitmap(
            $source.Width,
            $source.Height,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

        try {
            for ($y = 0; $y -lt $source.Height; $y++) {
                for ($x = 0; $x -lt $source.Width; $x++) {
                    $pixel = $source.GetPixel($x, $y)
                    $luminance = (0.2126 * $pixel.R) + (0.7152 * $pixel.G) + (0.0722 * $pixel.B)
                    $color = if ($luminance -ge $Threshold) {
                        [System.Drawing.Color]::FromArgb(255, 255, 255, 255)
                    }
                    else {
                        [System.Drawing.Color]::FromArgb(0, 0, 0, 0)
                    }

                    $mask.SetPixel($x, $y, $color)
                }
            }

            $mask.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $mask.Dispose()
        }
    }
    finally {
        $source.Dispose()
    }
}
