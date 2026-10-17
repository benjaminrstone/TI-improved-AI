using System;
using System.Collections.Generic;
using System.Linq;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;
using Vector3d = UnityEngine.Vector3d;

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
    ///     arrival of the last fleet needed to reach the goal's strength (fleets taken in order of arrival, those beyond
    ///     RendezvousMaxJoinDays left out) + the transfer from the point to the target,
    /// using each fleet's own flip-and-burn time between current positions (see Time), so a point whose planet is on the
    /// far side of the Sun scores badly. If the best point beats the current anchor by RendezvousGain, the goal is
    /// re-anchored on the fleet there and the old anchor joins it.
    /// v1 musters only where an alien fleet already is; empty staging points (Sun-planet L4/L5, moons) need a move order
    /// that keeps the attack goal, which vanilla does not offer (FactionGoal_SendFleet would take the fleet off it).
    /// </summary>
    internal static class Rendezvous
    {
        private static TIGameState Place(TISpaceFleetState f) => (TIGameState)f.ref_orbit ?? f.ref_spaceBody;

        private static Vector3d? Pos(TIGameState s)
        {
            try
            {
                if (s is TISpaceObjectState o) return o.GetGlobalPosition();
                if (s?.ref_spaceBody != null) return s.ref_spaceBody.GetGlobalPosition();
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Days for fleet <paramref name="mover"/> to fly from one point to another as the planets stand now: a constant-
        /// thrust flip-and-burn at its cruise acceleration, or burn-coast-burn when its delta-v (keeping a 40% reserve
        /// for combat and return) can't cover the full brachistochrone. The game's GenericTransferTime_d is a minimum-
        /// energy Hohmann estimate meant for slow logistics: it put Titan -> Jupiter at ~4,500 days, which the alien
        /// strike fleet actually flies in ~5 months (2039 probe, 2026-10-17).
        /// </summary>
        private static float Time(TISpaceFleetState mover, Vector3d? from, Vector3d? to)
        {
            if (mover == null || from == null || to == null) return float.PositiveInfinity;
            double d = (from.Value - to.Value).magnitude;
            if (d < 1e6) return 0f;
            double a = GameRead.Safe(() => mover.cruiseAcceleration_mps2), dv = 0.6 * GameRead.Safe(() => mover.currentDeltaV_mps);
            if (!(a > 0) || !(dv > 0)) return float.PositiveInfinity;
            double t;
            if (dv >= 2.0 * Math.Sqrt(d * a)) t = 2.0 * Math.Sqrt(d / a);
            else
            {
                double v = dv / 2.0, burn = v / a, burnDist = v * v / a;   // two burns of v/a seconds cover v^2/a metres
                t = 2.0 * burn + Math.Max(0.0, d - burnDist) / v;
            }
            return (float)(t / 86400.0);
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

            // When can ENOUGH force be at this point? Fleets in order of arrival there, accumulated until the need is met;
            // the score is the arrival of the last one needed, plus the transfer on to the target. Fleets that can't
            // arrive within MaxJoinDays don't count (v1 sorted by strength, so one distant fleet decades away dominated
            // every candidate: 2039 probe, 2026-10-17). If the need can't be met in time, the score is infinite.
            float maxJoin = Plugin.Cfg.RendezvousMaxJoinDays.Value;
            var targetPos = Pos(target);
            float Score(TISpaceFleetState c, out int used)
            {
                var point = Pos(c);
                float have = GameRead.Safe(() => c.SpaceCombatValue()), last = 0f;
                used = 0;
                var arrivals = pool.Where(d => d != c).Select(d => (d, t: Time(d, Pos(d), point))).Where(x => x.t <= maxJoin).OrderBy(x => x.t);
                foreach (var (d, t) in arrivals)
                {
                    if (have >= need) break;
                    have += GameRead.Safe(() => d.SpaceCombatValue());
                    last = t;
                    used++;
                }
                if (have < need) return float.PositiveInfinity;
                return last + Time(c, point, targetPos);
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
