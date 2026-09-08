<#
.SYNOPSIS
    Runs the production Squirrel classifier with isolated campaign/soldier fixtures.
.NOTES
    Requires an upstream Squirrel 3.x sq.exe. Does not load or modify the game.
#>
param([Parameter(Mandatory = $true)][string] $SqExe)
$ErrorActionPreference = 'Stop'
$source = Get-Content (Join-Path $PSScriptRoot '../overlay_hud_export/scripts/vscripts/overlay_hud_export.nut') -Raw
# Load all production definitions; suppress only the engine startup call.
if ($source -notmatch '(?m)^::OvlHud\.Boot\(\)\s*$') { throw 'Exporter startup boundary changed' }
$source = $source -replace '(?m)^::OvlHud\.Boot\(\)\s*$', ''
$fixture = Get-Content (Join-Path $PSScriptRoot 'tests/exporter-roster.nut') -Raw
$tempScript = Join-Path ([System.IO.Path]::GetTempPath()) ('overlay-roster-' + [guid]::NewGuid() + '.nut')
try {
    [System.IO.File]::WriteAllText($tempScript, '::printl <- function(s) { print(s + "\n") }' + "`n" + $source + "`n" + $fixture)
    $output = & $SqExe $tempScript 2>&1
    $exitCode = $LASTEXITCODE
    $output | Write-Output
    # sq.exe can return zero after a runtime exception; require the completion sentinel.
    if ($exitCode -ne 0 -or ($output -join "`n") -notmatch 'PASS: exporter roster fixtures') {
        throw 'Exporter roster regression failed'
    }
}
finally { Remove-Item -LiteralPath $tempScript -Force }
