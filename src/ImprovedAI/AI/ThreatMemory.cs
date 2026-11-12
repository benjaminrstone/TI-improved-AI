using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;

namespace ImprovedAI.AI
{
    /// <summary>One sighting of a ship: the game day (DayNumber) and its SCV that day.</summary>
    public class ThreatObs
    {
        public int day;
        public float scv;
    }

    /// <summary>
    /// The aliens remember what human ships have shown them (exploits E1, E2). Vanilla judges a fleet only by the SCV of
    /// its currently fitted design (TISpaceShipState.SpaceCombatValue), so refitting to weak missiles, leaving magazines
    /// empty or burning fuel makes a fleet look harmless until moments before the fight.
    /// Memory is per ship: its best SCV over the last ThreatMemoryDays. A fleet's remembered threat is the sum, over the
    /// ships it has now, of max(current SCV, remembered best), so destroyed ships drop out exactly.
    /// Ships are keyed by game-state ID, so a rename changes nothing. A refit creates a new ship state
    /// (TISpaceShipState.CopyDataForRefit), which inherits the original's memory. Kept in the sidecar.
    /// </summary>
    internal static class ThreatMemory
    {
        public static bool Enabled => Plugin.Cfg.UseThreatMemory;

        // Per ship: sightings with strictly falling SCV, oldest first, so the first is the best within the window.
        private static Dictionary<int, List<ThreatObs>> Ships => AlienStrategy.Current?.threat;

        /// <summary>Called once per game day.</summary>
        public static void Sample(TIDateTime now)
        {
            var ships = Ships;
            if (!Enabled || ships == null) return;
            int day = DayNumber(now);
            int window = (int)Plugin.Cfg.ThreatMemoryDays.Value;
            var seen = new HashSet<int>();
            foreach (var faction in GameStateManager.AllHumanFactions())
                foreach (var fleet in faction.fleets)
                {
                    if (fleet.ships == null) continue;
                    foreach (var ship in fleet.ships)
                    {
                        if (ship == null) continue;
                        float scv = GameRead.Safe(() => ship.SpaceCombatValue());
                        if (float.IsNaN(scv)) continue;
                        int id = GameRead.Id(ship);
                        seen.Add(id);
                        if (!ships.TryGetValue(id, out var list)) ships[id] = list = new List<ThreatObs>();
                        while (list.Count > 0 && list[list.Count - 1].scv <= scv) list.RemoveAt(list.Count - 1);
                        list.Add(new ThreatObs { day = day, scv = scv });
                        while (list.Count > 1 && day - list[0].day > window) list.RemoveAt(0);
                    }
                }
            foreach (int id in ships.Keys.Where(k => !seen.Contains(k)).ToList()) ships.Remove(id);
        }

        /// <summary>The SCV the aliens attribute to this fleet: per ship it has now, the better of current and remembered best.</summary>
        public static float Remembered(TISpaceFleetState fleet)
        {
            float current = GameRead.Safe(() => fleet.SpaceCombatValue());
            var ships = Ships;
            if (!Enabled || ships == null || fleet?.ships == null || float.IsNaN(current)) return current;
            float total = 0f;
            foreach (var ship in fleet.ships)
            {
                if (ship == null) continue;
                float now = GameRead.Safe(() => ship.SpaceCombatValue());
                if (float.IsNaN(now)) now = 0f;
                float best = ships.TryGetValue(GameRead.Id(ship), out var list) && list.Count > 0 ? list[0].scv : 0f;
                total += best > now ? best : now;
            }
            return total > current ? total : current;
        }

        internal static void Inherit(TISpaceShipState refitted, TISpaceShipState original)
        {
            var ships = Ships;
            if (ships == null || refitted == null || original == null) return;
            if (ships.TryGetValue(GameRead.Id(original), out var list)) ships[GameRead.Id(refitted)] = list;
        }

        /// <summary>Ships remembered and the sum of their best SCV (diagnostics).</summary>
        internal static (int ships, float best) Totals()
        {
            var ships = Ships;
            return ships == null ? (0, 0f) : (ships.Count, ships.Values.Where(l => l.Count > 0).Sum(l => l[0].scv));
        }

        private static int DayNumber(TIDateTime d) => d.year * 372 + d.month * 31 + d.day;
    }

    [HarmonyPatch(typeof(TISpaceShipState), nameof(TISpaceShipState.CopyDataForRefit))]
    internal static class ThreatMemoryRefit
    {
        private static void Postfix(TISpaceShipState __instance, TISpaceShipState originalShip)
        {
            try { ThreatMemory.Inherit(__instance, originalShip); }
            catch (System.Exception e) { Plugin.Log.LogWarning($"[ThreatMemory] refit: {e.Message}"); }
        }
    }
}
