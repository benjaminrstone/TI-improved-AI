using System;
using System.Collections.Generic;
using System.Linq;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Tasks;

namespace ImprovedAI.Harness
{
    /// <summary>
    /// Real orders for any faction, issued the way the game's own AI issues them (user, 2026-10-22: scripted orders in
    /// probes, and the building blocks of the player bot and the strategist). A move picks a trajectory with
    /// AIDailyFactionPlanner.SelectTrajectoryAsync (the fastest within the delta-v budget) and confirms the game's
    /// TransferOperation, as SingleFleetOperation does. Every order is journaled ("order").
    /// For an AI-run faction the AI may later re-task the fleet; for a human-controlled (passive) faction it sticks.
    /// </summary>
    internal static class Orders
    {
        private static TransferOperation Transfer => OperationsManager.operationsLookup[typeof(TransferOperation)] as TransferOperation;
        private static MergeFleetOperation Merge => OperationsManager.operationsLookup[typeof(MergeFleetOperation)] as MergeFleetOperation;

        private static bool Is(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// A faction's fleets by selector: a fleet name; "strongest" or "all", optionally "@System" (e.g. strongest@Mars);
        /// "idle@System" = combat fleets with no transfer, operation or combat under way.
        /// </summary>
        public static List<TISpaceFleetState> Fleets(TIFactionState f, string sel)
        {
            string kind = sel, system = null;
            int at = sel.IndexOf('@');
            if (at >= 0) { kind = sel.Substring(0, at); system = sel.Substring(at + 1); }
            var pool = f.fleets.Where(x => TIGameState.Valid(x) && (system == null || Is(GameRead.System(x), system))).ToList();
            switch (kind.ToLowerInvariant())
            {
                case "all": return pool;
                case "idle": return pool.Where(Idle).ToList();
                case "strongest": return pool.OrderByDescending(x => GameRead.Safe(() => x.SpaceCombatValue())).Take(1).ToList();
                default: return pool.Where(x => Is(x.displayName, kind)).ToList();
            }
        }

        public static bool Idle(TISpaceFleetState x) =>
            x != null && !x.transferAssigned && !x.inCombatOrWaitingForCombat && (x.currentOperations == null || x.currentOperations.Count == 0)
            && GameRead.Safe(() => x.CombatFleet() ? 1f : 0f) > 0f;

        /// <summary>A destination by name: a fleet, a hab, or a body (its lowest orbit).</summary>
        public static TIGameState Target(string name)
        {
            var fleet = GameStateManager.IterateByClass<TISpaceFleetState>().FirstOrDefault(x => TIGameState.Valid(x) && Is(x.displayName, name));
            if (fleet != null) return fleet;
            var hab = GameStateManager.IterateByClass<TIHabState>().FirstOrDefault(x => TIGameState.Valid(x) && Is(x.displayName, name));
            if (hab != null) return hab;
            var body = GameStateManager.AllSpaceBodies().FirstOrDefault(b => Is(b.displayName, name) || Is(b.templateName, name));
            var orbit = body?.orbits?.OrderBy(o => o.semiMajorAxis_km).FirstOrDefault();
            if (orbit != null) return orbit;
            throw new ArgumentException("no fleet, hab or body named " + name);
        }

        /// <summary>Send a fleet to a destination, keeping <paramref name="reserve"/> of its delta-v (vanilla uses 0.2-0.5).</summary>
        public static string Move(TISpaceFleetState fleet, TIGameState target, float reserve, string why)
        {
            var op = Transfer;
            if (op == null) return "! no transfer operation";
            if (!op.ActorCanPerformOperation(fleet, target)) return $"! {fleet.displayName} can't start a transfer now";
            if (!op.ValidTransferDestinationForFleet(fleet, target)) return $"! {GameRead.Name(target)} is not a valid destination for {fleet.displayName}";
            Trajectory traj = null;
            AIDailyFactionPlanner.SelectTrajectoryAsync(fleet, target, reserve, out var result, t => traj = t);
            if (traj == null) return $"! no trajectory for {fleet.displayName} -> {GameRead.Name(target)} ({result?.Result})";
            bool ok = op.OnOperationConfirm(fleet, target, null, traj);
            Session.Journal?.Write(new
            {
                t = GameRead.Now(), kind = "order", order = "move", ok, why, faction = GameRead.Faction(fleet.faction), fleet = fleet.displayName,
                scv = GameRead.Safe(() => fleet.SpaceCombatValue()), from = GameRead.Body(fleet), to = GameRead.Name(target), toSystem = GameRead.System(target),
                arrive = GameRead.Date(traj.arrivalTime), flightDays = (float)Math.Round(traj.flightDuration_s / 86400.0, 1), dvKps = (float)Math.Round(traj.DV_kps, 1),
            });
            return ok ? $"> {fleet.displayName} -> {GameRead.Name(target)}, arrives {GameRead.Date(traj.arrivalTime)}" : $"! {fleet.displayName}: transfer refused";
        }

        /// <summary>Merge a fleet into another at the same location.</summary>
        public static string MergeInto(TISpaceFleetState fleet, TISpaceFleetState into, string why)
        {
            var op = Merge;
            if (op == null) return "! no merge operation";
            if (!op.ActorCanPerformOperation(fleet, into) || !op.GetPossibleTargets(fleet).Contains(into)) return $"! {fleet.displayName} can't merge into {into.displayName} now";
            bool ok = op.OnOperationConfirm(fleet, into, null, null);
            Session.Journal?.Write(new { t = GameRead.Now(), kind = "order", order = "merge", ok, why, faction = GameRead.Faction(fleet.faction), fleet = fleet.displayName, into = into.displayName, at = GameRead.Body(fleet) });
            return ok ? $"> {fleet.displayName} merges into {into.displayName}" : $"! {fleet.displayName}: merge refused";
        }
    }
}
