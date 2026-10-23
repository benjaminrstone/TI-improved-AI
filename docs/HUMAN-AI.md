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
- **Mission control (2026-10-19).** Grow MC much harder **and** reserve MC for the fleet. Retiring weak habs is usually not optimal: develop a hab enough to have an MC building and positive resource flow, or don't found it if it's not worth holding (it doesn't take much to make a hab net positive).
- **MC buildings (2026-10-19, user's game knowledge):**
  - The **Operations Center** (4 MC, $30/month upkeep) and **Command Center** (10 MC, $100/month) are much better MC sources than the Administration buildings until late. Their money upkeep has to be considered.
  - **One MC building per hab** is a game limit for everyone.
  - In the 2039 debug, 24–43 habs per AI faction already had an Ops or Command Center and generic MC was still 0. MC buildings on habs are therefore largely maxed already; MC growth has to come from elsewhere (more habs that pay for themselves, nations, orgs), or from spending MC better.
  - The **Administration Tower/Complex** cost boost upkeep (4/12 a month) and give only 1–2 MC. They pay off through their percent bonus to a hab's production, so they belong on very high-value habs, or in Earth orbit when starved for CP cap. That is a separate rule.
- **Gang-up-on-the-leader: keep it, but smarter.** The first suggestion ("only when the leader actually threatens them, never at the cost of ignoring the aliens") was judged too lenient or too restrictive.
  - 2026-10-19, chosen: **scale by the lead**, **grudges from injury**, **alien-aware weighting** (graded, not "never").
  - New idea from the user: **client/minor ally of the leader**, limited to compatible factions. For example, Exodus is compatible with most anti-alien factions: it would rather be left alone to build its escape ship than spend on blowing up Resistance bases and ships. "Consider the web of relationships and which this works for."
  - "There is a distinct feel between pro-alien and anti-alien NPCs, but not much distinction within anti-alien NPCs, and I'd like them to feel very different."
  - Proposal: §9.

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
| H5 | **Idle savings** (found 2026-10-19, see "Human market and the real queue bottleneck"). | `ShouldTapSavings` | Savings for a ship that can't start for months starve every startable ship. |
| H6 | **Zero stock values a resource at ~0** (found 2026-10-20). `GetResourceStorageModifier` rescales storage days by `(stock + extra) / stock`; at stock 0 that is 0/0 (NaN, modifier 1) for the first slice, then +∞ days (modifier 0) for every later slice. | TradeAI.cs:353 | A faction with none of a resource will not pay for it. The Initiative at 0 metals valued 7.7k metals at 153 and refused 12 of 12 deals a run. Also affects how an AI prices the player's offers. |

**Step 1 results** (2031 save, 90–120 days, one run each against vanilla; `Human/VanillaFixes.cs`):

| Fix | Result | Default |
|---|---|---|
| H1 | Mod: 1 direct investment (Exodus, Mission Control). Vanilla: 0. Real but **low impact**: even the intended budget (10–20% of influence) rarely covers a priority. | on |
| H2 | Flips Losing → LosingBig when strength is below half the rival's (the Protectorate, Dec 2031). Vanilla's other LosingBig route (every Earth interface orbit full) also fires in 2031. | on |
| H3 | Re-run passes bought 64 extra orgs in 120 days, but AI org totals at the end **matched vanilla** (355 vs 353): the extra orgs are shed again, so the fix churns money. Purchases often **replace** a councilor's weakest org (`TryAddOrgToCouncilor` moves it to the pool, then it is sold), so counts hide upgrades. On org quality (sum of tiers on councilors), the re-test gave **+26 with the fix vs +25 vanilla**. **No measurable gain**: the early `break` isn't what holds the AI back. | **off** |
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

### Why so few warships: candidate causes (code trace, 2026-10-18; not yet measured)
1. **Demand is self-referential, and aliens count only at war.**
   - The fleet-goal baseline uses the threat at the location with `warEnemiesOnly` for humans. `GetWarAdjustment` truncates to an integer, so a faction not formally at war counts as 0 (AIE 4471-4478) and the baseline falls to `minimumFleetStrength = 1`.
   - DefendWithFleet sizes each goal from the faction's **own** total fleet: 0.15 / (stations + base bodies), or 0.3 for the primary hab, then × 0.6 for humans (FactionGoal_DefendWithFleet 196-335). AssembleFleet = 0.33 × own fleets. This is circular.
   - Threat-based local sizing needs `IsDefenseFeasible`, which holds only for the best station in a system.
2. **The mission-control gate.** Ships are queued only with generic MC available (ADFP 3757; TFS 1360-1375: income minus usage minus a 7.5% reserve). Hab founding bypasses it through `GrantMissionControlIndulgence` (FactionGoal_FoundHab 26-35).
3. **No war with the alien faction.**
   - War needs hate ≥ 100/clamp(ideological distance, 1, 2.5) and intel ≥ 0.25 (AIE 5973-6017).
   - When the alien nation is founded, war is declared on the AlienProxy; only veryAntiAlien factions also declare on the aliens (AIE 6172-6175).
4. **Hab-first scheduling and early loop exits.**
   - Hab goals win saving-target ties; FoundBase sits at importance 17/19.
   - Ship queuing runs about 2 passes a month, 1 ship per goal.
   - `UpdateShipyardQueues` stops at the first goal with unpaid pending ships, or at the first can't-afford (ADFP 3762-3765, 3822-3825).
5. **Attack thresholds out of reach** (1.2–3× the defenders, against 35k alien stacks), plus a hull-size bias toward small hulls (fleetSmalls 3–4).

### Measured (vanilla, 2026-10-18): a year from 2034 with the `human-fleet` diagnostic, plus the 2039 vanilla soak
- **Demand and war are NOT the binding constraint where measured.** In July 2034, Humanity First was at war with the aliens, its defence goals wanted 12k and attack goals 28k, all wanting ships. It had 25 shipyards, generic MC available (18), $47k and 9k metals, and 32 ships queued. It still fielded 199 SCV (13 ships), reaching 873 a year later.
- **Early on, the AIs keep pace with the player.** In 2034 the Resistance (the player's own faction in this save) has 0.9–1.1k SCV at ~19–27 per ship, the same scale as the AI factions (0.1–1.6k). The aliens are 24–32k at ~97 per ship: the gap there is technology.
- **By 2039 the AIs have stalled:**

  | Faction | SCV (Mar 2039 → Mar 2040) | Per ship |
  |---|---|---|
  | Resistance (player) | 11.3k → 13.1k | 101–109 |
  | AI factions | 1.0–4.6k each | 21–76 |
  | Aliens | 40k → 69k | — |

  The Academy went 4.4k → 4.6k, Exodus 4.6k → 4.4k. **The player outgrows them 3–10×.**
- **What actually constrains them** (2034–35, per faction):
  - generic MC available is **0** most months for the Academy, Exodus and, later, Humanity First;
  - **metals 0 all year** for the Initiative;
  - **$15–54k idle money** everywhere;
  - **small, weak hulls**: half the player's SCV per ship (`fleetSmalls` 3–4 vs `fleetMediums` 2–3 vs large 1), which matches the community's tiny-fleet spam (S7).
- **Restated symptom:** the AI factions want warships but can't build them efficiently. Each is stuck on a different bottleneck (MC, metals), while money sits idle and the hulls they do build are small.

### Mod: the MC plan (2026-10-19, human-fleet-growth ×2, 2039 + 365 days)
- **Player vs AI MC (2039 debug):**
  - The player had 83 habs and 768 MC income, 467 of it from 65 MC buildings (~7.2 each); 183 MC was available.
  - Each AI faction had 17–45 habs and 236–336 MC, at ~3.6–4 MC per building (nearly all Operations Centers).
- **The plan:** upgrade Ops Centers to Command Centers first (+6 MC each, within the one-per-hab limit), then build new MC buildings.
  - The runs started 52 and 56 buildings or upgrades in the year.
  - An Ops Center produces nothing during the 240-day upgrade, so MC dipped mid-year (Academy 337→297, Exodus 332→277).
  - Since fdf7589, MC offline in upgrades is capped at 15% of MC income. These two runs predate the cap.
- **Result:**
  - MC income rose by December: Academy 335→390, Exodus 332→391–411, Protectorate 251→350.
  - Generic MC turned **positive at the end of the year** (+29 to +75) for four starved factions.
  - Fleet growth was ×1.18 / ×1.12, no better than vanilla's ×1.18, at 50–53 SCV per ship.
  - **The MC arrives after ~8 months, too late for a one-year window.** Next: a 2-year run, or a 2034 start where the buildings compound.

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

## 9. Faction personalities and the gang-up web (design, 2026-10-19)

**Vanilla (ADFP.JealousyAndDeescalation, monthly):**
- The "most threatening enemy" is simply the strongest human faction overall (`GetStrongestHumanFaction`), whether or not it touches this faction.
- Every AI faction adds +1 / +1.75 / +2.25 hate toward it (self-assessment Even / Losing / LosingBig). Add +1.4 for a very-pro vs very-anti pair, and subtract 3 (0.75 after year 7) when both sit on the same side.
- Against the player this needs a minimum ideological distance (2.5 / 1.8 / 1.2 / 0 by difficulty). Between AIs it always applies.
- The only per-faction flavour is pro- vs anti-alien. Vanilla even refuses a non-aggression pact with the strongest faction (TIFactionState:14205), so nobody can make peace with the leader.

**Ideology map** (x = alien stance, y = malleability; distances from `ideologyDistanceGrid`):

| | Resist (1,0) | HF (2,−0.5) | Exodus (0.5,1) | Initiative (0,−1) | Academy (−0.5,1) |
|---|---|---|---|---|---|
| Resist | 0 | 1.12 | 1.12 | 1.41 | 1.80 |
| HF | 1.12 | 0 | 2.12 | 2.06 | 2.92 |
| Exodus | 1.12 | 2.12 | 0 | 2.06 | 1.00 |

- Anti-alien (x > 0): Resistance, Humanity First, Exodus. Initiative is neutral (x = 0). Academy is mildly pro-alien but "malleable" like Exodus.

**Three shared terms** (chosen 2026-10-19). Each faction weights them with its own personality numbers:
1. **Lead term:** hate scales with the leader's strength over the runner-up's (ratio r). It is ~0 below r ≈ 1.15 and above vanilla for a runaway (r ≥ 2).
2. **Grudge term:** hate from recent injuries by that faction (councilors killed or detained, CPs or orgs taken, habs or ships destroyed). Each personality decays it at its own rate.
3. **Alien term:** while the aliens are a live threat (escalated, at war, or winning in space), hate toward a leader that is actually fighting them is damped for anti-alien factions and amplified for pro-alien ones. Graded by the threat, never zero.

**Personalities (anti-alien factions should feel very different):**

| Faction | Feel | Lead | Grudge | Alien damping | Client? |
|---|---|---|---|---|---|
| **Resistance** | The coalition-builder: pragmatic, leads the anti-alien bloc, punishes collaborators | medium | medium, normal decay | strong (won't sabotage an anti-alien leader while the aliens press) | patron of Exodus/Academy; becomes a client itself only under severe alien escalation |
| **Humanity First** | The zealot: jealous, vengeful, sees softness as betrayal | high | high, very slow decay (remembers) | weak; none toward Academy/Exodus leaders ("collaborators and cowards") | never a client; a harsh patron (clients are dropped at the first injury) |
| **Exodus** | The isolationist: wants to be left alone to build the ship | low (×0.4) | high but short: retaliates, then lets go | strong | seeks client status with the strongest compatible faction (Resistance, Academy, grudgingly HF) |
| **Initiative** | The opportunist: balances a close race, bandwagons a runaway | inverted: up to r ≈ 1.5, then falls | medium | none (profit first) | a client of whoever runs away, pro-alien patrons included |
| **Academy** | The scholar: hostile to HF and the Servants | medium | medium | — (pro-alien side) | client of Exodus/Resistance (closest anti-alien neighbours) |
| Protectorate, Servants | Pro-alien: vanilla feel, plus the alien term amplifies hate toward an anti-alien leader | vanilla | medium | amplified | no |

**Client/minor ally:**
- Who: a compatible pair (table above), the patron is the leader or within 15% of it, and the client is not itself contending (≤ 60% of the patron's strength).
- What the client does:
  - Gang-up hate toward the patron is cut to ~10%.
  - It may hold a non-aggression pact with the patron (lifting vanilla's no-NAP-with-the-leader rule for that pair) and shares intel.
  - It spends on its own goals (Exodus: the escape objective, hab defence).
- What the patron gives: its gang-up hate toward the client is cut to ~10%. It doesn't target the client's CPs or habs.
- What breaks it:
  - an injury either way (grudge above a threshold);
  - an ideology flip;
  - the client becoming a contender;
  - HF patrons drop clients at the first injury.
- Player: the player can be the patron. Exodus and Academy would then stop gang-up missions against a player Resistance and seek a NAP (via the normal diplomacy screen). Whether an AI ever offers to be the player's client is a question for the user.

**User answers (2026-10-19):**
- The player can be a patron, under the same rules as any faction.
- Initiative bandwagons with a runaway leader even when the leader is pro-alien ("profit first").
- HF is never a client and is a harsh patron. Resistance becomes a client only under severe alien escalation.

**Results: close race (accel-2031-hot-war, 365 days, vanilla ×2 vs mod ×2, 2026-10-19; `tools/hate.py`):**
- **Vanilla:**
  - The leader rotates between Exodus, the Resistance (the player) and the Servants, with strengths 0.9 / 0.9 / 0.7.
  - Jealousy nets **negative** over the year (−107 / −119 summed over all AI pairs): the periodic decay and shared-enemy de-escalation outweigh it. Toward the player it is +3 / +14.
  - The real hostility is **missions** (+306 / +333 hate) and the Servants' wars with everyone (93–95 faction-months at war).
- **Mod:**
  - Mission hate is lower (+201 / +248) and war months are the same (92–97).
  - Jealousy toward the player is +32 / +37. Only the Servants and Protectorate feel it, ~2 a month each. That is the very-pro vs very-anti term (vanilla's +1.4) amplified by the alien term while the Resistance is at war with the aliens (pressure 0.6). The lead term is ~0 at a 1.19× lead.
  - In these runs the player was the strongest all year (vanilla: mostly Exodus), so this is not like for like.
  - Per month, vanilla's equivalent against a leading player would be 1.75 + 1.4 ≈ 3.2.
- **Clients:**
  - Both runs: the Academy became a client of Exodus (Dec 2031) and signed a non-aggression pact.
  - It broke in Aug 2032 (the Academy grew past 0.75× Exodus, "contender") and in Apr 2032 ("injury").
  - No other pairs formed: in a close race nobody is ≥ 0.85× the top while the client is ≤ 0.6× it.
- **Unrelated vanilla event:** in Nov 2032 of mod run 2, every faction gained ~100 hate toward the Servants at once (cause bucket "other", likely "Alien Nation Created").
- **Verdict:** in a close race the mod mostly turns jealousy off, as designed. The runaway-leader case needs the 2039 save (the player at 3–10× the AIs' fleets): soaks running.

**Results: runaway leader (accel-2039-clean-jupiter, 180 days, vanilla ×2 vs mod ×2, 2026-10-19):**
- The player (Resistance) is at a 1.9–2.1× lead.
- **Jealousy toward the player:** mod +104 / +94 vs vanilla +57 / +58.
  - It comes from the Servants, Protectorate and Academy, plus the Initiative (+9/+10), which bandwagons in vanilla's place.
  - Humanity First and Exodus add 0: vanilla's ideology gate (distance 1.12 < 1.8 on Normal) exempts them from ganging up on a player Resistance.
- **No practical effect:**
  - Standing hate toward the player is already 330–2,340 (war at ~50–100), and five AIs are at war with the player all along (30 faction-months in each run).
  - No clients form: the player is the only possible patron and everyone is at war with it.
- **Finding: vanilla hate between humans only ratchets up.** The periodic decay is a flat −1 a month, so by the late game Exodus is at war with the player at 827 hate, the opposite of "would rather be left alone".
- **Added: per-personality forgiveness** (5d953c5), within the design. Each month an AI forgives a share of its standing hate toward each human faction:

  | Exodus | Initiative | Academy | Resistance | Protectorate, Servants | Humanity First |
  |---|---|---|---|---|---|
  | 15% | 6% | 4% | 3% | 2% | never |

  Hate then settles near the monthly injury divided by that share. Wars end once hate falls below the war threshold (`FactionWarOnFaction.ShouldDiscardGoal`). Test: 2039 + 365 days, mod ×2.

**Results: forgiveness (accel-2039-clean-jupiter, 365 days, mod ×2, 2026-10-19):** hate toward the player, Apr 2039 → Mar 2040.

| Faction | Hate | Rate | Note |
|---|---|---|---|
| Exodus | 830 → 136 | 15% | Both runs. War ends below ~89, a few months later |
| Initiative | 413 → 331–337 | 6% | Its bandwagon jealousy and missions keep adding |
| Academy | 315 → 228 | 4% | |
| Protectorate | 1780 → 1481–1486 | 2% | |
| Servants | 2311 → 1912 | 2% | |
| Humanity First | 0 | never | No war |

- Still at war with the player all year (60 faction-months in each run). No clients formed, since the player is the only possible patron.
- This is vanilla's accumulated hate in a late save bleeding off. In a campaign that starts with the mod, hate settles near the injury rate divided by the forgiveness rate.

### Mod: 2-year test from 2034 and ablation (2026-10-19; `tools/hfleet.py`, gross SCV built = net + combat losses)
- **Two-year soak, accel-2034-fusion-drives, 22 months, vanilla ×2 vs mod ×2:**
  - The mod built less: gross 6.5k / 7.0k vs vanilla 7.7k / 8.2k.
  - Combat losses, mostly to the aliens, are about half of gross.
  - Generic MC ended higher with the mod (189–279 vs 144–164): **MC is not binding in 2034**.
  - Metals are: the Initiative sits at 0 all run, the Academy near 1k, and money idles at $30–100k.
- **Ablation (first year, 2 runs per arm):**

  | Arm | Gross built |
  |---|---|
  | Vanilla | 3.80 / 3.48k (mean 3.64k) |
  | Full mod | 2.37 / 3.10k (2.73k) |
  | Mod without the MC group | 2.80 / 2.61k (2.71k) |
  | **Mod without the fleet group** | **3.18 / 3.24k (3.21k)** |

- **Verdict:**
  - **The fleet group costs ships:** boost for ships, bigger hulls, queue unblock, mine what you lack. Bigger hulls didn't even raise SCV per ship (25–27 vs vanilla 26–30), so the upsizer only delayed builds.
  - **The fleet group is now off by default.**
  - The MC group is neutral in 2034 and raises MC income by 2039, so it stays on.
  - The remaining gap to vanilla (~0.4k) is within run noise. One part is by design: the alien proxy supply line takes ~1k metals a quarter from the Servants, Protectorate and Academy.
- **Next bottleneck: metals.**
  - Vanilla AI humans never trade with each other (0 human-to-human trades in all four runs).
  - Candidate: metal-starved AIs with idle money buy metals from other humans with a surplus (the ProxyTrade machinery).

### Human market and the real queue bottleneck (2026-10-19)
- **Market** (metal-starved AIs buy metals and nobles with money from AIs with a surplus):
  - It works mechanically: 14–18 deals a year. In July 2034 the Initiative, at 215 metals, bought 7.6k metals from Exodus for $6.7k.
  - It brought **no gain**: gross 2.90k / 2.56k vs 3.18k / 3.24k without it.
  - The Initiative then held 3–7k metals, $15–28k, 1–1.5k boost and spare MC, with ~50 goals wanting ships, and still sat at 31 ships for nine months, saving for the same design.
  - **Off by default.**
- **The saving targets were cheap and fully banked.** About 120–240 metals each, `banked == cost`; Exodus held 34k metals while "saving" for one small ship for months.
- **Queue diagnostic** (`human-fleet.queues`: the yard heads in `GetOrderedShipyards` order, paid or unpaid, the shortfall, the blocker, the saving target's position). Findings, 2034:
  1. **The shortfall is water** in most cases (Humanity First 68–263, Protectorate 412–447, Initiative 386–1061 plus metals), probably propellant included in the build cost.
  2. Vanilla's daily walk (`ShipConstructionQueueDailyUpdate`) skips every later yard's unpaid head once one head is unaffordable (`lastUnaffordableShipShipyard`).
  3. **Vanilla bug H5, idle savings.** The saving target is usually a ship queued **behind a ship already under construction** (Humanity First: yard 23 of 26). It can't start for months, but its savings stay reserved (`GetShortfall` subtracts them unless `ShouldTapSavings`). So the startable heads show a shortfall caused by the savings themselves, and the first one blocks the walk.
- **Fix H5** (`FixIdleSavings`, a `ShouldTapSavings` postfix, AI humans, ships only): while the target ship is queued but not at the head of any unpaid queue, other ships may tap the savings.
- An earlier version (attempt the target's yard when blocked) never fired: the target is never at a head.

### Clean A/B with ships completed (2026-10-19; `human-ship-built`, accel-2034-fusion-drives, 365 days, vanilla ×3 vs defaults ×3)
- Defaults: the MC group and H5 (idle savings) on; the fleet group and the market off.
- **Ships completed by the six AI factions:**

  | | Ships | SCV |
  |---|---|---|
  | Vanilla | 72 / 74 / 60 (mean 69) | 2,259 / 2,272 / 1,763 (mean 2,098) |
  | Mod | 74 / 71 / 93 (mean 79) | 2,383 / 2,241 / 3,021 (mean 2,548) |

  **+15% ships, +21% SCV**: noisy but positive.
- The Protectorate builds more in all three mod runs (21 / 20 / 23 vs 15 / 15 / 10), consistent with H5.
- **The Initiative completes exactly 3 ships a year in every run, both arms.** Its metal income is negative (upkeep −316 a month), so it can never stock metals. This is a separate problem: its habs and fleet upkeep exceed what it mines.
- Gross SCV (net + losses) is too noisy to judge changes, since combat losses swing 0.6–1.8k between runs. Ships completed is the metric from now on.

### Batch 2 (2026-10-19; three changes in one run, ×3, same fixture as the A/B)
- **Fleet subgroup** (queue unblock + boost for ships + mine what you lack; bigger hulls stays off):

  | Arm | Ships completed | Mean SCV |
  |---|---|---|
  | Vanilla | mean 69 | 2,098 |
  | Defaults | mean 79 | 2,548 |
  | **Batch 2** | **82 / 82 / 84 (mean 83)** | **2,694** |

  It is the best and steadiest arm, so **all three are on by default again**. Bigger hulls was the harmful member of the old fleet group.
- **Mission bias** (mission hate caused, per run, mean of 3):
  - Exodus caused **20** with the bias vs 34 with the defaults (vanilla 26): its restraint works.
  - Humanity First against pro-alien factions: 7 vs 9 (vanilla 8). **The ×1.5 zeal does not show.** Its hostile missions there seem capped by something other than payoff (goal multipliers, free councilors), and scaling the payoff only reorders missions it would already consider.
  - The Resistance is the player in these fixtures, so its bias is untested.
- **Exodus objective focus: never applied.** Exodus had no active objective project all year, with 13 objective projects done. Its next step is gated elsewhere (the deep space telescope for BeginTheSearch, or HydraInterrogation), so research focus is not the lever.

### The Initiative's metal deficit (2026-10-19, `human-fleet.flow`, vanilla 2034)
- Metals come only from habs, for every faction: ships, nations, HQ and orgs are all 0.

  | Faction | Habs | Net metals a month |
  |---|---|---|
  | Initiative | 49 | **−316** |
  | Humanity First | 40 | +491 |
  | Protectorate | 32 | +680 |
  | Exodus | 50 | +1,302 |
  | Player (Resistance) | 54 | +2,347 |

- The Initiative's worst drains are stations at −38 to −45 a month each (Diogo Cao, Ma Huan, Cordoba), with few metal mines.
- By the user's rule (a hab should pay for itself, or not be founded), the Initiative has over-built metal-draining stations. That is why it completes 3 ships a year in every run.
- **Next:** while a faction's net flow of a ship metal is negative, its AI should:
  - not found or extend habs that drain it further (only mining sites with that metal, via need weighting);
  - prefer developing existing habs into positive flow.
  Vanilla has the hook for aliens (`ResourcesExperiencingUpkeepInsecurityInTheFuture`, see Economy); check the human planner for the same.

### Batch 3 (2026-10-20): the drain guard
- **The guard never fired** (0 records in 3 runs): the Initiative never tried to found a station. Its drain is its existing stations.
- Its hab metal flow recovered from −316 to between −47 and +89 a month only as it lost habs (49 → 41–46). It completed 3 / 6 / 6 ships.
- Ships completed: 95 / 107 / 87, with the same effective settings as batch 2 (82 / 82 / 84). **Run-to-run noise is about ±12 ships.**
  - The six runs with these settings average **~90 vs vanilla's 69** (×3).
  - Single batches of 3 can't resolve effects under ~10%.
- The guard stays on: it is harmless and prevents new drains.
- Next for the Initiative: the market again (it has the money). It failed before only because the queues were jammed (before H5 and queue unblock).

### Batch 4 (2026-10-20): the market again, after the queue fixes
- Ships completed: **93 / 103 / 100 (mean 99)**, vs ~93 over batches 2 and 3 without the market, and vanilla 69.
- **The Initiative: 7 / 6 / 14 ships vs 3–6.** Its 14-ship run is the one where it bought 7.6k metals from Exodus in July 2034. With the queues unjammed, metals now become ships.
- 14–16 deals a year, mostly noble metals and metals from Exodus.
- **Market on by default.**
- Summary of the human fleet track (ships completed by the six AI factions, 2034 + 365 days):

  | Arm | Ships |
  |---|---|
  | Vanilla | 69 |
  | First defaults | 79 |
  | Batch 2/3 (+ queue fixes) | ~90–93 |
  | Batch 4 (+ market) | 99 |

### Batch 5 (2026-10-20/21): H6 and water in the market

**Results** (measured in `flow-2034` and the first year of three 2-year soak runs, `soak2yv6`, instead of separate batch runs):

| | Ships completed by the six AI factions, first year |
|---|---|
| Vanilla | 69 (mean of 3) |
| Batch 4 (market) | 93 / 103 / 100 (mean 99) |
| **Batch 5 (+ H6, water)** | **114 / 102 / 117 / 113 (mean 112)** |

- **The Initiative now buys:** 10 deals succeeded and 3 failed (flow-2034), against 1–4 successes and ~20 failures a run before. It completed 14 ships in the 2034 run and 19 in the 2039 run.
- **Water trades:** 8 water deals in the 2034 year; 0 in 2039, where nobody was short.
- Defaults: both on.

**Diagnosis** (from the batch-4 journals, 3 runs):
Diagnosis from the batch-4 journals (3 runs):
- **Water is the most common ship-queue shortfall.** Humanity First, the Initiative, the Academy and Exodus all show water-short heads. The Protectorate holds 20–26k water and the Servants 7–15k. Propellant is part of every ship's build cost (`propellantTanks` × 100 t × the drive's per-tank mix).
- **The Initiative's drain is Nanofactories.** In the 2034 save they are 540 of its 828 metals/month of module upkeep (54 modules, 3–4 per station, mostly on stations without shipyards). Each pays +90 money for 10 metals + 1 noble. At market prices (~0.9 money per metal) that is a good trade, *if* it can buy metals. So the market is the right lever, not decommissioning.
- **It can't buy, because of H6.** With 0 metals it valued 7.7k metals at 153 against $7.1k asked, so 12 of 12 metal deals with Exodus failed in each of two runs. With 359 nobles, 467 more were worth 10k to it, and those deals went through.

Changes (probe `human-batch5`, each with its own metric):
- **H6 fix** (`FixTradeZeroStock`, AI human valuers): storage days are rescaled by `(stock + floor + extra) / (stock + floor)`, with vanilla's own per-resource floor (300 for metals). That is exactly recomputing storage days with the extra stock, and it differs from vanilla only at low stock. Metric: the Initiative's metal deals that succeed, and its ships.
- **Water in the market** (`MarketWater`). Metric: water deals; water shortfalls in `human-fleet.queues`.
- Not done: including propellant in the human need weights. At near-zero water stock the need weight is already at its cap, so it would change little.

### The AI-run Resistance against the 2039 Jupiter strike (2026-10-22, `flow-2039-long-ai`, one run)
- With the Resistance handed to the game's AI (`ai.control`), it **pulled its Jupiter fleet out by September 2039**, and 5–9k of its fleet spent most of the year in interplanetary transit. The alien strike took Jupiter completely (5 habs → 0), while the passive player kept 2.
- It kept its fleet (15.5k at the end vs 10.9k passive), so this is a choice of where to fight, not a loss of strength. But a human player would rarely give up a gas-giant foothold without a fight.
- Candidate weakness (one run, to confirm): AI human factions don't weigh holding remote habs against an incoming alien strike, and their fleets wander between goals. A natural lever for the strategist (docs/STRATEGIST.md: defence priorities).

### Exodus's expansion in the early game (2026-10-22, two years from 2026, one run each)
- Vanilla Exodus founded 15 habs (a first Mars base in March 2028, then a Mars site every two weeks); with the mod, 4 (Deimos, 3353 Jarvis, 3753 Cruithne; never Mars), despite spare MC, boost and money.
- Suspect: `MineWhatYouLack` for humans (need-weighted site scores) steers the first off-Earth sites away from Mars, so vanilla's same-body cascade never starts. The drain guard never fired. To confirm with an A/B (the switch off) over more than one run.
