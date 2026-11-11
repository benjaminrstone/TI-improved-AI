# Findings: how the vanilla alien AI works, and why it loses

Reverse-engineered from game build 1.0.53b (decompiled into `_ref/decompiled/`, gitignored), from the user's own campaign saves (one accelerated Resistance campaign on Normal, 2026 to 2039) and from harness runs of those saves. Paths are relative to `_ref/decompiled/`:
- `T/` = `PavonisInteractive.TerraInvicta.Tasks/`
- `P/` = `PavonisInteractive.TerraInvicta/`

Line numbers are for that build. Treat them as pointers, not contracts. Where the mod changes a behaviour, the item says so and names the switch (`src/ImprovedAI/AIConfig.cs`).

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
- **AI logging is stubbed out.** `TIFactionState.LogAI` is empty (`P/TIFactionState.cs:14552`) but has 38 call sites, including `DumpGoals`, `DumpShipyards` and `DumpMissions`. The mod's Harmony prefix brings back the developers' full AI trace (`Diagnostics / AILog`).

## 2. Why it loses

Each item below names the mechanism and what the player sees.

1. **It perceives strength from the fitted design, with no memory.**
   - Strength is read via `TISpaceShipState.SpaceCombatValue` → `template.TemplateSpaceCombatValue()` (`P/TISpaceShipState.cs:1289`). Fleet strength is the sum (`P/TISpaceFleetState.cs:810`).
   - The only learning is a per-faction factor that moves ±15% per battle, clamped to 0.67–1.5 (`P/TISpaceCombatState.cs:1340-1402`).
   - Its intel on human fleets is perfect: there is no intel gating in AI code, and its detection range is 500 AU.
   - **Exploit:** refit to weak missiles to look weak, then refit back. A weapons-only refit costs about 0.05 × hull build time per weapon (`TISpaceShipTemplate.GetRefitBuildTimeDays`, :2554).
   - Mod: `Perception / ThreatMemory` judges a fleet by its best recent per-ship SCV.
2. **It sends just enough force, on purpose.**
   - Attack size = defenders × 1.395 superiority, capped by `initialMaxAlienAttackFleetStrengthRatio_*` (1.05/1.25/2/2.5) and growing slowly over years (`P/FactionGoal_AttackWithFleet.cs:367-451`). Excess ships are split off (`T/AIDailyFactionPlanner.cs:5446`).
   - Defenders only count fleets within 1000 km of the target (`P/TIFactionState.cs:8215`). Reinforcements in transit, STO fighters and the ability to refit back are ignored.
   - It launches the moment a fleet reaches the goal's desired SCV. The measured launch ratio is 1.05–1.21 × desired, where desired already includes the superiority and a small-target bonus.
   - **Result:** fleets arrive in bite-sized chunks. The weakness is less the superiority number than what counts as a defender, one-at-a-time commitment, and the lack of pooling (item 2b).
   - Mod: `Commitment / DecisiveForce` sizes attacks against remembered defenders plus half of that faction's other fleets in the system. For the 2031 targets measured, that came out close to what vanilla already wanted (435 vs 422, 485 vs 481).
2b. **It never pools force.**
   - Each attack goal is handed one fleet. Nothing gathers ships from the many `DefendWithFleet` postings into one strike force.
   - In the campaign saves the aliens held 3–6× the player's hulls and 1.5–4× the mass through the whole mid game, spread over 10+ fleets of 10–25 ships. They concentrated (one 64-ship fleet) only after the player's Jupiter assault had started.
   - In a one-year run from 2039 (alien SCV 40k → 65k across 10+ systems, the Resistance 11k → 13k), the aliens created six attack goals on Jupiter targets and launched none. Each goal wanted 5k–18.5k SCV, got a single fleet of 1.5k–3.8k, was dropped (importance → 0) and re-created, over and over. That run had threat memory and decisive force on, neither of which pools.
   - **Result:** the late-game problem is allocation, not thresholds. Raising the required superiority without pooling only means waiting longer.
   - Mod: `Commitment / ForcePooling` pools idle fleets and surplus defenders into one focus attack at a time.
3. **It doesn't re-check once committed.** `LeaveMyFleetAlone` keeps a transferring attack going (`P/FactionGoal_AttackWithFleet.cs:233-258`).
4. **It almost never retreats.** Aliens have zero evade weight until the enemy has the `AccessAlienShip` milestone (`T/StratCombatInitStrategy.cs:143-146`), and they always fight in their primary system. Mod: `Combat / StanceCourage` only replaces an Evade (defending their own habs, or arriving at about even odds).
5. **It avoids the strongest enemy fleet.** `SelectFleetToAttack` removes it from consideration (`P/AIEvaluators.cs:4849`). Targets are then picked weighted-random by SCV², where SCV is space combat value (the game's single strength number).
6. **It is slow to react to undefended outposts.**
   - Outposts count as "impudent" only inside alien turf: Jupiter and beyond, or systems with alien bases (`MyTurf`, `P/AIEvaluators.cs:4182`). Antimatter producers count anywhere.
   - At most one base or station target is added per month.
   - Emergency (importance-20) attacks need narrow conditions (`ShouldLaunchEmergencyAttackAgainstAsset`, :5037).
   - **Result:** spamming undefended asteroid mines is safe. Of 15 mining outposts spawned across the main belt during the hot war of mid-2034, none was lost in 180 days and no alien attack goal targeted any of them. The attack goals went to Earth (33), Mercury (25) and Mars (17).
   - Mod: `Targeting / RaidPlanner` adds attack goals on valuable, weakly defended human bases and stations anywhere.
7. **Its expansion is capped and scripted.**
   - The base cap is `maxAlienBaseGoals_C/N/V/B` = 10/12/18/25 (+0/3/7/10 in total war). In the campaign saves the aliens sat at exactly 12 bases plus 12 matching stations from 2030 to 2035.
   - The order is fixed: Neptune → Uranus → Saturn every 6 months, then Jupiter (importance 19) and 3 belt bases after the middle-colonization delay (`T/AlienHabPlanner.cs`, `T/AIDailyFactionPlanner.cs:3178-3205`).
   - Inner-system offensives target belt → Mars → Luna, top 3 only (:3411).
   - The aliens never plan hab captures: `SelectHabToCapture` returns null for them (`P/AIEvaluators.cs:4786`).
   - Mod: `Economy / ExoticsUpkeep` replaces the fixed cap with what the exotics drip and stockpile can carry. `Targeting / RaidCaptures` plans marine captures of undefended bases.
8. **Its quiet and setup phases are unused in the base scenarios.**
   - `alienQuietDuration_years` and `alienSetupDuration_years` are read from `TIStartTimeTemplate`. The base scenarios leave them out of the shipped JSON, so both are 0. Only the Dark Skies scenarios set them (table in DIALS.md).
   - The phase machinery exists, which makes it a cheap lever for reshaping alien pacing.
9. **It builds by a per-goal greedy rule.** `GetDesiredShipToBuild` (`P/TIFactionState.cs:15439`):
   - takes a random role from the goal's secondary-role weights and a size from `aiValues.fleetSmalls` / `fleetMediums`;
   - picks the max-SCV design if importance ≥ 10, otherwise the lightest;
   - drops designs it can't afford in exotics or antimatter.
   - There is no fleet-level doctrine and no counter-design. One design is kept per role × size × exotics category, the one with the best `TemplateSpaceCombatValue`.
   - **The role locks the weapon class** (`TIShipWeaponTemplate.GetCuratedDesignScore`: own classes ×1e7, others ÷1e7). Short-range roles carry lasers, medium-range magnetic or particle, long-range missile or plasma.
   - **Vanilla bug:** `DesignShipsCoroutine` (~5996) asks for hull `"AlienBattleCruiser"`. The data name is `AlienBattlecruiser` and the template lookup is case-sensitive, so that slot (`SS_Interceptor` k=3, the exotics medium interceptor) falls back to the lightest valid hull. Fixed by `Fixes / AlienBattlecruiserName` (`Fixes/AlienBattlecruiserName.cs`, a transpiler on the coroutine's string literal).
   - Mod: `Fleet / FleetDoctrine` biases weapon class, hull size and role mix by doctrine (`AI/FleetDoctrine.cs`).
10. **Vanilla bug: alien ships can never assault a base.**
    - `TIUtilityModuleTemplate.marineOpsValue` counts a module's marine value only if its *first* special rule is Assault or MarineOpsDefenseOnly.
    - The aliens' only assault module, `SalamanderTerrorUnitPod`, lists `["Crashdown","Assault",…]`, so every alien ship has assault value 0.
    - Bases can only die to marines (`DestroyHabOperation` / `AssaultHabOperation` need assault > 0) or sustained bombardment. Undefended human bases, including mines, are therefore effectively immune to alien fleets.
    - Every human marine module lists Assault first.
    - Related: when the aliens "capture" a hab, `TIHabState.CaptureHab` *destroys* it (alien branch, `:1987`). They never own human habs.
    - Vanilla also never lets the aliens plan captures (`SelectHabToCapture` returns null for them).
    - Fixed by `Fixes / AlienMarineValue` (`Fixes/AlienMarineValue.cs`): the value counts whenever the rules include Assault. Only that alien pod changes.
10b. **Aliens can't buy from Earth in practice.** Vanilla prices a missing space resource as boost plus money for every faction (`TIResourcesCost.GetBoostSubstitutedCost`), but alien boost stock and income are 0, and `CanAfford` needs it in stock. Alien builds simply wait for metals and nobles.
    - **Result:** alien metals oscillate near zero while ships keep coming slowly, and the other inputs are stranded. In the campaign saves metals kept dropping to 100–300 while water and volatiles passed 330k each by 2038, fissiles reached 19k and exotics climbed from 1.7k to 7.8k.
    - **Trade is open to them.** Once a pro-alien faction (usually the Servants or Protectorate, perhaps the Academy later) completes `AlienDiplomacy` with a Contact mission on an alien councilor, AI-to-AI trades run through `TIPromptQueueState.HandleFactionContactMakeOffer` → `DiplomacyTradeAction` → `TIFactionState.ProcessTrade`.
    - The aliens can hold and receive every resource, boost included (`UnlockedResource`). Control points, habs, orgs and projects can change hands too.
    - Boost received by trade would make the Earth substitution affordable.
    - Mod: `Economy / NoEarthPurchases` blocks the substitution (it bites only once the aliens hold boost). `Economy / MineWhatYouLack` scores base sites by need. `Economy / ProxyTrade` has the aliens offer trades to pro-alien factions. Trades are journaled (`kind: "trade"`).
11. **Station defenses are weak by construction.**
    - Station batteries reach 600/700/850 km (T1/T2/T3), against 800–1000 km for nose weapons.
    - HP is tiny, the hit sphere is huge (`tier*480*scale`), and station ECM is hard-coded to 0 (`TIHabModuleTemplate.ECMValue`).
    - Surface bases never enter tactical combat. They resolve in the bombardment layer at 200–600 km altitude against 2000 km lasers, with near-certain hits and 60× point-defense multipliers. That is why they feel strong.
11b. **War attack goals are randomly discarded.** `FactionGoal_WarOnFaction.DailyGoalMaintenance` runs weekly and zeroes any of the war's attack goals with a 2.3% chance (`RandomFloatValue() < 0.0233`), unless `LeaveMyFleetAlone()`. A goal that takes months to assemble a strike will usually be dropped before launch. This is a big part of why alien offensives never mature. Mod: `ForcePooling` keeps its focus goal protected until the attack is under way.
12. **Weapon and armor tech ends early.** Weapons, armor and radiators stop at about 0.5M cumulative RP. Only drives and power plants go on, to about 3.4M (the Protium Converter Torch). The repeatable FutureTech projects have no military effects.

## 3. Difficulty

**Human AI factions' difficulty bonuses** (C# defaults in `TIGlobalConfig`, not overridden in the JSON). None of them is a direct space-resource bonus:

| Knob | C / N / V / B |
|---|---|
| `AI_BonusCPCap` | 0 / 0 / 50 / 100 |
| `AI_BonusMissionControl` | 0 / 0 / 20 / 50 |
| `TIModifier_HumanAIShipBuildingScaling` (ship cost) | 1 / 1 / 0.75 / 0.5 |
| `AI_MissionAttackerBonus` / `AI_MissionDefenderBonus` | −3 / 0 / 1 / 2 |
| `passiveTechInvestment` / `activeTechInvestment` | 0.5/0 · 0.25/0.15 · 0.15/0.25 · 0.05/0.25 |


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
