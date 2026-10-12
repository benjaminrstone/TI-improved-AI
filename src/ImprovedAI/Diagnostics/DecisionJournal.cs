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
        private class FleetSnap { public string name; public int ships; public float scv; public int destId; public string where; }
        private class HabSnap { public string faction, type, body; public int tier; }

        private static Dictionary<int, GoalSnap> _goals = new Dictionary<int, GoalSnap>();
        private static Dictionary<int, FleetSnap> _fleets = new Dictionary<int, FleetSnap>();
        private static Dictionary<int, HabSnap> _habs = new Dictionary<int, HabSnap>();

        public static void Init()
        {
            _goals.Clear(); _fleets.Clear(); _habs.Clear();
            Daily(emit: false);
            Emit(new { kind = "init", goals = _goals.Count, alienFleets = _fleets.Count, habs = _habs.Count });
        }

        private static void Emit(object rec) => Session.Journal?.Write(rec);

        public static void Daily(bool emit = true)
        {
            var alien = GameStateManager.AlienFaction();
            if (alien == null) return;
            string t = GameRead.Now();
            try { DiffGoals(alien, t, emit); } catch (Exception e) { Plugin.Log.LogWarning($"[Journal] goals: {e.Message}"); }
            try { DiffFleets(alien, t, emit); } catch (Exception e) { Plugin.Log.LogWarning($"[Journal] fleets: {e.Message}"); }
            try { DiffHabs(t, emit); } catch (Exception e) { Plugin.Log.LogWarning($"[Journal] habs: {e.Message}"); }
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
                };
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
                        from = old?.where ?? snap.where, to = GameRead.Name(traj.destination), toBody = GameRead.Body(traj.destination),
                        goal = goal?.GetGoalType().ToString(), goalImp = goal?.importance, goalTarget = GameRead.Name(gt),
                        desiredScv = goal == null ? (float?)null : GameRead.Safe(() => goal.desiredFleetCombatValue),
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
                        Emit(new { t, kind = "hab+", id, faction = snap.faction, hab = h.displayName, habType = snap.type, snap.tier, snap.body, system = GameRead.System(h) });
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
