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
    /// P4, a late-game arc (DESIGN "A late-game arc"): real losses move the aliens from managing a nuisance to fighting a
    /// war they could lose. Monthly, the command judges its DESPERATION (0..1) from:
    ///  - threat: human habs in the gas-giant systems (Jupiter, Saturn, Uranus, Neptune), the alien heartland;
    ///  - lost systems: gas-giant systems humans hold where the aliens hold nothing;
    ///  - respect: how far the lowest contempt for any human faction has fallen below RespectContempt;
    ///  - losses: alien habs lost to humans in the last 12 months.
    /// At EscalateAt it ESCALATES (latched; calms only after CalmMonths below CalmAt):
    ///  - total war becomes possible at once (vanilla waits for yearsBeforeAlienTotalWarAllowed, 20 progression-years on
    ///    Normal), against factions the aliens already hate enough (vanilla's own hate threshold still applies);
    ///  - loss aversion relaxes: lower attack superiority and arrival fight ratio, reserves committed (Dials);
    ///  - more raids and captures, raids aimed harder at hated factions;
    ///  - the drip goes to war: the sustainable base cap commits a smaller share of it to expansion.
    /// Justified, not rubber-banding: every effect spends what the aliens really have. A player who has raided their
    /// economy meets a weaker surge.
    /// </summary>
    internal static class Escalation
    {
        public static bool On => Plugin.Cfg.UseEscalation && AlienStrategy.Current != null;
        public static bool Escalated => On && AlienStrategy.Current.escalated;

        private static readonly string[] GasGiants = { "Jupiter", "Saturn", "Uranus", "Neptune" };

        public static void RecordHabLoss()
        {
            if (!On) return;
            var s = AlienStrategy.Current;
            s.habLosses.Add(GameRead.Now());
        }

        public struct Reading { public float threat, lost, respect, losses, desperation; public int humanHabs, lostSystems, recentLosses; public float minContempt; }

        public static Reading Judge()
        {
            var s = AlienStrategy.Current;
            var r = new Reading();
            var alien = GameStateManager.AlienFaction();
            var humans = GameStateManager.AllHumanFactions().Where(h => h != null).ToList();
            foreach (var sys in GasGiants)
            {
                int human = humans.Sum(h => h.habs.Count(x => GameRead.System(x) == sys));
                int ours = alien?.habs.Count(x => GameRead.System(x) == sys) ?? 0;
                r.humanHabs += human;
                if (human > 0 && ours == 0) r.lostSystems++;
            }
            r.threat = Mathf.Clamp01(r.humanHabs / Mathf.Max(1f, Plugin.Cfg.EscalationThreatHabs.Value));
            r.lost = Mathf.Clamp01(r.lostSystems);
            r.minContempt = humans.Count == 0 ? 1f : humans.Min(h => Disposition.Contempt(h));
            float rc = Mathf.Max(0.01f, Plugin.Cfg.EscalationRespectContempt.Value);
            r.respect = Mathf.Clamp01((rc - r.minContempt) / rc);
            string cutoff = YearAgo();
            s.habLosses.RemoveAll(d => string.CompareOrdinal(d, cutoff) < 0);
            r.recentLosses = s.habLosses.Count;
            r.losses = Mathf.Clamp01(r.recentLosses / Mathf.Max(1f, Plugin.Cfg.EscalationLossHabs.Value));
            r.desperation = Mathf.Clamp01(0.3f * r.threat + 0.3f * r.lost + 0.2f * r.respect + 0.2f * r.losses);
            return r;
        }

        private static string YearAgo()
        {
            try { var n = TITimeState.Now(); return $"{n.year - 1:D4}-{n.month:D2}-{n.day:D2}"; } catch { return ""; }
        }

        public static void Monthly()
        {
            if (!On) return;
            var s = AlienStrategy.Current;
            var r = Judge();
            s.desperation = r.desperation;
            bool was = s.escalated;
            if (!s.escalated && r.desperation >= Plugin.Cfg.EscalationEscalateAt.Value) Set(true, r);
            else if (s.escalated)
            {
                s.calmMonths = r.desperation < Plugin.Cfg.EscalationCalmAt.Value ? s.calmMonths + 1 : 0;
                if (s.calmMonths >= Plugin.Cfg.EscalationCalmMonths.Value) Set(false, r);
            }
            List<string> totalWar = null;
            try { totalWar = GameStateManager.AlienFaction()?.enemyTotalWarFactions.Select(GameRead.Faction).ToList(); } catch { }
            Session.Journal?.Write(new { t = GameRead.Now(), kind = "escalation", r.desperation, r.threat, r.lost, r.respect, r.losses, r.humanHabs, r.lostSystems, r.recentLosses, r.minContempt, escalated = s.escalated, changed = was != s.escalated, totalWar });
        }

        public static void Set(bool escalate, Reading r, string why = null)
        {
            var s = AlienStrategy.Current;
            s.escalated = escalate;
            s.calmMonths = 0;
            if (escalate) s.escalatedSince = GameRead.Now();
            why = why ?? $"desperation {r.desperation:F2} (threat {r.threat:F2}, lost systems {r.lostSystems}, respect {r.respect:F2}, hab losses {r.recentLosses})";
            s.history.Add($"{GameRead.Now()} {(escalate ? "escalated" : "calmed")}: {why}");
            Plugin.Log.LogInfo($"[Strategy] {(escalate ? "ESCALATED to total war" : "Calmed")}: {why}");
            Session.Journal?.Write(new { t = GameRead.Now(), kind = "posture", ev = escalate ? "escalated" : "calmed", why, dials = Dials.Snapshot() });
        }

        /// <summary>Harness: "ai.escalate on|off".</summary>
        public static string Force(bool escalate)
        {
            if (AlienStrategy.Current == null) return "! no strategy state";
            Set(escalate, Judge(), "forced");
            return "> escalated " + escalate;
        }
    }

    /// <summary>Total war at once when escalated (vanilla: only after yearsBeforeAlienTotalWarAllowed).</summary>
    [HarmonyPatch(typeof(TIGlobalConfig), nameof(TIGlobalConfig.IsAlienTotalWarPossible))]
    internal static class EscalationTotalWar
    {
        private static void Postfix(ref bool __result)
        {
            if (!__result && Escalation.Escalated) __result = true;
        }
    }

    /// <summary>Alien habs lost to humans feed desperation.</summary>
    [HarmonyPatch(typeof(TIHabState), nameof(TIHabState.DestroyHab))]
    internal static class EscalationFromHabLoss
    {
        private static void Prefix(TIHabState __instance, TIFactionState destroyer)
        {
            if (__instance?.faction != null && __instance.faction.IsAlienFaction && destroyer != null && !destroyer.IsAlienFaction)
                Escalation.RecordHabLoss();
        }
    }
}
