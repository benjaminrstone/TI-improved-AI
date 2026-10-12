<#
.SYNOPSIS
  Build the ImprovedAI plugin and deploy it into this project's game copy (never the Steam install).
#>
param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\paths.ps1"

if (-not (Test-Path (Join-Path $GameDir 'TerraInvicta.exe'))) { throw "Game copy not found at $GameDir" }
if (-not (Test-Path (Join-Path $GameDir 'BepInEx\core\BepInEx.dll'))) { throw "BepInEx 5 is not installed in $GameDir" }
if ($GameDir -like '*steamapps*') { throw "Refusing to deploy into the Steam install ($GameDir); this project uses its own copy." }

$proj = Join-Path $ProjectRoot 'src\ImprovedAI\ImprovedAI.csproj'
if (-not $NoBuild) {
    $log = Join-Path $env:TEMP 'improvedai-build.log'
    & dotnet build $proj -c Release -p:GameDir="$GameDir" -nologo -v q *> $log
    if ($LASTEXITCODE -ne 0) { Get-Content $log | Select-String 'error' | Select-Object -First 20 | ForEach-Object Line; throw 'Build failed' }
}
New-Item -ItemType Directory -Force $PluginDir | Out-Null
$bin = Join-Path $ProjectRoot 'src\ImprovedAI\bin\Release'
Copy-Item (Join-Path $bin 'ImprovedAI.dll') $PluginDir -Force
if (Test-Path (Join-Path $bin 'ImprovedAI.pdb')) { Copy-Item (Join-Path $bin 'ImprovedAI.pdb') $PluginDir -Force }
Write-Host "Deployed plugin -> $PluginDir"
