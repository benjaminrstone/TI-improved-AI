using System;
using System.Collections.Generic;
using System.Linq;
using PavonisInteractive.TerraInvicta;

namespace ImprovedAI.Diagnostics
{
    /// <summary>
    /// One line per alien strategic decision, detected by diffing state once per game day:
    /// goals created / dropped / re-prioritised, alien fleet transfers (with the goal and the strength it was sized
    /// against), alien fleets formed / lost, and habs founded / lost by any faction.
    /// Diffing (rather than patching each decision site) keeps this independent of the AI code we'll be replacing.
    /// </summary>
    internal static class DecisionJournal
    {
        private class GoalSnap { public string type; public int imp; public bool inProgress; }
        private class FleetSnap { public string name; public int ships; public float scv; public int destId; public string where; public string ops; }
        private class HabSnap { public string faction, type, body; public int tier; }

        private static Dictionary<int, GoalSnap> _goals = new Dictionary<int, GoalSnap>();
        private static Dictionary<int, FleetSnap> _fleets = new Dictionary<int, FleetSnap>();
        private static Dictionary<int, HabSnap> _habs = new Dictionary<int, HabSnap>();

        public static void Init()
        {
            _goals.Clear(); _fleets.Clear(); _habs.Clear(); _day = 0;
            Daily(emit: false);
            Emit(new { kind = "init", goals = _goals.Count, alienFleets = _fleets.Count, habs = _habs.Count });
        }

        private static void Emit(object rec) => Session.Journal?.Write(rec);

        /// <summary>How a transfer is flown: launch and arrival dates, wait before launch, flight time, distance, delta-v.</summary>
        private static object Flight(Trajectory traj, TISpaceFleetState f)
        {
            try
            {
                return new
                {
                    launch = GameRead.Date(traj.launchTime), arrive = GameRead.Date(traj.arrivalTime),
                    waitDays = (float)Math.Round(traj.launchTime.DifferenceInDays(traj.assignedTime), 1),
                    flightDays = (float)Math.Round(traj.flightDuration_s / 86400.0, 1),
                    au = (float)Math.Round(traj.straightLineDistance_m / 149597870700.0, 2),
                    dvKps = (float)Math.Round(traj.DV_kps, 1),
                    fleetDvKps = (float)Math.Round(f.currentDeltaV_kps, 1),
                    accel = (float)Math.Round(traj.fleetCruiseAcceleration_mps2, 3),
                    intercept = traj.interceptTrajectory,
                    model = traj.GetType().Name,
                };
            }
            catch { return null; }
        }

        private static int _day;

        public static void Daily(bool emit = true)
        {
            var alien = GameStateManager.AlienFaction();
            if (alien == null) return;
            string t = GameRead.Now();
            if (emit && _day++ % 7 == 0)
                try { Perception(alien, t); } catch (Exception e) { Plugin.Log.LogWarning($"[Journal] perception: {e.Message}"); }
            try { DiffGoals(alien, t, emit); } catch (Exception e) { Plugin.Log.LogWarning($"[Journal] goals: {e.Message}"); }
            try { DiffFleets(alien, t, emit); } catch (Exception e) { Plugin.Log.LogWarning($"[Journal] fleets: {e.Message}"); }
            try { DiffHabs(t, emit); } catch (Exception e) { Plugin.Log.LogWarning($"[Journal] habs: {e.Message}"); }
        }

        /// <summary>Weekly: how strong the aliens believe the player's fleets are, by system, next to their true SCV.</summary>
        private static void Perception(TIFactionState alien, string t)
        {
            var player = GameControl.control.activePlayer;
            if (player == null) return;
            var bySystem = new Dictionary<string, float[]>();
            foreach (var f in player.fleets)
            {
                string sys = GameRead.System(f) ?? "?";
                if (!bySystem.TryGetValue(sys, out var v)) bySystem[sys] = v = new float[2];
                v[0] += GameRead.Safe(() => f.SpaceCombatValue());
                v[1] += GameRead.Safe(() => alien.GetPerceivedEnemyFleetStrength(f));
            }
            Emit(new { t, kind = "perceive", faction = GameRead.Faction(player), systems = bySystem.ToDictionary(kv => kv.Key, kv => new { scv = kv.Value[0], perceived = kv.Value[1] }) });
        }

        private static void DiffGoals(TIFactionState alien, string t, bool emit)
        {
            var seen = new HashSet<int>();
            foreach (var list in alien.factionGoals.Values)
                foreach (var g in list)
                {
                    int id = GameRead.Id(g);
                    if (id == 0 || !seen.Add(id)) continue;
                    bool inProg = false;
                    try { inProg = g.InProgress(); } catch { }
                    var snap = new GoalSnap { type = g.GetGoalType().ToString(), imp = g.importance, inProgress = inProg };
                    if (!_goals.TryGetValue(id, out var old))
                    {
                        if (emit) Emit(GoalRecord("goal+", t, id, g, snap));
                    }
                    else if (old.imp != snap.imp || old.inProgress != snap.inProgress)
                    {
                        if (emit) Emit(GoalRecord("goal~", t, id, g, snap, old));
                    }
                    _goals[id] = snap;
                }
            foreach (var id in _goals.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                if (emit) Emit(new { t, kind = "goal-", id, type = _goals[id].type, imp = _goals[id].imp });
                _goals.Remove(id);
            }
        }

        private static object GoalRecord(string kind, string t, int id, TIFactionGoalState g, GoalSnap s, GoalSnap old = null)
        {
            TIGameState target = null;
            try { target = g.target(); } catch { }
            var fg = g as FactionGoal_Fleet;
            return new
            {
                t, kind, id, type = s.type, imp = s.imp, prevImp = old?.imp, inProgress = s.inProgress,
                target = GameRead.Name(target), targetType = target?.GetType().Name, targetBody = GameRead.Body(target),
                targetSystem = GameRead.System(target), targetFaction = GameRead.Faction((target as TISpaceAssetState)?.faction),
                fleet = GameRead.Name(fg?.assignedFleet),
                fleetScv = fg?.assignedFleet == null ? (float?)null : GameRead.Safe(() => fg.assignedFleet.SpaceCombatValue()),
                desiredScv = fg == null ? (float?)null : GameRead.Safe(() => fg.desiredFleetCombatValue),
            };
        }

        private static void DiffFleets(TIFactionState alien, string t, bool emit)
        {
            var seen = new HashSet<int>();
            foreach (var f in alien.fleets.ToList())
            {
                int id = GameRead.Id(f);
                if (id == 0) continue;
                seen.Add(id);
                var traj = f.trajectory;
                int destId = traj == null ? 0 : GameRead.Id(traj.destination);
                var snap = new FleetSnap
                {
                    name = f.displayName, ships = f.ships?.Count ?? 0, scv = GameRead.Safe(() => f.SpaceCombatValue()),
                    destId = destId, where = GameRead.Body(f),
                    ops = f.currentOperations == null ? "" : string.Join(",", f.currentOperations.Select(o => o.operationDataName + ">" + GameRead.Name(o.target))),
                };

                _fleets.TryGetValue(id, out var prev);
                // What attack fleets actually do (bombard, destroy hab, assault...), whenever it changes.
                if (emit && prev != null && prev.ops != snap.ops && snap.ops.Length > 0 && f.AssignedGoal() is FactionGoal_AttackWithFleet ag)
                    Emit(new { t, kind = "op", id, fleet = snap.name, snap.ships, snap.scv, at = snap.where, ops = snap.ops, goalTarget = GameRead.Name(ag.target()) });
                if (!_fleets.TryGetValue(id, out var old))
                {
                    if (emit) Emit(new { t, kind = "fleet+", id, fleet = snap.name, snap.ships, snap.scv, at = snap.where });
                }
                if (destId != 0 && (old == null || old.destId != destId) && emit)
                {
                    var goal = FindGoalFor(alien, f);
                    TIGameState gt = null;
                    try { gt = goal?.target(); } catch { }
                    Emit(new
                    {
                        t, kind = "transfer", id, fleet = snap.name, snap.ships, snap.scv,
                        from = old?.where ?? snap.where, to = GameRead.Name(traj.destination), toBody = GameRead.Body(traj.destination), toSystem = GameRead.System(traj.destination),
                        goal = goal?.GetGoalType().ToString(), goalImp = goal?.importance, goalTarget = GameRead.Name(gt), targetFaction = GameRead.Faction(gt?.ref_faction),
                        desiredScv = goal == null ? (float?)null : GameRead.Safe(() => goal.desiredFleetCombatValue),
                        // What the alien expects to meet at the goal target (remembered defenders + half the system), for launch-ratio analysis.
                        defThreat = (gt == null || gt.ref_faction == null || gt.ref_faction.IsAlienFaction || gt.ref_system == null) ? (float?)null
                            : GameRead.Safe(() => AI.DecisiveForce.DefenderThreat(alien, gt)),
                        // How the move is flown (rendezvous v2 diagnosis, 2026-10-21): joins to the Jupiter muster took 4-10 months.
                        flight = Flight(traj, f),
                    });
                }
                _fleets[id] = snap;
            }
            foreach (var id in _fleets.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                var o = _fleets[id];
                if (emit) Emit(new { t, kind = "fleet-", id, fleet = o.name, o.ships, o.scv, at = o.where });
                _fleets.Remove(id);
            }
        }

        private static FactionGoal_Fleet FindGoalFor(TIFactionState alien, TISpaceFleetState fleet)
        {
            foreach (var list in alien.factionGoals.Values)
                foreach (var g in list)
                    if (g is FactionGoal_Fleet fg && fg.assignedFleet == fleet) return fg;
            return null;
        }

        /// <summary>Monthly basic-resource production of a base's site (what "mine what you lack" chose), or null.</summary>
        private static Dictionary<string, float> SiteYields(TIHabState h)
        {
            try
            {
                var site = h.habSite;
                if (site == null) return null;
                return AI.Economy.Basic.ToDictionary(r => r.ToString(), r => (float)Math.Round(site.GetMonthlyProduction(r), 1));
            }
            catch { return null; }
        }

        private static void DiffHabs(string t, bool emit)
        {
            var seen = new HashSet<int>();
            foreach (var fac in GameStateManager.AllFactions())
            {
                if (fac == null) continue;
                foreach (var h in fac.habs)
                {
                    int id = GameRead.Id(h);
                    if (id == 0) continue;
                    seen.Add(id);
                    var snap = new HabSnap { faction = GameRead.Faction(fac), type = h.habType.ToString(), tier = h.tier, body = GameRead.Body(h) };
                    if (!_habs.ContainsKey(id) && emit)
                        Emit(new { t, kind = "hab+", id, faction = snap.faction, hab = h.displayName, habType = snap.type, snap.tier, snap.body, system = GameRead.System(h), yields = SiteYields(h) });
                    _habs[id] = snap;
                }
            }
            foreach (var id in _habs.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                var o = _habs[id];
                if (emit) Emit(new { t, kind = "hab-", id, o.faction, habType = o.type, o.tier, o.body });
                _habs.Remove(id);
            }
        }
    }
}
