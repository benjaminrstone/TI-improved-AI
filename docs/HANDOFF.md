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

**The campaign to tune for:** Veteran, an anti-alien faction (probably the Academy; never the Protectorate or Servants), with the strategist running. The feel wanted is a challenging game for a somewhat experienced player: not easy, and not a puzzle box for experts. Veteran is therefore the reference level for tuning; the other levels are set relative to it.

## What is not known

- **How it plays against opposition.** No run has had a player who fights the aliens. The Autopilot gives no orders, and the two stand-ins built so far do not change that: the game's AI on the player's faction builds and fights the other humans but attacked the aliens in one run of nine, and the bot only reinforces and merges. Against either, the aliens' numbers are the same as against a passive player (`TESTING.md`, baseline). What the runs show is that the mod is stable over twelve more long runs and that its aliens out-build and out-fight vanilla's against a player who does not come for them.
- **A real campaign's conditions.** No run has started from a new game (the harness cannot; "fresh" has meant the 2026 fixture), and every fixture is one accelerated Normal Resistance campaign. Day one of a new game, Veteran (which the user will play), and any player faction but the Resistance are untested. On Veteran the mod differs from Normal in five dials (lower starting contempt, faster learning, faster widening, a cheaper terminus that begins by year 9) and two extra doctrines (Blitz, Hunter-killer), on top of vanilla's own Veteran changes. None of that has run.
- **The strategist in a played game.** It will run in the user's campaign, but it has only run in unattended harness runs with a passive player. The watcher stops on any exception and waits without a timeout.
- **Save and reload.** One check so far: 180 days from 2039, a save, 180 more from that save (`run.ps1 -FinalSave`, then `-SavePath`). Everything in the sidecar carried over (doctrine, escalation and its dials, terminus progress, contempt, grudges, raid goals) and a pooled strike that was gathering at the save re-chose the same muster point and launched five weeks later. Not yet checked: a reload during a strike's flight, during a strategist checkpoint, or repeated many times. Known to be lost on a load: threat memory (below) and the muster clock (a complete force waits its `MusterDays` again).
- **Single runs.** Many recent fixes (terminus by date, hull retirement, the base block, dry-ship splits) rest on one run each.

So: no new features, dials or switches until steps 1 to 4 are done. They will change what matters.

## Next steps

1. **A stand-in that attacks the aliens.** The game's AI and the bot are done and repeated (three runs each from 2034 and 2039) and neither pressures the aliens. Build an aggressor: a bot mode that builds and gathers a fleet and sends it at alien fleets and bases through `Harness/Orders.cs`, strong enough to win some fights (give it resources and techs if it must). It is what the exploit probes need as well. The strategist directing the player's faction is the later, dearer version.
2. **Campaign safety.** Go through what could ruin the one real play and close each: the terminus ending the campaign (is it fair, is it visible in time, does switching it off mid-campaign work); the load-time defects below; a reload in the middle of a pooled strike, a checkpoint, an escalation; the strategist watcher dying or hanging mid-campaign; turning `VanillaAI` on mid-campaign as the escape hatch. Add a probe that saves and reloads mid-run.
3. **Veteran.** Add a harness command that sets the difficulty of a loaded save, then repeat the flow and stand-in runs at Veteran with the strategist on. Judge the result against the feel the user wants, and retune the per-difficulty dials with Veteran as the reference.
4. **A campaign from a new game.** Teach the harness to start one (or script it once and keep the day-one save as a fixture): Veteran, the Academy as the player with a stand-in at the controls, the strategist on. Run it for years.
5. **The open exploits.** They are the original definition of "no idiot ball" and were passed over for new systems. E4, E12 and E13 have their mechanism located in vanilla code; each needs a probe that fails on vanilla, then a fix. Most need the aggressor from step 1.
6. **Why the aliens built no ships in 2041–42** (542 → 541 with 1.5k exotics above the war chest): read the build pick and the yard queues in the `fixes-2041` run.
7. **Exotics balance after retirement** (open question 1).
8. **Strategist:** a digest of what happened since the last checkpoint in each brief; event-triggered checkpoints; more levers (research focus, build mix). Watch the gang-up on the player: 5 of 6 factions named the Resistance a rival in 2039.
9. **Later:** rendezvous at empty staging points, proxy trade with a player-run pro-alien faction, AI humans defending remote holdings, fury, the posture panel, the information game (WS5), stations (WS6), the tech extension (WS7), fleet controls (WS8).

## Known defects

Found by reading the code; each changes behaviour, so each needs its probe before and after.

- **Threat memory is lost on a load** (it is not in the sidecar) **and on a ship rename** (it is keyed by ship name, because a refit creates a new ship state that keeps only the name). A reload or a rename reopens the bait exploit E1. Fix: key by ship ID, carry the memory across `CopyDataForRefit`, persist the best showing per ship in the sidecar.
- **Station targets are over-sized.** `DecisiveForce.DefenderThreat` adds the defending fleets and the perception factor on top of vanilla's `PerceivedAggregateDefensiveScore_Station`, which already includes both.
- **Two exotics budgets disagree.** The base cap (`Economy.ExoticsBaseCap`) ignores the terminus draw and the war chest; the slack and the "short of exotics" test count both. The cap can allow bases that the base block then refuses. Settle this with open question 1.
- **The muster is usually skipped.** `ForcePooling` runs after vanilla's `ManageFleets`, which launches the anchor as soon as it meets the goal's desired strength; the pool wants `PoolMargin` more and then `MusterDays`. Across 13 mod runs a muster began 13 times and ended in a pooled launch 4 times, never from 2039. Not yet traced which path launched the rest.
- **The muster point is rarely reconsidered.** `Rendezvous.Choose` reports "decided" on its early exits too, so a goal that could not gather its need is not looked at again until escalation flips. Reported by review, not yet confirmed in a run.
- **Human personalities:** a "friend" directive can raise hate when the base term is already below −1.5; client relations flip at a single compatibility threshold with no band; a `defend` goal is never lowered when a later directive drops the system. Reported by review, not yet confirmed.
- **Body names are compared as English text** (`Escalation`, `RaidPlanner`): a non-English game would read no gas-giant threat.
- **Playtest launcher:** it does not restore the display registry key, leaves `run.json` and the strategist watcher behind if killed, and ignores `@N` commands. The watcher stops on any exception in its loop.

## Open questions for the user

1. **Exotics after retirement.** Bases (38/month) and the terminus (21) take ~83% of the 2041 drip, so the stock hovers near 0 even once upkeep fits. Tune the base upkeep (2/month) with the widening clock?
2. **"Allied" for intel.** The game has no formal alliance, so a non-aggression pact or an intel-sharing treaty counts. Narrower?
3. **Forgiveness rates** are starting values: Exodus 15%/month, Initiative 6%, Academy 4%, Resistance 3%, pro-alien 2%, Humanity First never.
4. **Accelerated or standard campaign speed** for the next play? The last one was accelerated (200% research), and so is every fixture.

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
