# Difficulty dials

Every knob the mod adds or turns, with its current value and its intended direction across difficulty levels. The user's goal (2026-10-12): it is fine for the mod to punish a passive player, but these dials must stay visible so we can build a smooth difficulty curve.

**Principle (DESIGN P2).** Difficulty sets the aliens' *constraints* (supply, reach, how far they may concentrate, which doctrines they may use), not how smart they are.

**Status:** every mod dial is one value today, shared by all difficulties (the plugin config in `BepInEx/config/com.improvedai.terrainvicta.cfg`). Per-difficulty tables come once soaks on several difficulties exist.

Direction key:
- `+` means this dial should rise with difficulty.
- `−` means it should fall.
- `=` means competence, kept the same at every difficulty.

## Mod dials

| Section / key | Now | Direction | What it changes |
|---|---|---|---|
| Perception / ThreatMemory | on | = | Remembering a fleet's best recent showing is competence, not difficulty. |
| Perception / ThreatMemoryDays | 180 | `+` (maybe) | A shorter memory leaves more room for deception at low difficulty. |
| Commitment / DecisiveForce | on | = | |
| Commitment / AlienAttackSuperiority | 2.0 | `−` | Higher means more cautious attacks: fewer, safer fights. Lower means bolder attacks that accept losses. Candidate for the "never loses a ship" question. |
| Commitment / ForcePooling | on | = | |
| Commitment / PoolMargin | 1.1 | = | |
| Commitment / PoolReserveFraction | 0.25 | `−` | How much of each system's force stays home when pooling. Lower means harder concentration. |
| Commitment / AttackMaxRatioFloor | 1.5 | = | A bug fix in effect. |
| Targeting / RaidPlanner | on | `+` (Cinematic off?) | |
| Targeting / MaxRaidGoals | 4 | `+` | Raid tempo. |
| Targeting / RaidImportance | 16 | `+` | How raids compete with other goals for fleets. |
| Targeting / RaidHateWeight | 1.0 | `+` | How strongly hate focuses raids on one faction. |
| Targeting / RaidBombardMultiplier | 2.0 | = | |

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

## Measurements to collect per difficulty

To be filled in by soaks:
- alien attack launches per year, battles won/lost, ships lost on each side, human habs destroyed;
- alien SCV and base count curves;
- time until the first alien strike on the player's assets.
