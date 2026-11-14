# Testing

A full campaign takes 50–80 hours, so it can't be the inner loop. Everything runs on the isolated game copy (`C:\Games\TI-ImprovedAI`) through the harness: load a save, inject a situation, run game days unattended at max speed, read what the AI decided.

| Layer | Answers | Cost | Built |
|---|---|---|---|
| Save forensics | What did the AI do across a real campaign? | seconds | `tools/saves/timeline.py` |
| Probes | Given situation X, does the AI decide Y within N days? | 2–50 min | yes |
| Soaks | Over years, do the curves look right? Vanilla vs mod, repeated. | ~1 min per 20 game days | yes |
| Stand-in player | How does it hold up against an opponent that acts? | as a soak | the game's AI on the player's faction, or the bot (`hunt` attacks the aliens) |
| Combat lab | Do station defences and fleet compositions work tactically? | — | not built |

## Rules

- **Probe first.** A change starts with a probe that shows the problem on vanilla (`-Vanilla`).
- **Never conclude balance from one run.** Runs are not deterministic: the RNG is clock-seeded, mission planning runs on worker threads, frame time varies. Use `soaks.ps1` and compare means and ranges with `aggregate.py`.
- **The regression check is two flow probes**, not the whole roster: `tools\probes.ps1 flow-2034, flow-2039` (~45 min). Their asserts are health checks and wide floors that natural variance passes; the `info` lines are the observations to read. Probes with injected setups are run only for the feature being changed.
- **The harness player is passive** unless the probe says otherwise. The game's Autopilot clears prompts and resolves combat but gives no orders, so the player's faction slowly loses ground. For a player that acts, hand the faction to the game's AI (`ai.control ResistCouncil ai`) or the rule-based bot (`ai.bot <faction> reinforce,merge,hunt`, rules in `Harness/PlayerBot.cs`). The game's AI fights the other human factions, not the aliens. The bot's `hunt` rule attacks alien stations and parked fleets it outguns, destroys the stations and flies home to resupply; it needs a fleet worth sending, which `ai.spawnfleet` provides. An AI-run player costs little speed (14–21 days/min).
- **The Autopilot never presses a fight.** It answers every stance prompt with Defend and does not answer the delta-v bid of a pursuit, so a fight the player's fleet starts happens only if the other side pursues. The `hunt` bot pursues in the fights it starts and bids as the game's AI would. An AI-run player is not affected: the game answers its prompts.
- **With an AI-run player the clock waits for the mission planner** (`Harness/PlannerClock.cs`). The game plans each AI faction's councilor missions on a second thread. A human's game holds the clock meanwhile (the player's own prompt, and Confirm reads "AI working" until every faction is done); a handed-over player's prompt is answered by the AI at once, so the clock used to run on beside the planner. That cancelled missions at every phase start (about 500 in a three-year leg), made the planner throw "Collection was modified" now and then, and froze the game for good in 3 of 8 three-year legs (the event manager is not thread-safe). `results['planner']['clockMovedBeside']` counts frames on which it happened and must be 0; `planner-clock-off` switches the hold off to show the problem. A null reference in `TIEffectsState.ProcessInstantEffect` from a narrative event is the game's own (seen once, in a vanilla run).
- **A frozen run ends itself.** A watchdog thread counts frames; after two minutes without one it aborts the main thread for its stack trace and the run finishes with `endReason: "frozen"`, the trace and the CPU use in `results['frozen']`, and no final save (`harness-freeze` tests this with `ai.freeze`). Behind it, `run.ps1` kills a game whose heartbeat is 15 minutes old.
- **One run at a time.** The game copy is locked by `C:\Games\TI-ImprovedAI-harness\harness.lock`. A run also waits while the play install's game is open and until 16 GB of memory is free (`-MinFreeGB`): beside another game it swaps, and its stalls and timings mean nothing.
- **`status['exceptions']` includes what `PatchGuard` swallowed** in the mod's own patches (`status['guarded']`), so a health assert on exceptions also catches a patch that fails quietly.
- **A run is quiet by default:** the mod's player notices (terminus, intel, the strategist's pause box) are only journaled. A probe with `"notices": true` shows them as a real play does and saves a screenshot of each (`notice-N-<what>.png`); the harness holds the Autopilot off until the shot is taken and closes the campaign-ending popup once, as a player does.

## Commands

```
tools\probes.ps1 flow-2034, flow-2039                    # the regression check
tools\run.ps1 -Probe <name> [-Vanilla] [-Days N]         # one probe (exit 2 on a failed assert)
tools\run.ps1 -Fixture <name> -Days 90 -Name look        # ad-hoc run, no asserts
tools\run.ps1 -Probe <name> -SavePath <run>\final.gz     # start from a run's final save (-FinalSave writes one)
tools\probes.ps1 flow-2034-ai -Runs 3 -WithVanilla       # a probe repeated, each also as a vanilla baseline
tools\soaks.ps1 -Fixture accel-2034-fusion-drives -Days 730 -Runs 3 [-ModOnly]   # the same for a bare fixture
tools\strategist.ps1 -Harness                            # the strategist watcher (start it before the run)
python -I tools\probe.py lab\probes\<probe>.json lab\results\<run>   # re-evaluate a run
python -I tools\summarize.py lab\results\<run>           # the story of one run
python -I tools\compare.py <runA> <runB>                 # two runs side by side
python -I tools\aggregate.py "<glob of run folders>"     # means over soaks
```

## What a run does

1. Builds and deploys the plugin (`deploy.ps1`; it resets the plugin `.cfg` so current defaults apply).
2. Stages a copy of the fixture save (`lab\fixtures.json` maps names to files in the user's save folder, which is only read) and of the player profile under `C:\Games\TI-ImprovedAI-harness`. The plugin redirects the game's save folder there for this launch; autosaves are blocked.
3. Launches the game windowed. Unity's display settings live in a registry key shared with the Steam install: they are backed up and restored, also after a killed run.
4. Loads the save, runs the probe's `commands`, starts the Autopilot and holds max speed.
5. Stops after `runDays`, writes `results.json`, quits, and evaluates the asserts.

Throughput is ~17–24 game days per real minute on a mid-game save; a save loads in about a minute.

## Output: `lab\results\<stamp>-<name>\` (gitignored)

| File | What |
|---|---|
| `journal.jsonl` | One record per decision or event, each with a `kind`. The record shapes are defined where they are written (`Session.Journal?.Write` in the plugin). |
| `metrics.jsonl` | One row at load and one per game month: per faction resources, habs, fleets, ships, SCV; alien hate and goal counts. |
| `ai-log.txt` | The developers' own AI trace (`TIFactionState.LogAI`, a no-op in vanilla). |
| `results.json` | End reason, throughput, exception samples, terminal output. `missionPhase`: the game's own alarms (finalizes outside a phase, missions cancelled at a phase start). `planner`: mission phases planned, the longest in real seconds, frames the clock moved beside the planner. `offThread`: which of the mod's patches ran on the planner's thread, and how often. `frozen`: the trace, when the watchdog ended the run. |
| `asserts.json` | Probe outcomes and `info` values. |
| `strategist\` | Briefs and directives, when the strategist ran. |
| `player.log`, `bepinex.log`, `stall-N.png` | Logs, and a screenshot whenever the date stops advancing for 90 s. |

## Probes (`lab\probes\*.json`)

```jsonc
{
  "name": "e5-undefended-mines",
  "expect": "vanilla FAILS ...",        // printed with the results
  "fixture": "accel-2034-fusion-drives",
  "runDays": 180,
  "commands": ["ai.foundoutposts ResistCouncil belt 15 mine", "@60 ai.move ..."],
  "config": { "Targeting": { "MaxRaidGoals": "2" } },   // per-run plugin config overrides
  "asserts": ["count('hab-', faction='ResistCouncil', habType='Base') >= 5"],
  "info": ["last['date']"]              // tracked numbers: printed, never fail the probe
}
```

- **Commands.** `ai.*` lines are harness commands (`Harness/HarnessCommands.cs`); an `@N` prefix runs one N game days after the start. Anything else goes to the game's own debug terminal (`addresource`, `givealltechs`, …).
- **`ai.reload`** saves, renames the save (as the game renames its autosaves) and loads it in the running game, then the run goes on. Each one journals a `reload` record: `same` says whether the mod's state came back unchanged, `memoryGB` is the game's memory afterwards.
- **A new campaign.** Instead of `"fixture"`, `"newGame": { "faction": "CooperateCouncil", "difficulty": 3, "accelerated": true }` starts one from the main menu through the New Game screen's own controls (difficulty 1 Cinematic to 4 Brutal; `"scenario"` picks a start other than the default). Hand the player's faction to the game's AI with `ai.control`, or the campaign has no player.
- **Asserts and info** are Python expressions over `journal`, `metrics`, `results`, `status`, `first`, `last`, with `count(kind, **match)`, `events(kind, **match)`, `faction(row, name)` and a few builtins (`len sum min max any all abs round sorted set median mean`; no `zip`, `dict` or `str`).

| Probe | Use |
|---|---|
| `flow-2034`, `flow-2039` | The regression check: a natural year each. 2034 covers the alien economy, proxy trade, raids, contempt and the human AI track; 2039 covers escalation, pooled strikes, rendezvous and the terminus. |
| `flow-2034-ai` | Stand-in player: two years from 2034 with the game's AI playing the Resistance. Health asserts only; run repeated with a vanilla arm. |
| `flow-2034-hunt` | Stand-in player that attacks: the same two years with the bot hunting, given about 3,000 SCV of ships at the start and 2,000 more every 180 days. Health asserts, and that it attacked. |
| `flow-2039-long` (`-ai`, `-bot`) | 540 days from 2039: the Jupiter strike arrives and fights. Passive, AI-run or bot-run Resistance. |
| `flow-2026`, `campaign-2026-15y` | The early game with the strategist; the 15-year campaign (overnight, ~4.5 h). |
| `strategist-2039` | The strategist end to end. |
| `newgame-smoke` | A new Veteran campaign with the Academy as the player, 90 days. |
| `campaign-veteran` | The stand-in for the user's campaign: that new game with the strategist, in three-year legs (`-FinalSave`, then `-SavePath` the final). |
| `reload-2039` | Campaign safety: a year with eight reloads in the running game; the mod's state must come back the same each time. |
| `e1-bait-reload` | Threat memory across a reload. |
| `guard-fault` | A patch of ours that throws on every call must not disturb the game. |
| `harness-freeze` | The harness's own test: a main thread blocked for good must end the run as `frozen`, with its trace. |
| `control-smoke`, `planner-clock-off` | The AI at the player's controls for 60 days: no cancelled missions and the clock never beside the planner; and the same with the hold off, where both happen. |
| `terminus-ending` | The terminus notices and the campaign-ending popup, shown and closed as a player would. |
| `strategist-timeout` | A checkpoint nobody answers: the pause notice, then play goes on. Run it with no watcher. |
| `strategist-failed` | A checkpoint the watcher answers with an error (it cannot reach Claude): the player is told, play goes on. Its second checkpoint begins with the clock running and must stop it. |
| everything else | Targeted probes with injected setups, for the feature being changed. |

Fixtures are all from one Normal accelerated Resistance campaign. A run's `final.gz` (`-FinalSave`) is a fixture too: `-SavePath` loads it and copies the sidecars beside it (`ImprovedAI\`) along. The mod switched off and on again mid-campaign is checked with three such legs: a mod run, `-Vanilla` from its final save, the mod again from that one's.

## Baseline: 2 years from 2034, three runs each

Means, with the range in brackets where it was kept.

| | Vanilla, passive | Mod, passive | Vanilla, AI-run player | Mod, AI-run player |
|---|---|---|---|---|
| Alien SCV at end | 35.3k | 42.8k (39.8–44.5) | 35.0k (32.3–36.7) | 43.6k (39.7–45.9) |
| Alien ships lost | 29 | 4.3 | 25.7 (9–43) | 5.7 (1–12) |
| Human ships destroyed by aliens | 144 | 190 | 142 (89–179) | 171 (146–196) |
| Habs lost, all / Resistance | 95 / 24 | 81 / 26 | 89 / 31 (27–36) | 83 / 28 (27–28) |
| Alien attack launches, all / at the player | 64 / – | 50 / – | 54 / 12 | 48 / 8 |
| Resistance SCV at end (873 at start) | – | – | 3.2k (1.7–3.9) | 2.6k (2.1–2.9) |
| AI human ships completed, year 1 / both years | 69 / – | 102, 117, 113 / – | – / 161 | – / 211 |
| Exotics stock | 2.0k → 2.4–2.7k | 2.0k → 1.4k | | |

**The AI-run player columns, and the `flow-2039-long-ai` line below, predate the clock hold** (`Harness/PlannerClock.cs`): missions were cancelled at every phase start in those runs, in both arms, so every faction's councilors did less than they should have. Rerun them before comparing anything against them.

The AI-run player (`flow-2034-ai`) changes nothing for the aliens: it loses about half its 54 habs in both arms and does not attack them. The aliens won every battle they started in all six runs.

### Against a player who attacks (`flow-2034-hunt`), three runs each

| | Vanilla | Mod |
|---|---|---|
| Alien SCV at end | 29.7k (24.0–36.4) | 43.8k (42.8–44.6) |
| Alien ships lost, all / to the player | 91 (36–180) / 84 (32–178) | 21 (7–29) / 13 (7–19) |
| Player ships lost to aliens | 49 (13–67) | 33 (18–51) |
| Fights the player started (won all) | 7.0 (6–9) | 4.0 (3–5) |
| Fights the aliens started against the player (won all) | 7.3 (3–11) | 12.0 (7–16) |
| Alien stations destroyed | 2.7 (2–3) | 2.3 (2–3) |
| Human ships destroyed by aliens | 128 (46–169) | 129 (114–154) |
| Habs lost, all / Resistance | 61 / 24.0 (19–28) | 74 / 31.7 (31–32) |
| Alien attack launches, all / at the player | 30 / 8.0 | 32 / 3.7 |
| Resistance SCV at end (about 970 at start, about 8k handed over) | 6.9k (6.0–7.9) | 7.8k (7.0–8.4) |

Being attacked costs vanilla's aliens about a sixth of their strength (29.7k against 35k with a player who leaves them alone) and the mod's nothing (43.8k against 43k). The bot destroys the same two or three stations in both arms; the difference is in ships. Vanilla's aliens leave fleets where the bot can pick them off, docked at a station it then takes or parked in an orbit; the mod's offer fewer such fights, so the bot starts fewer. In both arms a hunter that reaches Jupiter is caught there by a much larger fleet and lost.

What to keep in mind when reading these:

- **Big fights are all or nothing, and SCV does not predict them.** The game resolves them itself. The bot's fleet won twice without loss at 2.3 and 2.8 times its SCV (both in one vanilla run, which is why that run lost 180 ships) and was wiped out without a kill at 2.8, 3.2 and 4.6 times.
- **The bot chases an evading fleet every day.** An outgunned alien fleet in the bot's orbit can refuse the fight for months (91 refused attempts in one mod run, 36 in one vanilla run) and the bot does nothing else with that fleet meanwhile.
- **Its ships are given to it**, it leaves surface bases alone, and it only starts fights it outguns by 1.25.

540 days from 2039, mod only, three runs each (`flow-2039-long-ai`, `-bot`): Resistance habs lost 9.3 (8–11) with the AI-run player and 14.7 (11–19) with the bot; alien ships lost 0 and 2.0 (0–5); alien SCV at end 65.7k and 70.0k.

## The user's campaign: the play install

The user does not playtest: the target is their next real campaign, played fresh (`HANDOFF.md`). It runs on its own copy of the game, so development, deploys and harness runs never change what the campaign runs.

| | |
|---|---|
| `C:\Games\TI-ImprovedAI-play` | The play install: a copy of the game copy with a pinned plugin build. `BepInEx\plugins\ImprovedAI\build.txt` names the commit. |
| `C:\Games\TI-ImprovedAI-play-data\saves` | The campaign's saves and sidecars. The game never reads or writes the Documents saves: a standing `home.json` in the plugin folder redirects every launch, also one that starts the exe directly. |
| `…\sessions\<stamp>\<stamp>\` | Journal, metrics and logs of each loaded game. Read these when the user reports a symptom; they are spoilers for the user. |
| `…\options.json` | The options `play.ps1` remembers. |

```
tools\release.ps1                               # install the committed build (creates the install on first use: 27 GB)
tools\play.ps1 -Strategist on                   # first launch; the option is remembered
tools\play.ps1                                  # every later launch
tools\play.ps1 -Set Strategy.Terminus=false     # switch one feature off (remembered)
tools\play.ps1 -Set General.VanillaAI=true      # the escape hatch: every AI change off (-Unset General.VanillaAI undoes it)
```

- **Release only what passed the regression check.** `release.ps1` refuses uncommitted changes under `src\`. A campaign in progress keeps its saves and sidecars across releases.
- **Switches take effect at the next launch**, for the campaign as it stands: the sidecar is carried through saves made with `VanillaAI` on, so turning it off again resumes where the mod's state was.
- **A harness run waits while the play install's game is open**, so a test batch never competes with the user's session.
- Symptoms the user reports become probes before anyone touches AI code.
