<#
.SYNOPSIS
  Run several probes back to back (each a separate game launch) and print a summary table.
  A run that throws is recorded and the batch goes on.
.EXAMPLE
  tools\probes.ps1 flow-2034, flow-2039
.EXAMPLE
  tools\probes.ps1 flow-2034-ai -Runs 3 -WithVanilla     # repeated, each also as a vanilla baseline
#>
param(
    [Parameter(Mandatory)] [string[]]$Probes,
    [int]$Runs = 1,
    # Also run each probe with every AI change off, for comparison (aggregate.py groups by the -vanilla suffix).
    [switch]$WithVanilla
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\paths.ps1"
$summary = @()
$first = $true
for ($i = 1; $i -le $Runs; $i++) {
    foreach ($p in $Probes) {
        foreach ($vanilla in @($false, $true)) {
            if ($vanilla -and -not $WithVanilla) { continue }
            $a = @{ Probe = $p }
            if ($vanilla) { $a.Vanilla = $true }
            if (-not $first) { $a.NoDeploy = $true }
            $first = $false
            $t0 = Get-Date
            try {
                & (Join-Path $PSScriptRoot 'run.ps1') @a
                $code = $LASTEXITCODE
                $result = if ($code -eq 0) { 'PASS' } elseif ($code -eq 2) { 'FAIL' } else { "ERROR($code)" }
            } catch {
                $result = "THREW: $($_.Exception.Message)"
            }
            $name = if ($vanilla) { "$p-vanilla" } else { $p }
            $summary += [pscustomobject]@{ probe = $name; run = $i; result = $result; minutes = [Math]::Round(((Get-Date) - $t0).TotalMinutes, 1) }
        }
    }
}
Write-Host "`n=== probe summary ==="
$summary | Format-Table -AutoSize | Out-String | Write-Host
