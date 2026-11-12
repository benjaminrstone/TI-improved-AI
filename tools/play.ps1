<#
.SYNOPSIS
  Play the campaign: launch the play install (its own game copy, save folder and pinned mod build).
.DESCRIPTION
  - The game never reads or writes your Documents saves: saves and the mod's sidecars are in
    C:\Games\TI-ImprovedAI-play-data\saves, and are there again next time.
  - Options are remembered between launches (options.json in the data folder). Give one only to change it.
  - With the strategist on, its watcher runs alongside the game and is restarted if it stops. The game pauses for a
    few seconds every three months while it answers, and says so on screen.
  - Diagnostics of each session go to the data folder's sessions\<stamp>\.
  Starting the game's exe in the play install directly also works (same isolation and options, but no strategist
  watcher: checkpoints then time out and play goes on under the last directives).
.EXAMPLE
  tools\play.ps1 -Strategist on                   # the first launch: strategist on from now on
.EXAMPLE
  tools\play.ps1                                  # every later launch
.EXAMPLE
  tools\play.ps1 -Set Strategy.Terminus=false     # switch the alien-victory clock off (remembered)
.EXAMPLE
  tools\play.ps1 -Set General.VanillaAI=true      # the escape hatch: every AI change off; -Unset General.VanillaAI undoes it
#>
param(
    [ValidateSet('on', 'off')][string]$Strategist,
    [string]$StrategistModel,
    # Plugin settings as Section.Key=value (see the generated BepInEx\config\com.improvedai.terrainvicta.cfg for names).
    [string[]]$Set,
    [string[]]$Unset,
    # Write the options and the install's standing request, but do not start the game.
    [switch]$NoLaunch
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\paths.ps1"
if (-not (Test-Path (Join-Path $PlayPlugin 'ImprovedAI.dll'))) { throw "No play install at $PlayDir. Create it with tools\release.ps1." }

$savesDir = Join-Path $PlayData 'saves'
$stratDir = Join-Path $PlayData 'strategist'
$sessions = Join-Path $PlayData 'sessions'
New-Item -ItemType Directory -Force $savesDir, $stratDir, $sessions | Out-Null

$optFile = Join-Path $PlayData 'options.json'
$opt = @{ strategist = $false; model = 'opus'; set = @{} }
if (Test-Path $optFile) {
    $o = Get-Content $optFile -Raw | ConvertFrom-Json
    $opt.strategist = [bool]$o.strategist
    if ($o.model) { $opt.model = $o.model }
    if ($o.set) { foreach ($p in $o.set.PSObject.Properties) { $opt.set[$p.Name] = [string]$p.Value } }
}
if ($Strategist) { $opt.strategist = ($Strategist -eq 'on') }
if ($StrategistModel) { $opt.model = $StrategistModel }
foreach ($s in $Set) {
    if ($s -notmatch '^([^.=]+)\.([^.=]+)=(.*)$') { throw "-Set wants Section.Key=value, got '$s'" }
    $opt.set["$($Matches[1]).$($Matches[2])"] = $Matches[3]
}
foreach ($u in $Unset) { $opt.set.Remove($u) }
$opt | ConvertTo-Json -Depth 4 | Set-Content -Path $optFile -Encoding utf8

$config = @{}
foreach ($k in $opt.set.Keys) {
    $section, $key = $k.Split('.', 2)
    if (-not $config[$section]) { $config[$section] = @{} }
    $config[$section][$key] = $opt.set[$k]
}
if (-not $config['Strategist']) { $config['Strategist'] = @{} }
$config['Strategist']['Enabled'] = "$($opt.strategist)".ToLower()
$config['Strategist']['Folder'] = $stratDir
[ordered]@{
    name = 'play'; interactive = $true; savesDir = $savesDir; outDir = $sessions
    quitOnFinish = $false; allowSaves = $true; commands = @(); config = $config
} | ConvertTo-Json -Depth 6 | Set-Content -Path (Join-Path $PlayPlugin 'home.json') -Encoding utf8

# Your own display and audio options, copied once; changes made while playing stay with the play install.
$profileSrc = Join-Path $UserSaves 'PlayerOptions.TIProfile'
$profileDst = Join-Path $savesDir 'PlayerOptions.TIProfile'
if ((Test-Path $profileSrc) -and -not (Test-Path $profileDst)) { Copy-Item $profileSrc $profileDst }

$build = Get-Content (Join-Path $PlayPlugin 'build.txt') -ErrorAction SilentlyContinue | Select-Object -First 1
$extra = if ($opt.set.Count) { '; ' + (($opt.set.Keys | Sort-Object | ForEach-Object { "$_=$($opt.set[$_])" }) -join ', ') } else { '' }
Write-Host "Improved AI build $build. Strategist $(if ($opt.strategist) { "on ($($opt.model))" } else { 'off' })$extra"
if ($NoLaunch) { return }
if (& $Playing) { throw 'The play install is already running.' }
Remove-Item (Join-Path $PlayPlugin 'run.json') -ErrorAction SilentlyContinue

$watcher = $null
$watcherArgs = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'strategist.ps1'), '-Folder', "`"$stratDir`"", '-Model', $opt.model)
try {
    if ($opt.strategist) { $watcher = Start-Process -FilePath 'powershell.exe' -PassThru -WindowStyle Minimized -ArgumentList $watcherArgs }
    Write-Host "Saves: $savesDir"
    Write-Host 'Quit the game normally when done; this window waits for it.'
    $p = Start-Process -FilePath (Join-Path $PlayDir 'TerraInvicta.exe') -WorkingDirectory $PlayDir -PassThru
    while (-not $p.WaitForExit(5000)) {
        if ($watcher -and $watcher.HasExited) {
            Write-Host 'The strategist watcher stopped; starting it again.'
            $watcher = Start-Process -FilePath 'powershell.exe' -PassThru -WindowStyle Minimized -ArgumentList $watcherArgs
        }
    }
}
finally {
    if ($watcher -and -not $watcher.HasExited) { Stop-Process -Id $watcher.Id -Force -ErrorAction SilentlyContinue }
}
$last = Get-ChildItem $sessions -Directory | Sort-Object Name | Select-Object -Last 1
if ($last) { Copy-Item (Join-Path $PlayDir 'BepInEx\LogOutput.log') (Join-Path $last.FullName 'bepinex.log') -ErrorAction SilentlyContinue }
