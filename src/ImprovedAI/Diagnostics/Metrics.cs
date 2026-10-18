using System;
using System.Collections.Generic;
using System.Linq;
using PavonisInteractive.TerraInvicta;

namespace ImprovedAI.Diagnostics
{
    /// <summary>One row per game month (and one at load): the curves that soaks compare across runs and against vanilla.</summary>
    internal static class Metrics
    {
        public static Dictionary<string, object> Snapshot()
        {
            var alien = GameStateManager.AlienFaction();
            var factions = new List<object>();
            foreach (var f in GameStateManager.AllFactions())
            {
                if (f == null) continue;
                try { factions.Add(FactionRow(f, alien)); }
                catch (Exception e) { Plugin.Log.LogWarning($"[Metrics] {f.templateName}: {e.Message}"); }
            }
            var goals = new Dictionary<string, int>();
            var bases = new List<string>();
            if (alien != null)
            {
                foreach (var list in alien.factionGoals.Values)
                    foreach (var g in list)
                    {
                        string k = g.GetGoalType().ToString();
                        goals[k] = goals.TryGetValue(k, out var n) ? n + 1 : 1;
                    }
                bases = alien.habs.Where(h => h.IsBase).Select(h => GameRead.Body(h)).OrderBy(x => x).ToList();
            }
            return new Dictionary<string, object>
            {
                ["t"] = GameRead.Now(),
                ["kind"] = "month",
                ["realtime"] = UnityEngine.Time.realtimeSinceStartup,
                ["factions"] = factions,
                ["alienGoals"] = goals,
                ["alienBases"] = bases,
            };
        }

        private static object FactionRow(TIFactionState f, TIFactionState alien)
        {
            var res = new Dictionary<string, float>();
            foreach (var kv in f.resources) res[kv.Key.ToString()] = (float)Math.Round(kv.Value, 1);
            var income = new Dictionary<string, float>();
            foreach (var r in AI.Economy.Basic.Concat(new[] { FactionResource.Exotics }))
                income[r.ToString()] = (float)Math.Round(GameRead.Safe(() => f.GetMonthlyIncome(r)), 1);
            var habs = f.habs;
            var ships = f.fleets.Sum(x => x.ships?.Count ?? 0);
            return new
            {
                f = GameRead.Faction(f),
                ai = f.player?.isAI,
                res,
                income,
                bases = habs.Count(h => h.IsBase),
                stations = habs.Count(h => h.IsStation),
                fleets = f.fleets.Count,
                ships,
                scv = (float)Math.Round(f.fleets.Sum(x => GameRead.Safe(() => x.SpaceCombatValue())), 1),
                hate = (alien == null || f == alien) ? (float?)null : GameRead.Safe(() => alien.GetFactionHate(f)),
                cps = f.controlPoints?.Count,
                orgs = GameRead.Safe(() => f.GetAllOrgs()?.Count ?? 0),
                councilors = f.councilors?.Count,
                self = f.IsAlienFaction ? null : f.selfAssessement.ToString(),
                scvBySystem = f.fleets.GroupBy(x => GameRead.System(x) ?? "?")
                    .ToDictionary(g => g.Key, g => (float)Math.Round(g.Sum(x => GameRead.Safe(() => x.SpaceCombatValue())), 1)),
                habsBySystem = habs.GroupBy(h => GameRead.System(h) ?? "?").ToDictionary(g => g.Key, g => g.Count()),
            };
        }

        public static void Write() => Session.Metrics?.Write(Snapshot());
    }
}
