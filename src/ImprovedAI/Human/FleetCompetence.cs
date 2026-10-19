using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Tasks;
using UnityEngine;

namespace ImprovedAI.Human
{
    /// <summary>
    /// Human AI step 2 (user, 2026-10-19: fix the bottleneck they're stuck on, bigger hulls, queue unblocking).
    /// Measured on vanilla (docs/HUMAN-AI.md §6b): AI human factions want warships but build them slowly and small.
    ///  - Generic MC sits at 0 for some factions; with none, AttemptInitiateShipConstruction refuses every AI ship and
    ///    the queue loop skips fleet goals, while hab founding is exempt.
    ///  - Another faction has metals at 0 all year; $15-54k sits idle everywhere.
    ///  - Hulls are small (fleetSmalls 3-5, Large fixed at 1, and the pick falls back to smaller sizes, never larger).
    ///  - One unaffordable ship stops the whole queue loop, and one stuck shipyard head blocks every other yard's start.
    /// Competence, not handouts: no bonus resources; the AI spends what it has on what blocks it.
    /// Every change is gated to AI human factions (never the player, never the aliens).
    /// </summary>
    internal static class FleetCompetence
    {
        public static bool AIHuman(TIFactionState f) => f != null && !f.IsAlienFaction && f.player != null && f.player.isAI;

        private static readonly Dictionary<TIFactionState, (string day, bool v)> _wantsShips = new Dictionary<TIFactionState, (string, bool)>();

        /// <summary>Some space-combat fleet goal wants ships (cached per faction per day).</summary>
        public static bool WantsWarships(TIFactionState f)
        {
            string today = GameRead.Now();
            if (_wantsShips.TryGetValue(f, out var c) && c.day == today) return c.v;
            bool v = false;
            try
            {
                v = f.factionGoals.Values.SelectMany(l => l).OfType<FactionGoal_Fleet>()
                    .Any(g => g.importance > 0 && (g is FactionGoal_DefendWithFleet || g is FactionGoal_AttackWithFleet) && g.NeedsShipsOrdered());
            }
            catch { }
            _wantsShips[f] = (today, v);
            return v;
        }

        /// <summary>The faction wants warships and has no generic MC to queue or start them.</summary>
        public static bool MissionControlStarved(TIFactionState f) =>
            AIHuman(f) && GameRead.Safe(() => f.AI_GenericMissionControlAvailable) <= 0f && WantsWarships(f);
    }

    // ---- C1. Mission control: invest in it when it is what blocks the warships ----

    /// <summary>
    /// After vanilla's ManageNations, a starved faction runs an extra direct-investment pass (at most every 14 days):
    /// the MissionControl priority in its majority-controlled nations, or a spaceflight programme where there is none.
    /// Budget: shares of money, influence and ops (vanilla's own direct-invest shares, H1 fixed).
    /// </summary>
    [HarmonyPatch(typeof(AIDailyFactionPlanner), "ManageNations")]
    internal static class HumanMissionControlInvest
    {
        private static readonly Dictionary<TIFactionState, TIDateTime> Last = new Dictionary<TIFactionState, TIDateTime>();

        private static void Postfix(AIDailyFactionPlanner __instance, TIFactionState faction)
        {
            if (!Plugin.Cfg.UseHumanMissionControl || !FleetCompetence.MissionControlStarved(faction)) return;
            var now = TITimeState.Now();
            if (Last.TryGetValue(faction, out var last) && now.DifferenceInDays(last) < 14) return;
            Last[faction] = now;
            try
            {
                var budget = new Dictionary<FactionResource, float>
                {
                    { FactionResource.Money, faction.GetCurrentResourceAmount(FactionResource.Money) * 0.5f },
                    { FactionResource.Influence, faction.GetCurrentResourceAmount(FactionResource.Influence) * (faction.councilors.Count == faction.maxCouncilSize ? 0.2f : 0.1f) },
                    { FactionResource.Operations, faction.GetCurrentResourceAmount(FactionResource.Operations) * 0.25f },
                };
                int tried = 0;
                foreach (var nation in faction.majorityControlNations.ToList())
                {
                    if (nation == null || nation.alienNation) continue;
                    __instance.AttemptFundPriority(faction, nation, nation.spaceFlightProgram ? PriorityType.MissionControl : PriorityType.Civilian_InitiateSpaceflightProgram, ref budget);
                    tried++;
                }
                if (Plugin.Cfg.DecisionJournal.Value)
                    Session.Journal?.Write(new { t = GameRead.Now(), kind = "human-mc-invest", faction = GameRead.Faction(faction), nations = tried, mcAvailable = GameRead.Safe(() => faction.AI_GenericMissionControlAvailable) });
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[HumanAI] MC invest: {e.Message}"); }
        }
    }

    /// <summary>While starved, the AI values mission control income more (MC orgs, MC hab modules, control points).</summary>
    [HarmonyPatch(typeof(AIEvaluators), nameof(AIEvaluators.EvaluateMonthlyResourceIncome))]
    internal static class HumanMissionControlValue
    {
        private static void Postfix(TIFactionState faction, FactionResource resource, ref float __result)
        {
            if (resource != FactionResource.MissionControl || __result <= 0f || !Plugin.Cfg.UseHumanMissionControl) return;
            if (!FleetCompetence.MissionControlStarved(faction)) return;
            __result *= Mathf.Max(1f, Plugin.Cfg.HumanMissionControlValue.Value);
        }
    }

    // ---- C2a. Idle boost and money buy what a ship lacks (pay from Earth), as the player does ----

    /// <summary>
    /// Vanilla substitutes a missing space resource with money + boost at Earth-system shipyards, but
    /// ShouldSpendBoostAtShipyard only allows it for tiny amounts or with 500+ boost to spare. With warships wanted,
    /// an AI human spends boost down to HumanBoostReserve. Money is never the limit (market price); boost is.
    /// </summary>
    [HarmonyPatch(typeof(AIEvaluators), nameof(AIEvaluators.ShouldSpendBoostAtShipyard))]
    internal static class HumanBoostForShips
    {
        private static void Postfix(TIFactionState faction, TIHabModuleState shipyard, float boost, ref bool __result)
        {
            if (__result || !Plugin.Cfg.UseHumanBoostForShips || !FleetCompetence.AIHuman(faction) || !FleetCompetence.WantsWarships(faction)) return;
            if (!AIEvaluators.ValidShipyardToSpendBoost(faction, shipyard)) return;
            if (faction.GetCurrentResourceAmount(FactionResource.Boost) - boost >= Plugin.Cfg.HumanBoostReserve.Value) __result = true;
        }
    }

    // ---- B. Bigger hulls ----

    /// <summary>
    /// Build-pick size weights for AI humans: vanilla's fleetSmalls 3-5 and fleetMediums 2-4 against a fixed Large 1
    /// make about 1 in 8 picks large. Scaled down here (restored after the call). Then an upsizer: if a larger design of
    /// the same role exists that can fulfil the goal and the faction can pay for it within HumanUpsizeDays, build that.
    /// </summary>
    [HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.GetDesiredShipToBuild))]
    internal static class HumanBiggerHulls
    {
        private struct Saved { public bool active; public float smalls, mediums; }

        private static void Prefix(TIFactionState __instance, out Saved __state)
        {
            __state = default;
            if (!Plugin.Cfg.UseHumanBiggerHulls || !FleetCompetence.AIHuman(__instance)) return;
            __state = new Saved { active = true, smalls = __instance.aiValues.fleetSmalls, mediums = __instance.aiValues.fleetMediums };
            __instance.aiValues.fleetSmalls *= Mathf.Clamp(Plugin.Cfg.HumanSmallsFactor.Value, 0.05f, 1f);
            __instance.aiValues.fleetMediums *= Mathf.Clamp(Plugin.Cfg.HumanMediumsFactor.Value, 0.05f, 1f);
        }

        private static void Postfix(TIFactionState __instance, FactionGoal_Fleet factionGoal, ref TISpaceShipTemplate __result, Saved __state)
        {
            if (!__state.active || __result == null || !__result.combatant || __result.size == ShipSize.Large) return;
            try
            {
                var current = __result;
                var bigger = __instance.shipDesigns.Where(d => d != null && d.role == current.role && d.size > current.size && !d.Obsolete(__instance)
                        && (!d.requiresExotics || current.requiresExotics) && (!d.requiresAntimatter || current.requiresAntimatter)
                        && d.CanFulfillGoal(factionGoal)
                        && d.spaceResourceConstructionCost(false, null).CanPayInFuture(__instance, (int)Plugin.Cfg.HumanUpsizeDays.Value))
                    .OrderByDescending(d => GameRead.Safe(() => d.TemplateSpaceCombatValue())).FirstOrDefault();
                if (bigger != null) __result = bigger;
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[HumanAI] upsizer: {e.Message}"); }
        }

        private static Exception Finalizer(Exception __exception, TIFactionState __instance, Saved __state)
        {
            if (__state.active)
            {
                __instance.aiValues.fleetSmalls = __state.smalls;
                __instance.aiValues.fleetMediums = __state.mediums;
            }
            return __exception;
        }
    }

    // ---- A. Queue unblocking ----

    /// <summary>
    /// A1. UpdateShipyardQueues stops its whole goal loop (break) when a goal's ship can't be afforded, skipping every
    /// lower goal. AI_GetBestShipyardForBuild reports that as Failure_CantAfford; for AI humans it becomes
    /// Failure_Generic, which the loop treats as "this goal, not now" and continues. The only other caller tests == Success.
    /// </summary>
    [HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.AI_GetBestShipyardForBuild))]
    internal static class HumanQueueContinue
    {
        private static void Postfix(TIFactionState __instance, ref TIFactionState.ShipyardAISearchResult result)
        {
            if (result == TIFactionState.ShipyardAISearchResult.Failure_CantAfford && Plugin.Cfg.UseHumanQueueUnblock && FleetCompetence.AIHuman(__instance))
                result = TIFactionState.ShipyardAISearchResult.Failure_Generic;
        }
    }

    /// <summary>
    /// A2. In ShipConstructionQueueDailyUpdate, after the first unpaid head that can't be afforded, every other
    /// shipyard's head waits (and GetOrderedShipyards puts the stuck yard first every day, so it can block for good).
    /// Afterwards, other yards' unpaid heads may start, unless they need a resource the stuck head is short of and are
    /// not more important, or need exotics (vanilla's own rule).
    /// </summary>
    [HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.ShipConstructionQueueDailyUpdate))]
    internal static class HumanShipyardUnblock
    {
        private static void Postfix(TIFactionState __instance)
        {
            var stuck = __instance.lastUnaffordableShipShipyard;
            if (stuck == null || !Plugin.Cfg.UseHumanQueueUnblock || !FleetCompetence.AIHuman(__instance)) return;
            try
            {
                var queues = __instance.nShipyardQueues;
                if (!queues.TryGetValue(stuck, out var sq) || sq.Count == 0) return;
                var head = sq[0];
                int stuckImp = head.AIFactionGoal?.importance ?? 10;
                var short_ = new HashSet<FactionResource>(head.resourcesCost.GetShortfall(__instance, head.shipDesign, stuck, stuckImp).resourceCosts.Where(x => x.value > 0f).Select(x => x.resource));
                int started = 0;
                foreach (var yard in queues.Keys.ToList())
                {
                    if (yard == stuck || !yard.active || queues[yard].Count == 0) continue;
                    var item = queues[yard][0];
                    if (item.costPaid) continue;
                    if (item.resourcesCost.GetSingleCostValue(FactionResource.Exotics) > 0f) continue;
                    int imp = item.AIFactionGoal?.importance ?? 10;
                    if (imp <= stuckImp && item.resourcesCost.resourceCosts.Any(r => r.value > 0f && short_.Contains(r.resource))) continue;
                    if (__instance.AttemptInitiateShipConstruction(yard)) started++;
                }
                if (started > 0 && Plugin.Cfg.DecisionJournal.Value)
                    Session.Journal?.Write(new { t = GameRead.Now(), kind = "human-yard-unblock", faction = GameRead.Faction(__instance), started });
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[HumanAI] yard unblock: {e.Message}"); }
        }
    }
}
