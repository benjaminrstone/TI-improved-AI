using System;
using System.Collections.Generic;
using HarmonyLib;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;

namespace ImprovedAI.Human
{
    /// <summary>
    /// A hab should pay for itself, or not be founded (user, 2026-10-19). The Initiative's habs drain -316 metals a month
    /// (2034: 49 habs, the worst being stations at -38..-45 each) and it completes 3 ships a year in every run; the human
    /// hab planner never checks upkeep (only the alien planner reads ResourcesExperiencingUpkeepInsecurityInTheFuture).
    /// Drain guard v1, founding only: while an AI human's net hab flow of a ship metal (metals, noble metals) is
    /// negative, it doesn't found stations (they can't mine, and their modules and crew draw metals). Bases stay allowed.
    /// Module builds are left alone: the planner's module pass stops at the first blocked module (break), so blocking
    /// there would also stop the mines.
    /// </summary>
    [HarmonyPatch(typeof(AIEvaluators), nameof(AIEvaluators.ShouldNotBuildHabModuleRightNow))]
    internal static class HabDrainGuard
    {
        private static readonly FactionResource[] ShipMetals = { FactionResource.Metals, FactionResource.NobleMetals };
        private static readonly Dictionary<(TIFactionState, FactionResource), (string day, float v)> _flow = new Dictionary<(TIFactionState, FactionResource), (string, float)>();

        internal static float HabFlow(TIFactionState f, FactionResource r)
        {
            string day = GameRead.Now();
            if (_flow.TryGetValue((f, r), out var c) && c.day == day) return c.v;
            float v = GameRead.Safe(() => f.GetYearlyIncomeFromHabs(r) / 12f);
            _flow[(f, r)] = (day, v);
            return v;
        }

        private static void Postfix(TIHabModuleTemplate module, TIFactionState faction, ref bool __result)
        {
            if (__result || !Plugin.Cfg.UseHumanDrainGuard || module == null || !module.coreModule || module.habType == HabType.Base) return;
            if (!FleetCompetence.AIHuman(faction)) return;
            try
            {
                // Core modules cost only money; a station's drain comes from the modules built on it later (and its
                // crew), and a station can never mine. So no new station at all while a ship metal's hab flow is negative.
                foreach (var r in ShipMetals)
                {
                    float flow = HabFlow(faction, r);
                    if (flow >= 0f) continue;
                    __result = true;
                    if (Plugin.Cfg.DecisionJournal.Value)
                        Session.Journal?.Write(new { t = GameRead.Now(), kind = "human-drain-guard", faction = GameRead.Faction(faction), module = module.dataName, resource = r.ToString(), flow = (float)Math.Round(flow) });
                    return;
                }
            }
            catch { }
        }
    }
}
