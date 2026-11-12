# Shared paths for this project's tools. Dot-source: . "$PSScriptRoot\paths.ps1"
# The game copy and harness scratch live outside OneDrive (31 GB install; 100 MB saves).
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$GameDir     = if ($env:TI_AI_DIR) { $env:TI_AI_DIR } else { 'C:\Games\TI-ImprovedAI' }
$HarnessDir  = if ($env:TI_AI_HARNESS) { $env:TI_AI_HARNESS } else { 'C:\Games\TI-ImprovedAI-harness' }
$UserSaves   = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'My Games\TerraInvicta\Saves'
$PluginDir   = Join-Path $GameDir 'BepInEx\plugins\ImprovedAI'
$ResultsDir  = Join-Path $ProjectRoot 'lab\results'
# The play install: the user's own campaign. A second game copy with a pinned plugin build (release.ps1 installs it,
# play.ps1 launches it). Harness runs and deploys never touch it. Its saves, session diagnostics, strategist exchange
# and remembered options are in $PlayData.
$PlayDir     = if ($env:TI_AI_PLAY) { $env:TI_AI_PLAY } else { 'C:\Games\TI-ImprovedAI-play' }
$PlayData    = "$PlayDir-data"
$PlayPlugin  = Join-Path $PlayDir 'BepInEx\plugins\ImprovedAI'
foreach ($d in $GameDir, $PlayDir) { if ($d -like '*steamapps*') { throw "Refusing to use the Steam install ($d); this project runs on its own game copies." } }
# True while the play install's game is open.
$Playing = { [bool](Get-Process TerraInvicta -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($PlayDir + '\', 'OrdinalIgnoreCase') }) }
