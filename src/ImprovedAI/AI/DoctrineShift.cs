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
    /// Doctrinal inertia (DESIGN P6 "Shame drives doctrinal inertia"; M2 step 2 leftover). Failure is shameful to a Hydra,
    /// so the command persists with a failing strategic focus longer than is optimal, then abandons it outright.
    ///  - A ledger counts each month's exchange under the current doctrine: alien ships lost vs human ships destroyed,
    ///    alien habs lost vs human habs destroyed (a hab counts HabWeight ships).
    ///  - Monthly, over the last ShameWindowMonths: loss share = lost / (lost + destroyed). When there was enough
    ///    fighting and the share is above ShameTolerance, shame grows by (share - tolerance) x ShameRate; otherwise it
    ///    decays by ShameDecay. A command losing 80% of its exchanges shifts in about 4 months; one losing 60%, in years.
    ///  - At ShameThreshold the focus is abandoned for another from the difficulty's pool, preferring the opposite
    ///    temperament (aggressive <-> defensive). Shame and the ledger reset. Fleet axes and posture are kept.
    /// One will, no factions: a shift is one command revising its course (user, 2026-10-11).
    /// </summary>
    internal static class DoctrineShift
    {
        public static bool On => Plugin.Cfg.UseDoctrineShift && AlienStrategy.Current != null;

        private static readonly HashSet<StrategicFocus> Aggressive = new HashSet<StrategicFocus>
        {
            StrategicFocus.Raider, StrategicFocus.Strangler, StrategicFocus.Blitz, StrategicFocus.HunterKiller, StrategicFocus.EarthPresence, StrategicFocus.LandGrab,
        };

        public static bool IsAggressive(StrategicFocus f) => Aggressive.Contains(f);

        private static LedgerMonth Current()
        {
            var s = AlienStrategy.Current;
            string month = GameRead.Now()?.Substring(0, 7) ?? "?";
            var last = s.ledger.LastOrDefault();
            if (last == null || last.month != month) { last = new LedgerMonth { month = month }; s.ledger.Add(last); }
            return last;
        }

        public static void Record(int alienShipsLost, int humanShipsKilled, int alienHabsLost, int humanHabsKilled)
        {
            if (!On) return;
            var m = Current();
            m.alienShipsLost += alienShipsLost;
            m.humanShipsKilled += humanShipsKilled;
            m.alienHabsLost += alienHabsLost;
            m.humanHabsKilled += humanHabsKilled;
        }

        /// <summary>Loss share over the window, or null when there was too little fighting to judge.</summary>
        public static float? LossShare(out float lost, out float destroyed)
        {
            var s = AlienStrategy.Current;
            int window = Math.Max(1, Plugin.Cfg.ShameWindowMonths.Value);
            float hab = Plugin.Cfg.ShameHabWeight.Value;
            var rows = s.ledger.Skip(Math.Max(0, s.ledger.Count - window)).ToList();
            lost = rows.Sum(r => r.alienShipsLost + hab * r.alienHabsLost);
            destroyed = rows.Sum(r => r.humanShipsKilled + hab * r.humanHabsKilled);
            if (lost + destroyed < Plugin.Cfg.ShameMinActivity.Value) return null;
            return lost / (lost + destroyed);
        }

        /// <summary>Monthly, after the month's battles: judge the doctrine and shift it past the threshold.</summary>
        public static void Monthly()
        {
            if (!On) return;
            var s = AlienStrategy.Current;
            float before = s.shame;
            float? share = LossShare(out float lost, out float destroyed);
            float tol = Plugin.Cfg.ShameTolerance.Value;
            if (share.HasValue && share.Value > tol) s.shame += (share.Value - tol) * Plugin.Cfg.ShameRate.Value;
            else s.shame = Mathf.Max(0f, s.shame - Plugin.Cfg.ShameDecay.Value);
            int window = Math.Max(1, Plugin.Cfg.ShameWindowMonths.Value);
            if (s.ledger.Count > window * 2) s.ledger.RemoveRange(0, s.ledger.Count - window * 2);
            Session.Journal?.Write(new { t = GameRead.Now(), kind = "shame", focus = s.doctrine.focus.ToString(), before, after = s.shame, lossShare = share, lost, destroyed });
            Current(); // open the new month's row
            if (s.shame >= Plugin.Cfg.ShameThreshold.Value) Shift($"shame {s.shame:F2}: lost {lost:F0} vs destroyed {destroyed:F0} over {window} months");
        }

        /// <summary>Abandon the strategic focus for another, preferring the opposite temperament. Deterministic per campaign.</summary>
        public static void Shift(string why)
        {
            var s = AlienStrategy.Current;
            var from = s.doctrine.focus;
            var pool = AlienStrategy.Pool(Dials.Difficulty).Where(f => f != from).ToList();
            if (pool.Count == 0) return;
            var rng = new System.Random(StableHash(s.campaignKey + "|shift|" + s.shifts));
            float W(StrategicFocus f) => IsAggressive(f) != IsAggressive(from) ? Plugin.Cfg.ShiftOppositeWeight.Value : 1f;
            double r = rng.NextDouble() * pool.Sum(W);
            var to = pool.Last();
            foreach (var f in pool) { r -= W(f); if (r <= 0) { to = f; break; } }

            s.doctrine.focus = to;
            s.shifts++;
            s.shame = 0f;
            s.ledger.Clear();
            s.history.Add($"{GameRead.Now()} shifted {from} -> {to} ({why})");
            Plugin.Log.LogInfo($"[Strategy] Doctrine shifted {from} -> {to}: {why}");
            Session.Journal?.Write(new { t = GameRead.Now(), kind = "doctrine", ev = "shifted", from = from.ToString(), to = to.ToString(), why, doctrine = s.doctrine, dials = Dials.Snapshot() });
            FleetDoctrine.RequestRedesign("doctrine shifted");
        }

        private static int StableHash(string s)
        {
            unchecked
            {
                int h = (int)2166136261;
                foreach (char c in s ?? "") h = (h ^ c) * 16777619;
                return h;
            }
        }
    }

    /// <summary>Battles fill the ledger (ships destroyed on each side, in alien-vs-human fights).</summary>
    [HarmonyPatch(typeof(TISpaceCombatState), nameof(TISpaceCombatState.EndCombatForStrategyGame))]
    internal static class DoctrineLedgerFromCombat
    {
        private static void Postfix(TISpaceCombatState __instance)
        {
            if (!DoctrineShift.On) return;
            try
            {
                var c = __instance;
                if (!c.combatOccurs || c.factions == null || c.combatRecord.singleAssetRecords == null) return;
                if (!c.factions.Any(f => f != null && f.IsAlienFaction) || !c.factions.Any(f => f != null && !f.IsAlienFaction)) return;
                var dead = c.combatRecord.singleAssetRecords.Where(r => r.outcome == SingleAssetCombatOutcome.Destroyed && r.faction != null).ToList();
                // Only ships here: habs are counted when they are destroyed (DestroyHab), whoever does it.
                int alienLost = dead.Count(r => r.faction.IsAlienFaction && r.asset is TISpaceShipState);
                int humanLost = dead.Count(r => !r.faction.IsAlienFaction && r.asset is TISpaceShipState);
                DoctrineShift.Record(alienLost, humanLost, 0, 0);
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[DoctrineShift] combat: {e.Message}"); }
        }
    }

    /// <summary>Habs lost or destroyed (alien captures destroy human habs).</summary>
    [HarmonyPatch(typeof(TIHabState), nameof(TIHabState.DestroyHab))]
    internal static class DoctrineLedgerFromHabs
    {
        private static void Prefix(TIHabState __instance, TIFactionState destroyer)
        {
            if (!DoctrineShift.On || __instance?.faction == null || destroyer == null) return;
            if (__instance.faction.IsAlienFaction && !destroyer.IsAlienFaction) DoctrineShift.Record(0, 0, 1, 0);
            else if (!__instance.faction.IsAlienFaction && destroyer.IsAlienFaction) DoctrineShift.Record(0, 0, 0, 1);
        }
    }
}
