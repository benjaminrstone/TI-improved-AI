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
- **An AI-run player makes the game's own mission planner throw.** `AICouncilorMissionPlanner` logs "Collection was modified" and retries on the main thread, in the vanilla arm too; these do not count as exceptions. A null reference in `TIEffectsState.ProcessInstantEffect` from a narrative event is also the game's (seen once, in a vanilla run).
- **One run at a time.** The game copy is locked by `C:\Games\TI-ImprovedAI-harness\harness.lock`.

## Commands

```
tools\probes.ps1 flow-2034, flow-2039                    # the regression check
tools\run.ps1 -Probe <name> [-Vanilla] [-Days N]         # one probe (exit 2 on a failed assert)
tools\run.ps1 -Fixture <name> -Days 90 -Name look        # ad-hoc run, no asserts
tools\run.ps1 -Probe <name> -SavePath <run>\final.gz     # start from a run's final save (-FinalSave writes one)
tools\probes.ps1 flow-2034-ai -Runs 3 -WithVanilla       # a probe repeated, each also as a vanilla baseline
tools\soaks.ps1 -Fixture accel-2034-fusion-drives -Days 730 -Runs 3 [-ModOnly]   # the same for a bare fixture
tools\strategist.ps1 -Harness                            # the strategist watcher (start it before the run)
tools\playtest.ps1 -Fixture accel-2031-hot-war [-Strategist]   # play it yourself, isolated saves
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
| `results.json` | End reason, throughput, exception samples, terminal output. |
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
- **Asserts and info** are Python expressions over `journal`, `metrics`, `results`, `status`, `first`, `last`, with `count(kind, **match)`, `events(kind, **match)`, `faction(row, name)` and a few builtins (`len sum min max any all abs round sorted set median mean`; no `zip`, `dict` or `str`).

| Probe | Use |
|---|---|
| `flow-2034`, `flow-2039` | The regression check: a natural year each. 2034 covers the alien economy, proxy trade, raids, contempt and the human AI track; 2039 covers escalation, pooled strikes, rendezvous and the terminus. |
| `flow-2034-ai` | Stand-in player: two years from 2034 with the game's AI playing the Resistance. Health asserts only; run repeated with a vanilla arm. |
| `flow-2034-hunt` | Stand-in player that attacks: the same two years with the bot hunting, given about 3,000 SCV of ships at the start and 2,000 more every 180 days. Health asserts, and that it attacked. |
| `flow-2039-long` (`-ai`, `-bot`) | 540 days from 2039: the Jupiter strike arrives and fights. Passive, AI-run or bot-run Resistance. |
| `flow-2026`, `campaign-2026-15y` | The early game with the strategist; the 15-year campaign (overnight, ~4.5 h). |
| `strategist-2039` | The strategist end to end. |
| everything else | Targeted probes with injected setups, for the feature being changed. |

Fixtures are all from one Normal accelerated Resistance campaign, plus the final save of the 15-year mod campaign (2041) in its run folder. The harness cannot start a brand-new campaign; "fresh" means the 2026 fixture.

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

The AI-run player (`flow-2034-ai`) changes nothing for the aliens: it loses about half its 54 habs in both arms and does not attack them. The aliens won every battle they started in all six runs.

540 days from 2039, mod only, three runs each (`flow-2039-long-ai`, `-bot`): Resistance habs lost 9.3 (8–11) with the AI-run player and 14.7 (11–19) with the bot; alien ships lost 0 and 2.0 (0–5); alien SCV at end 65.7k and 70.0k.

## Playing it

The user does not playtest: the target is their next real campaign, played fresh (`HANDOFF.md`). `tools\playtest.ps1` is how that campaign is played on the game copy: it launches the game with the mod in an isolated save folder (`C:\Games\TI-ImprovedAI-playtest\saves`), from the main menu or from a save, and writes the journal and metrics for each loaded game under `lab\results`. Symptoms the user reports from a play become probes before anyone touches AI code.
