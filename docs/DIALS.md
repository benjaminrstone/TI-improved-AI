# Difficulty dials

Which way each alien-side dial should move across difficulty levels. The mod may punish a passive player, but the dials stay visible so that a smooth difficulty curve can be built from them.

Current values and descriptions live in `src/ImprovedAI/AIConfig.cs` (the plugin config, `BepInEx/config/com.improvedai.terrainvicta.cfg`). How doctrine, escalation and the strategist's posture scale the dials is in `src/ImprovedAI/AI/Dials.cs`. This file carries only what the code does not: the intended direction.

**Principle (DESIGN P2).** Difficulty sets the aliens' *constraints* (supply, reach, how far they may concentrate, which doctrines they may use), not how smart they are.

**Status.** Five dials are per difficulty today, as a "C,N,V,B" list (Cinematic, Normal, Veteran, Brutal): `ContemptDifficultyOffset`, `ContemptLearning`, `TerminusCost`, `TerminusStartYear` and `WideningPerYear`. Every other dial is one value shared by all difficulties. Per-difficulty values for the rest wait for soaks on several difficulties.

Direction key:
- `+` means the dial should rise with difficulty.
- `−` means it should fall.
- `=` means competence, kept the same at every difficulty.
- `?` means never decided.

## Mod dials

| Section / key | Direction | Note |
|---|---|---|
| Strategy / StartingContempt | `=` | The Normal base. The difficulty shift is ContemptDifficultyOffset. |
| Strategy / ContemptDifficultyOffset | `−` | Per difficulty. Arrogant on easy, wary on hard. |
| Strategy / ContemptLearning | `+` | Per difficulty. |
| Strategy / Terminus | `=` | Decision: completion is an alien victory at every difficulty. |
| Strategy / TerminusCost | `−` | Per difficulty. |
| Strategy / TerminusStartYear | `−` | Per difficulty. Lower means an earlier hard clock. |
| Strategy / TerminusDripShare | `+` | |
| Strategy / TerminusStartContempt | `+` | |
| Strategy / TerminusSetbackBattle | `−` | |
| Strategy / TerminusSetbackHab | `−` | |
| Strategy / Escalation | `=` | |
| Strategy / EscalationEscalateAt | `−` | Tentative. |
| Strategy / EscalationCalmAt | `?` | |
| Strategy / EscalationCalmMonths | `?` | |
| Strategy / EscalationThreatHabs | `=` | Scale of a desperation input. |
| Strategy / EscalationRespectContempt | `=` | Scale of a desperation input. |
| Strategy / EscalationLossHabs | `=` | Scale of a desperation input. |
| Strategy / EscalationBoldness | `+` | |
| Strategy / EscalatedMaxSuperiority | `+` | |
| Strategy / EscalatedJoinReserve | `−` | |
| Strategy / EscalatedExoticsFitBonus | `+` | |
| Strategy / EscalationDripShareFactor | `−` | |
| Strategy / DoctrineShift | `=` | |
| Strategy / ShameWindowMonths | `=` | |
| Strategy / ShameHabWeight | `=` | |
| Strategy / ShameMinActivity | `=` | |
| Strategy / ShameTolerance | `−` | Tentative. Lower means the command abandons a failing plan sooner. |
| Strategy / ShameRate | `+` | Tentative. A slower shift is a bigger exploitable flaw, which fits lower difficulties. |
| Strategy / ShameDecay | `?` | |
| Strategy / ShameThreshold | `?` | |
| Strategy / ShiftOppositeWeight | `=` | |
| Perception / ThreatMemory | `=` | Remembering a fleet's best recent showing is competence. |
| Perception / ThreatMemoryDays | `+` | Tentative. A shorter memory leaves more room for deception at low difficulty. |
| Commitment / DecisiveForce | `=` | |
| Commitment / AlienAttackSuperiority | `−` | Higher means fewer, safer attacks. Lower means bolder attacks that accept losses. |
| Commitment / ForcePooling | `=` | |
| Commitment / PoolMargin | `=` | |
| Commitment / PoolReserveFraction | `−` | Lower means harder concentration. |
| Commitment / Rendezvous | `=` | |
| Commitment / RendezvousGain | `=` | |
| Commitment / RendezvousMaxJoinDays | `+` | Longer means bigger but later strikes. |
| Commitment / RendezvousDvShare | `=` | A calibration of the travel estimate. |
| Commitment / PoolTimeAware | `=` | |
| Commitment / LeaveDryShips | `=` | |
| Commitment / DryShipShare | `=` | |
| Commitment / ReleaseSendFleet | `+` | |
| Commitment / MusterDays | `+` | Tentative. Longer gives the player a bigger window to pre-empt. |
| Commitment / AttackMaxRatioFloor | `=` | A bug fix in effect. |
| Commitment / ShortcutSuperiority | `−` | At 2.0 the aliens never lost a ship. |
| Combat / StanceCourage | `=` | |
| Combat / ArrivalFightRatio | `−` | Lower means bolder attackers. |
| Combat / DefendHabHopelessRatio | `−` | |
| Targeting / RaidPlanner | `+` | Possibly off on Cinematic (undecided). |
| Targeting / MaxRaidGoals | `+` | Raid tempo. |
| Targeting / RaidImportance | `+` | |
| Targeting / RaidHateWeight | `+` | |
| Targeting / RaidBombardMultiplier | `=` | |
| Targeting / RaidCaptures | `+` | |
| Targeting / MaxCaptureGoals | `+` | Capture tempo. |
| Fleet / FleetDoctrine | `=` | Variety, not difficulty. |
| Fleet / WeaponFocusOdds | `=` | |
| Fleet / WeaponFitBonus | `=` | |
| Fleet / HullSizeBias | `=` | |
| Fleet / MothershipFitBonus | `=` | |
| Fleet / EngagementRoleWeight | `=` | |
| Fleet / AssaultCaptureSlots | `+` | |
| Economy / MineWhatYouLack | `=` | |
| Economy / NeedWeightMax | `=` | |
| Economy / ResourceBaseNeedThreshold | `=` | |
| Economy / NoEarthPurchases | `=` | A lore constraint, the same at every level. |
| Economy / ProxyTradeIntervalMonths | `?` | |
| Economy / ProxyTradeNeed | `?` | |
| Economy / ProxyTradeMargin | `?` | |
| Economy / Widening | `=` | |
| Economy / WideningPerYear | `+` | Per difficulty. The smooth difficulty lever on the drip. It multiplies vanilla's `AI_AlienExoticMultiplier_*`. |
| Economy / WideningMax | `+` | |
| Economy / ExoticsPressureGain | `=` | |
| Economy / ExoticsUpkeep | `=` | |
| Economy / BaseExoticsUpkeep | `−` | Lower means more bases. It takes the place of vanilla `maxAlienBaseGoals_*` as the expansion limit. |
| Economy / SustainableDripShare | `=` | |
| Economy / CapitalShipExoticsUpkeep | `−` | |
| Economy / HugeShipExoticsUpkeep | `−` | |
| Economy / ExoticsStockHorizonMonths | `−` | Shorter means the hoard turns into bases faster. |
| Economy / MaxAlienBasesCeiling | `+` | |
| Economy / ExoticsWarChest | `=` | |
| Economy / ExoticsWarChestMonths | `+` | Bigger means a larger surge when escalated and fewer exotic builds before. |
| Economy / ExoticsHoardMonths | `−` | Shorter means more capital hulls now and a tighter squeeze later. |
| Economy / RetireCapitalHulls | `+` | |
| Economy / RetireMaxShare | `+` | |
| Economy / RetireMargin | `+` | |
| Economy / NoBasesWhenShort | `=` | |
| Fixes / AlienMarineValue | `=` | Vanilla bug fix. |
| Fixes / AlienBattlecruiserName | `=` | Vanilla bug fix. |

Not in the table: the Diagnostics and Strategist sections, the HumanAI section (see `HUMAN-AI.md`), and plain on/off switches with no difficulty direction (`Strategy / Disposition`, `Economy / ProxyTrade`).

## Vanilla difficulty knobs we expect to retune

These are JSON-only (`TIGlobalConfig`), with C/N/V/B values. The full table is in FINDINGS.md §3.

| Knob | C / N / V / B | Notes |
|---|---|---|
| `maxAlienBaseGoals_*` | 10/12/18/25 | Superseded by the exotics-based cap while `Economy / ExoticsUpkeep` is on (`AI/Economy.cs`, `ExoticsBaseCap`). The scripted base goals still bypass the cap, as in vanilla. |
| `initialMaxAlienAttackFleetStrengthRatio_*` | 1.05/1.25/2/2.5 | Interacts with ForcePooling and AttackMaxRatioFloor. |
| `AI_AlienExoticMultiplier_*` | 1/2/3/4 | The wormhole drip. `Economy / Widening` multiplies it over the campaign. |
| `yearsBeforeAlienTotalWarAllowed_*` | 25/20/12/0 | |
| `steadyAlienHateGainModifier_*` | 0/1/1.9/3.3 | |
| `alienQuietDuration_years` (start template) | 0 in the base scenarios | A pacing lever, see below. |

## How the developers pace scenarios

All alien pacing levers live in `TIStartTimeTemplate`. C# defaults: progression modifier 1.0, everything else 0 or off. The base scenarios use none of them. The Dark Skies scenarios use several.

| Lever | 2022 / 2026 / 2030 | 2003 (Dark Skies) | Broken Earth (Dark Skies) |
|---|---|---|---|
| `alienQuietDuration_years`: quiet phase (no spontaneous attacks, ×0.6 hate) | 0 (loud from day 1) | **22.5** | 0 |
| `alienSetupDuration_years`: length of the wormhole income ramp | 0 (full income at once) | **20** | **10** |
| `alienSetupStartIncome` → `alienSetupEndIncome` (× wormhole drip) | none | **0.15 → 1.5** | 0.2 → 1.0 |
| `alienProgressionModifier`: speed of all year-based alien timers | 1.0 | **0.75** | **0.75** |
| `alienStartingProgression_years`: offset on those timers | 0 | **−5** | 0 |
| `alienSurveillanceDelay_years` | 0 | 0 | **5** |
| `invasionFocusedAliens`: 2 concurrent InvadeEarth goals | no | no | **yes** |
| `TIGlobalConfig` overrides | none | none | `AI_WormholeSetupSpeed` C/N/V/B 1.5/1/0.5/**0.01**; `extraYearsToDelayAlienInvasion` 16/9/4/0; `AI_invaderArmiesLostBeforeBuildup` 0 |

What this tells us:

- **Base scenarios get all their alien pacing from the difficulty timers** (years before total war, invasion delay and so on). There is no quiet phase and no ramp. The smoother levers exist but are unused there.
- **2003's income ramp is already a "widening wormhole".** It runs 0.15 → 1.5× the drip over 20 years, scaled by difficulty via `AI_WormholeSetupSpeed`. The mod's widening (`AI/Wormhole.cs`) multiplies the drip on top of this ramp and does not replace it. Setting the widening back by strikes is not built.
- **The developers' own difficulty levers are pacing levers** (delay, ramp, timer speed) plus focus (invasion-focused). That matches the DESIGN P2 principle.
- **Candidate per-difficulty use for the base scenarios (not built):** give them a ramp (for example 0.3 → 1.0 over 8 to 15 years) and a short quiet phase on lower difficulties, so early pressure eases in smoothly.

## Measurements to collect per difficulty

To be filled in by soaks:
- alien attack launches per year, battles won and lost, ships lost on each side, human habs destroyed;
- alien SCV and base count curves;
- time until the first alien strike on the player's assets.
