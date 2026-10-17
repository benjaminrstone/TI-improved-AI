using System;
using System.Linq;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;

namespace ImprovedAI.Diagnostics
{
    /// <summary>
    /// Trades involving the aliens (user, 2026-10-13: the Servants and Protectorate, maybe the Academy later, can trade
    /// with them). Once a pro-alien faction completes AlienDiplomacy (a Contact mission on an alien councilor),
    /// AI-to-AI offers run through TIPromptQueueState.HandleFactionContactMakeOffer -> DiplomacyTradeAction ->
    /// TIFactionState.ProcessTrade, called on the receiving side with the giver's offer. One "trade" record per side
    /// received: who gave what (resources, projects, control points, habs, orgs, treaty).
    /// </summary>
    [HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.ProcessTrade))]
    internal static class TradeJournal
    {
        private static void Postfix(TIFactionState __instance, TradeOffer acceptedOffer, TIFactionState otherFaction, bool originalContactingFaction)
        {
            if (__instance == null || otherFaction == null || acceptedOffer == null) return;
            if (!__instance.IsAlienFaction && !otherFaction.IsAlienFaction) return;
            if (!Plugin.Cfg.DecisionJournal.Value) return;
            try
            {
                Session.Journal?.Write(new
                {
                    t = GameRead.Now(),
                    kind = "trade",
                    from = GameRead.Faction(acceptedOffer.offeringFaction ?? otherFaction),
                    to = GameRead.Faction(__instance),
                    // DiplomacyTradeAction calls the sender's ProcessTrade with originalContactingFaction = true.
                    initiator = GameRead.Faction(originalContactingFaction ? __instance : otherFaction),
                    resources = acceptedOffer.resourceValues?.Where(r => r.value != 0f).ToDictionary(r => r.resource.ToString(), r => (float)Math.Round(r.value, 1)),
                    projects = acceptedOffer.projects?.Select(p => p.dataName).ToList(),
                    controlPoints = acceptedOffer.controlPoints?.Count ?? 0,
                    habs = acceptedOffer.habs?.Select(h => h.displayName).ToList(),
                    orgs = acceptedOffer.orgs?.Count ?? 0,
                    treaty = acceptedOffer.treatyType.ToString(),
                });
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[TradeJournal] {e.Message}"); }
        }
    }
}
