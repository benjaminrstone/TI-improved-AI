# Human AI factions: how vanilla works, and what the mod changes

The aim for the AI human factions is **competence over handouts**: they play better and get no bonus resources. Every change has a `[HumanAI]` switch (`src/ImprovedAI/AIConfig.cs`), applies only to AI-run human factions, and is off under `VanillaAI=true`. Code: `src/ImprovedAI/Human/`.

References are relative to `_ref/decompiled/`: ADFP = `PavonisInteractive.TerraInvicta.Tasks/AIDailyFactionPlanner.cs`, ACMP = `…Tasks/AICouncilorMissionPlanner.cs`, HHP = `…Tasks/HumanHabPlanner.cs`, AIE = `PavonisInteractive.TerraInvicta/AIEvaluators.cs`, TFS = `…/TIFactionState.cs`.

## 0. Decisions
- **Role: both, alien-first.** Rivals on Earth who also defend their space assets, fight the aliens and contest the gas giants. Target symptom: "you fight the aliens alone" (S1).
- **Mission control:** grow MC much harder and reserve it for the fleet. Weak habs are not retired: a hab gets an MC building and positive resource flow, or is not founded.
- **MC buildings (game knowledge):** the Operations Center (4 MC, $30/month) and Command Center (10 MC, $100/month) are the MC sources until late, one per hab (a game limit). Administration buildings cost boost for 1–2 MC and a production bonus: a separate rule, not built.
- **Gang-up on the leader: keep it, smarter.** Scaled by the lead, with grudges from injury and graded alien-aware weighting.
- **Clients:** a compatible weaker faction may become the leader's client, the player's included. The Initiative bandwagons with a runaway leader, pro-alien or not. Humanity First is never a client. The Resistance is one only under severe alien escalation.
- **Feel:** the anti-alien factions should feel very different from each other.

## 1. Architecture and timing
- **Daily at midnight:** `FactionOperations0000` (ADFP:269) shuffles the AI factions and builds task lists. Early: ManageCouncilors, SetResearchPriorities, ManageNations. Late: BuildSpaceAssets, ArmyOperations, FleetOperations. It runs `AnalyzeDeficiencies_Human` (ADFP:345; self-assessment, deficiencies, CP trouble, risk aversion) and `ReviewAndSetGoals` (ADFP:2284).
- **During the day:** each faction pops one random task per tick (ADFP:230-239). The rest is flushed at 23:00 (ADFP:333).
- **Cadences:** councilors weekly (days ≤ 28); research daily; nation priorities and direct investment every 14 days; space assets weekly (habs every run, modules on even days, ships on odd days); armies daily; fleet goals every 4 days; hab goals every 14 days.
- **Mission phase:** `MissionPhasePrep` (ACMP:2012; CP self-disabling ADFP:1133, alliances and rivalries ADFP:4009), `ManagePriorityNationControlGoalsForFaction` (ADFP:890), then `PlanMissionsTask` on a worker thread (ACMP:2069).
- **Habs:** only `HumanHabPlanner` is live. `LegacyHumanHabPlanner` is dead apart from the static site selector.

## 2. Councilor missions
- **Expansion targets.** At start, two random supra-regions: their biggest nation, its neighbours, and filler by opinion × population (ADFP:799). Each mission phase adds capture goals: nations where the faction has CPs, allies and claims, neighbours of the alien nation, one major power once it has 4+ councilors (ADFP:1055), and extras by `EvaluateNation × opinion` with a 25% random pick (ADFP:1106).
- **Plan order:** victory missions, go-to-ground, loyalty checks, objective hunts, fleet councilors, high-value policies, inspire, then "focus fire" on a major power (only if none is controlled, ACMP:3200). Then every mission × target is scored per councilor (expected utility, AIMissionEntry.cs:104), the top N are kept, and the pick among them is **weighted-random** (ACMP:3518).
- **CP defence:** payoff-driven DefendInterests/Protect only. No reaction to known enemy councilors.

## 3. Space
- **Stations:** always expand below 4 (HHP:117).
- **Bases:** capped at 5 + (years − 5)/5, at most 9, unless a resource income is ≤ 0 (HHP:304-310). Founding needs MC available (HHP:514).
- **Ships:** each fleet goal saves for one design, and ships are queued only when the goal needs them (ADFP:3600-3846). All human fleets assemble at Earth's first interface orbit (ADFP:1682).
- **Defence:** fleets defend only the primary hab, or the best station in a system (`IsDefenseFeasible`, AIE:4529; ADFP:1449), never bases. No primary-system peril recall for humans (AIE:4335), no defence mode in the Earth system (AIE:4659).
- **Offence:** needs a `WarOnFaction` goal, discarded when hate drops below threshold (FactionGoal_WarOnFaction.cs:140), and ≥ 0.25 intel on the target (AIE:6007). War attack goals are randomly zeroed at 2.3% a week (FINDINGS 11b), found-hab goals at 5% (HHP:192-199).

## 4. Research, economy, difficulty
- **Research:** global tech is highest tier only, then weighted-random by `ScoreTech` (AIE:1159, 1729). Projects follow the same pattern, one slot per trigger. Non-critical project weights are re-rolled daily (ADFP:3052). The "don't race" check covers only techs with zero prerequisites (ADFP:3133-3138).
- **Economy:** org buying takes up to 85–95% of money, 25–75% of influence, 75% of ops. The mission budget is 50% of money, and influence minus savings. CPs are self-disabled over the free allowance (ADFP:1136). Resources are sold only when money is negative. AI humans never trade with each other.
- **Difficulty:** FINDINGS §3. The AI's mission attacker/defender bonus applies only against or by the *player* (TIGlobalConfig.cs:2819, 2847). The player's influence bonus is 0/30/45/60.

## 5. Ideology
- **Hate toward the aliens:** veryProAlien factions gain none monthly, anti-alien factions gain extra (ADFP:583-612).
- **The alien nation:** every non-veryProAlien faction gets a NeutralizeNation goal on it (ADFP:938). When it is founded, non-veryProAlien humans reset hate between themselves (AIE:6177-6192) and declare war on the AlienProxy. Only veryAntiAlien factions also declare on the aliens (AIE:6172-6175).
- **Pro-alien only:** TransferControl and PassTechnology (ACMP:389, 1250).
- **`shouldNeverAttackAliens`** blocks assault and assassination of aliens (TFS:974; ACMP:326, 353).

## 6. Vanilla bugs and fixes (`Human/VanillaFixes.cs`)

| # | Bug | Where | Switch (default) and fix |
|---|---|---|---|
| H1 | **Direct investment never fires.** `(influence * councilors.Count == maxCouncilSize) ? 0.2f : 0.1f`: precedence makes the influence budget the literal 0.1 or 0.2. | ADFP:4774 | `FixDirectInvest` (on): budget = influence × 0.1 or 0.2. Low impact: it rarely covers a priority. |
| H2 | **"Losing big" is unreachable.** `Losing` (< 0.8) is tested before `LosingBig` (< 0.5). | ADFP:370-377 | `FixLosingBig` (on): Losing below half the strongest rival becomes LosingBig. |
| H3 | **Org buying stops at the first conflict** (`break` for `continue`): about one org per weekly pass. | ADFP:2604-2607 | `FixOrgBuying` (**off**): re-runs the pass. Off because the extra orgs were shed again (§10). |
| H4 | **The mission risk relaxation is dead.** `acceptableMinimumSuccess` reads `payoff` before it is assigned. | AIMissionEntry.cs:74 vs 91 | `FixMissionRisk` (on). Verified by reading the code only. |
| H5 | **Idle savings.** Savings for a ship queued behind one under construction stay reserved (`GetShortfall`, unless `ShouldTapSavings`) and starve every startable ship. | `TIResourcesCost` | `FixSavingDeadlock` (on): until the target heads an unpaid queue, other ships may tap the savings. |
| H6 | **Zero stock values a resource at about 0.** `GetResourceStorageModifier` rescales storage days by `(stock + extra) / stock`, so a faction with none of a resource will not pay for it. | TradeAI.cs:353 | `FixTradeZeroStock` (on): rescales with vanilla's own stock floor. Also changes how an AI prices the player's offers. |

**Suspected, not verified:** the focus goal is never passed to normal missions (AIMissionEntry.cs:96, ACMP:158-161); the favourites boost is `.Take(0)` (ACMP:3495); focus-fire removes the wrong councilor's entries (ACMP:3359); the `SellOrgs` condition looks inverted (ADFP:2771); a dead AccessLiveHydra branch in `DetainCouncilorPayoff` (ACMP:718-728); a discarded LINQ `Append` (ADFP:965/982); integer division in `RecruitCouncilors` (ADFP:2395).

## 6b. Why AI humans field few warships (code trace)
1. **Demand is self-referential, and aliens count only at war.** The fleet-goal baseline uses the local threat with `warEnemiesOnly`, and `GetWarAdjustment` truncates to an integer, so a faction not at war counts as 0 (AIE 4471-4478). DefendWithFleet sizes each goal from the faction's **own** fleet: 0.15 / (stations + base bodies), 0.3 for the primary hab, × 0.6 for humans (FactionGoal_DefendWithFleet 196-335). AssembleFleet = 0.33 × own fleets.
2. **The MC gate.** Ships are queued only with generic MC available (ADFP 3757; TFS 1360-1375: income minus usage minus a 7.5% reserve). Hab founding bypasses it (`GrantMissionControlIndulgence`, FactionGoal_FoundHab 26-35). Modules also need generic MC (HHP `BuildHabModules`, `HabMayBeUpgraded`), so a starved faction cannot build the MC buildings it needs.
3. **War with the aliens** needs hate ≥ 100/clamp(ideological distance, 1, 2.5) and intel ≥ 0.25 (AIE 5973-6017).
4. **Hab-first scheduling and early loop exits.** Hab goals win saving-target ties (FoundBase importance 17/19). Ship queuing runs about 2 passes a month, 1 ship per goal. `UpdateShipyardQueues` stops at the first goal with unpaid pending ships or the first can't-afford (ADFP 3762-3765, 3822-3825). The daily walk (`ShipConstructionQueueDailyUpdate`) skips every later yard once one head is unaffordable (`lastUnaffordableShipShipyard`).
5. **Small hulls, high thresholds.** `fleetSmalls` 3–4 vs `fleetMediums` 2–3 vs large 1. Attack thresholds are 1.2–3× the defenders.
6. **Propellant is in every ship's build cost** (`propellantTanks` × 100 t × the drive's per-tank mix), so water blocks builds.
7. **The human hab planner never checks upkeep** (only the alien planner reads `ResourcesExperiencingUpkeepInsecurityInTheFuture`).

## 7. Likely weaknesses (from code, not probed)
1. **Expansion into minor nations stops at one CP over the free cap.** `MinorCPTrouble` is maintenance cost > 0 (TFS:15412), and ControlNation then returns −1 for non-major powers (ACMP:497-501).
2. **Small nations (≤ 4 CP) are safe from Crackdown and Purge** unless already targeted (ACMP:615-623, 1210-1218).
3. **Weighted-random mission choice** (§2), **shallow space defence, fragile offence and passive expansion** (§3).

**Player reports** (1.0 and later): helpless against the aliens, "you fight the aliens and Servants alone" (S1); they gang up on the leading player (S2, by design: `AI_GangUpOnLeader…`); factions can't be killed (S4); tiny single-ship fleets (S7); never a peer opponent (S9).

## 8. What the mod adds
All on by default except `BiggerHulls`. Sources: `FleetCompetence.cs`, `MissionControlPlan.cs`, `HumanMarket.cs`, `HabFlow.cs`.
- `MissionControl`: an MC-starved faction that wants warships invests directly in MC and values MC income × 3.
- `MCBuildings`: monthly, a starved faction upgrades Ops Centers to Command Centers (240 days offline each, capped at 15% of MC income), then builds MC buildings on habs with none.
- `FleetMCReserve`: while warships are wanted, 30% of MC income is closed to hab founding.
- `QueueUnblock`: one unaffordable ship no longer stops the queue loop or the other shipyards.
- `BoostForShips`: a ship's missing space resources may be paid with boost and money.
- `MineWhatYouLack`: hab sites are scored by need (`AI/Economy.cs`).
- `BiggerHulls` (**off**): favours larger hulls. Suspected of costing ships (§10).
- `Market`, `MarketWater`: monthly, an AI human short of metals, noble metals or water for warships buys them with money from an AI human with a surplus. Never the player.
- `DrainGuard`: no new stations while net hab flow of metals or noble metals is negative. It never fired in 3 runs and is kept as harmless.

## 9. Personalities, gang-up and clients

**Vanilla** (`ADFP.JealousyAndDeescalation`, monthly). Every AI faction adds +1 / +1.75 / +2.25 hate (self-assessment Even / Losing / LosingBig) toward the strongest human faction, +1.4 for a very-pro vs very-anti pair, −3 (−0.75 after year 7) on the same side. Against the player it needs a minimum ideological distance (2.5 / 1.8 / 1.2 / 0 by difficulty). Decay is a flat −1 a month, so late-game hate only ratchets up. Nobody accepts a non-aggression pact with the strongest faction (TFS:14205).

**Built** (`Personalities`, `ClientAllies`, both on). `Human/Personality.cs` replaces the jealousy block with per-faction terms. The numbers and the feel of each faction are in its `Profiles` table.
- **Lead:** vanilla's base × a factor of the leader's strength over the runner-up's (0 below 1.15, 1 at 2, at most 1.5). The Initiative balances a close race and bandwagons a runaway.
- **Grudge:** hate raised by a faction's missions, space attacks and war accumulates per pair and decays at the victim's half-life (Exodus 2 months, Humanity First 18).
- **Alien:** toward a leader at war with the aliens, anti-alien factions damp their jealousy and pro-alien ones amplify it, graded by alien pressure.
- **Forgiveness:** a monthly share of standing hate (Exodus 15%, Initiative 6%, Academy 4%, Resistance 3%, pro-alien 2%, Humanity First never).
- **Clients:** a faction attaches to a compatible patron (`Personality.Compatible`; the player included) at least 0.85× the strongest faction, while it is at most 0.6× the patron and not at war with it. Between the pair there is no jealousy, hostile mission payoffs are × 0.2, and a non-aggression pact is allowed. It breaks on war, injury, or the client passing 0.75× the patron. State is in the sidecar. Client intel sharing is not built.
- **Mission bias** (`PersonalityActions.cs`): hostile mission payoffs are × 1.5 for Humanity First against pro-alien factions, × 1.3 for the Resistance against very-pro-alien ones, × 0.5 for Exodus unless it holds a grudge (then × 1.2).

## 10. What the measurements showed

Runs are non-deterministic, and every fixture comes from one Normal campaign with a passive Resistance player.

**Headline.** Ships completed by the six AI factions in one year from the 2034 fixture: **vanilla 69** (72 / 74 / 60, 3 runs), **mod 112** (114 / 102 / 117 / 113, 4 runs, current defaults). On the way: MC plan + H5 79 (3 runs); + queue unblock, boost for ships and mine what you lack 90 (6 runs); + market 99 (3 runs). Noise is about ±12 ships, so three runs cannot resolve effects under 10%. Count ships completed: gross SCV built swings with combat losses.

**Vanilla symptom** (3 two-year runs from 2034). Each AI faction fields 0.3–2.4k SCV against about 35k for the aliens, nearly every battle with the aliens is alien-initiated, and the AI factions lose 60–80 habs per run. By 2039 the player's fleet is 3–10× each AI faction's (one run).

**Bottlenecks.**
- **Mission control, late.** In 2039 generic MC is 0 all year for five of six AI factions, although 24–43 habs each already hold an MC building (3.6–4 MC per building against the player's 7.2). In 2034 MC is not binding. The MC plan raises MC income within a year from 2039, too late to move fleet growth in that year (2 runs). Longer windows are unmeasured.
- **Queue blocking** (H5 and the walk's early exit). The market showed no gain before the queue fixes (2 runs) and a gain after.
- **Water and metals.** Water is the most common ship-queue shortfall. The Initiative's habs drain 316 metals a month (its Nanofactories alone use 540). It completed 3 to 6 ships a year without the market and 6 to 14 with it. Until H6 was fixed most of its deals failed.
- **Not binding where measured:** demand and war. In July 2034 Humanity First, at war with the aliens, wanted 40k SCV and fielded 199 with yards, MC and money to spare.

**Resting on one or two runs.**
- **H1, H2, H3** (one run each): one direct investment vs none; one faction flipped to LosingBig; with H3 on, org totals after 120 days were 355 vs 353 and org quality +26 vs +25.
- **Bigger hulls off:** the whole fleet group built less than vanilla (2 runs per arm, before H5). The other three members did well once re-enabled. Bigger hulls was never tested alone.
- **Remote defence** (one run): run by the game's AI, the Resistance pulled its fleet out of Jupiter ahead of an alien strike and lost all 5 habs there.

**Personalities** (2 runs per arm). In a close race vanilla jealousy nets negative over a year (the hostility is mission hate and the Servants' wars) and the mod turns it mostly off. Against a runaway player the added jealousy is lost in standing hate of 330–2,340. A client formed only in the close race (the Academy under Exodus, broken within months). Forgiveness took Exodus's hate toward the player from 830 to 136 in a year. Exodus's mission restraint shows (hate caused 20, against 34 without the bias and 26 in vanilla, 3 runs). Humanity First's × 1.5 does not.
