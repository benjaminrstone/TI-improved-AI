# Human AI factions: how vanilla works, and what's wrong

This is the second track (user, 2026-10-18: "pivot to human faction improvements"). It covers the AI-controlled human factions (Resistance, Humanity First, Academy, Initiative, Exodus, Servants, Protectorate). The aim is the one already set for the aliens: **competence over handouts**. Strengthen the AI factions by making them play well, not by giving them bonus resources (user, 2026-10-17). Same rules: start from symptoms, probe first, use soaks rather than single runs, put every change behind a switch.

Code references are relative to `_ref/decompiled/`. Abbreviations:
- ADFP = `PavonisInteractive.TerraInvicta.Tasks/AIDailyFactionPlanner.cs`
- ACMP = `…Tasks/AICouncilorMissionPlanner.cs`
- AIE = `PavonisInteractive.TerraInvicta/AIEvaluators.cs`
- TFS = `…/TIFactionState.cs`
- HHP = `…Tasks/HumanHabPlanner.cs`

## 0. Decisions (user, 2026-10-18)
- **Role: both, alien-first.** Credible rivals on Earth, who also take the alien war seriously: they defend their space assets, fight alien forces and contest the gas giants. The target is to fix "you fight the aliens alone" (S1).
- **First step: the four verified vanilla bugs (H1–H4)**, each starting from a probe that shows it on vanilla.
- **Gang-up-on-the-leader: keep it, but smarter.** The first suggestion ("only when the leader actually threatens them, never at the cost of ignoring the aliens") was judged too lenient or too restrictive. The design is still open; propose alternatives when we get there.

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

**Step 1 results** (2031 save, 90–120 days, one run each against vanilla; `Human/VanillaFixes.cs`):

| Fix | Result | Default |
|---|---|---|
| H1 | Mod: 1 direct investment (Exodus, Mission Control). Vanilla: 0. Real but **low impact**: even the intended budget (10–20% of influence) rarely covers a priority. | on |
| H2 | Flips Losing → LosingBig when strength is below half the rival's (the Protectorate, Dec 2031). Vanilla's other LosingBig route (every Earth interface orbit full) also fires in 2031. | on |
| H3 | Re-run passes bought 64 extra orgs in 120 days, but AI org totals at the end **matched vanilla** (355 vs 353): the extra orgs are shed again, so the fix churns money. **Investigate before enabling** (org capacity, the pool overage → DropOrgs/SellOrgs path). | **off** |
| H4 | Verified by reading the code only; no behavioural probe yet (the effect on mission choice is subtle). | on |

**Reported by the survey, not yet verified by me:**
- the focus goal is never passed to normal missions (AIMissionEntry.cs:96, ACMP:158-161);
- the favourites boost is `.Take(0)` (ACMP:3495);
- focus-fire removes the wrong councilor's entries (ACMP:3359);
- `SellOrgs` condition looks inverted (ADFP:2771);
- `DetainCouncilorPayoff` has a dead AccessLiveHydra branch (ACMP:718-728);
- a discarded LINQ `Append` (ADFP:965/982);
- integer division in `RecruitCouncilors` (ADFP:2395).

## 6b. Vanilla evidence for S1 (3 × 2-year vanilla soaks from 2034, 2026-10-18)
- **Human AI fleets are tiny.** Each AI faction fields 0.3–2.4k SCV (all six together ~5–8k), against the aliens' ~35k.
- **They almost never attack the aliens.** Battles involving the aliens: 33–62 per run, all alien-initiated except **one** (Humanity First).
- **Their habs bleed.** AI factions lose 60–80 habs per run, to the aliens and to each other.
- **Fleets sit in the inner system** (Venus, Earth, Mercury) while bases and stations (14–36 per faction) spread across the belt.
- **So the root of S1 looks like human AIs building almost no warships,** more than how they use them. The next step after the vanilla bugs is to trace why (fleet goals, saving, budgets, gates).

## 7. Likely weaknesses (from code; symptoms to confirm by probe and soak)
1. **Expansion into minor nations stops at one CP over the free cap.** `MinorCPTrouble` is maintenance cost > 0 (TFS:15412); ControlNation then returns −1 for non-major powers (ACMP:497-501).
2. **Small nations (≤ 4 CP) are safe from Crackdown and Purge** unless the AI already targets them (ACMP:615-623, 1210-1218).
3. **Mission choice is weighted-random** among the top N, not best-first.
4. **Shallow space defence:** no fleet defence of bases, no peril recall, and every fleet sits at Earth's first interface orbit.
5. **Fragile offence:** hate-gated wars that vanish, an intel gate, and random goal discards.
6. **Passive space expansion:** a 5–9 base cap and the MC gate.

## 8. Player reports (community survey, 2026-10-18)
Sources: Steam reviews and discussions, the Something Awful thread, and Steam patch notes. Reddit wasn't reachable; the Pavonis forum and wiki returned 403. "Common" means several independent sources.

**1.0+ (Nov 2025 onward):**

| # | Symptom | How often |
|---|---|---|
| S1 | **Helpless against the aliens.** Few stations, mining neglected, they lose to small alien forces; "you fight the aliens and Servants alone". | Common |
| S2 | **They gang up on the leading player** (coups, stolen CPs). Designed in 0.3.21: extra hate toward the leader beyond a minimum ideological distance (`AI_GangUpOnLeader…`, 2.5/1.8/1.2/0). | Common |
| S3 | **They pursue the player instead of the aliens:** public campaigns in your nations, CPs taken then abandoned, excessive peace demands. | Several |
| S4 | **Factions can't be killed:** a full council again in ~3 turns, habs rebuilt without a launch site. 1.0.36 added an opt-in faction-disabling option. | Very common |
| S5 | **Perceived cheating:** unequal mission odds (the hidden attacker/defender bonus vs the player, §4), CPs taken with no councilor present, stronger ships. | Moderate |
| S6 | **Every faction grabs a whole country in its first turns; Europe early.** | Several |
| S7 | **Tiny-fleet spam.** Many single-ship fleets (UI clutter, performance), raids timed on your transfers or repairs; odd designs. | Several |
| S8 | **Mixed space presence:** some active orbit play, but also players with 1 base vs AI 20, the Initiative with "practically no defense" on Mars, and human-vs-human space war rare. | Mixed |
| S9 | **Never a satisfying peer opponent.** No threat early, irrelevant late; late-game AI turns slow for factions that no longer matter. | Several |
| S10 | **Research waste:** skips the global techs that lead to fusion; irrelevant projects; can't afford faction projects later. | Several |
| S11 | **The Protectorate rarely builds or keeps Sentinels.** Servants strong, Protectorate weak; HF and Servants forever assassinating each other. | Several |
| S12 | **Exploitable diplomacy:** bribes; intel sharing used to shift hate; war re-declared a day after a truce. | Several |
| S13 | **Councilors go to ground after an assassination**, so a crackdown can be timed against them. | One |

**Early Access, mostly addressed by later patches:** no space presence; stalled economies; anti-alien factions ignoring invasions; poor nation management (losing the USA in five months; unstable nations sliding into civil war); factions that feel identical.

**Developer direction (patch notes):**
- Jan 2025: "more ships and space combat".
- 0.4.78: more aggressive fleets.
- RC1: "smarter AI on Earth and in space".
- 1.0.38: hab capture and defence; mining-org pursuit.
- 1.0.40: alien tracking, cheaper tech choices, influence saved for councilors.
- 1.0.53: treachery checks, repeatable projects.
- No published roadmap for the human AI.

**Links to code (§6–§7):**
- S1 and S8: shallow space defence, the base cap, the MC gate.
- S9 and S10: research churn, direct investment never firing (H1), org buying stopping early (H3).
- S5: the hidden mission bonus (difficulty).
- S2 and S3: the designed gang-up rule.
