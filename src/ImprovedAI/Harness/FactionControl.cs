using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Tasks;

namespace ImprovedAI.Harness
{
    /// <summary>
    /// Hand a faction to the game's AI mid-game, or take it back (user, 2026-10-22: probes need a defender that reacts;
    /// the Autopilot only clears prompts and resolves combat, so the harness player was passive).
    ///
    /// Vanilla decides who is AI-run once, when a game loads: AIDailyFactionPlanner.Initialize builds its AIFactions
    /// array and the per-faction scheduling data (factionAIData) from player.isAI. Flipping the flag alone would leave
    /// the faction out of the daily planner. So this flips the flag (TIPlayerState.AssignAIStatus), then adds the faction
    /// to the planner's array and creates its scheduling data, exactly as Initialize would have.
    /// Our human AI changes apply to it too (they gate on player.isAI). Not saved: a save stores the flag, but this
    /// harness never writes the user's saves, and the next load rebuilds the planner from the flag.
    /// </summary>
    internal static class FactionControl
    {
        private static readonly FieldInfo FactionsF = AccessTools.Field(typeof(AIDailyFactionPlanner), "AIFactions");
        private static readonly FieldInfo CountF = AccessTools.Field(typeof(AIDailyFactionPlanner), "numAIFactions");

        public static string Set(TIFactionState f, bool ai)
        {
            var planner = AIDailyFactionPlanner.singleton;
            if (planner == null || FactionsF == null || CountF == null) return "! no AI planner";
            if (f.player == null) return "! faction has no player";
            if (f.IsAlienFaction) return "! the aliens are always AI";
            var list = ((TIFactionState[])FactionsF.GetValue(planner) ?? new TIFactionState[0]).ToList();
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
}
