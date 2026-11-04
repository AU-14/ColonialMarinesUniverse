[CmdletBinding()]
param(
    [string] $Log,
    [double] $MaximumMeanMs = 33.34,
    [double] $MaximumP95Ms = 50
)

$ErrorActionPreference = 'Stop'
if (-not $Log) { $Log = Join-Path $PSScriptRoot '../../bin/FighterTest/Pilot.log' }
$fighterWindows = @(Get-Content -LiteralPath $Log | ForEach-Object {
    if ($_ -match 'Fighter performance phase=Pass, frames=(\d+), mean_ms=([\d.]+), p95_ms=([\d.]+), fps=([\d.]+), over_33ms_pct=([\d.]+)') {
        [pscustomobject]@{
            Frames = [int] $Matches[1]
            MeanMs = [double]::Parse($Matches[2], [Globalization.CultureInfo]::InvariantCulture)
            P95Ms = [double]::Parse($Matches[3], [Globalization.CultureInfo]::InvariantCulture)
            Fps = [double]::Parse($Matches[4], [Globalization.CultureInfo]::InvariantCulture)
            SlowPercent = [double]::Parse($Matches[5], [Globalization.CultureInfo]::InvariantCulture)
        }
    }
})
if ($fighterWindows.Count -eq 0) { throw 'No complete ten-second pass measurements found.' }
$fighterWindows | Format-Table
if ($fighterWindows.Where({ $_.MeanMs -gt $MaximumMeanMs -or $_.P95Ms -gt $MaximumP95Ms }).Count -gt 0) {
    throw "Fighter pass exceeded frame budget: mean $MaximumMeanMs ms, p95 $MaximumP95Ms ms."
}
Write-Output 'Fighter pass frame budget passed.'
