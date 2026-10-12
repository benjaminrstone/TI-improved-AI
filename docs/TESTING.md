# Testing a 60-hour game

A full human campaign takes 50–80 hours, so it can't be the inner loop. We test in layers instead. Each layer is roughly 10× cheaper than the one above it, and most changes should be proven in the cheapest layer that can show them.

| Layer | What it answers | Cost | Who runs it |
|---|---|---|---|
| L0 Save forensics | What did the AI actually do across a real campaign? | seconds | script |
| L1 Decision probes | Given situation X, does the AI decide Y within N game-days? | 2–10 min | harness, unattended |
| L2 Campaign soaks | Over 5–20 game-years, do the curves look right? Does it break, stall or run away? | 1–4 h per run, overnight batches | harness, unattended |
| L3 Combat lab | Do station defenses, fleet compositions and new controls work tactically? | 2–5 min | harness (skirmish) |
| L4 Chapter playtests | Is it fun, fair and readable for a human? | 1–3 h | the user, from fixture saves |
| L5 Full campaign | Does the whole arc hold together? | 50–80 h | the user, rarely |

## Isolation from the Honorverse project

This project uses its **own copy of the game** (31 GB). It lives *outside* OneDrive, e.g. `C:\Games\TI-ImprovedAI\`; the path is set in `tools/paths.ps1`. The copy has its own BepInEx, plugin, Mods folder and lab lock, so it never collides with Honorverse runs on the Steam install. Re-sync it from the Steam install after game patches.
- `steam_appid.txt` (1176470) goes in the copy so it launches outside Steam.
- **Shared folder:** saves and `PlayerOptions.TIProfile` still live in `Documents/My Games/TerraInvicta`, which both copies use. The harness must:
  - never overwrite the user's saves, by redirecting or disabling autosave (`TIMissionPhaseState` autosaves and `CombatAutosave`) during harness runs;
  - write its own saves under `lab/results/`;
  - restore `PlayerOptions.TIProfile` after changing it.

## L0 Save forensics (`tools/saves/`)

Saves are gzipped FullSerializer JSON, 40–120 MB uncompressed. They contain every faction's resources and **every AI goal** (`FactionGoal_*`), with targets and assigned fleets.
- `timeline.py` covers fleet and resource curves.
- To add: alien habs by body, goal census per save, and attack history (from `TISpaceFleetState.Logs`/`CombatLogs`).
- **Fixture library:** the user's existing saves cover every chapter of a Normal accelerated Resistance campaign. Examples: `past-start` 2026, `mid-mars-construction` 2027, `hot-war` 2031-32, `fusion-drives-mid-build` 2034, `launch-jupiter-assault-fleet` 2037, `clean-jupiter` 2039. Copy the ones we use into `lab/fixtures/` with a manifest. We'll want more from other factions and difficulties over time.

## L1 Decision probes (the backbone)

A probe has four steps:
1. Load a fixture save.
2. Inject a situation: harness commands that wrap the game's own debug terminal (`addresource`, `givealltechs`, …) and runtime spawns (fleet at Jupiter, 15 undefended mines in the belt, refit the Earth fleet to weak missiles).
3. Run N game-days at max speed with the player passive (the game's own Autopilot clears prompts and auto-resolves combat).
4. Assert on **what the alien AI decided**: goals created, targets chosen, fleets launched, forces committed. Battle outcomes don't matter here.

Example probe:

```jsonc
{
  "name": "undefended-mines-get-raided",
  "fixture": "accel-2034-fusion-drives-mid-build",
  "inject": ["spawnMines player belt 15 undefended"],
  "runDays": 120,
  "asserts": [
    "alien.goals.AttackWithFleet[target.isMine].count >= 3",
    "player.habs[isMine & destroyed].count >= 5"
  ]
}
```

**Every known exploit becomes a probe.** The community survey and our own play produce an exploit catalogue (`docs/EXPLOITS.md`), and each entry gets a probe that fails on vanilla. That is the regression suite: a change that re-opens an exploit turns a probe red.

Each probe carries `baseline: "vanilla"`, so the same probe runs with the mod disabled to show the delta. Runs aren't deterministic: the RNG is clock-seeded, mission planning runs on a worker thread, and frame time varies. We seed what we can (`TIUtilities.InitRandom`). For behavioural asserts we run a probe 3–5 times and assert on the rate ("raids mines in ≥ 4 of 5 runs").

## L2 Campaign soaks

Load a fixture (or start a new game) and run 5–20 game-years with the human factions under AI control. This uses a Harmony patch that marks the player's faction `isAI`, leaving the UI attached. Record one JSONL row per game-month:
- date and wall-clock time;
- per faction: resources and income, habs by body, tier and type, fleets with summed SCV and location, control points, techs;
- alien hate, war state, and active alien goals by type.

Soaks answer curve questions that a probe can't:
- Does the alien expand past its old ceiling?
- Does it spend its stockpiles?
- Does it ever concentrate forces?
- Does the human AI get crushed in year 8? That is a warning sign: the aliens shouldn't trivially beat human AI factions either.
- Do exceptions or stalls appear?

Each soak is compared against a vanilla soak of the same fixture and seed. Overnight batches: 4–8 runs.

**Throughput is unmeasured.** The theoretical top speed is about 1 game-month per real minute. Measuring the real rate is the first harness milestone, because it decides how much of L2 we can afford. Levers: extra speed tiers via JSON, `Time.timeScale`, uncapped frame rate, a small window, autosaves off, `smoothAIMissionPlanning` off.

**Caveat:** human factions under AI control are not a human. Soaks tell us about stability, pacing and gross balance. They don't tell us whether a skilled player is challenged. That is what the exploit probes (L1) and chapter playtests (L4) are for.

## L3 Combat lab

The Honorverse skirmish lab is the reference design: a scenario JSON, an auto-run at max speed, results, an event log, a replay, and asserts. We reimplement a slim version in this repo's plugin; no shared code. Uses:
- station defense rework (station vs fleet at the old and new ranges);
- alien fleet doctrine compositions (does a beam-heavy doctrine beat a missile-escort swarm?);
- the new formation and station-keeping commands.

## L4 Chapter playtests

The user plays a 1–3 hour chapter from a fixture save with the mod, for example "Jupiter assault, 2037", "hot war at Earth, 2031" or "first asteroid expansion, 2029". The mod's changes take effect on load (all AI changes are behavioural and read from the current state), so a mid-game save is a valid test. Things that must be set at game start, such as alien doctrines, get a "pick one on load if missing" path and a debug override.

After each session, a short playtest log goes in `playtests/YYYY-MM-DD-<chapter>.md`, with four questions:
1. What did the aliens do that surprised you?
2. What did they do that felt dumb?
3. What did you exploit?
4. Where did you feel pressure, and where did you feel safe?

Following "the user knows when something's wrong, not how to fix it", the log records symptoms. Triage turns each symptom into a probe before anyone touches AI code.

## L5 Full campaigns

These are rare and run by the user. A full campaign is the only thing that tests the late-game arc as a whole: the Jupiter/Saturn plateau and whether the tech tree extension is worth turtling for. In the mod, gate the big changes behind config flags so one campaign can isolate one variable.

## Instrumentation the mod ships with

All of this goes to log files and is off by default for players:
- **Revived `LogAI`:** the developers' own AI trace, to `BepInEx/LogOutput` or its own file.
- **Decision journal:** one line per alien strategic decision (goal created or dropped, attack launched with perceived vs actual SCV, target chosen with its runners-up). This is how we read why the AI did something, from a playtest or a soak.
- **Monthly metrics row:** the same schema as the soaks, so a human campaign produces soak-comparable curves for free.
