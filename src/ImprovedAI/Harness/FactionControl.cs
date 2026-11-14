using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using PavonisInteractive.TerraInvicta.Tasks;

namespace ImprovedAI.Harness
{
    /// <summary>
    /// Hand a faction to the game's AI mid-game, or take it back. The Autopilot only clears prompts and resolves combat,
    /// so the harness player is otherwise passive.
    ///
    /// Vanilla decides who is AI-run once, when a game loads: AIDailyFactionPlanner.Initialize builds its AIFactions
    /// array and the per-faction scheduling data (factionAIData) from player.isAI. Flipping the flag alone would leave
    /// the faction out of the daily planner. So this flips the flag (TIPlayerState.AssignAIStatus), then adds the faction
    /// to the planner's array and creates its scheduling data, as Initialize would have.
    /// The mod's human AI changes apply to it too (they gate on player.isAI).
    ///
    /// A save stores the flag, and the game cannot load a save in which no player is human
    /// (GeneralControlsController.Initialize dereferences GameControl.control.activePlayer). So the harness saves with
    /// the factions it handed over marked human again, and hands them back to the AI after a reload.
    /// </summary>
    internal static class FactionControl
    {
        // By template name: a load replaces the faction states.
        private static readonly HashSet<string> Handed = new HashSet<string>();

        public static bool IsHanded(TIFactionState f) => f != null && Handed.Contains(f.templateName);

        public static void SaveAsHuman(System.Action save)
        {
            var players = GameStateManager.AllFactions().Where(f => f?.player != null && Handed.Contains(f.templateName)).Select(f => f.player).ToList();
            foreach (var p in players) p.AssignAIStatus(false);
            try { save(); }
            finally { foreach (var p in players) p.AssignAIStatus(true); }
        }

        public static void HandBackAfterLoad()
        {
            foreach (var f in GameStateManager.AllFactions().Where(f => f != null && Handed.Contains(f.templateName)).ToList()) Set(f, true);
        }

        private static readonly FieldInfo FactionsF = AccessTools.Field(typeof(AIDailyFactionPlanner), "AIFactions");
        private static readonly FieldInfo CountF = AccessTools.Field(typeof(AIDailyFactionPlanner), "numAIFactions");

        public static string Set(TIFactionState f, bool ai)
        {
            var planner = AIDailyFactionPlanner.singleton;
            if (planner == null || FactionsF == null || CountF == null) return "! no AI planner";
            if (f.player == null) return "! faction has no player";
            if (f.IsAlienFaction) return "! the aliens are always AI";
            var list = ((TIFactionState[])FactionsF.GetValue(planner) ?? new TIFactionState[0]).ToList();
            if (ai && !f.player.isAI) Handed.Add(f.templateName);
            else if (!ai) Handed.Remove(f.templateName);
            f.player.AssignAIStatus(ai);
            if (ai)
            {
                if (!list.Contains(f)) list.Add(f);
                if (!AIDailyFactionPlanner.factionAIData.ContainsKey(f))
                    AIDailyFactionPlanner.factionAIData.Add(f, new StaticFactionAIData(f, list.Count));
            }
            else list.Remove(f);   // its factionAIData can stay; it is only read for listed factions
            FactionsF.SetValue(planner, list.ToArray());
            CountF.SetValue(planner, list.Count);
            Session.Journal?.Write(new { t = GameRead.Now(), kind = "control", faction = GameRead.Faction(f), ai, aiFactions = list.Select(GameRead.Faction).ToList() });
            return $"> {f.templateName} is now {(ai ? "AI" : "human")}-controlled ({list.Count} AI factions)";
        }
    }

    /// <summary>
    /// The game's AI finalizes a handed-over faction's missions itself. Confirm becomes clickable once every AI faction
    /// has finalized (CouncilorMissionCanvasController.CheckReleaseUIToPlayer), which with an AI-run player is the moment
    /// the phase ends, and the Autopilot presses whatever is clickable (Autopilot.Update): one more finalize per phase,
    /// outside it, which the game logs as an error ("tried to finalize councilor missions outside of mission phase").
    /// </summary>
    [HarmonyPatch(typeof(CouncilorMissionCanvasController), nameof(CouncilorMissionCanvasController.PlayerConfirmsMissionAssignments))]
    internal static class HandedFactionConfirm
    {
        private static bool Prefix() => !FactionControl.IsHanded(GameControl.control?.activePlayer);
    }

    /// <summary>
    /// The game settles a contact offer between two AI factions itself and hands one that involves the active player to
    /// the diplomacy window (TIPromptQueueState.HandleFactionContactMakeOffer). With the AI at the player's controls
    /// nobody answers that window and the prompt holds the clock for good. A handed-over player trades as any AI does.
    /// </summary>
    [HarmonyPatch(typeof(TIPromptQueueState), "HandleFactionContactMakeOffer")]
    internal static class HandedFactionTrade
    {
        private const string Prompt = "PromptFactionContactMakeOffer";

        private static bool Prefix(TIMissionState mission)
        {
            var player = GameControl.control?.activePlayer;
            var from = mission?.councilor?.faction;
            var to = mission?.target?.ref_faction;
            if (!FactionControl.IsHanded(player) || from == null || to == null || (from != player && to != player)) return true;
            TradeAI.PrepareCachesForTrading(from, to);
            var agreement = TradeAI.CreateTradeAgreement(from, to);
            if (TradeAI.ScoreAgreement(agreement, to) > 0f)
                from.playerControl.StartAction(new DiplomacyTradeAction(from, to, agreement.GetOffer(from), agreement.GetOffer(to), 1f));
            TIPromptQueueState.RemovePromptStatic(from, mission.councilor, mission, Prompt);
            // Where the game left it when it tried the diplomacy window.
            if (mission.target.ref_councilor != null) TIPromptQueueState.RemovePromptStatic(to, mission.target.ref_councilor, mission, Prompt);
            return false;
        }
    }
}
