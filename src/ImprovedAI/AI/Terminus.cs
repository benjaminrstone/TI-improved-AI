using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;
using UnityEngine;

namespace ImprovedAI.AI
{
    /// <summary>
    /// The full terminus: P2's hard clock (DESIGN "Two clocks"; user, 2026-10-12: completion is a hard alien victory,
    /// LW2 Avatar-style). The Hydras build a full wormhole terminus on this side; if it completes, they win.
    ///  - Site: the alien HQ (Kuiper dwarf planet, hosts the AlienWormholeFacility; vanilla makes its core and wormhole
    ///    modules indestructible and aims the human victory missions at it).
    ///  - Start: when the aliens take humanity seriously: escalation (P4), or contempt for any faction at or below
    ///    TerminusStartContempt; and in any case by TerminusStartYear (calendar campaign years, per difficulty).
    ///  - Funding: TerminusDripShare of the monthly exotics drip, really spent; a wider wormhole builds it faster, and
    ///    progress = exotics invested / TerminusCost (per difficulty).
    ///  - Setbacks: alien defeats in the HQ's system knock it back; alien habs lost there knock it back further.
    ///  - Legibility: the player is told at 25/50/75/90%, and when it completes the campaign ends in alien victory.
    ///    [ext] Later the posture panel shows the meter once intel is high enough; before that, captives go silent on it.
    /// </summary>
    internal static class Terminus
    {
        public static bool On => Plugin.Cfg.UseTerminus && AlienStrategy.Current != null;
        private static readonly float[] Milestones = { 0.25f, 0.5f, 0.75f, 0.9f };

        public static TIHabState Site()
        {
            try { return GameStateManager.AlienFaction()?.primaryHab ?? GameStateManager.FindByTemplate<TIHabState>("AlienHQ"); } catch { return null; }
        }

        public static float Cost => Mathf.Max(100f, Dials.ByDifficulty(Plugin.Cfg.TerminusCost.Value, 2400f));

        public static void Monthly()
        {
            if (!On) return;
            var s = AlienStrategy.Current;
            if (s.terminusComplete) return;
            var alien = GameStateManager.AlienFaction();
            if (alien == null) return;

            if (s.terminusStarted == null)
            {
                float minContempt = GameStateManager.AllHumanFactions().Where(h => h != null).Select(h => Disposition.Contempt(h)).DefaultIfEmpty(1f).Min();
                // By date as well (user, 2026-10-23): in a 15-year campaign the humans never threatened the aliens, so
                // neither trigger fired and the aliens never moved toward their victory.
                float startYear = Dials.ByDifficulty(Plugin.Cfg.TerminusStartYear.Value, 11f);
                bool byDate = startYear > 0f && Wormhole.CampaignYears >= startYear;
                bool start = Escalation.Escalated || minContempt <= Plugin.Cfg.TerminusStartContempt.Value || byDate;
                if (!start) return;
                s.terminusStarted = GameRead.Now();
                string why = Escalation.Escalated ? "escalation" : minContempt <= Plugin.Cfg.TerminusStartContempt.Value ? "respect" : "date";
                s.history.Add($"{s.terminusStarted} terminus begun ({why}; escalated {Escalation.Escalated}, min contempt {minContempt:F2}, campaign year {Wormhole.CampaignYears:F1})");
                Session.Journal?.Write(new { t = GameRead.Now(), kind = "terminus", ev = "started", why, escalated = Escalation.Escalated, minContempt, campaignYears = Wormhole.CampaignYears, cost = Cost, site = GameRead.Body(Site()) });
            }

            float drip = Economy.Drip(alien);
            float want = Mathf.Clamp01(Plugin.Cfg.TerminusDripShare.Value) * drip;
            float invest = Mathf.Min(want, Mathf.Max(0f, alien.GetCurrentResourceAmount(FactionResource.Exotics)));
            if (invest > 0f) alien.SubtractFromCurrentResource(invest, FactionResource.Exotics, true, "ImprovedAI terminus");
            float before = s.terminusProgress;
            s.terminusProgress = Mathf.Clamp01(s.terminusProgress + invest / Cost);
            Session.Journal?.Write(new { t = GameRead.Now(), kind = "terminus", ev = "month", before, after = s.terminusProgress, invest, drip, cost = Cost });
            Announce(s);
            if (s.terminusProgress >= 1f) Complete(s);
        }

        public static void Setback(float amount, string why)
        {
            if (!On || AlienStrategy.Current.terminusStarted == null || AlienStrategy.Current.terminusComplete) return;
            var s = AlienStrategy.Current;
            float before = s.terminusProgress;
            s.terminusProgress = Mathf.Max(0f, s.terminusProgress - amount);
            // Milestones already passed may be announced again on the way back up.
            s.terminusMilestones = Milestones.Count(m => s.terminusProgress >= m);
            Session.Journal?.Write(new { t = GameRead.Now(), kind = "terminus", ev = "setback", before, after = s.terminusProgress, why });
            Notify($"Alien terminus construction disrupted", $"Strikes near the alien homeworld have set back the Hydra terminus project ({why}). Estimated completion: {s.terminusProgress:P0}.", false);
        }

        private static void Announce(StrategyState s)
        {
            while (s.terminusMilestones < Milestones.Length && s.terminusProgress >= Milestones[s.terminusMilestones])
            {
                float m = Milestones[s.terminusMilestones++];
                Session.Journal?.Write(new { t = GameRead.Now(), kind = "terminus", ev = "milestone", at = m });
                Notify($"Alien terminus {m:P0} complete",
                    $"Wormhole traffic at the alien homeworld near {GameRead.Body(Site())} keeps rising. The Hydras are building a full terminus on this side; if it is completed, they will be able to bring through whatever they want, and humanity will have lost. Estimated completion: {m:P0}. Strike the site to set it back.", false);
            }
        }

        private static void Complete(StrategyState s)
        {
            s.terminusComplete = true;
            s.history.Add($"{GameRead.Now()} terminus complete: alien victory");
            Plugin.Log.LogInfo("[Strategy] Terminus complete: alien victory.");
            Session.Journal?.Write(new { t = GameRead.Now(), kind = "terminus", ev = "complete" });
            // Notify journals only in unattended harness runs (soaks run on); playtests and normal play end the campaign.
            Notify("The alien terminus is complete",
                "The full wormhole terminus is open. The Hydras can now bring through fleets, colonists and armies without limit, and nothing humanity has can stop the flood. The war is lost.", true);
        }

        /// <summary>
        /// A player notification with literal text (no localization needed), through vanilla's private
        /// TINotificationQueueState.AddItem with the existing LogFactionWin template (always shown, cannot be suppressed).
        /// endGame: the popup offers only Close, which runs the credits and returns to the main menu (vanilla's
        /// campaign-over flow; aliens have no victory template, so LogFactionWin itself can't be used for them).
        /// </summary>
        private static void Notify(string headline, string text, bool endGame)
        {
            // Unattended harness runs: journal only (an unsuppressible popup could stall the clock).
            if (Harness.CampaignHarness.Active && !Harness.CampaignHarness.Interactive)
            {
                Session.Journal?.Write(new { t = GameRead.Now(), kind = "terminus-notice", headline, endGame });
                return;
            }
            try
            {
                var alien = GameStateManager.AlienFaction();
                var player = GameControl.control?.activePlayer;
                if (alien == null || player == null) return;
                var item = new NotificationQueueItem
                {
                    templateName = "LogFactionWin",
                    primaryFactions = new List<TIFactionState> { player },
                    relevantFactions = GameStateManager.AllHumanFactions().Where(h => h != null).ToList(),
                    icon = alien.factionIcon64path,
                    popupResource1 = alien.factionIcon256path,
                    popupResource2 = alien.factionIcon256path,
                    itemHeadline = headline,
                    itemSummary = text,
                    itemDetail = text,
                    triggerEndGame = endGame,
                };
                AccessTools.Method(typeof(TINotificationQueueState), "AddItem").Invoke(null, new object[] { item, true });
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[Terminus] notification: {e.Message}"); }
        }

        /// <summary>Harness: "ai.terminus &lt;progress 0..1&gt;" (starts it if needed).</summary>
        public static string Force(float progress)
        {
            if (AlienStrategy.Current == null) return "! no strategy state";
            var s = AlienStrategy.Current;
            if (s.terminusStarted == null) s.terminusStarted = GameRead.Now();
            s.terminusProgress = Mathf.Clamp01(progress);
            s.terminusMilestones = Milestones.Count(m => s.terminusProgress >= m);
            return $"> terminus {s.terminusProgress:P0}";
        }
    }

    /// <summary>Alien defeats in the terminus system set it back.</summary>
    [HarmonyPatch(typeof(TISpaceCombatState), nameof(TISpaceCombatState.EndCombatForStrategyGame))]
    internal static class TerminusSetbackFromCombat
    {
        private static void Postfix(TISpaceCombatState __instance)
        {
            if (!Terminus.On) return;
            try
            {
                var c = __instance;
                var site = Terminus.Site();
                if (!c.combatOccurs || site == null || c.factions == null || c.winner == null || c.winner.IsAlienFaction) return;
                if (!c.factions.Any(f => f != null && f.IsAlienFaction)) return;
                TIGameState where = (TIGameState)c.hab ?? c.fleets?.FirstOrDefault(x => x != null);
                if (where?.ref_system == null || where.ref_system != site.ref_system) return;
                Terminus.Setback(Plugin.Cfg.TerminusSetbackBattle.Value, $"alien defeat at {GameRead.Body(where)}");
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[Terminus] combat: {e.Message}"); }
        }
    }

    /// <summary>Alien habs lost in the terminus system set it back further.</summary>
    [HarmonyPatch(typeof(TIHabState), nameof(TIHabState.DestroyHab))]
    internal static class TerminusSetbackFromHabLoss
    {
        private static void Prefix(TIHabState __instance, TIFactionState destroyer)
        {
            if (!Terminus.On || __instance?.faction == null || !__instance.faction.IsAlienFaction || destroyer == null || destroyer.IsAlienFaction) return;
            var site = Terminus.Site();
            if (site == null || __instance.ref_system != site.ref_system) return;
            Terminus.Setback(Plugin.Cfg.TerminusSetbackHab.Value, $"{__instance.displayName} destroyed");
        }
    }
}
