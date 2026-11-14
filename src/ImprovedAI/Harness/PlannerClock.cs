using System;
using System.Collections.Generic;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using PavonisInteractive.TerraInvicta.Systems.GameTime;
using PavonisInteractive.TerraInvicta.Tasks;
using UnityEngine;

namespace ImprovedAI.Harness
{
    /// <summary>
    /// With the game's AI at the player's controls, the clock must still wait for the mission planner.
    /// Each AI faction's councilor missions are planned on a pool thread, one faction after another
    /// (AICouncilorMissionPlanner.PlanMissions), while the main thread carries on. Both raise events through
    /// EventManager.TriggerEvent, whose collections are not thread-safe: a HashSet written by two threads can be left in
    /// a state its Contains never returns from, and the game freezes. A human's game is safe because the clock is held by
    /// the player's own mission prompt (SimulationTimeTick.OnUpdate) and Confirm stays disabled until every AI faction has
    /// finalized (CouncilorMissionCanvasController.CheckReleaseUIToPlayer). A handed-over player's prompt is answered by
    /// the AI and removed as soon as its planning is queued (TIPromptQueueState.HandlePlanMissions), so the clock ran on
    /// beside the planner, and now and then a month began with a freeze (the monthly nation update raises thousands of
    /// events on the first frame the clock moves on).
    /// Here the clock waits, for a handed-over player, until every faction asked to plan has finalized.
    /// </summary>
    internal static class PlannerClock
    {
        /// <summary>Off only for the probe that shows the problem.</summary>
        internal static bool Hold = true;

        private static readonly HashSet<TIFactionState> Planning = new HashSet<TIFactionState>();
        private static float _startedAt, _longest;
        private static int _phases, _clockMovedBeside;

        internal static bool Waiting => Hold && Planning.Count > 0 && FactionControl.IsHanded(GameControl.control?.activePlayer);

        internal static void Asked(TIFactionState faction)
        {
            if (Planning.Count == 0) { _startedAt = Time.realtimeSinceStartup; _phases++; }
            Planning.Add(faction);
        }

        internal static void Finalized(TIFactionState faction)
        {
            if (Planning.Remove(faction) && Planning.Count == 0) _longest = Math.Max(_longest, Time.realtimeSinceStartup - _startedAt);
        }

        internal static void Reset() => Planning.Clear();

        /// <summary>
        /// For a run's result: the longest the factions took to plan one phase, and on how many frames the clock moved
        /// while one of them was planning (TIFactionState.planningMissions).
        /// </summary>
        internal static object Report() => new { phases = _phases, longestSeconds = Math.Round(_longest, 1), clockMovedBeside = _clockMovedBeside };

        internal static void ClockMoved()
        {
            foreach (var f in GameStateManager.AllFactions())
                if (f != null && f.planningMissions) { _clockMovedBeside++; return; }
        }
    }

    [HarmonyPatch(typeof(AICouncilorMissionPlanner), nameof(AICouncilorMissionPlanner.PlanMissionsCoroutine))]
    internal static class PlannerClockAsked
    {
        private static void Postfix(TIFactionState faction) => PlannerClock.Asked(faction);
    }

    [HarmonyPatch(typeof(FinalizeCouncilorMissions), nameof(FinalizeCouncilorMissions.Execute))]
    internal static class PlannerClockFinalized
    {
        private static void Prefix(TIFactionState ___faction) => PlannerClock.Finalized(___faction);
    }

    /// <summary>Read each frame by SimulationTimeTick.OnUpdate, which pauses and skips the clock while it is true.</summary>
    [HarmonyPatch(typeof(TIPromptQueueState), nameof(TIPromptQueueState.anyActivePlayerBlocking), MethodType.Getter)]
    internal static class PlannerClockHolds
    {
        private static void Postfix(ref bool __result)
        {
            if (!__result && PlannerClock.Waiting) __result = true;
        }
    }

    [HarmonyPatch(typeof(GameTimeManager), "UpdateTime")]
    internal static class PlannerClockMeasure
    {
        private static void Prefix(float deltaTime)
        {
            if (deltaTime != 0f && CampaignHarness.Active) PlannerClock.ClockMoved();
        }
    }
}
