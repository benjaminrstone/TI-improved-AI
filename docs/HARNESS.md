# Harness: running the strategic layer unattended

The harness covers layers L1 (probes) and L2 (soaks) from TESTING.md. It runs on the isolated game copy at `C:\Games\TI-ImprovedAI` and never touches the Steam install, the user's saves, or the user's display settings.

```
tools\run.ps1 -Probe e5-undefended-mines        # one probe: run, then evaluate asserts (exit 2 on failure)
tools\run.ps1 -Fixture accel-2034-fusion-drives -Days 90 -Name look   # ad-hoc run, no asserts
tools\probes.ps1 e3-attack-sizing, e9-jupiter-plateau   # several probes back to back, with a summary table
tools\probes.ps1 -All
```

## What a run does

1. **Builds and deploys** the plugin (`tools\deploy.ps1`).
2. **Stages copies** under `C:\Games\TI-ImprovedAI-harness\saves\<stamp>\`:
   - the fixture save, from `lab\fixtures.json`;
   - the user's `PlayerOptions.TIProfile`, adjusted to a 1280×720 window with no sound.

   The plugin points the game's save folder there for this launch only. Autosaves and autopilot saves (about 100 MB each) are blocked for speed.
3. **Launches the game** windowed on a secondary display if there is one, then returns focus to the user's window.
   - Unity's window prefs live in a registry key shared with the Steam install. They are backed up before the launch and restored after.
   - Unity's log goes to the run folder (`-logFile`).
4. **Loads the save.** The plugin then:
   - turns off the exception dialog and runs in the background;
   - executes the probe's `commands`;
   - starts the game's own Autopilot (clears prompts, auto-resolves combat, picks random techs);
   - holds max speed.
5. **Stops** after `runDays` game days, writes `results.json` and quits.

## Output: `lab\results\<stamp>-<name>\` (gitignored)

| File | What |
|---|---|
| `journal.jsonl` | Alien decisions, from a daily diff (see `DecisionJournal.cs`). Kinds: `init`, `goal+`/`goal-`/`goal~` (type, importance, target, assigned fleet, fleet SCV vs desired SCV), `transfer` (alien fleet launches with its goal and the SCV it was sized against), `fleet+`/`fleet-`, `hab+`/`hab-` (any faction), `inject`, `stall` |
| `metrics.jsonl` | One row at load and one per game month. Per faction: resources, bases and stations, fleets, ships, SCV, SCV and habs by system, alien hate. Also alien goal counts and alien bases |
| `ai-log.txt` | The developers' own AI trace (`TIFactionState.LogAI`, a no-op in vanilla): habs founded, projects picked, wars, probes |
| `results.json` | End reason, throughput (`gameDaysPerRealMinute`), exception samples, terminal output, saves blocked |
| `heartbeat.json` | Live status during the run |
| `asserts.json` | Probe outcomes |
| `player.log`, `bepinex.log` | Unity and plugin logs |
| `stall-N.png` | Screenshot whenever the game date stops advancing for 90 s (e.g. a prompt the Autopilot can't clear) |

## Probes (`lab\probes\*.json`)

```jsonc
{
  "name": "e5-undefended-mines",
  "exploit": "E5",                      // EXPLOITS.md entry
  "expect": "vanilla FAILS ...",        // what vanilla should do, printed with the results
  "fixture": "accel-2034-fusion-drives",
  "runDays": 180,
  "commands": ["ai.foundoutposts ResistCouncil belt 15 mine", "addresource Money,1000,AlienCouncil"],
  "config": { "Diagnostics": { "AILog": "true" } },   // per-run plugin config overrides
  "asserts": ["count('hab-', faction='ResistCouncil', habType='Base') >= 5"]
}
```

- **Commands.** Lines starting with `ai.` run as harness commands (`HarnessCommands.cs`). Everything else goes to the game's debug terminal (`help` lists about 90 commands, e.g. `addresource`, `givealltechs`, `giveproject`).
- **Asserts.** Python expressions evaluated by `tools\probe.py` over `journal`, `metrics`, `results` and `status`. Helpers: `count(kind, **match)`, `events(kind, **match)`, `faction(row, name)`, `first`, `last`, `median`, `mean`.
- **Expected failures.** Exploit probes are written so that vanilla fails them. A probe going green is the evidence that a fix works.

## Numbers so far

| Measure | Value |
|---|---|
| Throughput, 2034 mid-game save | about 19 game days per real minute (about 19 min per game year) |
| Save load | about 55 s |
| Memory | about 7.6 GB |

## Gotchas

- **Runs aren't deterministic.** The RNG seeds from the clock, mission planning runs on worker threads, and frame time varies. Assert on rates over repeated runs, not single outcomes.
- **The player is passive.** The Autopilot doesn't assign councilor missions or give fleet orders, so the player's faction slowly loses ground. Probes measure what the *aliens* decide.
- **One run at a time.** Runs on the game copy are serialised by `C:\Games\TI-ImprovedAI-harness\harness.lock`.
