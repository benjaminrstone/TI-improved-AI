using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;

namespace ImprovedAI.Diagnostics
{
    /// <summary>
    /// The gang-up web (docs/HUMAN-AI.md §9): every human-to-human hate change, by cause, summed per month. The
    /// victim's hate toward the actor is how the game records injuries (missions " targets faction", combat, habs), so
    /// this is both the probe for the vanilla gang-up rule and, later, the grudge source. One "human-hate" record per
    /// human faction per month: hate toward each other human faction, this month's change by cause bucket, war, the
    /// strongest faction and strength estimates. Runs on vanilla too.
    /// </summary>
    internal static class HateLedger
    {
        // (from, to) -> bucket -> sum this month
        private static readonly Dictionary<(TIFactionState, TIFactionState), Dictionary<string, float>> Month =
            new Dictionary<(TIFactionState, TIFactionState), Dictionary<string, float>>();

        public static string Bucket(string cause)
        {
            if (string.IsNullOrEmpty(cause)) return "other";
            if (cause.StartsWith("Jealousy")) return "jealousy";
            if (cause == "Periodic hate") return "periodic";
            if (cause.EndsWith("targets faction") || cause.StartsWith("Detected enemy councilor") || cause.StartsWith("My councilor")) return "missions";
            if (cause.Contains("Combat") || cause.Contains("hab") || cause.Contains("Hab") || cause.Contains("Facility") || cause.Contains("Fuel")) return "space";
            if (cause.Contains("Truce") || cause.Contains("Trade") || cause.Contains("Reprieve")) return "diplomacy";
            if (cause.Contains("war") || cause.Contains("War") || cause.Contains("Army")) return "war";
            return "other";
        }

        internal static void Record(TIFactionState from, TIFactionState to, string cause, float delta)
        {
            if (delta == 0f || from == null || to == null || from.IsAlienFaction || to.IsAlienFaction) return;
            string b = Bucket(cause);
            if (b == "missions" || b == "space" || b == "war") Human.Personality.OnInjury(from, to, delta);
            if (!Plugin.Cfg.DecisionJournal.Value) return;
            var key = (from, to);
            if (!Month.TryGetValue(key, out var d)) Month[key] = d = new Dictionary<string, float>();
            d[b] = (d.TryGetValue(b, out var v) ? v : 0f) + delta;
        }

        public static void Monthly()
        {
            var humans = GameStateManager.AllHumanFactions().Where(x => x != null).ToList();
            TIFactionState strongest = null;
            try { strongest = AIEvaluators.GetStrongestHumanFaction(); } catch { }
            var strength = humans.ToDictionary(x => x, x => GameRead.Safe(() => x.GetFactionStrengthEstimate()));
            foreach (var f in humans)
            {
                try
                {
                    var toward = humans.Where(o => o != f).ToDictionary(o => GameRead.Faction(o), o =>
                    {
                        Month.TryGetValue((f, o), out var d);
                        return new
                        {
                            hate = (float)Math.Round(GameRead.Safe(() => f.GetFactionHate(o)), 1),
                            war = Try(() => f.AI_AtWarWithFaction(o)),
                            gained = d?.ToDictionary(kv => kv.Key, kv => (float)Math.Round(kv.Value, 2)),
                            grudge = (float)Math.Round(Human.Personality.Grudge(f, o), 1),
                        };
                    });
                    Session.Journal?.Write(new
                    {
                        t = GameRead.Now(),
                        kind = "human-hate",
                        f = GameRead.Faction(f),
                        player = f.isActivePlayer,
                        self = f.selfAssessement.ToString(),
                        strength = (float)Math.Round(strength[f], 1),
                        strongest = GameRead.Faction(strongest),
                        mostThreatening = GameRead.Faction(Try(() => f.GetMostThreateningEnemyHumanFaction())),
                        lead = (float)Math.Round(Human.Personality.LeadRatio(f), 2),
                        patron = GameRead.Faction(Human.Personality.PatronOf(f)),
                        naps = humans.Where(o => o != f && Try(() => f.HasNAP(o))).Select(GameRead.Faction).ToList(),
                        toward,
                    });
                }
                catch (Exception e) { Plugin.Log.LogWarning($"[HateLedger] {f.templateName}: {e.Message}"); }
            }
            Month.Clear();
        }

        public static void Reset() => Month.Clear();

        private static T Try<T>(Func<T> f) { try { return f(); } catch { return default; } }
    }

    [HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.GainFactionHate))]
    internal static class HateLedgerPatch
    {
        private static void Prefix(TIFactionState __instance, TIFactionState enemyCouncil, out float __state) =>
            __state = enemyCouncil == null ? 0f : __instance.GetFactionHate(enemyCouncil);

        private static void Postfix(TIFactionState __instance, TIFactionState enemyCouncil, string cause, float __state)
        {
            if (enemyCouncil == null || (!Plugin.Cfg.DecisionJournal.Value && !Plugin.Cfg.UseHumanPersonalities)) return;
            try { HateLedger.Record(__instance, enemyCouncil, cause, __instance.GetFactionHate(enemyCouncil) - __state); }
            catch { }
        }
    }
}
