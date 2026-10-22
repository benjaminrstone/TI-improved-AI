using System;
using System.Linq;
using HarmonyLib;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Tasks;

namespace ImprovedAI.Strategist
{
    /// <summary>
    /// The strategist's "defend" systems for an AI human faction. Vanilla (ManageFleetGoals) only creates a defence goal
    /// for a station or base when enemy fleets are already in the system, or for the primary hab, and only its main
    /// shipyard station counts as defensible. So an AI-run Resistance pulled its fleet out of Jupiter while an alien
    /// strike was inbound (flow-2039-long-ai, 2026-10-22). After vanilla's pass, each directed system gets a defence goal
    /// at the faction's main hab there (its best station, else the body of its best base) with importance at least
    /// DefendImportance, so fleets are assigned to it ahead of most other work.
    /// </summary>
    [HarmonyPatch(typeof(AIDailyFactionPlanner), nameof(AIDailyFactionPlanner.ManageFleetGoals))]
    internal static class DefendDirective
    {
        internal const int DefendImportance = 17;

        private static void Postfix(TIFactionState faction)
        {
            if (!Directives.On || faction == null || faction.IsAlienFaction || !Human.HumanFixes.AI(faction)) return;
            foreach (var system in Directives.DefendSystems(faction).ToList())
            {
                try
                {
                    var habs = faction.habs.Where(h => TIGameState.Valid(h) && string.Equals(GameRead.System(h), system, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (habs.Count == 0) continue;
                    var station = habs.Where(h => h.IsStation).OrderByDescending(h => h.CompletedShipyards().Count).ThenByDescending(h => h.tier).FirstOrDefault();
                    TIGameState target = station != null ? (TIGameState)station : habs.OrderByDescending(h => h.tier).First().ref_spaceBody;
                    if (target == null) continue;
                    var goal = faction.GoalsOfType(GoalType.DefendWithFleet, orderByImportance: false).OfType<FactionGoal_DefendWithFleet>().FirstOrDefault(g => g.target() == target);
                    if (goal == null)
                    {
                        goal = faction.AddGoal(new FactionGoal_DefendWithFleet(faction, DefendImportance, target, "")) as FactionGoal_DefendWithFleet;
                        if (goal != null && Plugin.Cfg.DecisionJournal.Value)
                            Session.Journal?.Write(new { t = GameRead.Now(), kind = "strategist-defend", faction = faction.templateName, system, target = GameRead.Name(target) });
                    }
                    if (goal != null && goal.importance < DefendImportance) goal.SetImportance(DefendImportance);
                }
                catch (Exception e) { Plugin.Log.LogWarning($"[Strategist] defend {faction.templateName} {system}: {e.Message}"); }
            }
        }
    }
}
