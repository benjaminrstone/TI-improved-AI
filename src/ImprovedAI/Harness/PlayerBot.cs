using System;
using System.Collections.Generic;
using System.Linq;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;

namespace ImprovedAI.Harness
{
    /// <summary>
    /// A small rule-based player for probes (user, 2026-10-22, item 3): it reacts like a player would to a few
    /// situations, repeatably, so we can test how the aliens handle specific player behaviour. Enabled per faction with
    /// ai.bot &lt;faction&gt; &lt;rules|off&gt;; it issues real orders through Orders (journaled as "order", decisions as "bot").
    /// It sees true fleet strengths (perfect information): a v1 simplification, noted in docs/TESTING.md.
    ///
    /// Rules:
    ///  - reinforce: every 2 days. When a hostile fleet (the aliens, or a faction at war with us) is flying into a system
    ///    where we hold habs and outguns our fleets there, send idle combat fleets from unthreatened systems that can
    ///    arrive before it (travel estimate as for the alien rendezvous), nearest first, until the defence reaches
    ///    ReinforceRatio x the threat. If even everything reachable stays below OutmatchedRatio x the threat, send
    ///    nothing: a player doesn't feed fleets in piecemeal ("outmatched" is journaled instead).
    ///  - merge: weekly. Idle combat fleets at the same place merge into the strongest there.
    /// </summary>
    internal static class PlayerBot
    {
        private const float ReinforceRatio = 1.2f, OutmatchedRatio = 0.8f, ArrivalMarginDays = 3f;

        private static readonly Dictionary<string, HashSet<string>> Rules = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private static bool _hooked;

        public static string Set(TIFactionState f, string rules)
        {
            if (rules.Equals("off", StringComparison.OrdinalIgnoreCase)) { Rules.Remove(f.templateName); return $"> bot off for {f.templateName}"; }
            var set = new HashSet<string>(rules.Split(',').Select(r => r.Trim().ToLowerInvariant()).Where(r => r.Length > 0));
            var unknown = set.Where(r => r != "reinforce" && r != "merge").ToList();
            if (unknown.Count > 0) return "! unknown bot rules: " + string.Join(",", unknown);
            Rules[f.templateName] = set;
            if (!_hooked) { Observer.OnNewDay += Daily; _hooked = true; }
            Session.Journal?.Write(new { t = GameRead.Now(), kind = "bot", ev = "on", faction = f.templateName, rules = set.ToList() });
            return $"> bot on for {f.templateName}: {string.Join(",", set)}";
        }

        private static void Daily(TIDateTime now)
        {
            foreach (var kv in Rules.ToList())
            {
                var f = GameStateManager.AllFactions().FirstOrDefault(x => x.templateName == kv.Key);
                if (f == null || f.defeated) continue;
                try
                {
                    if (kv.Value.Contains("reinforce") && now.day % 2 == 0) Reinforce(f, now);
                    if (kv.Value.Contains("merge") && now.day % 7 == 0) MergeIdle(f);
                }
                catch (Exception e) { Plugin.Log.LogWarning($"[PlayerBot] {f.templateName}: {e.Message}"); }
            }
        }

        private static bool Hostile(TIFactionState us, TIFactionState them)
        {
            if (them == null || them == us) return false;
            if (them.IsAlienFaction) return !us.IsAlienProxy;
            try { return them.AI_AtWarWithFaction(us) || us.AI_AtWarWithFaction(them); } catch { return false; }
        }

        private static float Scv(TISpaceFleetState x) => GameRead.Safe(() => x.SpaceCombatValue());

        private static readonly HashSet<string> Answered = new HashSet<string>();

        private static void Reinforce(TIFactionState f, TIDateTime now)
        {
            var held = f.habs.Where(h => TIGameState.Valid(h)).GroupBy(h => GameRead.System(h)).Where(g => g.Key != null).ToDictionary(g => g.Key, g => g.ToList());
            if (held.Count == 0) return;
            // Hostile fleets in flight to a system we hold, with days to arrival.
            var inbound = GameStateManager.IterateByClass<TISpaceFleetState>()
                .Where(x => TIGameState.Valid(x) && x.transferAssigned && x.trajectory?.destination != null && Hostile(f, x.faction))
                .Select(x => (fleet: x, system: GameRead.System(x.trajectory.destination), days: (float)x.trajectory.arrivalTime.DifferenceInDays(now)))
                .Where(x => x.system != null && held.ContainsKey(x.system) && x.days > 0f)
                .ToList();
            var threatened = new HashSet<string>(inbound.Select(x => x.system));
            foreach (var g in inbound.GroupBy(x => x.system))
            {
                string system = g.Key;
                float threat = g.Sum(x => Scv(x.fleet));
                float firstArrival = g.Min(x => x.days);
                string key = system + "|" + string.Join(",", g.Select(x => x.fleet.displayName).OrderBy(n => n));
                if (Answered.Contains(key)) continue;
                var ours = f.fleets.Where(x => TIGameState.Valid(x) && (GameRead.System(x) == system && !x.transferAssigned
                                                || x.transferAssigned && x.trajectory?.destination != null && GameRead.System(x.trajectory.destination) == system)).ToList();
                float defence = ours.Sum(Scv);
                if (defence >= threat) continue;

                // Where to defend: our strongest hab there (a station itself, or the orbit of a base's body).
                var hab = held[system].OrderByDescending(h => h.IsStation).ThenByDescending(h => h.tier).First();
                TIGameState dest = hab.IsStation ? (TIGameState)hab : hab.ref_naturalSpaceObject?.orbits?.OrderBy(o => o.semiMajorAxis_km).FirstOrDefault();
                if (dest == null) continue;
                var destPos = AI.Rendezvous.Pos(dest);
                var candidates = f.fleets.Where(x => TIGameState.Valid(x) && Orders.Idle(x) && !threatened.Contains(GameRead.System(x)))
                    .Select(x => (fleet: x, eta: AI.Rendezvous.Time(x, AI.Rendezvous.Pos(x), destPos)))
                    .Where(x => x.eta <= firstArrival - ArrivalMarginDays)
                    .OrderBy(x => x.eta).ToList();
                float reachable = defence + candidates.Sum(x => Scv(x.fleet));
                if (reachable < OutmatchedRatio * threat)
                {
                    Answered.Add(key);
                    Session.Journal?.Write(new { t = GameRead.Now(), kind = "bot", ev = "outmatched", faction = f.templateName, system, threat, defence, reachable, firstArrival = (float)Math.Round(firstArrival) });
                    continue;
                }
                var sent = new List<object>();
                foreach (var (fleet, eta) in candidates)
                {
                    if (defence >= ReinforceRatio * threat) break;
                    string r = Orders.Move(fleet, dest, 0.2f, "bot:reinforce " + system);
                    if (!r.StartsWith(">")) continue;
                    defence += Scv(fleet);
                    sent.Add(new { fleet = fleet.displayName, scv = Scv(fleet), eta = (float)Math.Round(eta) });
                }
                Answered.Add(key);
                Session.Journal?.Write(new { t = GameRead.Now(), kind = "bot", ev = "reinforce", faction = f.templateName, system, threat, defence, firstArrival = (float)Math.Round(firstArrival), sent });
            }
        }

        private static void MergeIdle(TIFactionState f)
        {
            foreach (var g in f.fleets.Where(Orders.Idle).GroupBy(x => (TIGameState)x.dockedLocation ?? x.ref_orbit).Where(g => g.Key != null && g.Count() > 1))
            {
                var into = g.OrderByDescending(Scv).First();
                foreach (var x in g.Where(x => x != into)) Orders.MergeInto(x, into, "bot:merge");
            }
        }
    }
}
