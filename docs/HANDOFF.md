# Handoff

Where the project stands and what to do next. Design and user decisions are in `DESIGN.md`; how to run things is in `TESTING.md`. History is in git; this file says only what is true now.

## State

| Area | State |
|---|---|
| **Infrastructure** | Plugin, diagnostics, harness and tools work on the game copy. The harness can start a new campaign, reload mid-run and show the player's notices. Every patch is guarded (`PatchGuard`). The user's campaign has its own install (`TESTING.md`): created, started once to the main menu with the plugin loaded, strategist option on. |
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

- **How it plays against a player who builds and plans.** The only stand-in that attacks the aliens is the `hunt` bot, run three times per arm from 2034 (`TESTING.md`, baseline). Against it the mod's aliens end as strong as against a passive player and lose a sixth of the ships vanilla's lose, while the bot still destroys two or three of their stations per run. The bot is handed its ships, ignores surface bases, picks only fights it outguns and has run only at Normal, so this says the mod's aliens are not a pushover under attack; it does not say how hard they are for a person. The other stand-ins (the Autopilot, the game's AI on the player's faction, the bot without `hunt`) do not pressure the aliens at all.
- **A real campaign's conditions.** A campaign from a new game has run for 90 days only (`newgame-smoke`: Veteran, the Academy as the player, the game's AI at its controls), and every fixture is one accelerated Normal Resistance campaign. Years of a Veteran campaign, and any player faction but the Resistance beyond those 90 days, are untested. On Veteran the mod differs from Normal in five dials (lower starting contempt, faster learning, faster widening, a cheaper terminus that begins by year 9) and two extra doctrines (Blitz, Hunter-killer), on top of vanilla's own Veteran changes.
- **The strategist in a played game.** It has only run in unattended harness runs. What the player meets at a checkpoint (the pause notice, the time-out when nobody answers) has run once (`strategist-timeout`); the "Resume now" button and the watcher restarting have not been exercised. **The `claude` command's login on this machine expired on 2026-11-12**: until the user logs in again (`claude`, then `/login`) every checkpoint fails, in harness runs and in play.
- **Save and reload.** A year from 2039 with eight reloads in the running game ran clean (`reload-2039`, one run per build). Everything the mod keeps is in the sidecar and comes back the same: doctrine, escalation and its dials, terminus progress, contempt, grudges, raid goals, directives, threat memory, the muster point and muster clock of a pooled strike (four muster-point choices in the year with reloads, three in `flow-2039` without). Kept in memory only, and rebuilt within days of a load: which goal is the pooled focus. Not checked: a reload while a strategist checkpoint is waiting.
- **`VanillaAI` on and off again.** One chain from 2039: 60 days with the mod, 60 with `VanillaAI` on from that save, 45 with the mod again. The vanilla leg ran clean and carried the sidecar byte for byte; the mod leg loaded it (escalation and all) and went on.
- **What the player sees.** The terminus notices and the campaign-ending popup have each been shown once, in a harness run that displays them (`terminus-ending`). Closing the final popup returns to the main menu. Nobody has looked at the intel notices.
- **Single runs.** Many recent fixes (terminus by date, hull retirement, the base block, dry-ship splits) rest on one run each.

So: no new features, dials or switches until steps 1 and 2 are done. They will change what matters.

## Next steps

1. **A Veteran campaign from a new game.** `campaign-veteran` is it: Veteran, the Academy as the player with the game's AI at its controls, the strategist on, in legs of three years, each from the last leg's final save. The finals are the Veteran fixtures for the stand-in runs (`flow-2034-hunt` and its like). Judge the result against the feel the user wants, and retune the per-difficulty dials with Veteran as the reference.
2. **Campaign safety, the rest.** Done and probed: reloads, the sidecar following renamed saves, a guard on every patch, the strategist's pause notice and time-out, the alien-victory ending, `VanillaAI` on and off again mid-campaign. Still open: a reload while a checkpoint waits; the watcher's restart; the "Resume now" button; a look at the intel notices; whether the terminus is fair at Veteran (when it starts, how long it takes, whether a player can reach the site to set it back), which needs step 1's long run.
3. **Release to the play install again** once step 1's run is clean and its fixes have passed the regression check (`tools\release.ps1`), then tell the user it is ready. The install holds the first build that passed the campaign-safety probes; nothing in it has met Veteran for longer than 90 days.
4. **The open exploits.** They are the original definition of "no idiot ball" and were passed over for new systems. E4, E12 and E13 have their mechanism located in vanilla code; each needs a probe that fails on vanilla, then a fix. Most need an aggressor, which the `hunt` bot now is. A lead from its runs, in both arms: an outgunned alien fleet stays in the hunter's orbit and refuses the fight daily for up to three months until it is caught and destroyed, rather than leaving or being reinforced.
5. **A better aggressor.** The `hunt` bot is cruder than a person. It could stop chasing a fleet that keeps evading, attack surface bases, and build its own ships instead of being handed them. Worth doing when a question needs it, not before.
6. **Why the aliens built no ships in 2041–42** (542 → 541 with 1.5k exotics above the war chest): read the build pick and the yard queues in the `fixes-2041` run.
7. **Exotics balance after retirement** (open question 1).
8. **Strategist:** a digest of what happened since the last checkpoint in each brief; event-triggered checkpoints; more levers (research focus, build mix); directing the player's faction in a stand-in run. Watch the gang-up on the player: 5 of 6 factions named the Resistance a rival in 2039.
9. **Later:** rendezvous at empty staging points, proxy trade with a player-run pro-alien faction, AI humans defending remote holdings, fury, the posture panel, the information game (WS5), stations (WS6), the tech extension (WS7), fleet controls (WS8).

## Known defects

Found by reading the code; each changes behaviour, so each needs its probe before and after.

- **The campaign-ending popup queues behind the day's other notices**, like any notice. Harmless, but it is not the first thing on screen.
- **Refit inheritance of threat memory is untested.** `ThreatMemoryRefit` copies a ship's memory to its refitted state; no probe refits a ship.
- **Station targets are over-sized.** `DecisiveForce.DefenderThreat` adds the defending fleets and the perception factor on top of vanilla's `PerceivedAggregateDefensiveScore_Station`, which already includes both.
- **Two exotics budgets disagree.** The base cap (`Economy.ExoticsBaseCap`) ignores the terminus draw and the war chest; the slack and the "short of exotics" test count both. The cap can allow bases that the base block then refuses. Settle this with open question 1.
- **The muster is usually skipped.** `ForcePooling` runs after vanilla's `ManageFleets`, which launches the anchor as soon as it meets the goal's desired strength; the pool wants `PoolMargin` more and then `MusterDays`. Across 16 mod runs a muster began 15 times and ended in a pooled launch 4 times, never from 2039. Not yet traced which path launched the rest.
- **The muster point is rarely reconsidered.** `Rendezvous.Choose` reports "decided" on its early exits too, so a goal that could not gather its need is not looked at again until escalation flips. Reported by review, not yet confirmed in a run.
- **Human personalities:** a "friend" directive can raise hate when the base term is already below −1.5; client relations flip at a single compatibility threshold with no band; a `defend` goal is never lowered when a later directive drops the system. Reported by review, not yet confirmed.
- **Body names are compared as English text** (`Escalation`, `RaidPlanner`): a non-English game would read no gas-giant threat.

## Open questions for the user

1. **Exotics after retirement.** Bases (38/month) and the terminus (21) take ~83% of the 2041 drip, so the stock hovers near 0 even once upkeep fits. Tune the base upkeep (2/month) with the widening clock?
2. **"Allied" for intel.** The game has no formal alliance, so a non-aggression pact or an intel-sharing treaty counts. Narrower?
3. **Forgiveness rates** are starting values: Exodus 15%/month, Initiative 6%, Academy 4%, Resistance 3%, pro-alien 2%, Humanity First never.
4. **Accelerated or standard campaign speed** for the next play? The last one was accelerated (200% research), and so is every fixture.
5. **The terminus notices name the body the alien homeworld is at** (`[lore]` conflict: the base game reveals where it is through research). Keep the name, or say only "the alien homeworld" until the player has found it?

## Gotchas

- **Runs are not deterministic.** Single-run conclusions have been wrong several times (a few months' difference in Exodus's first Mars landing swings two-year totals by ~10 habs).
- **The strategist in the harness:** start `tools\strategist.ps1 -Harness` before the run. While it thinks, the harness holds the Autopilot off so the clock stays stopped. Don't `tail -f` the watcher's log on Windows: a reader holding the file can block the writer.
- **Escaping.** The shell tool collapses `\\` to `\` even inside quoted heredocs, which breaks JSON with Windows paths: write such files with the Edit or Write tool, or use forward slashes.
- **PowerShell 5.1** has no `??`, `$args` is reserved, `"$k:"` is a drive-scoped variable (use `${k}:`), `*>` writes UTF-16, and it pipes text to programs as ASCII unless `$OutputEncoding` is set.
- **Git Bash mangles robocopy flags** (`/E` becomes `E:/`); run robocopy from PowerShell. Exit code 1 is success.
- **BepInEx keeps old config values.** `deploy.ps1` and `release.ps1` delete the plugin `.cfg` so current defaults apply; probes override settings per run, and the play install takes its settings from `play.ps1`'s remembered options.
- **The game shows its crash dialog and blocks the clock on any exception Unity logs** (`GlobalInstaller.HandleException`). Harness runs switch that off; a real play does not. So the plugin's own per-frame code runs through `PatchGuard.Run`, and a probe's `status['exceptions']` counts what the guard swallowed too (`status['guarded']`).
- **The Autopilot presses a popup's button on every frame it is up.** The campaign-ending popup must be pressed once: each press starts another return to the main menu. The harness does that itself when notices are shown.
- **A run waits for 16 GB of free memory** (`run.ps1 -MinFreeGB`). The game takes 10 to 15 GB; started beside another game it swaps, and its stalls and timings mean nothing.
- **Loading a save in a running game about doubles the game's memory**, with or without the mod: 16.7, 18.2 and 18.7 GB of private memory after the first three reloads from 2039 with the mod, 17.2, 18.2 and 18.7 with `VanillaAI` on (one run each). It is the base game's; the mod adds nothing to it.
- **A running batch deploys once**, at its start. While one runs, compile-check with `dotnet build -o <scratch>` and don't deploy.
- **Aliens never own human habs** (a capture destroys the hab). **The user's campaign uses no missiles** (`ai.spawnfleet … missile` borrows another faction's design).
- **Game patches.** The patches target build 1.0.53b by method name and, in places, call order. After a game update: re-sync the game copy, regenerate `_ref/decompiled`, check `bepinex.log` for failed patch classes, and run the regression check.
