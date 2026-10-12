# Handoff (2026-10-12)

State at the end of the first working session. Read this first, then `docs/M2-PLAN.md`.

## Where things stand

**M0 (infrastructure): done.** Plugin, diagnostics, harness and tools all work on the isolated game copy.
- Throughput is about 19–22 game days per real minute; saves load in 55–65 s.
- 0 exceptions and 0 stalls across 2-year soaks, since the mission-phase unstick fix.

**M1 (no idiot ball): done, with honest caveats** (`docs/PROGRESS.md`):

| Item | Result |
|---|---|
| E1 bait (empty magazines / refit) | **Fixed** by per-ship threat memory. Vanilla's perceived strength collapses 4,248 → 1,262; the mod holds 4,104 (true 828). |
| E9 Jupiter plateau | **Improved** by force pooling. Vanilla: 0 launches at Jupiter in a year. Mod: 14.5k pooled and launches made. Assembly is slow. |
| E3 attack sizing | **Not reproduced**: vanilla is already decisive in the 2031 chapter. Kept as a regression guard. |
| E14 chained under-strength attack | Guard built (`NoUnderstrengthShortcut`, now 1.0×). Seen once; may be variance. |
| Soaks (3 × 1 year, 2031) | M1 ≈ vanilla in that chapter. The earlier dramatic single-run result was noise. |

**M2 (a different opponent each game): about half done** (`docs/M2-PLAN.md`):

| Step | Status |
|---|---|
| 1. Strategy state + sidecar | ✅ gate passed (same campaign → same doctrine; sidecar round-trips; key = `realWorldCampaignStart`) |
| 2. Doctrines scale dials | ✅ first pass. Raider vs Bastion: 22 vs 3 raid picks, 13 vs 3 captures, 20 vs 9 launches |
| 3. Raider capability | ✅ **Vanilla bug fixed:** the alien marine pod read as 0 because `marineOpsValue` only checks the first rule. Raids issue `CaptureHab` (a capture destroys the hab for aliens). E5 passes: 11 bases destroyed in 270 d vs 1. |
| 4. Fleet doctrines (designer bias) | **not started**, the next step |
| 5. Disposition (contempt) | ✅ v1, gate passed (2039 Resistance starts at 0.40, falls 0.02/month while holding Jupiter) |
| 6. Economy (mine what they lack; base cap → exotics upkeep) | **not started** |

**Also not started:**
- the doctrine "shame" shift;
- opening-weighted doctrine selection;
- fury;
- desperation → P4 escalation;
- per-difficulty dial tables;
- the posture panel UI;
- the info-game (WS5);
- stations (WS6), the tech extension (WS7), fleet controls (WS8).

## Open questions for the user

1. **Fresh mine spam.** Undefended bases now get raided, but newly spammed tier-1 mines are low priority: the aliens hit bigger bases first, and it takes about 8 months from goal to kill. Should fresh mine spam be punished faster? Levers:
   - more capture slots;
   - a cluster-value bonus;
   - keeping troop carriers in reserve.
2. **Starting contempt by difficulty.** Should difficulty set the starting contempt value? (Asked earlier; not answered.)
3. **Hard or soft terminus clock.** The user proposed both: steady widening plus a terminus megaproject that is an alien win. The details of what the terminus completion triggers are still to design.
4. **A chapter playtest by the user.** Not yet set up. The plugin can be installed on the game copy (`tools/deploy.ps1`). There is no one-command playtest setup yet: it needs a non-harness launch, and the user's own saves would then get sidecars.

## Next steps (recommended order)

1. **M2 step 4: fleet doctrines.** Bias `TIFactionState.GetDesiredShipToBuild` / `AIDailyFactionPlanner.DesignShipsCoroutine` (alien branch ~5740–6317, `DesignAlienShip` ~13196) by weapon class, hull size and engagement style.
   - Gate: fleet composition differs between two forced doctrines in a soak.
   - The journal needs a fleet-composition record first (weapon classes and hull mix per month).
2. **M2 step 6: economy.**
   - **Mine what you lack:** weight `LegacyHabPlanner.SelectHabSiteForDevelopment` (~773) by resource need.
   - **Base cap:** replace `maxAlienBaseGoals_*` with exotics upkeep (`AlienHabPlanner.ManageHabGoals`, `TIGlobalConfig.GetMaxAlienBases` ~2576).
   - Gate (soak): alien metals no longer sit near zero, and bases grow past 13 when exotics allow.
3. **Doctrine shift on failure** (bookkeeping on the combat journal and hab losses), and **opening-weighted selection**.
4. **P4 escalation:** desperation when contempt is low and alien losses are high. Build the widening clock on vanilla's `alienSetupStartIncome`/`EndIncome` ramp (`TIGlobalConfig` ~3035–3055; see DIALS.md).
5. **Regression pass on one build:** `tools\probes.ps1 -All`, plus `tools\soaks.ps1 -Fixture accel-2031-hot-war -Days 365 -Runs 3`, then `python -I tools/aggregate.py "soak-accel-2031-hot-war-r*"`.

## How to run things

```
tools\run.ps1 -Probe e5-undefended-mines                 # one probe (exit 2 on fail)
tools\run.ps1 -Probe e1-empty-magazine-bait -Vanilla     # vanilla baseline of a probe
tools\probes.ps1 -All                                    # every probe except smoke
tools\soaks.ps1 -Fixture accel-2031-hot-war -Days 365 -Runs 3
python -I tools\summarize.py lab\results\<run>           # story of a run
python -I tools\compare.py lab\results\<A> lab\results\<B>
```

Probe roster (`lab/probes/`):

| Probe | Kind | Status (mod) |
|---|---|---|
| smoke | harness check | pass |
| e1-empty-magazine-bait | exploit | pass |
| e3-attack-sizing | regression guard | pass |
| e5-undefended-mines | exploit | pass |
| e9-jupiter-plateau | exploit | pass |
| doctrine-raider, doctrine-bastion | M2 step 2 gate | pass |
| contempt-arc | M2 step 5 gate | pass |

## Gotchas learned the hard way

- **Runs are non-deterministic.** Single-run conclusions were wrong twice. Use repeated soaks.
- **The Autopilot can leave the councilor mission phase stuck.** The harness finalizes it after 20 s (`unsticks` in results). Stalls log the pending prompts and abort after 4 in a row.
- **Display settings.** Unity's display prefs share a registry key with the Steam install. `run.ps1` backs it up and restores it; it also repairs the key after an interrupted run, from `C:\Games\TI-ImprovedAI-harness\display-registry-backup.json`.
- **The user's campaign uses no missiles.** `ai.spawnfleet … missile` borrows another faction's design.
- **Aliens never own human habs.** `CaptureHab` by aliens destroys the hab.
- **Escaping.** Python heredoc string-replaces mangle Windows paths (`\f`, `\S`); use the Edit tool for those files. PowerShell 5.1 has no `??`, and `$args` is reserved.
- **Fixture saves are all from one Normal accelerated Resistance campaign** (`lab/fixtures.json`). There are no fixtures yet for other factions, difficulties or the Dark Skies scenarios.
