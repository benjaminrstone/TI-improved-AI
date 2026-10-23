using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Tasks;

namespace ImprovedAI.AI
{
    /// <summary>
    /// Concentrate alien force on one objective at a time (EXPLOITS E9, and the general "spread too thin" problem).
    ///
    /// Vanilla gives each attack goal exactly ONE fleet (AssignFleet replaces, never adds); existing fleets only merge
    /// within the same system, where a defender wins the merge; and GetMaximumFleetCombatValueRatio can fall to 0,
    /// which blocks merging and splits waiting attack fleets. In the 2039 probe the aliens held ~65k SCV across 24
    /// defence postings while every Jupiter attack goal starved on a single 1.5k-3.8k fleet and reset all year.
    ///
    /// Here, on vanilla's own 4-day fleet tick and after it has run, the alien faction picks ONE focus attack goal and
    /// orders idle fleets, assembly fleets and surplus defenders (above 1.25x their own desired strength, never the
    /// primary system, never earmarked boss defenders, never systems in defence mode) to join that goal's fleet until it
    /// reaches the goal's desired strength. Supporting patches keep vanilla from undoing it (see the classes below).
    /// </summary>
    [HarmonyPatch(typeof(AIDailyFactionPlanner), "ManageFleets")]
    internal static class ForcePooling
    {
        /// <summary>Goals currently gathering a pooled force; vanilla must leave their fleet alone.</summary>
        internal static readonly HashSet<FactionGoal_AttackWithFleet> Pooling = new HashSet<FactionGoal_AttackWithFleet>();
        /// <summary>Goals whose pool-ready was journaled (once each).</summary>
        private static readonly HashSet<FactionGoal_AttackWithFleet> Announced = new HashSet<FactionGoal_AttackWithFleet>();
        private static bool _wasEscalated;
        /// <summary>Goals whose rendezvous was chosen (once each, before joiners commit; again when escalation begins).</summary>
        private static readonly HashSet<FactionGoal_AttackWithFleet> Staged = new HashSet<FactionGoal_AttackWithFleet>();
        /// <summary>When each goal's force became complete at the rendezvous (muster start).</summary>
        private static readonly Dictionary<FactionGoal_AttackWithFleet, TIDateTime> MusterStart = new Dictionary<FactionGoal_AttackWithFleet, TIDateTime>();

        internal static bool On(TIFactionState f) => Plugin.Cfg.UseForcePooling && f != null && f.IsAlienFaction;

        private static void Postfix(TIFactionState faction)
        {
            if (!On(faction) || !AIEvaluators.ShouldAliensGoLoud()) return;
            if (!AIDailyFactionPlanner.factionAIData.TryGetValue(faction, out var data)) return;
            if ((TITimeState.Now().day + data.every4DaysOffset) % 4 != 0) return;   // vanilla's ManageFleets tick

            Pooling.RemoveWhere(g => g == null || g.skipGoal || g.importance <= 0 || !TIGameState.Valid(g.assignedFleet)
                                     || g.assignedFleet.transferAssigned || g.InProgress());
            if (Plugin.Cfg.LeaveDryShips.Value) LeaveDryShipsBehind(faction);

            var goal = faction.GoalsOfType(GoalType.AttackWithFleet, orderByImportance: true)
                .OfType<FactionGoal_AttackWithFleet>()
                .Where(g => !g.skipGoal && g.importance > 0 && !g.InProgress() && TIGameState.Valid(g.target()))
                .Where(g => !Strategist.Directives.Spared(GameRead.System(g.target())))
                .OrderBy(g => Strategist.Directives.StrikeRank(GameRead.System(g.target())))   // the strategist's targets first
                .ThenByDescending(g => Pooling.Contains(g))          // stick with the current focus
                .ThenByDescending(g => g.importance)
                .ThenByDescending(g => g.desiredFleetCombatValue)
                .FirstOrDefault();
            if (goal == null) return;
            var anchor = goal.assignedFleet;
            if (anchor == null || anchor.transferAssigned) return;    // let vanilla assign a fleet first

            float need = goal.desiredFleetCombatValue * Dials.PoolMargin;
            // Rendezvous: once per focus goal, before the joiners commit, muster at the best-placed fleet.
            // Escalation frees many more fleets ("commit everything"): choose the muster point again at that moment.
            if (Escalation.Escalated != _wasEscalated) { _wasEscalated = Escalation.Escalated; Staged.Clear(); }
            if (Plugin.Cfg.Rendezvous.Value && !Staged.Contains(goal))
            {
                var better = Rendezvous.Choose(faction, goal, anchor, need, f => Releasable(faction, f, goal), out bool decided);
                if (decided) Staged.Add(goal);
                if (better != null)
                {
                    var old = anchor;
                    goal.AssignFleet(better);
                    anchor = better;
                    faction.AddGoal(new FactionGoal_JoinFleet(faction, anchor), HandleDuplicateGoalRule.ResetImportance, old);
                }
            }
            float present = anchor.SpaceCombatValue();
            // Time-aware pooling (2026-10-21). Counting every joiner the moment it was ordered let fleets 300-470 days
            // out (Triton, Luison, Anansi -> Titan) fill the need on paper; recruiting stopped and the 2039 strike waited
            // a year for the slowest. Now only joiners that arrive within the join horizon count; the rest still come,
            // as reinforcements that follow the strike.
            bool timeAware = Plugin.Cfg.PoolTimeAware.Value;
            float horizon = Plugin.Cfg.RendezvousMaxJoinDays.Value;
            var pending = goal.pendingFleets.Where(TIGameState.Valid).ToList();
            float late = 0f;
            float have = present;
            foreach (var p in pending)
            {
                float v = p.SpaceCombatValue();
                if (timeAware && Rendezvous.Eta(p, anchor) > horizon) late += v;
                else have += v;
            }
            // The goal stays in Pooling (so LeaveMyFleetAlone protects it) until its attack is under way; the cleanup
            // above drops it then. Releasing it at "ready" let vanilla's weekly 2.3% random discard of war attack goals
            // (FactionGoal_WarOnFaction.DailyGoalMaintenance) kill an assembled Jupiter strike while its joiners were
            // still in transit (2039 escalation probe, 2026-10-16).
            Pooling.Add(goal);
            if (present >= need)
            {
                // Ready only when the force is actually there (joiners merged), not when it is merely on its way.
                // Then a muster: a short coordination period before setting out together (user, 2026-10-16: "getting
                // your captains aligned"). Later, a command node (mothership) present could shorten it.
                var now = TITimeState.Now();
                if (!MusterStart.TryGetValue(goal, out var since))
                {
                    MusterStart[goal] = since = now;
                    Session.Journal?.Write(new { t = GameRead.Now(), kind = "pool-muster", goalId = GameRead.Id(goal), target = GameRead.Name(goal.target()), targetSystem = GameRead.System(goal.target()), have = present, need, fleet = anchor.displayName, at = GameRead.Body(anchor) });
                }
                if (now.DifferenceInDays(since) < Dials.MusterDays) return;
                if (Announced.Add(goal))
                    Session.Journal?.Write(new { t = GameRead.Now(), kind = "pool-ready", goalId = GameRead.Id(goal), target = GameRead.Name(goal.target()), targetSystem = GameRead.System(goal.target()), have = present, need, fleet = anchor.displayName });
                AIDailyFactionPlanner.SingleFleetOperation(anchor);
                return;
            }
            MusterStart.Remove(goal);   // fell below need again (losses, need grew): muster restarts when complete
            if (have >= need)
            {
                // Enough is on its way: wait for the joiners (logged monthly so the assembly can be followed).
                if (TITimeState.Now().day <= 4)
                    Session.Journal?.Write(new
                    {
                        t = GameRead.Now(), kind = "pool-wait", goalId = GameRead.Id(goal), target = GameRead.Name(goal.target()), targetSystem = GameRead.System(goal.target()),
                        need, have, present, late, fleet = anchor.displayName,
                        // The slowest counted joiner: when the force can be complete.
                        lastEta = pending.Select(p => Rendezvous.Eta(p, anchor)).Where(x => x <= horizon || !timeAware).DefaultIfEmpty(0f).Max(),
                    });
                return;
            }

            // Why fleets are not donors (diagnostics for tuning).
            var rej = new Dictionary<string, int>();
            bool Ok(bool cond, string why) { if (!cond) rej[why] = (rej.TryGetValue(why, out var n) ? n : 0) + 1; return cond; }
            var eta = new Dictionary<TISpaceFleetState, float>();
            float Eta(TISpaceFleetState f) => eta.TryGetValue(f, out var e) ? e : eta[f] = Rendezvous.Eta(f, anchor);
            var candidates = faction.fleets
                .Where(f => f != anchor && TIGameState.Valid(f) && !goal.pendingFleets.Contains(f)
                            && Ok(!f.transferAssigned, "inTransfer") && Ok(f.isCapableOfTransfering, "cantTransfer")
                            && Ok(f.CombatFleet() && !f.InvasionFleet(), "notCombat") && Ok(!f.AI_NeedsRepairBadly(), "needsRepair")
                            && Ok(!AIEvaluators.ShouldSystemBeInDefenseMode(faction, f.ref_system), "defenseMode")
                            && Ok(Releasable(faction, f, goal), "goal:" + (f.AssignedGoal()?.GetGoalType().ToString() ?? "none")));
            // Time-aware: nearest arrival first, and only within the join horizon. Otherwise vanilla-like: same system
            // first, then the strongest.
            var donors = (timeAware
                    ? candidates.Where(f => Ok(Eta(f) <= horizon, "tooFar")).OrderBy(Eta)
                    : candidates.OrderBy(f => f.ref_system == anchor.ref_system ? 0 : 1).ThenByDescending(f => f.SpaceCombatValue()))
                .ToList();

            // Per-system release budget: what the aliens hold there beyond max(local human threat x superiority,
            // reserve fraction of their own force). The primary system keeps everything.
            var budget = new Dictionary<TISpaceBodyState, float>();
            float Budget(TISpaceBodyState sys)
            {
                if (sys == null) return 0f;
                if (budget.TryGetValue(sys, out var b)) return b;
                if (faction.primaryHab != null && sys == faction.primaryHab.ref_system) return budget[sys] = 0f;
                float held = faction.fleets.Where(x => x.ref_system == sys && !x.transferAssigned).Sum(x => GameRead.Safe(() => x.SpaceCombatValue()));
                float threat = GameStateManager.AllHumanFactions().SelectMany(h => h.fleets).Where(x => x.ref_system == sys).Sum(x => ThreatMemory.Remembered(x));
                float keep = System.Math.Max(threat * Dials.AttackSuperiority, held * Dials.PoolReserveFraction);
                return budget[sys] = System.Math.Max(0f, held - keep);
            }

            var sent = new List<object>();
            foreach (var d in donors)
            {
                if (have >= need) break;
                float dv = d.SpaceCombatValue();
                if (d.AssignedGoal() is FactionGoal_DefendWithFleet)
                {
                    if (Budget(d.ref_system) < dv) { Ok(false, "budget"); continue; }
                    budget[d.ref_system] -= dv;
                }
                var from = d.AssignedGoal();
                faction.AddGoal(new FactionGoal_JoinFleet(faction, anchor), HandleDuplicateGoalRule.ResetImportance, d);
                float v = d.SpaceCombatValue();
                have += v;
                sent.Add(new { fleet = d.displayName, scv = v, at = GameRead.System(d), was = from?.GetGoalType().ToString(), eta = (float)System.Math.Round(Eta(d)) });
            }
            // The large fleets left out, and why (15-year campaign, 2026-10-23: 29k sat idle while a 3.5k strike starved).
            string Why(TISpaceFleetState f)
            {
                if (f.transferAssigned) return "inTransfer";
                if (!f.isCapableOfTransfering) return "cantTransfer";
                if (!(f.CombatFleet() && !f.InvasionFleet())) return "notCombat";
                if (f.AI_NeedsRepairBadly()) return "needsRepair";
                if (AIEvaluators.ShouldSystemBeInDefenseMode(faction, f.ref_system)) return "defenseMode";
                if (!Releasable(faction, f, goal)) return "goal";
                if (timeAware && Eta(f) > horizon) return "tooFar";
                return "budgetOrEnough";
            }
            var refusedBig = faction.fleets
                .Where(f => f != anchor && TIGameState.Valid(f) && !goal.pendingFleets.Contains(f) && !donors.Take(sent.Count).Contains(f)
                            && GameRead.Safe(() => f.SpaceCombatValue()) >= 1000f)
                .OrderByDescending(f => GameRead.Safe(() => f.SpaceCombatValue())).Take(12)
                .Select(f => new
                {
                    fleet = f.displayName, scv = (float)System.Math.Round(GameRead.Safe(() => f.SpaceCombatValue())), at = GameRead.Body(f), why = Why(f),
                    goal = f.AssignedGoal()?.GetGoalType().ToString(), goalImp = f.AssignedGoal()?.importance,
                    eta = (float)System.Math.Round(System.Math.Min(99999f, Eta(f))),
                    dvKps = (float)System.Math.Round(GameRead.Safe(() => f.currentDeltaV_kps)), accel = (float)System.Math.Round(GameRead.Safe(() => f.cruiseAcceleration_mps2), 3),
                }).ToList();
            Session.Journal?.Write(new
            {
                t = GameRead.Now(), kind = "pool", goalId = GameRead.Id(goal), imp = goal.importance,
                target = GameRead.Name(goal.target()), targetSystem = GameRead.System(goal.target()),
                anchor = anchor.displayName, at = GameRead.System(anchor), need, have, present, late, donors = sent, rejected = rej, refusedBig,
                maxRatio = GameRead.Safe(() => goal.GetMaximumFleetCombatValueRatio()),
            });
        }

        /// <summary>
        /// Escalated (P4: "commit everything"), fleets on lesser work are stripped for the decisive strike too: other
        /// attacks and raids not yet under way, Earth surveillance, sends and joins elsewhere, all below the focus goal's
        /// importance. Repair, resupply and councilor transport are never stripped. In the 2039 Jupiter probe the pool
        /// otherwise sat at 4-8k of a 17-21k need for 8 months, its donors all busy (2026-10-16).
        /// </summary>
        private static readonly HashSet<GoalType> StrippableWhenEscalated = new HashSet<GoalType>
        {
            GoalType.AttackWithFleet, GoalType.CaptureHab, GoalType.SurveilEarth, GoalType.SendFleet, GoalType.JoinFleet, GoalType.SecureEarthSpace,
        };

        /// <summary>
        /// A fleet moves only as far as its emptiest ship. In the 15-year campaign (2026-10-23) Victor-27 (3.7k, 45 ships)
        /// sat for years at 409 Aspasia, an asteroid with no alien base, because a few of its ships had no delta-v left:
        /// its resupply goal could never be carried out. A fleet not in transfer or combat whose dry ships (under
        /// DryShipShare of their maximum delta-v) are a minority leaves them behind as a fleet of their own, so the rest
        /// can operate again. The game's own split action, as vanilla uses for ships that fail a goal's requirements.
        /// </summary>
        private static void LeaveDryShipsBehind(TIFactionState faction)
        {
            float share = UnityEngine.Mathf.Clamp(Plugin.Cfg.DryShipShare.Value, 0.01f, 0.5f);
            foreach (var f in faction.fleets.ToList())
            {
                try
                {
                    if (!TIGameState.Valid(f) || f.transferAssigned || f.inCombatOrWaitingForCombat || f.ships == null || f.ships.Count < 2) continue;
                    if (f.currentOperations != null && f.currentOperations.Count > 0) continue;
                    var dry = f.ships.Where(s => s != null && s.currentMaxDeltaV_kps > 0f && s.currentDeltaV_kps < share * s.currentMaxDeltaV_kps).ToList();
                    if (dry.Count == 0 || dry.Count * 2 >= f.ships.Count) continue;
                    float before = GameRead.Safe(() => f.currentDeltaV_kps);
                    faction.playerControl.StartAction(new PavonisInteractive.TerraInvicta.Actions.SplitFleetOperationAction(f, dry));
                    Session.Journal?.Write(new
                    {
                        t = GameRead.Now(), kind = "dry-split", fleet = f.displayName, at = GameRead.Body(f), ships = f.ships.Count, dry = dry.Count,
                        dvBefore = (float)System.Math.Round(before), dvAfter = (float)System.Math.Round(GameRead.Safe(() => f.currentDeltaV_kps)),
                        goal = f.AssignedGoal()?.GetGoalType().ToString(),
                    });
                }
                catch (System.Exception e) { Plugin.Log.LogWarning($"[Pooling] dry split {f?.displayName}: {e.Message}"); }
            }
        }

        private static bool Releasable(TIFactionState faction, TISpaceFleetState f, FactionGoal_AttackWithFleet focus)
        {
            var g = f.AssignedGoal();
            if (g == null || g is FactionGoal_AssembleFleet) return true;
            // A repositioning order (SendFleet) is lesser work than a pooled strike even outside escalation: 10 of the
            // fleets refused at every check in 2040-41 (15-year campaign, 2026-10-23) were on one.
            if (Plugin.Cfg.ReleaseSendFleet.Value && focus != null && g is FactionGoal_SendFleet && g.importance < focus.importance && !g.InProgress()) return true;
            if (Escalation.Escalated && focus != null && g != focus && StrippableWhenEscalated.Contains(g.GetGoalType())
                && g.importance < focus.importance && !g.InProgress() && !(g is FactionGoal_JoinFleet j && j.target()?.ref_fleet == focus.assignedFleet))
                return true;
            if (g is FactionGoal_DefendWithFleet d)
            {
                // Release is limited by the per-system budget in the caller (local threat, reserve), not by the defend
                // goal's own desired value, which vanilla sizes from the strongest enemy anywhere and so never shows surplus.
                if (d.importance >= 20 || d.IsPrimarySystemDefender) return false;
                if (faction.primaryHab != null && d.target() == faction.primaryHab) return false;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Escalated, fleets joining the pooled strike burn harder: vanilla picks the fastest trajectory within
    /// (1 - reserve) of the fleet's delta-v, keeping 50% in reserve for any move that doesn't end at a resupply point,
    /// joins included. In the 2039 probe the strike's joiners took 4-8 months to reach Titan. A desperate command keeps
    /// EscalatedJoinReserve instead (it spends real propellant; the muster base resupplies them).
    /// </summary>
    [HarmonyPatch(typeof(AIDailyFactionPlanner), nameof(AIDailyFactionPlanner.SelectTrajectoryAsync))]
    internal static class EscalatedJoinBurn
    {
        private static void Prefix(PavonisInteractive.TerraInvicta.TIVirtualFleetState.IMobileAsset fleet, ref float desiredReserveDVFraction)
        {
            if (!Escalation.Escalated || !(fleet is TISpaceFleetState f) || f.faction == null || !f.faction.IsAlienFaction) return;
            if (!(f.AssignedGoal() is FactionGoal_JoinFleet j)) return;
            var anchor = j.target()?.ref_fleet;
            if (anchor == null || !ForcePooling.Pooling.Any(g => g.assignedFleet == anchor)) return;
            desiredReserveDVFraction = System.Math.Min(desiredReserveDVFraction, Plugin.Cfg.EscalatedJoinReserve.Value);
        }
    }

    /// <summary>Vanilla's max fleet ratio for attack goals can fall below 1 and is then set to 0, which blocks merges and
    /// splits waiting attack fleets apart (SingleFleetOperation). Keep it at least 1.5 for the aliens.</summary>
    [HarmonyPatch(typeof(FactionGoal_AttackWithFleet), nameof(FactionGoal_AttackWithFleet.GetMaximumFleetCombatValueRatio))]
    internal static class PoolingMaxRatio
    {
        private static void Postfix(FactionGoal_AttackWithFleet __instance, ref float __result)
        {
            if (ForcePooling.On(__instance.faction) && __result < Plugin.Cfg.AttackMaxRatioFloor.Value) __result = Plugin.Cfg.AttackMaxRatioFloor.Value;
        }
    }

    /// <summary>A goal that is gathering a pooled force keeps its fleet: no 4-day swap for a closer fleet, not a joiner elsewhere.</summary>
    [HarmonyPatch(typeof(FactionGoal_AttackWithFleet), nameof(FactionGoal_AttackWithFleet.LeaveMyFleetAlone))]
    internal static class PoolingKeepFleet
    {
        private static void Postfix(FactionGoal_AttackWithFleet __instance, ref bool __result)
        {
            if (!__result && ForcePooling.On(__instance.faction) && __instance.assignedFleet != null && ForcePooling.Pooling.Contains(__instance)) __result = true;
        }
    }

    /// <summary>Outside a defensive emergency, alien defenders don't absorb attack fleets that happen to share their system.</summary>
    [HarmonyPatch(typeof(FactionGoal_Fleet), nameof(FactionGoal_Fleet.MayIncreaseFleetSize))]
    internal static class PoolingDefendersDontGrow
    {
        private static void Postfix(FactionGoal_Fleet __instance, ref bool __result)
        {
            if (!__result || !(__instance is FactionGoal_DefendWithFleet d) || !ForcePooling.On(d.faction)) return;
            var system = d.target()?.ref_system;
            if (system != null && !AIEvaluators.ShouldSystemBeInDefenseMode(d.faction, system)) __result = false;
        }
    }
}
