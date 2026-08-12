<#
.SYNOPSIS
    Publishes the overlay app as a single .exe for a GitHub release.

.DESCRIPTION
    The ordinary build in overlay-app/dist is a launcher stub plus a DLL and two JSON
    files, and it needs the .NET Desktop Runtime installed. None of that survives being
    copied on its own, so a release needs a different publish.

    Two shapes, both a single file:

      Standalone   self-contained, no runtime to install, large. What to hand to someone
                   who just wants to run the thing.
      Compact      framework-dependent, tiny, refuses to start without the .NET Desktop
                   Runtime. Offer it as the alternative, never as the only download.

    Output goes to version output/, which is gitignored - release artifacts are attached
    to a release, not committed.

.NOTES
    The exe is unsigned, so SmartScreen will warn on first run. Only a code-signing
    certificate fixes that; the warning is not a sign of a broken build.
#>

[CmdletBinding()]
param(
    [ValidateSet('Standalone', 'Compact', 'Both')]
    [string] $Shape = 'Both',

    [string] $OutputRoot = (Join-Path $PSScriptRoot '..\version output')
)

$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot '..\overlay-app\OverlayHud\OverlayHud.csproj'
$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Select-Object -First 1

function Publish-Shape {
    param([string] $Name, [bool] $SelfContained)

    $target = Join-Path $OutputRoot "$Name-v$version"
    if (Test-Path $target) { Remove-Item $target -Recurse -Force }

    $arguments = @(
        'publish', $project,
        '-c', 'Release',
        '-r', 'win-x64',
        '--self-contained', $SelfContained.ToString().ToLower(),
        '-o', $target,
        '-p:PublishSingleFile=true',
        # WPF ships native libraries; without this they land beside the exe and the
        # "single file" is not one.
        '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-p:DebugType=none',
        '-v', 'quiet', '--nologo'
    )

    # Compression is self-contained only, and roughly halves it.
    if ($SelfContained) { $arguments += '-p:EnableCompressionInSingleFile=true' }

    dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "publish failed for $Name" }

    # config.json is deliberately not shipped: AppConfig falls back to defaults and writes
    # the file beside the exe on the first Save & Apply. One file to download, and no
    # stale settings overwriting someone's tuned layout on update.
    Get-ChildItem $target -Exclude '*.exe' | Remove-Item -Recurse -Force

    $exe = Get-ChildItem $target -Filter *.exe | Select-Object -First 1
    [pscustomobject]@{
        Shape = $Name
        File  = $exe.FullName
        SizeMB = [Math]::Round($exe.Length / 1MB, 1)
        NeedsRuntime = -not $SelfContained
    }
}

$results = @()
if ($Shape -in 'Standalone', 'Both') { $results += Publish-Shape -Name 'Standalone' -SelfContained $true }
if ($Shape -in 'Compact', 'Both')    { $results += Publish-Shape -Name 'Compact'    -SelfContained $false }

$results | Format-Table -AutoSize

Write-Host "Release assets also need: compiled vpks\overlay_hud_export_v$version.vpk"
Write-Host "The addon half is not optional - the app draws nothing without it."
