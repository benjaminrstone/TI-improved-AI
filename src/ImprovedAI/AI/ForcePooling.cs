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

        internal static bool On(TIFactionState f) => Plugin.Cfg.UseForcePooling && f != null && f.IsAlienFaction;

        private static void Postfix(TIFactionState faction)
        {
            if (!On(faction) || !AIEvaluators.ShouldAliensGoLoud()) return;
            if (!AIDailyFactionPlanner.factionAIData.TryGetValue(faction, out var data)) return;
            if ((TITimeState.Now().day + data.every4DaysOffset) % 4 != 0) return;   // vanilla's ManageFleets tick

            Pooling.RemoveWhere(g => g == null || g.skipGoal || g.importance <= 0 || !TIGameState.Valid(g.assignedFleet)
                                     || g.assignedFleet.transferAssigned || g.InProgress());

            var goal = faction.GoalsOfType(GoalType.AttackWithFleet, orderByImportance: true)
                .OfType<FactionGoal_AttackWithFleet>()
                .Where(g => !g.skipGoal && g.importance > 0 && !g.InProgress() && TIGameState.Valid(g.target()))
                .OrderByDescending(g => Pooling.Contains(g))          // stick with the current focus
                .ThenByDescending(g => g.importance)
                .ThenByDescending(g => g.desiredFleetCombatValue)
                .FirstOrDefault();
            if (goal == null) return;
            var anchor = goal.assignedFleet;
            if (anchor == null || anchor.transferAssigned) return;    // let vanilla assign a fleet first

            float need = goal.desiredFleetCombatValue * Plugin.Cfg.PoolMargin.Value;
            float have = anchor.SpaceCombatValue() + goal.pendingFleets.Where(TIGameState.Valid).Sum(x => x.SpaceCombatValue());
            if (have >= need)
            {
                if (Pooling.Remove(goal))
                {
                    Session.Journal?.Write(new { t = GameRead.Now(), kind = "pool-ready", goalId = GameRead.Id(goal), target = GameRead.Name(goal.target()), targetSystem = GameRead.System(goal.target()), have, need, fleet = anchor.displayName });
                    AIDailyFactionPlanner.SingleFleetOperation(anchor);
                }
                return;
            }
            Pooling.Add(goal);

            // Why fleets are not donors (diagnostics for tuning).
            var rej = new Dictionary<string, int>();
            bool Ok(bool cond, string why) { if (!cond) rej[why] = (rej.TryGetValue(why, out var n) ? n : 0) + 1; return cond; }
            var donors = faction.fleets
                .Where(f => f != anchor && TIGameState.Valid(f) && !goal.pendingFleets.Contains(f)
                            && Ok(!f.transferAssigned, "inTransfer") && Ok(f.isCapableOfTransfering, "cantTransfer")
                            && Ok(f.CombatFleet() && !f.InvasionFleet(), "notCombat") && Ok(!f.AI_NeedsRepairBadly(), "needsRepair")
                            && Ok(!AIEvaluators.ShouldSystemBeInDefenseMode(faction, f.ref_system), "defenseMode")
                            && Ok(Releasable(faction, f), "goal:" + (f.AssignedGoal()?.GetGoalType().ToString() ?? "none")))
                .OrderBy(f => f.ref_system == anchor.ref_system ? 0 : 1)
                .ThenByDescending(f => f.SpaceCombatValue())
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
                float keep = System.Math.Max(threat * Plugin.Cfg.AlienAttackSuperiority.Value, held * Plugin.Cfg.PoolReserveFraction.Value);
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
                sent.Add(new { fleet = d.displayName, scv = v, at = GameRead.System(d), was = from?.GetGoalType().ToString() });
            }
            Session.Journal?.Write(new
            {
                t = GameRead.Now(), kind = "pool", goalId = GameRead.Id(goal), imp = goal.importance,
                target = GameRead.Name(goal.target()), targetSystem = GameRead.System(goal.target()),
                anchor = anchor.displayName, at = GameRead.System(anchor), need, have, donors = sent, rejected = rej,
                maxRatio = GameRead.Safe(() => goal.GetMaximumFleetCombatValueRatio()),
            });
        }

        private static bool Releasable(TIFactionState faction, TISpaceFleetState f)
        {
            var g = f.AssignedGoal();
            if (g == null || g is FactionGoal_AssembleFleet) return true;
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
