# Progress log

What is built, what the probes show, and what is next. Newest first. Every claim here is backed by a run in `lab/results/` (gitignored); probe definitions live in `lab/probes/`.

## 2026-10-13: M2 steps 4 and 6, shame, widening

### Soak: 3 × 1 year from the 2034 save, vanilla vs mod (build `93d5cd2`: steps 4 + 6 v2; before widening, Earth-purchase block and shame)

| | Vanilla | Mod |
|---|---|---|
| Alien bases | 13 → 13 (3/3) | **13 → 18 (3/3)**, then the exotics cap falls to 13–14 as the stock drains (2.0k → 1.4–1.5k) |
| New bases' metals + nobles share of yields | none founded | **0.37–0.39** (alien income 0.24) |
| Alien metal income over the year | +1% | +4–5% |
| Alien metals stock, median month | 4.2k / 4.6k / 10.1k | 2.2k / 0.9k / 0.6k |
| Alien ships built | 90 / 89 / 65 | 56 / 57 / 61 |
| Alien SCV at end | 31.6k | 30.9k (−2%) |
| Alien battles (won) / human ships killed / alien ships lost | 18.0 (17.7) / 49 / 0.7 | 26.7 (25.7) / 70 / 5.0 |
| Habs lost, all factions | 36.3 | 44.7 |

**Reading.**
- The base-cap half of the step 6 gate passes. Bases grow past 13 while exotics allow and stop when they don't, and they go where the aliens lack material.
- The metals half fails within a year. Five new bases cost metals to build, and their mines repay slowly (+5% metal income by year end). Year-one expansion competes with ship building: 25–35% fewer ships, though alien strength ends only 2% lower.
- Whether expansion pays back (metals recover, fleet grows faster) needs a 2-year soak, which should include widening and the Earth-purchase block.
- Combat is more active under the mod, as in the 2031 soaks.

## 2026-10-12: M0 done, M1 underway

### M2 progress

- **Step 5 v1 done (disposition):**
  - Per-faction alien contempt is seeded from vanilla's learned respect (`perceivedEnemyFleetStrengthFactor`).
  - It falls with alien losses, failed attacks, alien habs destroyed, and presence at Jupiter or beyond; it rises with clean alien wins.
  - It scales attack sizing against that faction.
  - Gate passed: from the 2039 save, Resistance contempt starts at 0.40 (others 0.58–0.80) and falls to 0.30 over 5 months.
- **Step 2 first pass done:** doctrines scale the dials (`AI/Dials.cs`). A forced Raider and a forced Bastion play measurably differently (gate passed).
- **Step 1 done:** alien strategy state, doctrine axes, deterministic derivation per campaign, sidecar persistence (gate passed).
- **Raider groundwork:**
  - Fixed a vanilla bug that made every alien ship's marine value 0.
  - Raids issue marine captures of undefended bases (captures destroy habs for aliens).
  - E5 now passes: 11 bases destroyed in 270 days vs 1 in vanilla's 180.
  - Fresh tier-1 mine spam is still low priority and slow to be punished (about 8 months from goal to kill).

### Infrastructure (M0)

- **Plugin** (`src/ImprovedAI`), on the isolated game copy. Every AI change can be toggled in config, and `[General] VanillaAI=true` turns them all off for baselines.
- **Diagnostics.** `ai-log.txt` (the developers' own AI trace, revived), `journal.jsonl` and `metrics.jsonl`. The journal records:
  - goals, launches, fleets and habs;
  - battles with outcomes;
  - force pooling and raids;
  - weekly alien perception of the player's fleets;
  - attack-fleet operations.
- **Harness.** About 19–22 game days per real minute, roughly 20 minutes per game year. Saves load in 55–65 s.
  - It finalizes the councilor mission phase if the Autopilot leaves it stuck. That stall cost one run 2 hours before the fix.
  - Probe setup commands: `ai.foundoutposts`, `ai.spawnfleet`, `ai.drainammo`, and scheduled `@N` commands.
- **Tools.** `tools/run.ps1`, `tools/probes.ps1`, `tools/probe.py` (asserts) and `tools/summarize.py` (prints a run's story).

### AI changes (M1, alien-only)

| Change | What it does | Evidence |
|---|---|---|
| **Threat memory** (`AI/ThreatMemory.cs`) | Judges human fleets by their best recent per-ship SCV (180 days), not the current loadout | **E1 passes the perception check.** After a 20-ship missile swarm's magazines were emptied, vanilla's estimate fell 4,248 → 1,262 (bait works). The mod's estimate held at 4,104 against a true 828. |
| **Decisive force** (`AI/DecisiveForce.cs`) | Attack requirement = (remembered defenders + half the target faction's other fleets in the system) × 2 | Neutral: vanilla already launched at ≥2× real defenders in the 2031 hot war (E3 vanilla passes). Kept as the basis for the guard below. |
| **Same-system shortcut guard** (E14) | Fleets already in the target's system may skip the desired-strength rule only if they beat the full expected threat | Observed in vanilla: a 907-SCV fleet attacked a station needing 1,861 and was destroyed 6/6. Built; not yet probed. |
| **Force pooling** (`AI/ForcePooling.cs`) | Each 4-day tick, focus ONE attack goal and pool idle and assembly fleets, plus defenders beyond a per-system budget (local human threat × 2, or a 25% reserve), into its fleet | **E9 passes.** Vanilla launched nothing at Jupiter in a year. The mod gathered 14.5k for the Yasui Station attack and launched at Jupiter. The force still assembles slowly: most alien fleets are in transit or busy (logged as donor rejection reasons). |
| **Raid planner** (`AI/RaidPlanner.cs`) | Every 7 days, attack goals on the best value/threat human bases and stations anywhere, during war; per-target failure memory; raids want 2× bombardment | Raids launch and bombard, but **cannot destroy bases**. **E5 still fails.** See below. |

### Why undefended mines are safe (E5): a mechanical finding

Stations die easily. An attacker docks and runs `DestroyHabOperation`, which needs no marines against a station. That is why Earth and Luna stations fall constantly (15 of the passive Resistance's in 6 months with the mod).

**Bases (all mines) can only be destroyed by assault** (`DestroyHabOperation` requires `AssaultCombatValue > 0`, i.e. marines) **or by sustained bombardment.**
- A single alien raider (one ship already exceeds even 2× the bombardment requirement) bombards for 14 days, then leaves to resupply.
- Alien raid fleets carry no marines; their assault carriers are reserved for InvadeEarth.

So the exploit is a missing *capability*, not just targeting. The fix is the **Raider doctrine** (DESIGN P3, the user's "long-range drop pods to pick up outposts without marines or PD"): raid fleets that carry assault ships or drop pods, and AI that builds and assigns them. Moved to M2 rather than hacked into M1.

### Corrections along the way

- **E3 "just enough" was a misreading.** The launch ratio to the *desired* SCV hides vanilla's own margin. Against real defenders, vanilla attacks are decisive and won 6/6 in the 2031 probe. E3 is now a regression guard.
- **E1's first fixture was wrong.** The user's campaign uses no missiles, so draining ammo changed little. The probe now spawns a missile swarm (borrowing another faction's design) and measures alien perception directly, because vanilla didn't attack Earth after the drain at all.

### Repeated soaks: 3 × 1 year from 2031, vanilla vs mod (supersedes the single 2-year runs below)

| 1 year from Nov 2031, mean [min–max] | Vanilla | Mod (M1 + courage, per-ship memory) |
|---|---|---|
| Alien battles (won) | 14.0 (11.7) | 13.0 (11.7) |
| Alien ships lost | 13.7 [11–16] | 12.0 [12–12] |
| Human ships lost to aliens | 35.7 [31–40] | 25.7 [22–32] |
| Alien attack launches | 9.7 | 8.3 |
| Human habs destroyed | 28.3 [26–30] | 25.7 [20–37] |
| Alien SCV at end | 20.3k | 21.5k |

**Correction.** In the 2031 hot war, M1 behaves about like vanilla over a year. The dramatic single 2-year run below (39 vs 9 launches, 0 losses) was mostly run-to-run variance. Single runs cannot settle balance; use `tools/soaks.ps1` + `tools/aggregate.py`.

What does hold up:
- the targeted fixes: E1 perception, E9 late-game pooling;
- mod runs do pool (about 3 ready strikes per year) and raid (about 21 picks);
- but before the marine fix, raids couldn't kill bases, so they changed little.

### First soak (L2): 2 years from the 2031 hot-war save, vanilla vs mod (single run each; see correction above)

| Over 2 game years (Nov 2031 → Nov 2033) | Vanilla | Mod (M1) |
|---|---|---|
| Alien attack launches | 9 | **39** |
| Alien battles won | 14 / 17 | **32 / 32** |
| Alien ships lost | 22 | **0** |
| Human ships destroyed by aliens | 64 | 95 |
| Human habs destroyed (all factions) | 24 | **52** |
| Alien SCV at the end | 21.4k | 27.0k |
| Alien bases | 13 → 13 | 13 → 13 (cap untouched; that is M2 strategy work) |
| Exceptions / stalls | 0 / 0 | 0 / 0 |

The M1 aliens attack about four times as often, never take a fight they lose, and keep the fleet that vanilla bleeds away. Most of the extra pressure falls on the *vanilla human AI factions*, which are out of scope for now. The passive Resistance loses about the same in both runs.

**Open questions:**
- Is "never loses a ship" too cautious? A competent opponent should accept some losses when the prize is worth it.
- Will the human AI factions be steamrolled over a full campaign?

### Watch list

- **Aggression against a passive player.** With the mod, the passive Resistance lost 15 Earth stations in 6 months of the 2034 save, against 3 in vanilla. Chapter playtests need to check this is "pressure", not "unfair".
- **Pooling speed.** Assembling 14k at Saturn took about 9 months of game time. A war-winning response to losing Jupiter (P4) needs mobilisation, not just pooling.
- **Raid targeting.** The raid planner prefers other factions' tier-2 mining bases (value 9) over the player's tier-1 mines (value 4), so the injected mines still drew no goals. Is that the right priority? It needs a design call.

### Next

1. Get raids to destroy bases (E5); decide the raid priorities.
2. Probe the E14 guard.
3. Re-run all probes on one build: a full regression pass.
4. The first soak (L2): 2–3 game years from 2031 with the mod vs vanilla, comparing curves.
5. Then M2: doctrines, the disposition model (contempt × hate) and the wormhole economy.
