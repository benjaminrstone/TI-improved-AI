# Difficulty dials

Every knob the mod adds or turns, with its current value and its intended direction across difficulty levels. The user's goal (2026-10-12): it is fine for the mod to punish a passive player, but these dials must stay visible so we can build a smooth difficulty curve.

**Principle (DESIGN P2).** Difficulty sets the aliens' *constraints* (supply, reach, how far they may concentrate, which doctrines they may use), not how smart they are.

**Status:** the contempt dials are per difficulty (a "C,N,V,B" list); every other mod dial is one value today, shared by all difficulties (the plugin config in `BepInEx/config/com.improvedai.terrainvicta.cfg`). Per-difficulty tables come once soaks on several difficulties exist.

Direction key:
- `+` means this dial should rise with difficulty.
- `−` means it should fall.
- `=` means competence, kept the same at every difficulty.

## Mod dials

| Section / key | Now | Direction | What it changes |
|---|---|---|---|
| Strategy / StartingContempt | 0.8 | (base) | Starting contempt at Normal, before vanilla's learned respect. |
| Strategy / ContemptDifficultyOffset | +0.15 / 0 / −0.1 / −0.2 | `−` | **Per difficulty (C/N/V/B).** Added to StartingContempt (user, 2026-10-12). |
| Strategy / ContemptLearning | 0.7 / 1 / 1.25 / 1.5 | `+` | **Per difficulty.** Multiplier on contempt losses: how fast the aliens learn respect. |
| Perception / ThreatMemory | on | = | Remembering a fleet's best recent showing is competence, not difficulty. |
| Perception / ThreatMemoryDays | 180 | `+` (maybe) | A shorter memory leaves more room for deception at low difficulty. |
| Commitment / DecisiveForce | on | = | |
| Commitment / AlienAttackSuperiority | 2.0 | `−` | Higher means more cautious attacks: fewer, safer fights. Lower means bolder attacks that accept losses. Candidate for the "never loses a ship" question. |
| Commitment / ForcePooling | on | = | |
| Commitment / PoolMargin | 1.1 | = | |
| Commitment / PoolReserveFraction | 0.25 | `−` | How much of each system's force stays home when pooling. Lower means harder concentration. |
| Commitment / AttackMaxRatioFloor | 1.5 | = | A bug fix in effect. |
| Commitment / ShortcutSuperiority | 1.0 | `−` | A fleet already in the system may attack below its goal's desired strength at this ratio to the full expected threat. Was 2.0, which gave "never loses a ship". |
| Combat / StanceCourage | on | = | Fight instead of evading when defending own habs, or on arrival at about even odds (user, 10-12). |
| Combat / ArrivalFightRatio | 0.9 | `−` | Lower means bolder attackers. |
| Combat / DefendHabHopelessRatio | 0.25 | `−` | Defenders of a hab evade only below this ratio. |
| Targeting / RaidPlanner | on | `+` (Cinematic off?) | |
| Targeting / MaxRaidGoals | 4 | `+` | Raid tempo. |
| Targeting / RaidImportance | 16 | `+` | How raids compete with other goals for fleets. |
| Targeting / RaidHateWeight | 1.0 | `+` | How strongly hate focuses raids on one faction. |
| Targeting / RaidBombardMultiplier | 2.0 | = | |
| Targeting / RaidCaptures | on | `+` | Marine captures (= destruction) of undefended bases off Earth/Luna |
| Targeting / MaxCaptureGoals | 6 | `+` | Capture tempo (one cheap troop carrier each) |
| Fixes / AlienMarineValue | on | = | Vanilla bug fix (alien marine pod read as 0) |
| Fleet / FleetDoctrine | on | = | Fleet doctrines bias alien design and build choices (M2 step 4). Variety, not difficulty. |
| Fleet / WeaponFocusOdds | 3 | = | Doctrine weapon class vs the role's own class while designing. |
| Fleet / WeaponFitBonus | 1.0 | = | Design-comparison bonus for doctrine-class weapons. |
| Fleet / HullSizeBias | 2 | = | Build-mix shift per size step (Swarm small; Capital/Motherships large). |
| Fleet / MothershipFitBonus | 0.5 | = | Motherships doctrine: huge-hull designs compare as 1.5× SCV. |
| Fleet / EngagementRoleWeight | 3 | = | Artillery: long-range roles ×3; Brawler: short-range roles ×3. |
| Fleet / AssaultCaptureSlots | 1.5 | `+` | Assault engagement style: more open marine captures. |
| Economy / MineWhatYouLack | on | = | Site scoring by need (competence, not difficulty). |
| Economy / NeedWeightMax | 4 | = | Clamp on need weights. |
| Economy / ResourceBaseNeedThreshold | 2 | = | The planner's "insecure upkeep" base rule targets resources at or above this need. |
| Economy / ExoticsUpkeep | on | = | Bases and capital hulls cost exotics; replaces the fixed base cap. |
| Economy / BaseExoticsUpkeep | 3 / month | `−` | Lower means more bases. **Replaces vanilla `maxAlienBaseGoals_*` as the expansion limit.** The drip already scales with difficulty (`AI_AlienExoticMultiplier_*` 1/2/3/4). |
| Economy / CapitalShipExoticsUpkeep | 0.5 / month | `−` | Large hulls. |
| Economy / HugeShipExoticsUpkeep | 2 / month | `−` | Motherships. |
| Economy / ExoticsStockHorizonMonths | 60 | `−` | The cap counts stock/this as monthly budget. Shorter means the hoard turns into bases faster. |
| Economy / MaxAlienBasesCeiling | 40 | `+` | Hard ceiling. |

## Vanilla difficulty knobs we expect to retune

These are JSON-only (`TIGlobalConfig`), with C/N/V/B values. The full table is in FINDINGS.md §3.

| Knob | C / N / V / B | Notes |
|---|---|---|
| `maxAlienBaseGoals_*` | 10/12/18/25 | To be replaced by exotics upkeep and population (P2). |
| `initialMaxAlienAttackFleetStrengthRatio_*` | 1.05/1.25/2/2.5 | Interacts with ForcePooling and AttackMaxRatioFloor. |
| `AI_AlienExoticMultiplier_*` | 1/2/3/4 | The wormhole drip; becomes the widening rate (P4). |
| `yearsBeforeAlienTotalWarAllowed_*` | 25/20/12/0 | |
| `steadyAlienHateGainModifier_*` | 0/1/1.9/3.3 | |
| `alienQuietDuration_years` (start template) | unset (0) | Dead in vanilla; a pacing lever. |

## How the developers pace scenarios (user suggestion, 2026-10-12)

All alien pacing levers live in `TIStartTimeTemplate`. C# defaults: progression modifier 1.0, everything else 0/off. The base scenarios use none of them; the Dark Skies scenarios use several.

| Lever | 2022 / 2026 / 2030 | 2003 (Dark Skies) | Broken Earth (Dark Skies) |
|---|---|---|---|
| `alienQuietDuration_years`: quiet phase (no spontaneous attacks, ×0.6 hate) | 0 (loud from day 1) | **22.5** | 0 |
| `alienSetupDuration_years`: length of the wormhole income ramp | 0 (full income at once) | **20** | **10** |
| `alienSetupStartIncome` → `alienSetupEndIncome` (× wormhole drip) | — | **0.15 → 1.5** | 0.2 → 1.0 |
| `alienProgressionModifier`: speed of all year-based alien timers | 1.0 | **0.75** | **0.75** |
| `alienStartingProgression_years`: offset on those timers | 0 | **−5** | 0 |
| `alienSurveillanceDelay_years` | 0 | 0 | **5** |
| `invasionFocusedAliens`: 2 concurrent InvadeEarth goals | no | no | **yes** |
| `TIGlobalConfig` overrides | — | — | `AI_WormholeSetupSpeed` C/N/V/B 1.5/1/0.5/**0.01**; `extraYearsToDelayAlienInvasion` 16/9/4/0; `AI_invaderArmiesLostBeforeBuildup` 0 |

What this tells us:

- **Base scenarios get all their alien pacing from the difficulty timers** (`years before total war`, `invasion delay`, …). There is no quiet phase and no ramp. The smoother levers exist but are unused there.
- **2003's income ramp is already a "widening wormhole".** It runs 0.15 → 1.5× the drip over 20 years (scaled by difficulty via `AI_WormholeSetupSpeed`). P4's soft clock should be built on this ramp, extended past its end and made attackable, not invented from scratch.
- **The developers' own difficulty levers are pacing levers** (delay, ramp, timer speed) plus focus (invasion-focused). That matches the DESIGN P2 principle.
- **Candidate per-difficulty use for the base scenarios:** give them a ramp (e.g. 0.3 → 1.0 over 8–15 years) and a short quiet phase on lower difficulties, so early pressure eases in smoothly.

## Measurements to collect per difficulty

To be filled in by soaks:
- alien attack launches per year, battles won/lost, ships lost on each side, human habs destroyed;
- alien SCV and base count curves;
- time until the first alien strike on the player's assets.
