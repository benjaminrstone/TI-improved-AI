# Shared paths for this project's tools. Dot-source: . "$PSScriptRoot\paths.ps1"
# The game copy and harness scratch live outside OneDrive (31 GB install; 100 MB saves).
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$GameDir     = if ($env:TI_AI_DIR) { $env:TI_AI_DIR } else { 'C:\Games\TI-ImprovedAI' }
$HarnessDir  = if ($env:TI_AI_HARNESS) { $env:TI_AI_HARNESS } else { 'C:\Games\TI-ImprovedAI-harness' }
$UserSaves   = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'My Games\TerraInvicta\Saves'
$PluginDir   = Join-Path $GameDir 'BepInEx\plugins\ImprovedAI'
$ResultsDir  = Join-Path $ProjectRoot 'lab\results'
