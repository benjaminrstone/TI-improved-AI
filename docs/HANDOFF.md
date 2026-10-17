# Handoff (2026-10-13)

State at the end of the second working session. Read this first, then `docs/M2-PLAN.md`.

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

**M2 (a different opponent each game): steps 1–5 pass their gates; step 6 is in progress** (`docs/M2-PLAN.md`):

| Step | Status |
|---|---|
| 1. Strategy state + sidecar | ✅ gate passed (same campaign → same doctrine; sidecar round-trips; key = `realWorldCampaignStart`) |
| 2. Doctrines scale dials | ✅ first pass. Raider vs Bastion: 22 vs 3 raid picks, 13 vs 3 captures, 20 vs 9 launches |
| 3. Raider capability | ✅ **Vanilla bug fixed:** the alien marine pod read as 0 because `marineOpsValue` only checks the first rule. Raids issue `CaptureHab` (a capture destroys the hab for aliens). E5 passes: 11 bases destroyed in 270 d vs 1. |
| 4. Fleet doctrines (designer bias) | ✅ v1, gate passed (single runs): Swarm/Missiles built 57% small hulls and 47% missiles; Capital/Beams 25% small and 38% beams; neutral 44% small. Weak spot: Brawler barely moves the role mix (`JoinFleet` weights all roles equally). |
| 5. Disposition (contempt) | ✅ v1, gate passed (2039 Resistance starts at 0.40, falls 0.02/month while holding Jupiter). **Now per difficulty** (user decision 2026-10-12). |
| 6. Economy (mine what they lack; base cap → exotics upkeep) | ✅ v2, probe gate passed (single run; soak pending): bases 13 → 18, the cap self-limits as exotics drain, new bases yield 34% metals + nobles against 24% in alien income. v1: bases 13 → 19, then the cap self-limited as exotics fell. Generic picks went metal-rich; the "insecure upkeep" rule kept founding water moons; v2 redirects it. See M2-PLAN step 6. |

**Also not started:**
- the doctrine "shame" shift: **v1 built 2026-10-13**, gate probe `doctrine-shame-shift` runs in the pending regression pass;
- opening-weighted doctrine selection;
- fury;
- desperation → P4 escalation;
- the posture panel UI;
- the info-game (WS5);
- stations (WS6), the tech extension (WS7), fleet controls (WS8);
- the terminus clock (decided: a hard alien victory).

## Decisions from 2026-10-12 (recorded in DESIGN.md)

- Fresh mine spam: leave it as is (grace period).
- Contempt by difficulty: yes. Higher on easy and slower to learn; lower on hard and faster to learn.
- Terminus completion = hard alien victory, LW2 Avatar-style.
- Chapter playtest: build the one-command setup **after steps 4 and 6 land**. That is now next.

## Decisions from 2026-10-13 (recorded in DESIGN.md)

- No buying from Earth for the aliens. v1 is built (`NoEarthPurchases`); probe `econ-earth-buy` still to run on vanilla, then on the mod.
- Exotics upkeep: bases and exotic warships should definitely compete. The widening wormhole is part of that balance, so tune the upkeep numbers together with the widening clock.
- Doctrine selection stays seeded: no opening-weighted selection.

## Open questions for the user

None pending.

## Next steps (recommended order)

State 2026-10-17 (details in PROGRESS):
- **Economy:**
  - the sustainable exotics cap works (2-year soak: alien SCV +25%, a fifth of the losses);
  - the proxy supply line runs at ~+17% alien metal income, capped at what the proxies mine.
- **Escalation (P4):**
  - fires in the Jupiter-held chapter and relaxes sizing to 1.2×;
  - strips lesser goals and burns harder;
  - musters by fleet-aware time to strike.
  - Strikes at Jupiter land in 2/3 runs, 6–10 months after the trigger.
- The AlienBattlecruiser fix, Brawler and Bastion are done.

1. **Chapter playtest by the user** (`tools\playtest.ps1 -Fixture accel-2031-hot-war`, or `accel-2039-clean-jupiter` to feel the escalation). Most M2 systems now exist, so this is the right moment for a human eye.
2. **Exotics-aware shipbuilding:** capital-hull upkeep grows after bases are built, so exotics drain ~20/month. Make the build picker respect the exotics budget.
3. **P4 still to do:**
   - an explicit exotics surge into wormhole-tier warships when escalated;
   - the terminus (hard alien victory) and its construction site;
   - widening setbacks from strikes at the terminus.
4. **Rendezvous v2:** muster at empty staging points (Sun–planet L4/L5, moons) needs a move order that keeps the attack goal. A command node (mothership) could shorten the muster (pherocyte command).
5. **Proxy trade v2:** offer deals to a player-controlled pro-alien faction too (vanilla's TradeToPlayerInitiated prompt).
6. **Regression pass on the current build:** `tools\probes.ps1 -All`, then aggregate a fresh 2-year soak.

## How to run things

```
tools\run.ps1 -Probe e5-undefended-mines                 # one probe (exit 2 on fail)
tools\run.ps1 -Probe e1-empty-magazine-bait -Vanilla     # vanilla baseline of a probe
tools\probes.ps1 -All                                    # every probe except smoke
tools\soaks.ps1 -Fixture accel-2031-hot-war -Days 365 -Runs 3
python -I tools\summarize.py lab\results\<run>           # story of a run
python -I tools\compare.py lab\results\<A> lab\results\<B>
tools\playtest.ps1 -Fixture accel-2031-hot-war               # play it yourself, isolated saves
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
| fleet-swarm-missiles, fleet-capital-beams, fleet-neutral | M2 step 4 gate (+ control) | pass |
| econ-mine-and-cap | M2 step 6 gate | pass (vanilla fails) |

## Gotchas learned the hard way

- **Runs are non-deterministic.** Single-run conclusions were wrong twice. Use repeated soaks.
- **The Autopilot can leave the councilor mission phase stuck.** The harness finalizes it after 20 s (`unsticks` in results). Stalls log the pending prompts and abort after 4 in a row.
- **Display settings.** Unity's display prefs share a registry key with the Steam install. `run.ps1` backs it up and restores it; it also repairs the key after an interrupted run, from `C:\Games\TI-ImprovedAI-harness\display-registry-backup.json`.
- **The user's campaign uses no missiles.** `ai.spawnfleet … missile` borrows another faction's design.
- **Aliens never own human habs.** `CaptureHab` by aliens destroys the hab.
- **Escaping.** Python heredoc string-replaces mangle Windows paths (`\f`, `\S`); use the Edit tool for those files. PowerShell 5.1 has no `??`, and `$args` is reserved.
- **BepInEx keeps old defaults.** Once the game copy's `BepInEx/config/com.improvedai.terrainvicta.cfg` holds an entry, changing its default in `AIConfig.cs` does not change the value in use. Edit the .cfg too (or delete the line).
- **Config overrides in probes** (`"config"` in a probe JSON) work for every type since `5ace626`. Before that, non-bool overrides silently disabled the harness.
- **Fixture saves are all from one Normal accelerated Resistance campaign** (`lab/fixtures.json`). There are no fixtures yet for other factions, difficulties or the Dark Skies scenarios.
