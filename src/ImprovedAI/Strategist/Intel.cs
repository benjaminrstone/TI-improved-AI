using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;

namespace ImprovedAI.Strategist
{
    /// <summary>
    /// The player learns a faction's intent only through a relationship (user, 2026-10-22): the faction has a
    /// non-aggression pact or an intel-sharing treaty with the player ("allied": the game has no formal alliance), is the
    /// player's client (Human/Personality), or has a councilor the player has turned (councilor.agentForFaction).
    /// Shown as a notification after each checkpoint; in unattended harness runs, journaled only.
    /// </summary>
    internal static class Intel
    {
        public static string Source(TIFactionState f, TIFactionState player)
        {
            if (f == null || player == null || f == player) return null;
            try { if (f.HasNAP(player)) return "non-aggression pact"; } catch { }
            try { if (f.intelSharingFactions != null && f.intelSharingFactions.Contains(player)) return "intel-sharing treaty"; } catch { }
            try { if (ImprovedAI.Human.Personality.PatronOf(f) == player) return "client"; } catch { }
            try { if (f.councilors != null && f.councilors.Any(c => c != null && c.agentForFaction == player)) return "turned councilor"; } catch { }
            return null;
        }

        public static void Show(TIFactionState player, List<TIFactionState> factions)
        {
            if (!Plugin.Cfg.StrategistIntel.Value || player == null) return;
            foreach (var f in factions)
            {
                var d = Directives.For(f);
                if (string.IsNullOrWhiteSpace(d?.intent)) continue;
                string src = Source(f, player);
                if (src == null) continue;
                string headline = $"Intelligence: {f.displayNameCapitalized}";
                string text = $"Through our {src}, we have learned what the {f.displayNameCapitalized} intend:\n\n{d.intent}";
                Session.Journal?.Write(new { t = GameRead.Now(), kind = "strategist-intel", faction = f.templateName, source = src, intent = d.intent });
                if (Harness.CampaignHarness.Active && !Harness.CampaignHarness.Interactive) continue;   // an unattended run must not stack popups
                Notify(f, player, headline, text);
            }
        }

        private static void Notify(TIFactionState f, TIFactionState player, string headline, string text)
        {
            try
            {
                var item = new NotificationQueueItem
                {
                    templateName = "LogFactionWin",
                    primaryFactions = new List<TIFactionState> { player },
                    relevantFactions = new List<TIFactionState> { player, f },
                    icon = f.factionIcon64path,
                    popupResource1 = f.factionIcon256path,
                    popupResource2 = f.factionIcon256path,
                    itemHeadline = headline,
                    itemSummary = text,
                    itemDetail = text,
                    triggerEndGame = false,
                };
                AccessTools.Method(typeof(TINotificationQueueState), "AddItem").Invoke(null, new object[] { item, true });
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[Strategist] intel notification: {e.Message}"); }
        }
    }
}
