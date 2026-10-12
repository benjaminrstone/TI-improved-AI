<#
.SYNOPSIS
  Run the campaign harness once: load a fixture save in the game copy, run N game days unattended at max speed,
  collect the output into lab\results\<stamp>-<name>\ and (for probes) evaluate asserts.
.EXAMPLE
  tools\run.ps1 -Probe smoke-2034
.EXAMPLE
  tools\run.ps1 -Fixture accel-2034-fusion-drives -Days 60 -Name throughput
#>
param(
    [string]$Probe,
    [string]$Fixture,
    # Load this save instead of a fixture (e.g. a run's final.gz); its .improvedai.json sidecar is copied along.
    [string]$SavePath,
    [double]$Days = 30,
    [string]$Name,
    [int]$TimeoutMinutes = 0,
    [switch]$NoDeploy,
    [switch]$KeepOpen,
    [switch]$FinalSave,
    # Turn every AI change off (diagnostics still run): the vanilla baseline for a probe.
    [switch]$Vanilla
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\paths.ps1"

$spec = @{ commands = @(); config = $null; asserts = @() }
if ($Probe) {
    $probeFile = Join-Path $ProjectRoot "lab\probes\$Probe.json"
    if (-not (Test-Path $probeFile)) { throw "No probe $probeFile" }
    $p = Get-Content $probeFile -Raw | ConvertFrom-Json
    $Fixture = $p.fixture
    if (-not $PSBoundParameters.ContainsKey('Days') -and $p.runDays) { $Days = [double]$p.runDays }
    if (-not $Name) { $Name = $p.name }
    if ($p.commands) { $spec.commands = @($p.commands) }
    if ($p.config) { $spec.config = $p.config }
}
if (-not $Fixture -and -not $SavePath) { throw 'Give -Probe, -Fixture or -SavePath' }
if (-not $Name) { $Name = if ($Fixture) { $Fixture } else { [IO.Path]::GetFileNameWithoutExtension($SavePath) } }
if ($Vanilla) {
    $Name = "$Name-vanilla"
    $cfg = @{}
    if ($spec.config) { foreach ($p in $spec.config.PSObject.Properties) { $cfg[$p.Name] = $p.Value } }
    $cfg['General'] = @{ VanillaAI = 'true' }
    $spec.config = $cfg
}

if ($SavePath) {
    $srcSave = (Resolve-Path $SavePath).Path
    $fx = [pscustomobject]@{ date = '?'; file = [IO.Path]::GetFileName($srcSave) }
    $Fixture = $fx.file
} else {
    $fixtures = Get-Content (Join-Path $ProjectRoot 'lab\fixtures.json') -Raw | ConvertFrom-Json
    $fx = $fixtures.$Fixture
    if (-not $fx) { throw "Unknown fixture '$Fixture' (see lab\fixtures.json)" }
    $srcSave = Join-Path $UserSaves $fx.file
}
if (-not (Test-Path $srcSave)) { throw "Fixture save missing: $srcSave" }

# One harness run at a time on this game copy.
New-Item -ItemType Directory -Force $HarnessDir | Out-Null
$lockPath = Join-Path $HarnessDir 'harness.lock'
$lock = $null
for ($i = 0; $i -lt 360 -and -not $lock; $i++) {
    try { $lock = [IO.File]::Open($lockPath, 'OpenOrCreate', 'ReadWrite', 'None') }
    catch { if ($i -eq 0) { Write-Host 'Waiting for another harness run...' }; Start-Sleep -Seconds 10 }
}
if (-not $lock) { throw 'Timed out waiting for the harness lock.' }

try {
    $ours = { Get-Process TerraInvicta -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($GameDir, 'OrdinalIgnoreCase') } }
    if (& $ours) { throw "The game copy at $GameDir is already running; close it first." }
    if (-not $NoDeploy) { & (Join-Path $PSScriptRoot 'deploy.ps1') }

    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $outDir = Join-Path $ResultsDir "$stamp-$Name"
    $savesDir = Join-Path $HarnessDir "saves\$stamp"
    New-Item -ItemType Directory -Force $outDir, $savesDir | Out-Null

    # Stage copies: the game never sees the user's save folder during a harness run.
    $save = Join-Path $savesDir ([IO.Path]::GetFileName($srcSave))
    Copy-Item $srcSave $save
    if (Test-Path "$srcSave.improvedai.json") { Copy-Item "$srcSave.improvedai.json" "$save.improvedai.json" }
    $profileSrc = Join-Path $UserSaves 'PlayerOptions.TIProfile'
    if (Test-Path $profileSrc) {
        $text = [IO.File]::ReadAllText($profileSrc)
        $set = @{ Resolution = '1280x720'; Fullscreen = 'False'; VolumeMaster = '0'; MuteInBackground = 'True'; QualitySetting = '0'; VSyncEnabled = 'False'; UseCouncilorVideo = 'False' }
        foreach ($k in $set.Keys) { $text = $text -replace "(?m)^${k}:.*$", "${k}:$($set[$k])" }
        [IO.File]::WriteAllText((Join-Path $savesDir 'PlayerOptions.TIProfile'), $text)
    }

    $maxReal = if ($TimeoutMinutes -gt 0) { $TimeoutMinutes * 60 } else { [Math]::Max(900, $Days * 20 + 600) }
    $run = [ordered]@{
        name = $Name; save = $save; runDays = $Days; maxRealSeconds = $maxReal - 120
        quitOnFinish = -not $KeepOpen; outDir = $outDir; savesDir = $savesDir
        commands = $spec.commands; finalSave = [bool]$FinalSave
    }
    if ($spec.config) { $run.config = $spec.config }
    New-Item -ItemType Directory -Force $PluginDir | Out-Null
    $runFile = Join-Path $PluginDir 'run.json'
    $run | ConvertTo-Json -Depth 6 | Set-Content -Path $runFile -Encoding utf8

    $playerLog = Join-Path $outDir 'player.log'
    # Window: 1280x720 windowed, on a secondary display when there is one (Unity's -monitor is 1-based).
    Add-Type -AssemblyName System.Windows.Forms
    $screens = [System.Windows.Forms.Screen]::AllScreens
    $gameArgs = @('-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720', '-logFile', "`"$playerLog`"")
    if ($screens.Count -gt 1) {
        $idx = [Array]::IndexOf($screens, @($screens | Where-Object { -not $_.Primary })[0]) + 1
        $gameArgs += @('-monitor', "$idx")
    }
    # Unity stores window/monitor prefs under one registry key that the Steam install shares; restore it afterwards
    # so a harness run never changes how the user's own game opens.
    $regPath = 'HKCU:\Software\Pavonis Interactive\TerraInvicta'
    # A killed launcher can't restore in its finally block, so the backup is also kept on disk and restored first.
    $regBackupFile = Join-Path $HarnessDir 'display-registry-backup.json'
    if ((Test-Path $regBackupFile) -and (Test-Path $regPath)) {
        $stale = Get-Content $regBackupFile -Raw | ConvertFrom-Json
        foreach ($p in $stale.PSObject.Properties) { Set-ItemProperty -Path $regPath -Name $p.Name -Value ([int]$p.Value) -Type DWord }
        Write-Host 'Restored display settings left over from an interrupted run.'
    }
    $regBackup = @{}
    if (Test-Path $regPath) {
        $key = Get-Item $regPath
        foreach ($n in $key.GetValueNames()) {
            if ($n -match '^(Screenmanager|UnitySelectMonitor)') { $regBackup[$n] = @{ v = $key.GetValue($n); k = $key.GetValueKind($n) } }
        }
    }
    if ($regBackup.Count -gt 0) {
        $flat = @{}; foreach ($n in $regBackup.Keys) { $flat[$n] = $regBackup[$n].v }
        $flat | ConvertTo-Json | Set-Content -Path $regBackupFile -Encoding utf8
    }
    Add-Type -Namespace TIAI -Name Win -MemberDefinition '[DllImport("user32.dll")] public static extern System.IntPtr GetForegroundWindow(); [DllImport("user32.dll")] public static extern bool SetForegroundWindow(System.IntPtr h);' -ErrorAction SilentlyContinue
    $userWindow = [TIAI.Win]::GetForegroundWindow()

    Write-Host "Run $Name : fixture $Fixture ($($fx.date)), $Days days -> $outDir"
    $t0 = Get-Date
    try {
        Start-Process -FilePath (Join-Path $GameDir 'TerraInvicta.exe') -WorkingDirectory $GameDir -ArgumentList $gameArgs | Out-Null
        Start-Sleep -Seconds 8
        [void][TIAI.Win]::SetForegroundWindow($userWindow)
        $deadline = $t0.AddSeconds($maxReal)
        while ((& $ours) -and (Get-Date) -lt $deadline) {
            Start-Sleep -Seconds 10
            $hb = Join-Path $outDir 'heartbeat.json'
            if (Test-Path $hb) { $h = Get-Content $hb -Raw | ConvertFrom-Json; Write-Host ("  {0}  day {1}/{2}  {3} days/min  fps {4}  exc {5}  stalls {6}" -f $h.date, $h.gameDays, $Days, $h.gameDaysPerRealMinute, $h.fps, $h.exceptions, $h.stalls) }
        }
        if (& $ours) {
            if ($KeepOpen) { Write-Host 'Game left open.' } else { Write-Warning 'Timed out; killing the game.'; & $ours | Stop-Process -Force; Start-Sleep -Seconds 3 }
        }
    }
    finally {
        Remove-Item $runFile -ErrorAction SilentlyContinue
        if (-not (& $ours) -and (Test-Path $regPath)) {
            foreach ($n in $regBackup.Keys) { Set-ItemProperty -Path $regPath -Name $n -Value $regBackup[$n].v -Type $regBackup[$n].k }
            Remove-Item $regBackupFile -ErrorAction SilentlyContinue
        }
    }

    Copy-Item (Join-Path $GameDir 'BepInEx\LogOutput.log') (Join-Path $outDir 'bepinex.log') -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force $savesDir -ErrorAction SilentlyContinue
    $wall = [int]((Get-Date) - $t0).TotalSeconds
    Write-Host "Wall time ${wall}s"
    $res = Join-Path $outDir 'results.json'
    if (-not (Test-Path $res)) {
        Write-Warning 'No results.json. Log tail:'
        Get-Content (Join-Path $outDir 'bepinex.log') -ErrorAction SilentlyContinue | Select-Object -Last 30
        exit 1
    }
    Get-Content $res -Raw
    if ($Probe) {
        & python -I (Join-Path $PSScriptRoot 'probe.py') $probeFile $outDir
        exit $LASTEXITCODE
    }
}
finally { if ($lock) { $lock.Dispose() } }
