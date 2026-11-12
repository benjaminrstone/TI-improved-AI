<#
.SYNOPSIS
  Install the current build of the plugin into the play install (the user's campaign).
.DESCRIPTION
  The play install is a second copy of the game with its own plugin build, so development and harness runs on the
  game copy never change what the campaign runs. The first use creates it by copying the game copy (about 27 GB).
  Release only a commit that passed the regression check (tools\probes.ps1 flow-2034, flow-2039): the commit is
  recorded in the install's build.txt. Saves and sidecars of a campaign in progress are kept and stay compatible.
#>
param(
    # Release a working tree with uncommitted changes under src\ (the build is then marked "+uncommitted").
    [switch]$AllowDirty
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\paths.ps1"
if (& $Playing) { throw 'The play install is running; close the game first.' }

$dirty = [bool](& git -C $ProjectRoot status --porcelain -- src)
if ($dirty -and -not $AllowDirty) { throw 'Uncommitted changes under src\. Commit first (or -AllowDirty).' }
$commit = (& git -C $ProjectRoot rev-parse --short HEAD) + $(if ($dirty) { '+uncommitted' } else { '' })

if (-not (Test-Path (Join-Path $PlayDir 'TerraInvicta.exe'))) {
    if (-not (Test-Path (Join-Path $GameDir 'BepInEx\core\BepInEx.dll'))) { throw "No game copy with BepInEx at $GameDir to create the play install from." }
    Write-Host "Creating the play install at $PlayDir from $GameDir (about 27 GB)..."
    & robocopy $GameDir $PlayDir /E /MT:8 /NFL /NDL /NP /NJH /XD (Join-Path $GameDir 'BepInEx\plugins\ImprovedAI') (Join-Path $GameDir 'BepInEx\cache') (Join-Path $GameDir 'BepInEx\ImprovedAI-strategist') (Join-Path $GameDir 'BepInEx\ImprovedAI-logs') /XF LogOutput.log com.improvedai.terrainvicta.cfg
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }
}

$log = Join-Path $env:TEMP 'improvedai-build.log'
& dotnet build (Join-Path $ProjectRoot 'src\ImprovedAI\ImprovedAI.csproj') -c Release -p:GameDir="$GameDir" -nologo -v q *> $log
if ($LASTEXITCODE -ne 0) { Get-Content $log | Select-String 'error' | Select-Object -First 20 | ForEach-Object Line; throw 'Build failed' }
New-Item -ItemType Directory -Force $PlayPlugin | Out-Null
$bin = Join-Path $ProjectRoot 'src\ImprovedAI\bin\Release'
Copy-Item (Join-Path $bin 'ImprovedAI.dll') $PlayPlugin -Force
if (Test-Path (Join-Path $bin 'ImprovedAI.pdb')) { Copy-Item (Join-Path $bin 'ImprovedAI.pdb') $PlayPlugin -Force }
# BepInEx never updates a value already in the .cfg; the install's settings come from play.ps1's options.
Remove-Item (Join-Path $PlayDir 'BepInEx\config\com.improvedai.terrainvicta.cfg') -ErrorAction SilentlyContinue
Set-Content -Path (Join-Path $PlayPlugin 'build.txt') -Encoding utf8 -Value @($commit, (Get-Date -Format 'yyyy-MM-dd HH:mm'))
& (Join-Path $PSScriptRoot 'play.ps1') -NoLaunch
Write-Host "Released $commit -> $PlayPlugin"
