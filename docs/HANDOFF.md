# Handoff

Where the project stands and what to do next. Design and user decisions are in `DESIGN.md`; how to run things is in `TESTING.md`. History is in git; this file says only what is true now.

## State

| Area | State |
|---|---|
| **Infrastructure** | Plugin, diagnostics, harness and tools work on the game copy. |
| **Aliens: no idiot ball (M1)** | Built: threat memory, decisive force and the shortcut guard, force pooling, stance courage. **Open in `EXPLOITS.md`:** E2 scarecrow, E4 no re-evaluation in flight, E6 fuel-drain decoy, E7 surveillance reset, E12 never targets the strongest fleet, E13 never retreats. None has a probe yet. |
| **Aliens: a different opponent each game (M2)** | Built: seeded doctrines and shame shifts, raids and captures, fleet doctrines, contempt, the economy (mine what you lack, exotics upkeep, sustainable base cap, war chest, hull retirement, no Earth purchases), the widening wormhole, escalation, time-aware pooling and rendezvous, proxy trade, the terminus (alien victory). |
| **AI human factions** | Built: vanilla bugs H1, H2, H4, H5, H6 fixed (H3 off), the mission-control plan, queue fixes, the human market, the drain guard, personalities, clients and forgiveness (`HUMAN-AI.md`). |
| **Strategist** | Built: a stronger model directs every non-player faction every 3 months with the game paused (`STRATEGIST.md`). 61 checkpoints over a 15-year run without a timeout. |

## What is not known

**Nobody has played it.** Every result so far comes from unattended runs with a passive player: the Autopilot gives no orders, and in the 15-year run the Resistance held one hab and no fleet throughout. The numbers show the mod is stable and that the aliens out-build and out-fight vanilla against opponents who do nothing. They do not show whether the aliens are a good opponent for a person, which is the goal. Many of the recent fixes (terminus by date, hull retirement, the base block, dry-ship splits) also rest on a single run each.

So: no new features, dials or switches until the first two steps below are done. They will change what matters.

## Next steps

1. **A chapter playtest by the user.** `tools\playtest.ps1 -Fixture accel-2034-fusion-drives` (add `-Strategist` for the strategist). One to three hours, then symptoms: what surprised, what felt dumb, what was exploitable, where there was pressure.
2. **Long runs against a defender that acts**, repeated: `flow-2039-long-ai` and `-bot` exist; run each three times and aggregate. The same from 2034 for two years. This is the first balance evidence against opposition.
3. **The open exploits.** They are the original definition of "no idiot ball" and were passed over for new systems. E4, E12 and E13 have their mechanism located in vanilla code; each needs a probe that fails on vanilla, then a fix. Most need an active defender, which step 2 provides.
4. **Why the aliens built no ships in 2041–42** (542 → 541 with 1.5k exotics above the war chest): read the build pick and the yard queues in the `fixes-2041` run.
5. **Exotics balance after retirement** (open question 1).
6. **Strategist:** a digest of what happened since the last checkpoint in each brief; event-triggered checkpoints; more levers (research focus, build mix). Watch the gang-up on the player: 5 of 6 factions named the Resistance a rival in 2039.
7. **Later:** rendezvous at empty staging points, proxy trade with a player-run pro-alien faction, AI humans defending remote holdings, fury, the posture panel, the information game (WS5), stations (WS6), the tech extension (WS7), fleet controls (WS8).

## Known defects

Found by reading the code; each changes behaviour, so each needs its probe before and after.

- **Threat memory is lost on a load** (it is not in the sidecar) **and on a ship rename** (it is keyed by ship name, because a refit creates a new ship state that keeps only the name). A reload or a rename reopens the bait exploit E1. Fix: key by ship ID, carry the memory across `CopyDataForRefit`, persist the best showing per ship in the sidecar.
- **Station targets are over-sized.** `DecisiveForce.DefenderThreat` adds the defending fleets and the perception factor on top of vanilla's `PerceivedAggregateDefensiveScore_Station`, which already includes both.
- **Two exotics budgets disagree.** The base cap (`Economy.ExoticsBaseCap`) ignores the terminus draw and the war chest; the slack and the "short of exotics" test count both. The cap can allow bases that the base block then refuses. Settle this with open question 1.
- **The muster point is rarely reconsidered.** `Rendezvous.Choose` reports "decided" on its early exits too, so a goal that could not gather its need is not looked at again until escalation flips. Reported by review, not yet confirmed in a run.
- **Human personalities:** a "friend" directive can raise hate when the base term is already below −1.5; client relations flip at a single compatibility threshold with no band; a `defend` goal is never lowered when a later directive drops the system. Reported by review, not yet confirmed.
- **Body names are compared as English text** (`Escalation`, `RaidPlanner`): a non-English game would read no gas-giant threat.
- **Playtest launcher:** it does not restore the display registry key, leaves `run.json` and the strategist watcher behind if killed, and ignores `@N` commands. The watcher stops on any exception in its loop.

## Open questions for the user

1. **Exotics after retirement.** Bases (38/month) and the terminus (21) take ~83% of the 2041 drip, so the stock hovers near 0 even once upkeep fits. Tune the base upkeep (2/month) with the widening clock?
2. **"Allied" for intel.** The game has no formal alliance, so a non-aggression pact or an intel-sharing treaty counts. Narrower?
3. **Forgiveness rates** are starting values: Exodus 15%/month, Initiative 6%, Academy 4%, Resistance 3%, pro-alien 2%, Humanity First never.

## Gotchas

- **Runs are not deterministic.** Single-run conclusions have been wrong several times (a few months' difference in Exodus's first Mars landing swings two-year totals by ~10 habs).
- **The strategist in the harness:** start `tools\strategist.ps1 -Harness` before the run. While it thinks, the harness holds the Autopilot off so the clock stays stopped. Don't `tail -f` the watcher's log on Windows: a reader holding the file can block the writer.
- **Escaping.** The shell tool collapses `\\` to `\` even inside quoted heredocs, which breaks JSON with Windows paths: write such files with the Edit or Write tool, or use forward slashes.
- **PowerShell 5.1** has no `??`, `$args` is reserved, `"$k:"` is a drive-scoped variable (use `${k}:`), `*>` writes UTF-16, and it pipes text to programs as ASCII unless `$OutputEncoding` is set.
- **Git Bash mangles robocopy flags** (`/E` becomes `E:/`); run robocopy from PowerShell. Exit code 1 is success.
- **BepInEx keeps old config values.** `deploy.ps1` deletes the plugin `.cfg` so current defaults apply; probes and playtests override settings per launch.
- **A running batch deploys once**, at its start. While one runs, compile-check with `dotnet build -o <scratch>` and don't deploy.
- **Aliens never own human habs** (a capture destroys the hab). **The user's campaign uses no missiles** (`ai.spawnfleet … missile` borrows another faction's design).
- **Game patches.** The patches target build 1.0.53b by method name and, in places, call order. After a game update: re-sync the game copy, regenerate `_ref/decompiled`, check `bepinex.log` for failed patch classes, and run the regression check.
