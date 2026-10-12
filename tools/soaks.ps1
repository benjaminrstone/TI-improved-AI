<#
.SYNOPSIS
  Repeated soaks for balance comparisons: N runs of each variant (vanilla and/or mod) from one fixture.
  Runs aren't deterministic (clock-seeded RNG, threaded planning), so compare means/ranges, not single runs.
.EXAMPLE
  tools\soaks.ps1 -Fixture accel-2031-hot-war -Days 365 -Runs 3
#>
param([Parameter(Mandatory)] [string]$Fixture, [double]$Days = 365, [int]$Runs = 3, [switch]$ModOnly, [switch]$VanillaOnly, [string]$Tag = 'soak')
$ErrorActionPreference = 'Stop'
$r = Join-Path $PSScriptRoot 'run.ps1'
$first = $true
for ($i = 1; $i -le $Runs; $i++) {
    foreach ($vanilla in @($true, $false)) {
        if ($vanilla -and $ModOnly) { continue }
        if (-not $vanilla -and $VanillaOnly) { continue }
        $a = @{ Fixture = $Fixture; Days = $Days; Name = "$Tag-$Fixture-r$i"; TimeoutMinutes = [int]($Days / 10 + 20) }
        if ($vanilla) { $a.Vanilla = $true }
        if (-not $first) { $a.NoDeploy = $true }
        $first = $false
        Write-Host "### run $i vanilla=$vanilla"
        & $r @a 2>&1 | Select-String -Pattern '^(Wall|WARNING|Restored|No results)' | ForEach-Object Line
    }
}
