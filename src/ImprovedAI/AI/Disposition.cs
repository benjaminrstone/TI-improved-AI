using System;
using System.Linq;
using HarmonyLib;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;
using UnityEngine;

namespace ImprovedAI.AI
{
    /// <summary>
    /// The alien attitude toward each human faction: hate (vanilla factionHate, unchanged) x CONTEMPT (new; DESIGN
    /// "Disposition"). Contempt is how little the Hydras think that faction can hurt them. It starts high and falls when
    /// the faction beats alien expectations; it drives how carelessly they size attacks against that faction.
    /// Stored in AlienStrategy.Current.contempt (persisted in the save sidecar).
    /// </summary>
    internal static class Disposition
    {
        public static bool On => Plugin.Cfg.UseDisposition && AlienStrategy.Current != null;

        public static float Contempt(TIFactionState human)
        {
            if (!On || human == null) return Plugin.Cfg.StartingContempt.Value;
            var map = AlienStrategy.Current.contempt;
            string k = GameRead.Faction(human);
            if (!map.TryGetValue(k, out var v)) map[k] = v = Plugin.Cfg.StartingContempt.Value;
            return v;
        }

        public static void Shift(TIFactionState human, float delta, string why)
        {
            if (!On || human == null || human.IsAlienFaction || Math.Abs(delta) < 1e-4f) return;
            float before = Contempt(human);
            float after = Mathf.Clamp01(before + delta);
            AlienStrategy.Current.contempt[GameRead.Faction(human)] = after;
            if (Math.Abs(after - before) >= 0.02f)
                Session.Journal?.Write(new { t = GameRead.Now(), kind = "contempt", faction = GameRead.Faction(human), before, after, why });
        }

        /// <summary>Attack superiority multiplier against this faction: careless when contemptuous, decisive when respectful.</summary>
        public static float SuperiorityFactor(TIFactionState human) => On ? 1.25f - 0.5f * Contempt(human) : 1f;

        /// <summary>Monthly: presence at or beyond Jupiter erodes contempt; record the disposition of every human faction.</summary>
        public static void Monthly()
        {
            if (!On) return;
            var alien = GameStateManager.AlienFaction();
            var rows = new System.Collections.Generic.List<object>();
            foreach (var h in GameStateManager.AllHumanFactions())
            {
                if (h == null) continue;
                bool outer = h.habs.Any(x => x.ref_system != null && x.ref_system.semiMajorAxis_AU >= GameStateManager.Jupiter().semiMajorAxis_AU * 0.95);
                if (outer) Shift(h, -0.02f, "holds habs at Jupiter or beyond");
                rows.Add(new { faction = GameRead.Faction(h), contempt = Contempt(h), hate = GameRead.Safe(() => alien.GetFactionHate(h)) });
            }
            Session.Journal?.Write(new { t = GameRead.Now(), kind = "disposition", factions = rows });
        }
    }

    /// <summary>Battles teach contempt or respect.</summary>
    [HarmonyPatch(typeof(TISpaceCombatState), nameof(TISpaceCombatState.EndCombatForStrategyGame))]
    internal static class DispositionFromCombat
    {
        private static void Postfix(TISpaceCombatState __instance)
        {
            if (!Disposition.On) return;
            try
            {
                var c = __instance;
                if (!c.combatOccurs || c.factions == null) return;
                var alien = c.factions.FirstOrDefault(f => f != null && f.IsAlienFaction);
                var human = c.factions.FirstOrDefault(f => f != null && !f.IsAlienFaction);
                if (alien == null || human == null) return;
                var recs = c.combatRecord.singleAssetRecords;
                int alienAssets = recs?.Count(r => r.faction == alien) ?? 0;
                int alienDestroyed = recs?.Count(r => r.faction == alien && r.outcome == SingleAssetCombatOutcome.Destroyed) ?? 0;
                bool alienAttacked = c.attackingFaction == alien;
                if (c.winner == alien && alienDestroyed == 0) { Disposition.Shift(human, +0.01f, "clean alien victory"); return; }
                float delta = -0.01f * alienDestroyed;
                if (alienAttacked && c.winner != alien) delta -= 0.08f;
                Disposition.Shift(human, delta, $"battle at {GameRead.Body((TIGameState)c.hab ?? c.fleets?.FirstOrDefault(x => x != null))}: aliens lost {alienDestroyed}/{alienAssets}" + (alienAttacked && c.winner != alien ? ", attack failed" : ""));
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[Disposition] combat: {e.Message}"); }
        }
    }

    /// <summary>Losing a base or station to a faction is the strongest lesson.</summary>
    [HarmonyPatch(typeof(TIHabState), nameof(TIHabState.DestroyHab))]
    internal static class DispositionFromHabLoss
    {
        private static void Prefix(TIHabState __instance, TIFactionState destroyer)
        {
            if (!Disposition.On || __instance?.faction == null || !__instance.faction.IsAlienFaction || destroyer == null || destroyer.IsAlienFaction) return;
            Disposition.Shift(destroyer, -0.1f * Math.Max(1, __instance.tier), $"destroyed alien {__instance.habType} at {GameRead.Body(__instance)}");
        }
    }
}
