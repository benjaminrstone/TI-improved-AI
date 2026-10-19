# Progress log

What is built, what the probes show, and what is next. Newest first. Every claim here is backed by a run in `lab/results/` (gitignored); probe definitions live in `lab/probes/`.

## 2026-10-18: the full M2 build, then the pivot to human factions

### Regression pass (`probes.ps1 -All`, build `221b8d6`)
14 of 17 pass. The three failures:
- **terminus:** completed, but its end-game notice was not journaled in harness runs (my ordering bug, fixed in `f565230`).
- **doctrine-shame-shift:** the aliens now destroy many more human ships in real battles, so the injected 30:5 losses left shame at 0.97. Recalibrated to 40:3.
- **fleet-capital-beams (Brawler):** the short-range share across 4 runs was 0.30/0.33/0.39/0.30, against neutral 0.27/0.26/0.17/0.38, with only ~18 combat-role builds per run. **The Brawler effect is not distinguishable from noise.** The gate was removed and the share kept as info.

### Soak: 3 × 2 years from 2034, vanilla vs the full current build (sustainable cap, escalation, rendezvous, proxy trade, exotics-aware builds, surge, terminus)

| | Vanilla | Mod |
|---|---|---|
| Alien SCV at end | 35.3k [34.0–37.8] | **40.8k [36.2–45.1]** |
| Alien ships lost / human ships destroyed by aliens | 29 / 144 | **8** / 141 |
| Alien metal income at end | ~2.6k | **4.1–5.1k** |
| Alien bases | 13 → 16 | 13 → 17–18 |
| Habs lost, all factions / Resistance | 95 / 24 | 93 / 29 |
| Alien attack launches | 64 | 47 |
| Proxy trades with the aliens | 0 | 8–14 |
| Escalation / terminus | — | none (correct for 2034: no human presence in the gas giants, contempt above 0.35) |
| Exotics stock | 2.0k → 2.4–2.7k | 2.0k → 1.3–1.7k (upkeep 65–78 against a drip of 60) |

**Reading.**
- The aliens end stronger and lose a fraction of the ships.
- **Exotics-aware shipbuilding did not stop the drain.** It biases new builds away from capital hulls, but the existing capital fleet's upkeep stays. The next lever: escalation-free decommissioning of the oldest capital hulls, or lower capital upkeep. A tuning question for the widening/upkeep balance.

## 2026-10-16: sustainable cap, escalation (P4), fixes

### Probes (build `b84ca9a` / `2adf6ee`)
- **Sustainable cap** (`econ-mine-and-cap`): bases 13 → 17, then stop. Cap 18 → 15, upkeep 42–55 against a drip of 57, exotics stock steady at ~2.0k. New bases yield 48% metals + nobles (alien income 24%). Pass.
- **AlienBattlecruiser fix:** the exotics medium interceptor now gets the Battlecruiser hull. No false escalation in 2034 (`fleet-neutral` pass).
- **Brawler** (opposite range /3): short-range share 0.39 (neutral 0.26–0.27, old Brawler 0.30–0.33). **Artillery** 0.12. Gates set from these observations.
- **doctrine-bastion:** passed twice (the earlier failure was variance).

### Escalation in the 2039 Jupiter save
- **v1:** escalated at once (desperation 0.60: threat 1.0, Jupiter lost), and total war against the Resistance became active. But behaviour matched the control: 2 attack launches in 8 months either way.
  - Total war was already possible by 2039 in this accelerated campaign.
  - The pooled strike sat at 4–8k of a 17–21k need, because every donor fleet was busy.
- **Strip lesser goals when escalated:** the pool filled to ~21k (counting joiners in transit). Two bugs then stopped the launch:
  - "ready" counted joiners in transit;
  - vanilla's weekly 2.3% random discard of war attack goals killed the goal once pooling stopped protecting it (FINDINGS 11b).
- **Fixed:** ready only when the force is present; the goal is protected until launch.

### Soak: 3 × 1 year from the 2039 save, vanilla vs mod (build `c1bc51d`)

| | Vanilla | Mod |
|---|---|---|
| Escalated | — | 3/3, at once |
| Pooled strike launched at Jupiter | 0 | **2/3** (Oct 7: 11.9k, 69 ships; Dec 14: 12.3k), both still in transit at run end |
| Alien attack launches | 8.0 | 3.3 (fewer, larger) |
| Habs lost, all / Resistance | 12.3 / 5.7 | 9.0 / 4.3 |
| Resistance habs at Jupiter (start 5) | 4 / 4 / 4 | 3 / 4 / 3 |
| Alien SCV at end | 67.0k | 61.8k |

**Reading.**
- The late-game arc now exists: the escalated aliens assemble a strike force out of their dispersed fleets and send it at Jupiter. Vanilla never does.
- **But it is slow.** Assembly takes 6–9 months, because joiners cross the outer system one by one. Transit from Saturn takes 5+ more. The first blows would land 12–15 months after the trigger.
- Both strikes targeted Protectorate bases at Jupiter, not the Resistance.
- Alien SCV is ~8% below vanilla, likely the exotics upkeep plus fleets kept in transit. Watch it.

### Escalated strike sizing and harder burns (2026-10-17)
- **Cap at 1.2× when escalated** (user: relax when escalated). The Jupiter need fell from 17–23k to ~8–14k, and the force was assigned by May.
  - But joiners took months to arrive: 0.3–0.5k present through July, 8–10k by December.
  - Committed to the main target, the aliens stopped hitting softer ones. 0/3 Jupiter strikes within the year.
- **Joiners keep a 20% delta-v reserve when escalated** (vanilla 50%), so they take faster trajectories. Jupiter strikes in 2/3 runs:
  - run 1: Maria Theresa Base, Jan 2040, 3.9k;
  - run 2: Tzu-hsi Base, Aug 2039, 3.8k, then **Yasui Station itself, Jan 2040, 9.6k**, the first strike at the main target;
  - run 3: none (10.3k of 14k present at year end).
- **Reading:** assembling a strike across the outer system takes 6–9 months. Joiners come from Neptune and the Kuiper belt, and many alien hulls accelerate slowly. That is mostly physics now, not AI. It also gives the player a readable window: the gathering at Titan is visible months ahead.

### Proxy supply line (2026-10-17)
- **Debug run, no production cap:** on day 30 the Servants gave 2,866 metals + 482 nobles and the Protectorate 1,176 + 295, for ~8.4k water each. By vanilla's own valuation each partner gained 1.2–2.8× what it gave.
- **Soak, 3 × 1 year from 2034, mod only** (build `fc31ee5`: a proxy sends only what its mines produced since the last deal):
  - 6–8 trades a year per run, split evenly between the Servants and the Protectorate;
  - 4.8–6.5k metals and 0.6–0.8k nobles a year into the aliens (~+17% on alien metal income);
  - alien ships built 59–72 (56–61 in the earlier no-trade 1-year soak), SCV at end 32.4–35.1k (30.5–31.4k).
  - The SCV comparison crosses builds; read it as indicative. No habs, control points or orgs ever moved.

### Rendezvous (user, 2026-10-16: "rendezvous somewhere and then set out")
Three fixes were needed to get here (each found with short 10–45-day debug runs):
- **The game's transfer estimate is a Hohmann time:** Titan → Jupiter ≈ 4,500 days. The rendezvous now uses a fleet-aware flip-and-burn time from current positions (Titan → Jupiter: 5.3 AU, 52 days at 1.09 m/s²).
- **Positions:** `GetGlobalPosition()` on fleets and habs failed silently, so positions now come from the natural body they sit at.
- **Decision timing:** decide on the first recruiting tick, ranking by force gathered when the need is out of reach, and decide again when escalation begins.

**escalation-jupiter × 3** (build `99dd575`, one year each):
- At escalation, Titan is the best muster point by time to strike. Saturn is the alien holding closest to Jupiter; Triton, Bononia, Hippasus and Oberon all score worse. So it stays put, correctly.
- Strikes at softer Jupiter targets (need ~5.6k) launched in 2 of 3 runs: Aug 25 (7.9k) and Oct 30 (12.2k). In the third, the big target (Yasui Station, ~17k need) was never reachable.
- **The limiter is force, not geography.** Decisive-force sizing wants 17–23k for Yasui Station, about 1.3–1.75× the whole Resistance navy, because every human fleet that could reinforce is counted. The aliens can free only ~12k within a year.

### Soak: 3 × 2 years from the 2034 save, vanilla vs mod (build `c1bc51d`: sustainable cap, escalation, strike fixes)

| | Vanilla | Mod |
|---|---|---|
| Alien bases (start → 1 y → 2 y) | 13 → 13 → 16 | 13 → 17–18 → 17–18 (**no overshoot**) |
| Alien metal income | 2606 → ~2620 | **2606 → 3901 / 3916 / 5120** |
| Alien metals, median month, year 2 | 13–19k | 2.0k / 4.4k / 8.6k |
| Alien SCV at end | 33.9k [33.4–34.2] | **42.2k [38.9–44.0]** |
| Alien ships lost / human ships destroyed by aliens | 33 / 206 | **7** / 156 |
| Habs lost, all factions | 82 | 103 |
| Exotics stock | 2.0k → 2.6k | 2.0k → 1.5k (upkeep 70–78 against a drip of 60) |
| Escalations | — | 0 (correct: no human threat in the gas giants) |
| **Trades involving the aliens** | **0** | **0** |

**Reading.**
- The sustainable cap holds the base count. With mine-what-you-lack, the economy and fleet end clearly stronger: SCV +25%, a fifth of the losses.
- Exotics still drain ~20/month, because capital-hull upkeep grows after the bases are built and the build picker doesn't budget for it. The stock lasts ~6 more years. Refinement: make the build picker exotics-aware.
- **No AI-to-AI trade with the aliens happened in 6 × 2 years.** The proxy supply line exists in code but not in play. Using it needs the aliens or their proxies to actively seek deals.

## 2026-10-13: M2 steps 4 and 6, shame, widening

### Soak: 3 × 2 years from the 2034 save, vanilla vs mod (build `8ff3f4c`: widening, no Earth purchases, shame, HullSizeBias 3)

| | Vanilla | Mod |
|---|---|---|
| Alien bases (start → 1 y → 2 y) | 13 → 13 → 16 | **13 → 21–23 → 22–23** |
| Alien metal income | 2606 → 2565–2659 | **2606 → 4762 / 4774 / 5946** |
| Alien metals, median month, year 2 | 5.5k / 7.9k / 19.0k | 7.4k / 3.1k / 2.5k |
| Alien SCV at end | 34.9k [33.7–36.8] | **38.5k [35.5–40.0]** |
| Alien ships lost / human ships destroyed by aliens | 38 / 180 | **8** / 142 |
| Alien ships built, year 1 / year 2 | 93–109 / 52–94 | 58–62 / 26–53. Fewer and larger: this campaign's seeded doctrine is Motherships, and HullSizeBias is now 3. |
| Exotics stock | 2.0k → 2.5–2.6k | **2.0k → 0.6–1.0k** (drip 60, upkeep 98–104 per month) |
| Natural doctrine shifts | — | none |

**Reading.**
- **Mine what you lack pays off in year 2.** Alien metal income nearly doubles, and alien strength ends 10% above vanilla with far fewer losses.
- **The exotics cap overshoots.** It allowed ~23 bases while stock and drip were high (stock / 60 counts as income). Capital-ship upkeep then grew with the Motherships doctrine. The cap fell back to 12–16, but bases are never given up, so upkeep sits ~40/month above the drip. The stock runs out in about a year, after which bases eat the whole drip and exotic warships starve.
- **Needs tuning with the widening** (user decision): budget the cap from a sustainable share of the drip, and/or lower the per-base upkeep.

### Regression pass (`probes.ps1 -All`, build `ff5a972`: everything to date)

11 of 13 probes pass:
- **Pass:** contempt-arc, doctrine-raider, doctrine-shame-shift, e1, e3, e5, e9, econ-earth-buy, econ-mine-and-cap, fleet-capital-beams, fleet-neutral.
- **doctrine-bastion fails** on its capture count: 5 goals against ≤ 4 (3 in its first run), with its capture-slot dial at 3 (Raider 13). Single-run variance.
- **fleet-swarm-missiles fails** on its hull mix: 44% small against 57% in its first run, and the neutral control was also 44%. **The hull-size bias is weak.** Missiles stayed strong: design missile mounts 51 → 117, 42% of weapons built. Being retested at `HullSizeBias` 3.

**What the new probes showed:**
- **Shame:** Raider, under injected 80% losses, went 0.31 → 0.61 → 0.82 → 1.05 and then shifted to Land grab (2034-10-01). Its dials followed (capture slots 13 → 8).
- **Widening:** factor 1.42 at 8.4 campaign years (Normal), so the drip went 40 → 56.8/month. With a 2.0k stock the exotics base cap is now 25.
- **Earth purchases:** alien boost stock and income are 0, and money income is 0. **Vanilla aliens could never afford Earth substitution** (`CanAfford` needs the boost in stock). The block changes no behaviour; it guards the lore if a future patch gives aliens boost.

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
