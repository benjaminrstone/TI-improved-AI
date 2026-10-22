<#
.SYNOPSIS
  The local strategist watcher (docs/STRATEGIST.md). Watches the exchange folder for briefs the mod writes at each
  checkpoint (the game is paused meanwhile), asks Claude (headless Claude Code) for one directive per faction, each call
  seeing only that faction's part of the brief, and writes directives/<date>.json back. The mod then resumes the game.
.EXAMPLE
  tools\strategist.ps1                                   # watch the game copy's default folder until stopped
.EXAMPLE
  tools\strategist.ps1 -Folder lab\results\<run>\strategist -Once
#>
param(
    [string]$Folder = 'C:\Games\TI-ImprovedAI\BepInEx\ImprovedAI-strategist',
    [string]$Model = 'opus',
    [int]$PollSeconds = 3,
    # Process pending briefs once and exit (otherwise watch until stopped).
    [switch]$Once,
    # Stop watching after this many real minutes (0 = never); for harness runs.
    [int]$StopAfterMinutes = 0,
    # Harness mode: follow the newest lab\results\<run>\strategist folder (each run keeps its own exchange files).
    [switch]$Harness
)
$ErrorActionPreference = 'Stop'
$promptFile = Join-Path $PSScriptRoot 'strategist\prompt.md'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$resultsDir = Join-Path (Split-Path $PSScriptRoot -Parent) 'lab\results'
function Resolve-Folder {
    if (-not $Harness) { return $Folder }
    $d = Get-ChildItem $resultsDir -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending |
        Where-Object { Test-Path (Join-Path $_.FullName 'strategist\briefs') } | Select-Object -First 1
    if ($d) { return (Join-Path $d.FullName 'strategist') } else { return $null }
}
if (-not $Harness) { foreach ($d in 'briefs', 'directives', 'responses') { New-Item -ItemType Directory -Force (Join-Path $Folder $d) | Out-Null } }
# The log must never stop the watcher (another program may hold the file open, e.g. a tail).
function Log($m) {
    $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $m"
    Write-Host $line
    try { [IO.File]::AppendAllText((Join-Path $Folder 'watcher.log'), $line + "`r`n", $utf8) } catch { }
}

# One headless call per faction, in parallel. Each job returns @{ faction; json; raw; error }.
$ask = {
    param($faction, $date, $header, $section, $previous, $model, $promptFile)
    # PS 5.1 pipes text to programs as ASCII by default, which would mangle names like "João".
    $OutputEncoding = New-Object System.Text.UTF8Encoding($false)
    [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
    $user = "Checkpoint $date. You command $faction.`n`nGame: $header`n`nYour previous directive: $previous`n`nYour brief:`n$section`n`nReply with your directive JSON only."
    $raw = ''
    for ($try = 1; $try -le 2; $try++) {
        try {
            $raw = ($user | & claude -p --model $model --system-prompt-file $promptFile --tools "" --output-format text) -join "`n"
            $i = $raw.IndexOf('{'); $j = $raw.LastIndexOf('}')
            if ($i -ge 0 -and $j -gt $i) {
                $json = $raw.Substring($i, $j - $i + 1)
                $null = $json | ConvertFrom-Json
                return @{ faction = $faction; json = $json; raw = $raw; error = $null }
            }
        } catch { $err = $_.Exception.Message }
    }
    return @{ faction = $faction; json = $null; raw = $raw; error = ("no valid JSON " + $err) }
}

$started = Get-Date
if ($Harness) { Write-Host "watching the newest run under $resultsDir (model $Model)" } else { Log "watching $Folder (model $Model)" }
while ($true) {
    $Folder = Resolve-Folder
    $pending = @()
    if ($Folder -and (Test-Path (Join-Path $Folder 'briefs'))) {
        foreach ($d in 'directives', 'responses') { New-Item -ItemType Directory -Force (Join-Path $Folder $d) | Out-Null }
        $pending = Get-ChildItem (Join-Path $Folder 'briefs') -Filter *.json | Where-Object { -not (Test-Path (Join-Path $Folder "directives\$($_.Name)")) } | Sort-Object Name
    }
    foreach ($b in $pending) {
        $date = $b.BaseName
        Log "checkpoint $date"
        $brief = [IO.File]::ReadAllText($b.FullName) | ConvertFrom-Json
        $header = @{ date = $brief.date; campaignYears = $brief.campaignYears; player = $brief.player } | ConvertTo-Json -Compress
        $prevFile = Get-ChildItem (Join-Path $Folder 'directives') -Filter *.json | Where-Object { $_.BaseName -lt $date } | Sort-Object Name | Select-Object -Last 1
        $prev = if ($prevFile) { [IO.File]::ReadAllText($prevFile.FullName) | ConvertFrom-Json } else { $null }
        $jobs = @()
        foreach ($p in $brief.factions.PSObject.Properties) {
            $section = $p.Value | ConvertTo-Json -Depth 8
            $previous = if ($prev -and $prev.factions.PSObject.Properties[$p.Name]) { $prev.factions.($p.Name) | ConvertTo-Json -Depth 6 -Compress } else { 'none (first checkpoint)' }
            $jobs += Start-Job -ScriptBlock $ask -ArgumentList $p.Name, $date, $header, $section, $previous, $Model, $promptFile
        }
        $results = $jobs | Wait-Job | Receive-Job
        $jobs | Remove-Job -Force
        $out = [ordered]@{ checkpoint = $date; factions = [ordered]@{} }
        $raw = New-Object System.Text.StringBuilder
        foreach ($r in $results) {
            [void]$raw.AppendLine("## $($r.faction)`n$($r.raw)`n")
            if ($r.json) { $out.factions[$r.faction] = ($r.json | ConvertFrom-Json) } else { Log "  $($r.faction): $($r.error)" }
        }
        [IO.File]::WriteAllText((Join-Path $Folder "responses\$date.md"), $raw.ToString(), $utf8)
        $tmp = Join-Path $Folder "directives\$date.json.tmp"
        [IO.File]::WriteAllText($tmp, ($out | ConvertTo-Json -Depth 8), $utf8)
        Move-Item $tmp (Join-Path $Folder "directives\$date.json") -Force
        Log "  directives written for $($out.factions.Count) factions"
    }
    if ($Once) { break }
    if ($StopAfterMinutes -gt 0 -and ((Get-Date) - $started).TotalMinutes -ge $StopAfterMinutes) { Log 'stop time reached'; break }
    Start-Sleep -Seconds $PollSeconds
}
