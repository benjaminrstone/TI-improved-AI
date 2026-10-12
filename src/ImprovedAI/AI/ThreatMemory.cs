using System.Collections.Generic;
using System.Linq;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;

namespace ImprovedAI.AI
{
    /// <summary>
    /// The aliens remember what human fleets have shown them. Vanilla judges a fleet only by the SCV of its currently
    /// fitted design (TISpaceShipState.SpaceCombatValue), so refitting to weak missiles, leaving magazines empty or
    /// burning fuel makes a fleet look harmless until moments before the fight (EXPLOITS E1, E2).
    ///
    /// Here a fleet's threat is its best recent showing: the peak SCV per ship over the last N days, times its current
    /// ship count (so splitting a fleet still lowers the remembered threat of each half), never less than its current SCV.
    /// Sampled once per game day for every non-alien fleet. Not persisted: after a load, memory starts from the
    /// fleet's current value and fills in over the following days.
    /// </summary>
    internal static class ThreatMemory
    {
        private struct Obs { public double day; public float perShip; }

        private static readonly Dictionary<int, List<Obs>> _samples = new Dictionary<int, List<Obs>>();

        public static bool Enabled => Plugin.Cfg.UseThreatMemory;

        public static void Reset() => _samples.Clear();

        /// <summary>Called once per game day.</summary>
        public static void Sample(TIDateTime now)
        {
            if (!Enabled) return;
            double day = DayNumber(now);
            double window = Plugin.Cfg.ThreatMemoryDays.Value;
            var live = new HashSet<int>();
            foreach (var faction in GameStateManager.AllHumanFactions())
            {
                foreach (var fleet in faction.fleets)
                {
                    int ships = fleet.ships?.Count ?? 0;
                    if (ships == 0) continue;
                    int id = GameRead.Id(fleet);
                    live.Add(id);
                    if (!_samples.TryGetValue(id, out var list)) _samples[id] = list = new List<Obs>();
                    list.Add(new Obs { day = day, perShip = GameRead.Safe(() => fleet.SpaceCombatValue()) / ships });
                    list.RemoveAll(s => day - s.day > window);
                }
            }
            foreach (var dead in _samples.Keys.Where(k => !live.Contains(k)).ToList()) _samples.Remove(dead);
        }

        /// <summary>The SCV the aliens attribute to this fleet: its best recent per-ship showing at its current size.</summary>
        public static float Remembered(TISpaceFleetState fleet)
        {
            float current = GameRead.Safe(() => fleet.SpaceCombatValue());
            if (!Enabled || fleet == null || float.IsNaN(current)) return current;
            int ships = fleet.ships?.Count ?? 0;
            if (ships == 0 || !_samples.TryGetValue(GameRead.Id(fleet), out var list) || list.Count == 0) return current;
            float peak = list.Max(s => s.perShip) * ships;
            return peak > current ? peak : current;
        }

        private static double DayNumber(TIDateTime d) => d.year * 372.0 + d.month * 31.0 + d.day;
    }
}
