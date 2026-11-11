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

## The target

**The user's next campaign: one long play, started fresh, with the new systems still to be discovered.** The user is the player, not a tester or a designer of record: they will not playtest, they bring symptoms from their last play and talk through ideas. It will probably be published if it comes out well, as not thoroughly playtested. So:

- **Testing is ours, with stand-in players.** Never ask the user to try a chapter.
- **The bar is one good campaign.** What can ruin a 60-hour game matters most: a crash or soft-lock, an unfair game-over, behaviour that breaks on save and reload, a feature that cannot be switched off mid-campaign. Balance to the last percent does not.
- **Don't spoil it.** Reports to the user give verdicts and health, not what the aliens did in a run or which doctrine a campaign drew.

## What is not known

- **How it plays against opposition.** Every result so far comes from unattended runs with a passive player: the Autopilot gives no orders, and in the 15-year run the Resistance held one hab and no fleet throughout. The numbers show the mod is stable and that the aliens out-build and out-fight vanilla against opponents who do nothing.
- **A real campaign's conditions.** No run has started from a new game (the harness cannot; "fresh" has meant the 2026 fixture), and every fixture is one accelerated Normal Resistance campaign. Day one of a new game, other difficulties and other factions are untested.
- **Save and reload.** Harness runs load once and never reload. A player reloads constantly, and several defects below only show then.
- **Single runs.** Many recent fixes (terminus by date, hull retirement, the base block, dry-ship splits) rest on one run each.

So: no new features, dials or switches until steps 1 to 3 are done. They will change what matters.

## Next steps

1. **A stand-in player, in repeated long runs.** `flow-2039-long-ai` (the game's AI plays the Resistance) and `-bot` exist: run each three times and aggregate, and the same from 2034 for two years. Then a stronger stand-in: let the strategist direct the player's faction too in harness runs, with `Harness/Orders.cs` for fleet orders. This is the first balance evidence against opposition and it doubles as the regression check for the last fixes.
2. **Campaign safety.** Go through what could ruin the one real play and close each: the terminus ending the campaign (is it fair, is it visible in time, does switching it off mid-campaign work); the load-time defects below; a reload in the middle of a pooled strike, a checkpoint, an escalation; turning `VanillaAI` on mid-campaign as the escape hatch. Add a probe that saves and reloads mid-run.
3. **A campaign from a new game.** Teach the harness to start one (or script it once and keep the day-one save as a fixture), on the settings the user will play (open question 4), and run it for years with a stand-in player.
4. **The open exploits.** They are the original definition of "no idiot ball" and were passed over for new systems. E4, E12 and E13 have their mechanism located in vanilla code; each needs a probe that fails on vanilla, then a fix. Most need an active defender, which step 1 provides.
5. **Why the aliens built no ships in 2041–42** (542 → 541 with 1.5k exotics above the war chest): read the build pick and the yard queues in the `fixes-2041` run.
6. **Exotics balance after retirement** (open question 1).
7. **Strategist:** a digest of what happened since the last checkpoint in each brief; event-triggered checkpoints; more levers (research focus, build mix). Watch the gang-up on the player: 5 of 6 factions named the Resistance a rival in 2039.
8. **Later:** rendezvous at empty staging points, proxy trade with a player-run pro-alien faction, AI humans defending remote holdings, fury, the posture panel, the information game (WS5), stations (WS6), the tech extension (WS7), fleet controls (WS8).

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
4. **The next campaign's settings:** faction, difficulty, accelerated or standard, and whether the strategist runs in it (it pauses the game for a few minutes every three months and uses the local Claude subscription). Testing goes where the play will be.

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
