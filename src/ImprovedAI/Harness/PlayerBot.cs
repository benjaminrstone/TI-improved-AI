using System;
using System.Collections.Generic;
using System.Linq;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;

namespace ImprovedAI.Harness
{
    /// <summary>
    /// A small rule-based player for probes: it reacts like a player would to a few situations, repeatably. Enabled per
    /// faction with ai.bot &lt;faction&gt; &lt;rules|off&gt;; it issues real orders through Orders (journaled as "order",
    /// decisions as "bot"). It sees true fleet strengths (perfect information).
    ///
    /// Rules:
    ///  - reinforce: every 2 days. When a hostile fleet (the aliens, or a faction at war with us) is flying into a system
    ///    where we hold habs and outguns our fleets there, send idle combat fleets from unthreatened systems that can
    ///    arrive before it, nearest first, until the defence reaches ReinforceRatio x the threat. If everything
    ///    reachable stays below OutmatchedRatio x the threat, send nothing ("outmatched" is journaled instead).
    ///  - merge: weekly. Idle combat fleets at the same place merge into the strongest there.
    ///  - hunt: daily, each idle combat fleet, strongest first, takes the war to the aliens. Docked at an alien
    ///    hab, it destroys it; with an alien station in its orbit that it outguns, it closes with it. Badly damaged or
    ///    below HomeDvShare of its delta-v, it flies to our nearest station and resupplies. Otherwise it attacks the
    ///    strongest alien station or parked fleet it outguns by HuntRatio and can reach within HuntMaxDays on half its
    ///    delta-v, keeping the rest for the way home (those within HuntNearDays first), unless a fleet of ours is
    ///    already on its way there. Alien bases on a surface are left alone.
    /// </summary>
    internal static class PlayerBot
    {
        private const float ReinforceRatio = 1.2f, OutmatchedRatio = 0.8f, ArrivalMarginDays = 3f;
        private const float HuntRatio = 1.25f, HuntNearDays = 30f, HuntMaxDays = 240f, HomeDvShare = 0.35f, HuntDvReserve = 0.5f;
        private static readonly string[] Known = { "reinforce", "merge", "hunt" };

        private static readonly Dictionary<string, HashSet<string>> Rules = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private static bool _hooked;

        public static string Set(TIFactionState f, string rules)
        {
            if (rules.Equals("off", StringComparison.OrdinalIgnoreCase)) { Rules.Remove(f.templateName); return $"> bot off for {f.templateName}"; }
            var set = new HashSet<string>(rules.Split(',').Select(r => r.Trim().ToLowerInvariant()).Where(r => r.Length > 0));
            var unknown = set.Except(Known).ToList();
            if (unknown.Count > 0) return "! unknown bot rules: " + string.Join(",", unknown);
            Rules[f.templateName] = set;
            if (!_hooked) { Observer.OnNewDay += Daily; _hooked = true; }
            Session.Journal?.Write(new { t = GameRead.Now(), kind = "bot", ev = "on", faction = f.templateName, rules = set.ToList() });
            return $"> bot on for {f.templateName}: {string.Join(",", set)}";
        }

        public static bool Hunting(TIFactionState f) => f != null && Rules.TryGetValue(f.templateName, out var set) && set.Contains("hunt");

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
                    if (kv.Value.Contains("hunt")) Hunt(f, now);
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

        private sealed class Prey { public TIGameState target; public bool station; public float defence; }

        /// <summary>Alien stations (with the fleets docked at them) and alien fleets parked in an orbit, one entry per orbit.</summary>
        private static List<Prey> AlienPrey()
        {
            var prey = new List<Prey>();
            foreach (var h in GameStateManager.IterateByClass<TIHabState>().Where(h => TIGameState.Valid(h) && h.IsStation && h.faction != null && h.faction.IsAlienFaction))
                prey.Add(new Prey { target = h, station = true, defence = GameRead.Safe(() => h.SpaceCombatValue()) + h.dockedFleets.Where(x => x.faction == h.faction).Sum(Scv) });
            var parked = GameStateManager.IterateByClass<TISpaceFleetState>()
                .Where(x => TIGameState.Valid(x) && x.faction != null && x.faction.IsAlienFaction && !x.transferAssigned && !x.dockedAtHab && !x.inCombatOrWaitingForCombat && x.ref_orbit != null);
            foreach (var g in parked.GroupBy(x => x.ref_orbit))
                prey.Add(new Prey { target = g.OrderByDescending(Scv).First(), defence = g.Sum(Scv) });
            return prey;
        }

        private static bool Yes(Func<bool> f) { try { return f(); } catch { return false; } }

        private static void Hunt(TIFactionState f, TIDateTime now)
        {
            var idle = f.fleets.Where(x => TIGameState.Valid(x) && Orders.Idle(x)).OrderByDescending(Scv).ToList();
            if (idle.Count == 0) return;
            var prey = AlienPrey();
            var claimed = new HashSet<TIGameState>();
            foreach (var d in f.fleets.Where(x => TIGameState.Valid(x) && x.transferAssigned && x.trajectory?.destination != null).Select(x => x.trajectory.destination))
            {
                claimed.Add(d);
                if (d.ref_fleet != null) claimed.Add(d.ref_fleet);
                if (d.ref_hab != null) claimed.Add(d.ref_hab);
            }
            if (now.day == 1)
            {
                var lead = idle[0];
                var leadPos = AI.Rendezvous.Pos(lead);
                Session.Journal?.Write(new
                {
                    t = GameRead.Now(), kind = "bot", ev = "hunt-scan", faction = f.templateName,
                    idle = idle.Take(6).Select(x => new { fleet = x.displayName, scv = Scv(x), at = GameRead.Body(x), dv = GameRead.Safe(() => x.currentDeltaV_kps) }).ToList(),
                    prey = prey.Count,
                    nearest = prey.Select(p => new { p, eta = AI.Rendezvous.Time(lead, leadPos, AI.Rendezvous.Pos(p.target)) }).OrderBy(x => x.eta).Take(8)
                        .Select(x => new { target = GameRead.Name(x.p.target), x.p.station, system = GameRead.System(x.p.target), x.p.defence, eta = (float)Math.Round(x.eta) }).ToList(),
                });
            }
            foreach (var fleet in idle)
            {
                float mine = Scv(fleet);
                bool docked = fleet.dockedAtHab;
                if (docked && fleet.ref_hab.faction != null && fleet.ref_hab.faction.IsAlienFaction)
                {
                    if (Orders.Do<DestroyHabOperation>(fleet, fleet.ref_hab, "bot:hunt")) continue;
                }
                else if (!docked && fleet.ref_orbit != null)
                {
                    var here = prey.FirstOrDefault(p => p.station && p.target.ref_orbit == fleet.ref_orbit && mine >= HuntRatio * p.defence);
                    if (here != null) { Orders.ApproachDock(fleet, (TIHabState)here.target, "bot:hunt"); continue; }
                }

                bool atHome = docked && fleet.ref_hab.faction == f;
                if (atHome && (Orders.Do<ResupplyAndRepairOperation>(fleet, fleet, "bot:home") || Orders.Do<ResupplyOperation>(fleet, fleet, "bot:home") || Orders.Do<RepairFleetOperation>(fleet, fleet, "bot:home")))
                    continue;
                var pos = AI.Rendezvous.Pos(fleet);
                bool worn = Yes(() => fleet.AI_NeedsRepairBadly()) || GameRead.Safe(() => fleet.currentDeltaV_kps) < HomeDvShare * GameRead.Safe(() => fleet.maxDeltaV_kps);
                if (worn)
                {
                    if (atHome) continue;
                    var home = f.habs.Where(h => TIGameState.Valid(h) && h.IsStation && Yes(() => h.AllowsResupply(f, false)))
                        .OrderBy(h => AI.Rendezvous.Time(fleet, pos, AI.Rendezvous.Pos(h))).FirstOrDefault();
                    if (home != null) Orders.Move(fleet, home, 0.05f, "bot:home");
                    continue;
                }

                var options = prey.Where(p => !claimed.Contains(p.target) && mine >= HuntRatio * p.defence)
                    .Select(p => new { p, eta = AI.Rendezvous.Time(fleet, pos, AI.Rendezvous.Pos(p.target)) })
                    .Where(x => x.eta <= HuntMaxDays)
                    .OrderBy(x => x.eta > HuntNearDays).ThenByDescending(x => x.p.defence).ThenBy(x => x.eta).Take(3).ToList();
                foreach (var o in options)
                {
                    if (!Orders.Move(fleet, o.p.target, HuntDvReserve, "bot:hunt").StartsWith(">")) continue;
                    claimed.Add(o.p.target);
                    Session.Journal?.Write(new { t = GameRead.Now(), kind = "bot", ev = "hunt", faction = f.templateName, fleet = fleet.displayName, scv = mine, target = GameRead.Name(o.p.target), o.p.station, system = GameRead.System(o.p.target), o.p.defence, eta = (float)Math.Round(o.eta) });
                    break;
                }
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

    /// <summary>
    /// The game's Autopilot answers every stance prompt with Defend (Autopilot.Update: StanceSubmit(2)), so a fight the
    /// player's fleet starts never happens unless the other side pursues. A hunting bot pursues in the fights it starts.
    /// </summary>
    [HarmonyLib.HarmonyPatch(typeof(PavonisInteractive.TerraInvicta.SpaceCombat.UI.PrecombatController), nameof(PavonisInteractive.TerraInvicta.SpaceCombat.UI.PrecombatController.StanceSubmit))]
    internal static class HunterPursues
    {
        private static void Prefix(PavonisInteractive.TerraInvicta.SpaceCombat.UI.PrecombatController __instance, ref int stanceValue)
        {
            if (!CampaignHarness.Active) return;
            var attacker = __instance.combat?.attackingFaction;
            if (attacker != null && attacker.isActivePlayer && PlayerBot.Hunting(attacker)) stanceValue = (int)CombatStance.Pursue;
        }
    }

    /// <summary>
    /// A pursuit against an evading fleet asks both sides for a delta-v bid (PromptSelectSpaceCombatBid). The Autopilot
    /// never answers it and the clock stays blocked. A hunting bot bids as the game's AI would.
    /// </summary>
    [HarmonyLib.HarmonyPatch(typeof(TIPromptQueueState), nameof(TIPromptQueueState.HandlePrompts))]
    internal static class HunterBids
    {
        private static readonly System.Reflection.FieldInfo ListF = HarmonyLib.AccessTools.Field(typeof(TIPromptQueueState), "factionList");
        private static readonly System.Reflection.MethodInfo BidM = HarmonyLib.AccessTools.Method(typeof(TIPromptQueueState), "HandleSelectSpaceCombatBid");

        private static void Postfix(TIPromptQueueState __instance)
        {
            if (!CampaignHarness.Active || BidM == null || !(ListF?.GetValue(__instance) is List<Prompt> prompts)) return;
            foreach (var p in prompts.ToList())
            {
                var f = p.actingState?.ref_faction;
                if (p.name != "PromptSelectSpaceCombatBid" || !PlayerBot.Hunting(f) || f.player.isAI || !(p.relatedGameState is TISpaceCombatState combat)) continue;
                BidM.Invoke(__instance, new object[] { f, combat });
            }
        }
    }
}
