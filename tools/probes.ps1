<#
.SYNOPSIS
  Run several probes back to back (each a separate game launch) and print a summary table.
.EXAMPLE
  tools\probes.ps1 e3-attack-sizing, e5-undefended-mines, e9-jupiter-plateau
.EXAMPLE
  tools\probes.ps1 -All
#>
param([string[]]$Probes, [switch]$All)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\paths.ps1"
if ($All) { $Probes = Get-ChildItem (Join-Path $ProjectRoot 'lab\probes') -Filter *.json | Where-Object Name -ne 'smoke.json' | ForEach-Object BaseName }
$summary = @()
$first = $true
foreach ($p in $Probes) {
    $t0 = Get-Date
    if ($first) { & (Join-Path $PSScriptRoot 'run.ps1') -Probe $p; $first = $false }
    else { & (Join-Path $PSScriptRoot 'run.ps1') -Probe $p -NoDeploy }
    $code = $LASTEXITCODE
    $result = if ($code -eq 0) { 'PASS' } elseif ($code -eq 2) { 'FAIL' } else { "ERROR($code)" }  # PS 5.1: no ??
    $summary += [pscustomobject]@{ probe = $p; result = $result; minutes = [Math]::Round(((Get-Date) - $t0).TotalMinutes, 1) }
}
Write-Host "`n=== probe summary ==="
$summary | Format-Table -AutoSize | Out-String | Write-Host
