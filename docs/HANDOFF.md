# Handoff (2026-10-23)

Read this first. Details live in `docs/PROGRESS.md` (newest first), decisions in `docs/DESIGN.md` ("Decisions"), every tunable in `docs/DIALS.md`, and the test method in `docs/TESTING.md`.

## State in brief

| Area | State |
|---|---|
| **M0 infrastructure** | Done. Plugin, diagnostics, harness and tools work on the game copy; ~17–24 game days per real minute. |
| **M1 no idiot ball** | Done: threat memory (E1 bait), force pooling (E9 Jupiter plateau), decisive force, the shortcut guard. |
| **M2 a different opponent each game** | Built: seeded doctrines and shame shifts, raids and captures, fleet doctrines, contempt, economy (mine what you lack, exotics upkeep, sustainable base cap, war chest, no Earth purchases), the widening wormhole, P4 escalation, time-aware pooling and rendezvous, proxy trade, the terminus (hard alien victory). |
| **Human AI track** | Built: vanilla bugs H1, H2, H4, H5 and H6 fixed (H3 off); MC plan; queue fixes; the human market (metals, nobles, water); the drain guard; personalities, clients and forgiveness. Year-one ships from 2034: **112 vs vanilla 69** (`docs/HUMAN-AI.md`). |
| **The strategist (v0)** | Built and working: a stronger model (Opus, local, headless Claude Code) directs every non-player faction every 3 months, with the game paused; intel to the player through pacts, intel-sharing, clients or turned councilors (`docs/STRATEGIST.md`). Ran 61 checkpoints over a 15-year campaign without a timeout. |
| **Testing tools** | Flow probes (the regression check), `ai.control` (any faction to the game's AI), `ai.move` / `ai.merge` (scripted orders), `ai.bot` (rule-based player), the strategist watcher's `-Harness` mode. |

## Latest work (2026-10-23)
**Since:** capital-hull retirement when exotics run out (user decision), a block on new alien bases while short of exotics, and the exotics slack now counts the terminus draw (`retire-2041`, PROGRESS).

A 15-year campaign with the strategist (2026 → 2041) exposed four problems; the fixes are built and passed a one-year test from that campaign's final save (`fixes-2041`, PROGRESS):
1. **Terminus by date** (user decision): it now also begins by campaign year 14 / 11 / 9 / 7 (Cinematic → Brutal). In the test it began on 1 Sep 2041 and reached 10.8% in a year.
2. **Stranded fleets:** alien fleets leave dry ships behind (a fleet moves only as far as its emptiest ship); fleets on send orders join strikes outside escalation.
3. **Exotics drain:** no new capital hulls in deficit at or below the war chest.
4. **Strategist strike lists** capped at five.

**Regression check passed on the final build** (`flow-2034`, `flow-2039`, 2026-10-23): retirement and the base block did not fire there (healthy stocks); 2034 aliens 24k → 34k, 18 bases, AI-human ships 107; 2039 aliens 40k → 70k.

## Open questions for the user
1. **Retirement pace.** Retirement (user decision 2026-10-23) works but cuts in one month: 132 capital hulls and 40% of alien SCV in the 2041 test. Keep it, or cap it per month (e.g. 10% of capital hulls) and spread it out?
2. **"Allied" for intel:** the game has no formal alliance, so a non-aggression pact or an intel-sharing treaty counts. Narrower?
3. **Forgiveness rates** (2026-10-19 starting values): Exodus 15%/month, Initiative 6%, Academy 4%, Resistance 3%, pro-alien 2%, Humanity First never.
4. **Chapter playtest:** postponed by the user (2026-10-20) while improvements land. `tools\playtest.ps1 -Fixture <name> -Strategist` is ready for it.

## Next steps (recommended order)
1. **Why the aliens built no ships in 2041–42** (542 → 541 with 1.5k exotics above the chest): read the build pick and the yard queues in `fixes-2041`.
2. **Retirement pace** once the user decides (open question 1). Also: with bases (38) and the terminus (21) taking 83% of the 2041 drip, the stock hovers near 0 even after retirement; the base cost per base (2/month) may want tuning with the widening clock.
3. **A long run with a player that acts.** Every harness run so far has a passive player (the Autopilot gives no orders): in 15 years the Resistance held one hab and no fleet. Use `ai.control ResistCouncil ai` or `ai.bot` in long runs, so the aliens and the strategist face a real opponent.
4. **Strategist next:** a digest of what happened since the last checkpoint in each brief; event-triggered checkpoints; more levers (research focus, build mix). Watch the gang-up on the player (5 of 6 factions named the Resistance a rival in 2039, where it is the runaway leader; none did in 2026–28).
5. **Later:** rendezvous v2 (empty staging points), proxy trade v2 (a player-run pro-alien faction), the AI human defence of remote holdings (the AI-run Resistance abandoned Jupiter; the strategist's `defend` lever addresses part of it), fury, the posture panel, the info-game (WS5), stations (WS6), the tech extension (WS7), fleet controls (WS8).

## How to run things

```
tools\probes.ps1 flow-2034, flow-2039                    # the regression check (~45 min)
tools\run.ps1 -Probe <name> [-Vanilla] [-Days N]         # one probe (exit 2 on a failed assert)
tools\run.ps1 -Probe fixes-2041 -SavePath lab\results\20261022-232243-campaign-2026-15y\final.gz   # from a run's final save
tools\soaks.ps1 -Fixture accel-2034-fusion-drives -Days 730 -Runs 3 [-ModOnly]
tools\strategist.ps1 -Harness                            # the strategist watcher for harness runs (start before the run)
tools\playtest.ps1 -Fixture accel-2031-hot-war [-Strategist]   # play it yourself, isolated saves
python -I tools\probe.py lab\probes\<probe>.json lab\results\<run>   # re-evaluate a run
python -I tools\summarize.py lab\results\<run>           # story of a run
python -I tools\aggregate.py "<glob of run folders>"     # means over soaks
```

Useful probes (`lab/probes/`):

| Probe | What |
|---|---|
| **flow-2034, flow-2039** | **The regression check**: natural years, health asserts and many observations. |
| flow-2039-long (-ai, -bot) | 540 days from 2039: the Jupiter strike arrives and fights; passive, AI-run or bot-run Resistance. |
| flow-2026, campaign-2026-15y | The early game with the strategist; the 15-year campaign (overnight, ~4.5 h). |
| strategist-2039, fixes-2041 | The strategist end to end; the 2026-10-23 fixes (from the 15-year final save). |
| econ-war-chest, human-batch5, human-siteweight-on/off | Feature gates and A/Bs. |
| e1, e3, e5, e9, doctrine-*, fleet-*, contempt-arc, econ-*, escalation-*, terminus | Targeted probes with injected setups: run only for the feature being changed. |

## Gotchas learned the hard way
- **Runs are non-deterministic.** Single-run conclusions were wrong several times (most recently Exodus's expansion: a few months' difference in its first Mars landing swings two-year totals by ~10 habs). Use repeated runs before changing anything.
- **Regression = two flow probes, not the whole roster** (user, 2026-10-21); consolidate several checks into one run.
- **The harness player is passive** (the Autopilot clears prompts and resolves combat, nothing more). The harness cannot start a brand-new campaign; "fresh" means the 2026 fixture.
- **The strategist in the harness:** start `tools\strategist.ps1 -Harness` before the run; each run keeps its exchange files in `lab\results\<run>\strategist`. While it thinks, the harness holds the Autopilot off so the clock stays stopped. Never `tail -f` the watcher's log on Windows (it locked the file and killed the watcher once; the log write is now fault-tolerant).
- **Escaping.** The shell tool collapses `\\` to `\` even inside quoted heredocs, which breaks JSON with Windows paths: write such files with the Edit or Write tool, or use forward slashes. PowerShell 5.1 has no `??`, `$args` is reserved, and it pipes text to programs as ASCII unless `$OutputEncoding` is set (the watcher sets UTF-8).
- **Probe expressions** run with a small set of builtins (`len sum min max any all abs round sorted set median mean`): no `zip`, `dict` or `str`.
- **BepInEx keeps old config values**; `deploy.ps1` deletes the plugin .cfg so the current defaults apply. Probes and playtests override settings per launch (`config` in the probe, `-Strategist` for playtests).
- **Display settings** share a registry key with the Steam install; `run.ps1` backs it up and restores it.
- **Aliens never own human habs** (a capture destroys the hab). **The user's campaign uses no missiles** (`ai.spawnfleet … missile` borrows another faction's design).
- **Fixtures** are all from one Normal accelerated Resistance campaign (`lab/fixtures.json`), plus the 15-year run's final save (2041, a mod campaign).
