# Findings: how the vanilla alien AI works, and why it loses

Reverse-engineered from the game build of 2026-09-12 (decompiled into `_ref/decompiled/`, gitignored) and from the user's own campaign saves. Paths are relative to `_ref/decompiled/`:
- `T/` = `PavonisInteractive.TerraInvicta.Tasks/`
- `P/` = `PavonisInteractive.TerraInvicta/`

Line numbers are for that build. Treat them as pointers, not contracts.

## 0. Evidence from a real campaign

Source: `tools/saves/timeline.py` over 19 saves of one accelerated Resistance campaign on Normal difficulty, 2026 to 2039. The player took Jupiter in 2038-39.

| Date | Alien ships | Alien mass | Player ships | Player mass | Largest alien fleets |
|---|---|---|---|---|---|
| 2031-09 | 141 | 1.2 | 23 | 0.3 | 21, 10, 9, 7 … |
| 2033-05 | 212 | 1.8 | 50 | 0.6 | 12, 12, 11, 10 … |
| 2035-02 | 283 | 2.5 | 45 | 0.6 | 15, 15, 13, 12 … |
| 2037-07 | 297 | 2.7 | 101 | 1.8 | 23, 23, 22, 22, 21 … |
| 2038-02 | 314 | 2.8 | 105 | 1.9 | **64**, 25, 23, 20 … |
| 2039-03 | 262 | 2.4 | 111 | 1.9 | 29, 29, 24, 23 … |

Mass is in Mt.

- **Forces are spread out.** The aliens had 3–6× the player's hulls and 1.5–4× the mass for the whole mid game. That force was spread over 10+ fleets of 10–25 ships. They concentrated it (the 64-ship fleet) only after the Jupiter assault had started.
- **Bases are hard-capped.** From 2030 to 2035 the aliens had exactly 12 bases plus 12 matching stations (24 habs) in every save. That is the Normal cap (`maxAlienBaseGoals_N` = 12). The bases were at Triton ×2, Titania, Titan, Callisto, Europa, 2 KBOs, a Centaur, Vesta, and 2 belt asteroids.
- **Resources are stranded.** Metals kept dropping to around 100–300, while volatiles and water grew without limit (330k+ each by 2038), fissiles reached 19k, and exotics climbed steadily (1.7k → 2.6k, then 7.8k in 2039). The aliens are short of one input and sitting on large unspent stockpiles of the others.

## 0b. Evidence from harness probes (2026-10-12)

Unattended runs of the user's saves; see HARNESS.md. These are the vanilla AI unless marked otherwise.

- **Undefended mines (E5).** 15 mining outposts were spawned across the main belt in mid-2034, during the hot war. After 180 days: **0 lost, and 0 alien attack goals targeted any of them.** Alien attack goals went to Earth (33), Mercury (25) and Mars (17).
- **Jupiter plateau (E9).** Year-long run from the `clean-jupiter` save (Mar 2039). This run had threat memory and decisive force on, which don't affect escalation.
  - **Total strength.** Alien SCV grew from 39.6k to 65.0k. The Resistance grew from 11.3k to 13.1k, of which 3.0–3.4k is at Jupiter.
  - **Planned but never launched.** The aliens created six attack goals on Jupiter targets and **launched none**. Each goal wanted 5k–18.5k SCV and was handed a single fleet of 1.5k–3.8k. It never reached its target, was dropped (importance → 0) and was re-created, over and over.
  - **Force is parked, not missing.** Their 65k is spread over 10+ systems: 14k in Sol transit, 9.9k at Luison (KBO home), 7.4k Neptune, 6.4k Uranus, 6.3k Saturn, 5.4k Hippasus, 5.2k Thetis, … Nothing pools ships from the 24 DefendWithFleet postings into one strike force.
  - **Stockpiles unspent again:** metals 1.1k against water 458k, volatiles 411k and exotics 7.7k.
  - **Conclusion.** The late-game problem is *allocation*: no force pooling, and attack goals that starve and reset. Thresholds are not the problem, and raising them (DecisiveForce) without pooling just means waiting longer.
- **Attack sizing (E3).** The vanilla launch ratio *to the goal's desired SCV* is 1.05–1.21. That understates the margin, because desired already includes ~1.4× superiority plus a small-target bonus.
  - With DecisiveForce on, desired became exactly 2 × (remembered defenders + half the system). For the 2031 targets this was close to what vanilla already wanted (435 vs 422, 485 vs 481).
  - The real weaknesses are what counts as a defender (1000 km, current SCV), one-at-a-time commitment, and pooling.
  - Outcome measurement (CombatJournal) was added to settle it.
- **The stall** in a 2039 run was intermittent: a blocking prompt the Autopilot couldn't clear. It did not recur in a later 140-day run. The harness now logs the player's pending prompts and aborts after 4 stalls in a row.

## 1. Architecture

- **No separate alien brain.** The aliens use the same goal planner as the human AI factions, with alien branches added.
  - Goals are `FactionGoal_*` objects with importance 0–20 (20 = emergency), serialized in the save. Every save is a snapshot of what the AI intends to do.
- **Daily loop.** `FactionPeriodicUpdate` drives `AIDailyFactionPlanner.FactionOperations0000` (`T/AIDailyFactionPlanner.cs:269`) and `ReviewAndSetGoals` (:2284):
  - `ManageWarsWithFaction`
  - `AliensCheckGoals`, monthly (:3158)
  - `ManageFleetGoals` (:1397)
  - `ManageFleets` (:1531; mostly every 4 days)
- **Slower cadences.**
  - Ship and hab building: weekly (`BuildSpaceAssetsCo`, :3541).
  - New alien targets: monthly, at most one fleet, one base and one station target per month outside emergencies.
- **Reactions.** `AIReaction` (:6379) handles hostile arrivals, new habs, and transfers toward Jupiter.
- **AI logging is stubbed out.** `TIFactionState.LogAI` is empty (`P/TIFactionState.cs:14552`) but has 38 call sites, including `DumpGoals`, `DumpShipyards` and `DumpMissions`. A Harmony prefix brings back the developers' full AI trace.

## 2. Why it loses

Each item below names the mechanism and what the player sees.

1. **It perceives strength from the fitted design, with no memory.**
   - Strength is read via `TISpaceShipState.SpaceCombatValue` → `template.TemplateSpaceCombatValue()` (`P/TISpaceShipState.cs:1289`). Fleet strength is the sum (`P/TISpaceFleetState.cs:810`).
   - The only learning is a per-faction factor that moves ±15% per battle, clamped to 0.67–1.5 (`P/TISpaceCombatState.cs:1340-1402`).
   - Its intel on human fleets is perfect: there is no intel gating in AI code, and its detection range is 500 AU.
   - **Exploit:** refit to weak missiles to look weak, then refit back. A weapons-only refit costs about 0.05 × hull build time per weapon (`TISpaceShipTemplate.GetRefitBuildTimeDays`, :2554).
2. **It sends just enough force, on purpose.**
   - Attack size = defenders × 1.395 superiority, capped by `initialMaxAlienAttackFleetStrengthRatio_*` (1.05/1.25/2/2.5) and growing slowly over years (`P/FactionGoal_AttackWithFleet.cs:367-451`). Excess ships are split off (`T/AIDailyFactionPlanner.cs:5446`).
   - Defenders only count fleets within 1000 km of the target (`P/TIFactionState.cs:8215`). Reinforcements in transit, STO fighters and the ability to refit back are ignored.
   - **Result:** fleets arrive in bite-sized chunks.
3. **It doesn't re-check once committed.** `LeaveMyFleetAlone` keeps a transferring attack going (`P/FactionGoal_AttackWithFleet.cs:233-258`).
4. **It almost never retreats.** Aliens have zero evade weight until the enemy has the `AccessAlienShip` milestone (`T/StratCombatInitStrategy.cs:143-146`), and they always fight in their primary system.
5. **It avoids the strongest enemy fleet.** `SelectFleetToAttack` removes it from consideration (`P/AIEvaluators.cs:4849`). Targets are then picked weighted-random by SCV², where SCV is space combat value (the game's single strength number).
6. **It is slow to react to undefended outposts.**
   - Outposts count as "impudent" only inside alien turf: Jupiter and beyond, or systems with alien bases (`MyTurf`, `P/AIEvaluators.cs:4182`). Antimatter producers count anywhere.
   - At most one base or station target is added per month.
   - Emergency (importance-20) attacks need narrow conditions (`ShouldLaunchEmergencyAttackAgainstAsset`, :5037).
   - **Result:** spamming undefended asteroid mines is safe.
7. **Its expansion is capped and scripted.**
   - The base cap is `maxAlienBaseGoals_C/N/V/B` = 10/12/18/25 (+0/3/7/10 in total war).
   - The order is fixed: Neptune → Uranus → Saturn every 6 months, then Jupiter (importance 19) and 3 belt bases after the middle-colonization delay (`T/AlienHabPlanner.cs`, `T/AIDailyFactionPlanner.cs:3178-3205`).
   - Inner-system offensives target belt → Mars → Luna, top 3 only (:3411).
   - The aliens never plan hab captures: `SelectHabToCapture` returns null for them (`P/AIEvaluators.cs:4786`).
8. **Its quiet and setup phases are effectively disabled.**
   - `alienQuietDuration_years` and `alienSetupDuration_years` are read from `TIStartTimeTemplate` but missing from the shipped JSON, so both are 0 (verified).
   - The phase machinery exists but is dead, which makes it a cheap lever for reshaping alien pacing.
9. **It builds by a per-goal greedy rule.** `GetDesiredShipToBuild` (`P/TIFactionState.cs:15439`):
   - takes a random role from the goal;
   - picks the max-SCV design if importance ≥ 10, otherwise the lightest;
   - drops designs it can't afford in exotics or antimatter.
   - There is no fleet-level doctrine and no counter-design.
   - **Confirmed vanilla bug (2026-10-12):** `DesignShipsCoroutine` (~5996) asks for hull `"AlienBattleCruiser"`; the data name is `AlienBattlecruiser` and the template lookup is case-sensitive, so that slot falls back to the lightest valid hull. Not fixed yet (low impact; candidate for `Fixes/`).
   - **The role locks the weapon class** (`GetCuratedDesignScore`: own classes ×1e7, others ÷1e7). Short-range roles carry lasers, medium-range magnetic or particle, long-range missile or plasma. Detail in M2-PLAN step 4.
10. **Vanilla bug: alien ships can never assault a base.**
    - `TIUtilityModuleTemplate.marineOpsValue` counts a module's marine value only if its *first* special rule is Assault or MarineOpsDefenseOnly.
    - The aliens' only assault module, `SalamanderTerrorUnitPod`, lists `["Crashdown","Assault",…]`, so every alien ship has assault value 0.
    - Bases can only die to marines (`DestroyHabOperation` / `AssaultHabOperation` need assault > 0) or sustained bombardment. Undefended human bases, including mines, are therefore effectively immune to alien fleets.
    - Every human marine module lists Assault first.
    - Related: when the aliens "capture" a hab, `TIHabState.CaptureHab` *destroys* it (alien branch, `:1987`). They never own human habs.
    - Vanilla also never lets the aliens plan captures (`SelectHabToCapture` returns null for them).
11. **Station defenses are weak by construction.**
    - Station batteries reach 600/700/850 km (T1/T2/T3), against 800–1000 km for nose weapons.
    - HP is tiny, the hit sphere is huge (`tier*480*scale`), and station ECM is hard-coded to 0 (`TIHabModuleTemplate.ECMValue`).
    - Surface bases never enter tactical combat. They resolve in the bombardment layer at 200–600 km altitude against 2000 km lasers, with near-certain hits and 60× point-defense multipliers. That is why they feel strong.
12. **Weapon and armor tech ends early.** Weapons, armor and radiators stop at about 0.5M cumulative RP. Only drives and power plants go on, to about 3.4M (the Protium Converter Torch). The repeatable FutureTech projects have no military effects.

## 3. Difficulty

Difficulty mostly changes timers, caps and multipliers, not intelligence. Full table: `TIGlobalConfig.cs:600-900, 2491-3056`. Examples (C/N/V/B):

| Setting | C / N / V / B |
|---|---|
| Exotics multiplier | 1 / 2 / 3 / 4 |
| Max attack ratio | 1.05 / 1.25 / 2 / 2.5 |
| Base cap | 10 / 12 / 18 / 25 |
| Total war after (years) | 25 / 20 / 12 / 0 |
| Alien ship cost | 1.25 / 1 / 1 / 0.75 |

Every public `TIGlobalConfig` field can be overridden from a JSON mod.

## 4. Levers

**JSON only:**
- all `TIGlobalConfig` fields (caps, ratios, timers, hate, exotics multipliers, refit cap)
- `TIStartTimeTemplate` phase fields
- hab-module incomes (wormhole exotics)
- alien `AIValues`
- weapons, modules, techs and projects (new tiers, station batteries with longer range)
- formation patterns

**Harmony (ranked by leverage):**
1. Threat perception: `GetPerceivedEnemyFleetStrength`/`SpaceCombatValue` called from AI code. Add memory and intel gating.
2. Attack sizing and timing: `ComputeDesiredFleetCombatValueForAttack`, `GetMaximumFleetCombatValueRatio`, `ReadyForTransferToTarget`, `LeaveMyFleetAlone`.
3. Target selection: `SelectFleetToAttack`/`SelectStationToAttack`/`SelectBaseToAttack`, `MyTurf`, `ShouldLaunchEmergencyAttackAgainstAsset`.
4. Strategy layer: `AliensCheckGoals`, `CheckTriggerInnerSystemOffensives`, `AlienHabPlanner.ManageHabGoals`, `ManageFleets`.
5. Ship building and design: `GetDesiredShipToBuild`, `DesignShipsCoroutine`.
6. Retreat and pursuit: `StratCombatInitStrategy.SelectStance`.
7. Logging: `LogAI`.

## 5. Tactical controls (for the separate controls workstream)

- **Match velocity already exists.** `MatchVelocity` and `FleetMatchVelocity` commands exist (`WaypointNavigationController.MatchVelocity`, :593). They match velocity only, not position, so formations drift.
  - No follow, escort or station-keeping command exists.
- **Formation spacing is a hard-coded table.** `TIFormationTemplate.GetSpacingOffset_km` (:78), Tight = 60/60/100 km.
- **The pre-battle setup screen is limited.** It has four dropdowns, a z-axis-only velocity slider, and pairwise swaps. You can't drag ships, set per-ship heading, or see the enemy.
