<#
.SYNOPSIS
  Chapter playtest: play the game copy yourself with the mod, isolated from your real saves.
.DESCRIPTION
  - Deploys the plugin to the game copy (never the Steam install).
  - Uses C:\Games\TI-ImprovedAI-playtest\saves as the game's save folder for this launch: your Documents saves,
    profile and autosaves are never read or written. Saves you make while playtesting (and their .improvedai.json
    sidecars) stay in that folder and are there again next time.
  - Copies the chosen fixture save in (a copy; the original is untouched) and loads it, or opens the main menu.
  - Diagnostics (journal, metrics, AI log) go to lab\results\<stamp>-playtest-<name>\<one folder per loaded game>.
  - Blocks until you quit the game, then removes the launch request.
.EXAMPLE
  tools\playtest.ps1 -Fixture accel-2031-hot-war
.EXAMPLE
  tools\playtest.ps1 -Fixture accel-2034-fusion-drives -Doctrine "focus=Raider weapons=Missiles"
.EXAMPLE
  tools\playtest.ps1                      # main menu; load any save already in the playtest folder
.EXAMPLE
  tools\playtest.ps1 -Fixture accel-2031-hot-war -Vanilla   # the same chapter without the mod's AI changes
#>
param(
    [string]$Fixture,
    # Any save to start from (copied in, with its sidecar if present).
    [string]$SavePath,
    # Force the alien doctrine for this launch: "focus=Raider weapons=Missiles hulls=Swarm engagement=Artillery".
    [string]$Doctrine,
    # Harness/terminal commands once the campaign is ready, e.g. "ai.terminus 0.995" (alien victory at the next month).
    [string[]]$Commands,
    [string]$Name,
    [switch]$Vanilla,
    [switch]$NoDeploy,
    # The strategist directs every non-player faction (docs/STRATEGIST.md): enables it for this launch and runs the
    # local watcher (tools\strategist.ps1) alongside the game; the game pauses at each 3-month checkpoint.
    [switch]$Strategist,
    [string]$StrategistModel = 'opus'
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\paths.ps1"
$PlaytestDir = if ($env:TI_AI_PLAYTEST) { $env:TI_AI_PLAYTEST } else { 'C:\Games\TI-ImprovedAI-playtest' }
$savesDir = Join-Path $PlaytestDir 'saves'
New-Item -ItemType Directory -Force $savesDir | Out-Null

$src = $null
if ($Fixture) {
    $fixtures = Get-Content (Join-Path $ProjectRoot 'lab\fixtures.json') -Raw | ConvertFrom-Json
    $fx = $fixtures.$Fixture
    if (-not $fx) { throw "Unknown fixture '$Fixture' (see lab\fixtures.json)" }
    $src = Join-Path $UserSaves $fx.file
    if (-not $Name) { $Name = $Fixture }
} elseif ($SavePath) {
    $src = (Resolve-Path $SavePath).Path
    if (-not $Name) { $Name = [IO.Path]::GetFileNameWithoutExtension($src) }
}
if (-not $Name) { $Name = 'menu' }
if ($Vanilla) { $Name = "$Name-vanilla" }

$save = ''
if ($src) {
    if (-not (Test-Path $src)) { throw "Save missing: $src" }
    $save = Join-Path $savesDir ([IO.Path]::GetFileName($src))
    if (-not (Test-Path $save)) {
        Copy-Item $src $save
        if (Test-Path "$src.improvedai.json") { Copy-Item "$src.improvedai.json" "$save.improvedai.json" }
        Write-Host "Copied $([IO.Path]::GetFileName($src)) into the playtest saves."
    } else { Write-Host "Using the playtest copy of $([IO.Path]::GetFileName($src)) (delete it from $savesDir for a fresh copy)." }
}
# Your own display/audio options, copied once; changes you make while playtesting stay in the playtest profile.
$profileSrc = Join-Path $UserSaves 'PlayerOptions.TIProfile'
$profileDst = Join-Path $savesDir 'PlayerOptions.TIProfile'
if ((Test-Path $profileSrc) -and -not (Test-Path $profileDst)) { Copy-Item $profileSrc $profileDst }

# One game-copy session at a time (shared with the harness).
New-Item -ItemType Directory -Force $HarnessDir | Out-Null
$lock = $null
try { $lock = [IO.File]::Open((Join-Path $HarnessDir 'harness.lock'), 'OpenOrCreate', 'ReadWrite', 'None') }
catch { throw 'A harness run is using the game copy; wait for it to finish.' }

$runFile = Join-Path $PluginDir 'run.json'
try {
    $ours = { Get-Process TerraInvicta -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($GameDir, 'OrdinalIgnoreCase') } }
    if (& $ours) { throw "The game copy at $GameDir is already running; close it first." }
    if (-not $NoDeploy) { & (Join-Path $PSScriptRoot 'deploy.ps1') }

    $outDir = Join-Path $ResultsDir ("{0}-playtest-{1}" -f (Get-Date -Format 'yyyyMMdd-HHmmss'), $Name)
    New-Item -ItemType Directory -Force $outDir | Out-Null
    $run = [ordered]@{
        name = "playtest-$Name"; interactive = $true; save = $save; outDir = $outDir; savesDir = $savesDir
        quitOnFinish = $false; allowSaves = $true; commands = @()
    }
    if ($Doctrine) { $run.commands = @("ai.doctrine $Doctrine") }
    if ($Commands) { $run.commands = @($run.commands) + @($Commands) }
    if ($Vanilla) { $run.config = @{ General = @{ VanillaAI = 'true' } } }
    $watcher = $null
    if ($Strategist -and -not $Vanilla) {
        $stratDir = Join-Path $outDir 'strategist'
        $run.config = @{ Strategist = @{ Enabled = 'true'; Folder = $stratDir } }
        $watcher = Start-Process -FilePath 'powershell.exe' -PassThru -WindowStyle Minimized -ArgumentList @(
            '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'strategist.ps1'), '-Folder', $stratDir, '-Model', $StrategistModel)
        Write-Host "Strategist on: watcher running (minimized window), exchange folder $stratDir"
    }
    New-Item -ItemType Directory -Force $PluginDir | Out-Null
    $run | ConvertTo-Json -Depth 6 | Set-Content -Path $runFile -Encoding utf8

    Write-Host "Playtest '$Name'. Saves: $savesDir   Diagnostics: $outDir"
    Write-Host 'Quit the game normally when done; this window waits for it.'
    $p = Start-Process -FilePath (Join-Path $GameDir 'TerraInvicta.exe') -WorkingDirectory $GameDir -PassThru
    $p.WaitForExit()
}
finally {
    if ($watcher -and -not $watcher.HasExited) { Stop-Process -Id $watcher.Id -Force -ErrorAction SilentlyContinue }
    Remove-Item $runFile -ErrorAction SilentlyContinue
    if ($lock) { $lock.Dispose() }
}
if (-not $outDir) { return }
Copy-Item (Join-Path $GameDir 'BepInEx\LogOutput.log') (Join-Path $outDir 'bepinex.log') -ErrorAction SilentlyContinue
Write-Host "Done. Story of the session: python -I tools\summarize.py `"<one of the folders in $outDir>`""
