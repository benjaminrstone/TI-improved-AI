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
