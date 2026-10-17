using System;
using System.Collections.Generic;
using System.Linq;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;

namespace ImprovedAI.AI
{
    /// <summary>
    /// Where a pooled strike musters (user, 2026-10-16: "rendezvous somewhere and then set out, like a normal navy;
    /// be smarter about the point, e.g. when Saturn is on the other side of the Sun").
    ///
    /// Vanilla anchors an attack goal on whatever fleet it assigned, and every joiner flies to that fleet. In the 2039
    /// Jupiter soak the anchor sat at Titan, so assembly took 6-9 months and the transit to Jupiter 5+ more.
    /// Here, once per focus goal and before joiners commit, each candidate muster point (the location of a sizable,
    /// releasable alien fleet, or the current anchor) is scored by TIME TO STRIKE:
    ///     max over the fleets needed to reach the goal's strength of transfer(fleet -> point) + transfer(point -> target)
    /// using the game's own estimate (TISpaceObjectState.GenericTransferTime_d: Hohmann time plus the wait for the next
    /// launch window, so a point with its planet on the far side of the Sun scores badly). If the best point beats the
    /// current anchor by RendezvousGain, the goal is re-anchored on the fleet there and the old anchor joins it.
    /// v1 musters only where an alien fleet already is; empty staging points (Sun-planet L4/L5, moons) need a move order
    /// that keeps the attack goal, which vanilla does not offer (FactionGoal_SendFleet would take the fleet off it).
    /// </summary>
    internal static class Rendezvous
    {
        private static TIGameState Place(TISpaceFleetState f) => (TIGameState)f.ref_orbit ?? f.ref_spaceBody;

        private static float Time(TIFactionState faction, TIGameState from, TIGameState to)
        {
            if (from == null || to == null) return float.PositiveInfinity;
            if (from == to) return 0f;
            try { return TISpaceObjectState.GenericTransferTime_d(faction, from, to); } catch { return float.PositiveInfinity; }
        }

        /// <summary>A better-placed anchor for the goal, or null to keep the current one.</summary>
        public static TISpaceFleetState Choose(TIFactionState faction, FactionGoal_AttackWithFleet goal, TISpaceFleetState anchor, float need, Func<TISpaceFleetState, bool> releasable)
        {
            var target = goal.target();
            if (target == null || anchor == null || Place(anchor) == null) return null;
            bool Usable(TISpaceFleetState f) => TIGameState.Valid(f) && !f.transferAssigned && f.isCapableOfTransfering && f.CombatFleet() && !f.InvasionFleet()
                                                && !f.AI_NeedsRepairBadly() && Place(f) != null && (f == anchor || releasable(f));
            var pool = faction.fleets.Where(Usable).OrderByDescending(f => GameRead.Safe(() => f.SpaceCombatValue())).ToList();
            if (pool.Count < 2) return null;
            float minSize = 0.1f * need;
            var candidates = pool.Where(f => f == anchor || GameRead.Safe(() => f.SpaceCombatValue()) >= minSize).Take(12).ToList();

            float Score(TISpaceFleetState c, out int used)
            {
                var point = Place(c);
                float have = GameRead.Safe(() => c.SpaceCombatValue()), worst = 0f;
                used = 0;
                foreach (var d in pool)
                {
                    if (have >= need) break;
                    if (d == c) continue;
                    have += GameRead.Safe(() => d.SpaceCombatValue());
                    worst = Math.Max(worst, Time(faction, Place(d), point));
                    used++;
                }
                return worst + Time(faction, point, target);
            }

            float current = Score(anchor, out int usedCurrent);
            TISpaceFleetState best = anchor;
            float bestScore = current;
            var rows = new List<object>();
            foreach (var c in candidates)
            {
                float s = c == anchor ? current : Score(c, out _);
                rows.Add(new { fleet = c.displayName, at = GameRead.Body(c), days = float.IsInfinity(s) ? -1f : (float)Math.Round(s) });
                if (s < bestScore) { bestScore = s; best = c; }
            }
            bool move = best != anchor && bestScore < current * Plugin.Cfg.RendezvousGain.Value;
            Session.Journal?.Write(new
            {
                t = GameRead.Now(), kind = "rendezvous", goalId = GameRead.Id(goal), target = GameRead.Name(target), targetSystem = GameRead.System(target),
                need, anchor = anchor.displayName, anchorAt = GameRead.Body(anchor), anchorDays = float.IsInfinity(current) ? -1f : (float)Math.Round(current),
                chosen = best.displayName, chosenAt = GameRead.Body(best), chosenDays = float.IsInfinity(bestScore) ? -1f : (float)Math.Round(bestScore), moved = move, candidates = rows,
            });
            return move ? best : null;
        }
    }
}
