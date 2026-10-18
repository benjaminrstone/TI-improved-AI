# Human AI factions: how vanilla works, and what's wrong

This is the second track (user, 2026-10-18: "pivot to human faction improvements"). It covers the AI-controlled human factions (Resistance, Humanity First, Academy, Initiative, Exodus, Servants, Protectorate). The aim is the one already set for the aliens: **competence over handouts**. Strengthen the AI factions by making them play well, not by giving them bonus resources (user, 2026-10-17). Same rules: start from symptoms, probe first, use soaks rather than single runs, put every change behind a switch.

Code references are relative to `_ref/decompiled/`. Abbreviations:
- ADFP = `PavonisInteractive.TerraInvicta.Tasks/AIDailyFactionPlanner.cs`
- ACMP = `…Tasks/AICouncilorMissionPlanner.cs`
- AIE = `PavonisInteractive.TerraInvicta/AIEvaluators.cs`
- TFS = `…/TIFactionState.cs`
- HHP = `…Tasks/HumanHabPlanner.cs`

## 1. Architecture and timing (survey 2026-10-18)

- **Daily at midnight:** `FactionOperations0000` (ADFP:269) shuffles the AI factions and builds per-faction task lists:
  - early: ManageCouncilors, SetResearchPriorities, ManageNations;
  - late: BuildSpaceAssets, ArmyOperations, FleetOperations.
  It runs `AnalyzeDeficiencies_Human` (ADFP:345; self-assessment, resource deficiencies, CP trouble, risk aversion) and `ReviewAndSetGoals` (ADFP:2284).
- **During the day:** each faction pops one random task per tick (ADFP:230-239). The remainder is flushed at 23:00 (ADFP:333).
- **Internal cadences:**

  | Task | Cadence |
  |---|---|
  | Councilors | weekly, days ≤ 28 |
  | Research | daily |
  | Nation priorities and direct investment | 14-day |
  | Space assets | weekly (habs every run, modules on even days, ships on odd days) |
  | Armies | daily |
  | Fleet goals | 4-day |
  | Human hab goals | 14-day |
- **Mission phase:**
  1. `MissionPhasePrep` (ACMP:2012): CP self-disabling (ADFP:1133), alliances and rivalries (ADFP:4009);
  2. `ManagePriorityNationControlGoalsForFaction` (ADFP:890);
  3. `PlanMissionsTask` on a worker thread (ACMP:2069).
- **Habs:** only `HumanHabPlanner` is live. `LegacyHumanHabPlanner` is dead code apart from the static site selector.

## 2. Councilor missions
- **Expansion targets:**
  - At start, two random supra-regions: their biggest nation, its neighbours, and filler chosen by opinion × population (ADFP:799).
  - Each mission phase adds capture goals: nations where the faction already has CPs, allies and claims, neighbours of the alien nation, one major power once it has 4+ councilors (ADFP:1055), and extras by `EvaluateNation × opinion` with a 25% random pick (ADFP:1106).
- **Plan order:** victory missions, then go-to-ground, then loyalty checks, then objective hunts, then fleet councilors, then high-value policies, then inspire, then "focus fire" on a major power (only if none is controlled, ACMP:3200). Then, per councilor, every mission × target is scored (expected utility, AIMissionEntry.cs:104), the top N are kept, and the choice among them is **weighted-random** (ACMP:3518).
- **CP defence:** purely payoff-driven DefendInterests/Protect. There is no reaction to known enemy councilors in a nation.

## 3. Space
- **Stations:** always expand below 4 (HHP:117).
- **Bases:** capped at 5 + (years − 5)/5, at most 9, unless a resource income is ≤ 0 (HHP:304-310). Founding needs mission control available (HHP:514).
- **Ships:** each fleet goal saves for one design, and ships are queued only when the goal needs them (ADFP:3600-3846). All human fleets assemble at Earth's first interface orbit (ADFP:1682).
- **Defence:**
  - Human fleets defend only the primary hab, or the best station in a system (`IsDefenseFeasible`, AIE:4529; ADFP:1449). Never bases.
  - No primary-system peril recall for humans (AIE:4335), and no defence mode in the Earth system (AIE:4659).
- **Offence:**
  - Needs a `WarOnFaction` goal, which is discarded when hate drops below threshold (FactionGoal_WarOnFaction.cs:140). It also needs ≥ 0.25 intel on the target (AIE:6007).
  - War attack goals are randomly zeroed at 2.3% a week (FINDINGS 11b), and found-hab goals at 5% (HHP:192-199).

## 4. Research, economy, difficulty
- **Research:**
  - Global tech: highest tier only, then a weighted-random pick by `ScoreTech` (AIE:1159, 1729).
  - Projects: the same pattern, reviewing one slot per trigger.
  - Non-critical project weights are re-rolled daily (ADFP:3052).
  - The "don't race" check applies only to techs with zero prerequisites (ADFP:3133-3138).
- **Economy:**
  - Org buying: up to 85–95% of money, 25–75% of influence, 75% of ops.
  - Mission budget: 50% of money; influence minus savings.
  - CPs are self-disabled when over the free allowance (ADFP:1136).
  - Resources are sold only when money is negative.
- **Difficulty:** FINDINGS §3. The AI's mission attacker/defender bonus applies only against or by the *player*: AI vs AI gets nothing (TIGlobalConfig.cs:2819, 2847). The player's influence bonus is 0/30/45/60.

## 5. Ideology
- **Hate toward the aliens:** veryProAlien factions gain none monthly; anti-alien factions gain extra (ADFP:583-612).
- **The alien nation:** every non-veryProAlien faction gets a NeutralizeNation goal on it (ADFP:938). When it is founded, all non-veryProAlien humans reset hate between themselves (AIE:6177-6192).
- **Pro-alien only:** TransferControl and PassTechnology (ACMP:389, 1250).
- **`shouldNeverAttackAliens`** blocks assault and assassination of aliens (TFS:974; ACMP:326, 353).

## 6. Vanilla bugs (verified by reading the code, 2026-10-18)

| # | Bug | Where | Effect |
|---|---|---|---|
| H1 | **Direct investment never fires.** `(influence * councilors.Count == maxCouncilSize) ? 0.2f : 0.1f`: an operator-precedence slip makes the influence budget the literal 0.1 or 0.2, not a share of influence. | ADFP:4774 | AI factions never fund national priorities directly (all of them cost influence). |
| H2 | **"Losing big" is unreachable.** `Losing` (< 0.8) is tested before `LosingBig` (< 0.5). | ADFP:370-377 | A faction far behind its strongest human rival never reacts as if losing badly (war threshold, risk aversion). |
| H3 | **Org buying stops at the first conflict.** `PurchaseOrgs` uses `break` where `continue` was meant, as soon as a candidate org or councilor is already taken. | ADFP:2604-2607 | About one org bought per weekly pass; councilors stay under-equipped. |
| H4 | **The mission risk relaxation is dead.** `acceptableMinimumSuccess` uses `payoff` before `payoff` is assigned. | AIMissionEntry.cs:74 vs 91 | High-value missions never get their lower success threshold. |

**Reported by the survey, not yet verified by me:**
- the focus goal is never passed to normal missions (AIMissionEntry.cs:96, ACMP:158-161);
- the favourites boost is `.Take(0)` (ACMP:3495);
- focus-fire removes the wrong councilor's entries (ACMP:3359);
- `SellOrgs` condition looks inverted (ADFP:2771);
- `DetainCouncilorPayoff` has a dead AccessLiveHydra branch (ACMP:718-728);
- a discarded LINQ `Append` (ADFP:965/982);
- integer division in `RecruitCouncilors` (ADFP:2395).

## 7. Likely weaknesses (from code; symptoms to confirm by probe and soak)
1. **Expansion into minor nations stops at one CP over the free cap.** `MinorCPTrouble` is maintenance cost > 0 (TFS:15412); ControlNation then returns −1 for non-major powers (ACMP:497-501).
2. **Small nations (≤ 4 CP) are safe from Crackdown and Purge** unless the AI already targets them (ACMP:615-623, 1210-1218).
3. **Mission choice is weighted-random** among the top N, not best-first.
4. **Shallow space defence:** no fleet defence of bases, no peril recall, and every fleet sits at Earth's first interface orbit.
5. **Fragile offence:** hate-gated wars that vanish, an intel gate, and random goal discards.
6. **Passive space expansion:** a 5–9 base cap and the MC gate.

## 8. Player reports
Still being gathered. They will be added here from the community survey.
