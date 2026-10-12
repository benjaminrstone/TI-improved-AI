using System.Collections.Generic;
using System.Linq;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;

namespace ImprovedAI.AI
{
    /// <summary>
    /// The aliens remember what human ships have shown them. Vanilla judges a fleet only by the SCV of its currently
    /// fitted design (TISpaceShipState.SpaceCombatValue), so refitting to weak missiles, leaving magazines empty or
    /// burning fuel makes a fleet look harmless until moments before the fight (EXPLOITS E1, E2).
    ///
    /// Memory is per SHIP: each ship's best SCV over the last N days. A fleet's remembered threat is the sum, over the
    /// ships it has now, of max(current SCV, remembered best). Destroyed ships drop out exactly (no "average x count"
    /// overestimate when a fleet loses its battleship), and a destroyed fleet stops counting at once.
    /// Ships are keyed by faction + name, not game-state ID, because a refit creates a new ship state that keeps the
    /// original's name (TISpaceShipState.CopyDataForRefit): a bait refit must not wipe the memory.
    /// Sampled once per game day for every human ship. Not persisted: after a load, memory starts from current values.
    /// </summary>
    internal static class ThreatMemory
    {
        private struct Obs { public double day; public float scv; }

        private static readonly Dictionary<string, List<Obs>> _ships = new Dictionary<string, List<Obs>>();

        public static bool Enabled => Plugin.Cfg.UseThreatMemory;

        public static void Reset() => _ships.Clear();

        private static string Key(TISpaceShipState s, TIFactionState f) => GameRead.Id(f) + "|" + s.displayName;

        /// <summary>Called once per game day.</summary>
        public static void Sample(TIDateTime now)
        {
            if (!Enabled) return;
            double day = DayNumber(now);
            double window = Plugin.Cfg.ThreatMemoryDays.Value;
            foreach (var faction in GameStateManager.AllHumanFactions())
                foreach (var fleet in faction.fleets)
                {
                    if (fleet.ships == null) continue;
                    foreach (var ship in fleet.ships)
                    {
                        if (ship == null) continue;
                        string k = Key(ship, faction);
                        if (!_ships.TryGetValue(k, out var list)) _ships[k] = list = new List<Obs>();
                        list.Add(new Obs { day = day, scv = GameRead.Safe(() => ship.SpaceCombatValue()) });
                    }
                }
            // Forget anything not seen within the window (destroyed or scrapped ships age out with their last sample).
            foreach (var k in _ships.Keys.ToList())
            {
                var list = _ships[k];
                list.RemoveAll(o => day - o.day > window);
                if (list.Count == 0) _ships.Remove(k);
            }
        }

        /// <summary>The SCV the aliens attribute to this fleet: per ship it has now, the better of current and remembered best.</summary>
        public static float Remembered(TISpaceFleetState fleet)
        {
            float current = GameRead.Safe(() => fleet.SpaceCombatValue());
            if (!Enabled || fleet?.ships == null || float.IsNaN(current)) return current;
            float total = 0f;
            foreach (var ship in fleet.ships)
            {
                if (ship == null) continue;
                float now = GameRead.Safe(() => ship.SpaceCombatValue());
                if (float.IsNaN(now)) now = 0f;
                float best = now;
                if (_ships.TryGetValue(Key(ship, fleet.faction), out var list))
                    foreach (var o in list) if (o.scv > best) best = o.scv;
                total += best;
            }
            return total > current ? total : current;
        }

        private static double DayNumber(TIDateTime d) => d.year * 372.0 + d.month * 31.0 + d.day;
    }
}
