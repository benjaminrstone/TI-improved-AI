using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;

namespace ImprovedAI.Diagnostics
{
    /// <summary>
    /// One journal line per strategic-layer battle (autoresolved or fought): where, who, each side's starting SCV,
    /// ships lost / fled, winner. Outcomes are the honest measure of "bite-sized fleets get eaten"; launch ratios alone
    /// can't show it.
    /// </summary>
    [HarmonyPatch(typeof(TISpaceCombatState), nameof(TISpaceCombatState.EndCombatForStrategyGame))]
    internal static class CombatJournal
    {
        private static void Postfix(TISpaceCombatState __instance)
        {
            if (Session.Journal == null) return;
            try
            {
                var c = __instance;
                var sides = new List<object>();
                foreach (var f in c.factions.Where(x => x != null))
                {
                    var recs = c.combatRecord.singleAssetRecords?.Where(r => r.faction == f).ToList() ?? new List<CombatRecord.SingleAssetCombatRecord>();
                    float initial = 0f;
                    if (c.initialFactionFleetStrengths != null) c.initialFactionFleetStrengths.TryGetValue(f, out initial);
                    float assets = c.assets != null && c.assets.TryGetValue(f, out var a) ? a.Sum(x => GameRead.Safe(() => x.SpaceCombatValue())) : float.NaN;
                    sides.Add(new
                    {
                        f = GameRead.Faction(f),
                        fleetScv0 = initial,
                        assetScvEnd = assets,
                        assets = recs.Count,
                        destroyed = recs.Count(r => r.outcome == SingleAssetCombatOutcome.Destroyed),
                        fled = recs.Count(r => r.fled),
                    });
                }
                TIGameState where = (TIGameState)c.hab ?? c.fleets?.FirstOrDefault(x => x != null);
                Session.Journal.Write(new
                {
                    t = GameRead.Now(),
                    kind = "combat",
                    body = GameRead.Body(where),
                    system = GameRead.System(where),
                    hab = GameRead.Name(c.hab),
                    attacker = GameRead.Faction(c.attackingFaction),
                    occurred = c.combatOccurs,
                    winner = GameRead.Faction(c.winner),
                    balance = c.combatBalance,
                    sides,
                });
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[Journal] combat: {e.Message}"); }
        }
    }
}
