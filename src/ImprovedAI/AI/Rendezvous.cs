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

        /// <summary>
        /// Days until fleet <paramref name="f"/> can be at <paramref name="anchor"/>: its scheduled arrival when it is
        /// already flying there, else the travel estimate from where it is now.
        /// </summary>
        public static float Eta(TISpaceFleetState f, TISpaceFleetState anchor)
        {
            if (f == null || anchor == null) return float.PositiveInfinity;
            try
            {
                var traj = f.transferAssigned ? f.trajectory : null;
                if (traj?.arrivalTime != null && traj.destination != null
                    && (traj.destination == anchor || ReferenceEquals(traj.destination.ref_fleet, anchor) || traj.destination == (TIGameState)anchor.ref_orbit))
                    return Math.Max(0f, (float)traj.arrivalTime.DifferenceInDays(TITimeState.Now()));
            }
            catch { }
            return Time(f, Pos(f), Pos(anchor));
        }

        /// <summary>Where something is now: the natural body it sits at (moon, planet, asteroid), else the object itself.</summary>
        internal static Vector3d? Pos(TIGameState s)
        {
            try { var n = s?.ref_naturalSpaceObject; if (n != null && !n.isSun) return n.GetGlobalPosition(); } catch { }
            try { if (s is TISpaceObjectState o) return o.GetGlobalPosition(); } catch { }
            return null;
        }

        internal static object Debug(TISpaceFleetState f, TIGameState target)
        {
            var p = Pos(f); var q = Pos(target);
            return new
            {
                fleetPos = p.HasValue, targetPos = q.HasValue,
                au = p.HasValue && q.HasValue ? (float)Math.Round((p.Value - q.Value).magnitude / 149597870700.0, 2) : -1f,
                accel = GameRead.Safe(() => f.cruiseAcceleration_mps2), dv = GameRead.Safe(() => f.currentDeltaV_kps),
                days = (float)Math.Round(Time(f, p, q), 1),
            };
        }

        /// <summary>
        /// Days for fleet <paramref name="mover"/> to fly from one point to another as the planets stand now: a constant-
        /// thrust flip-and-burn at its cruise acceleration, or burn-coast-burn when the delta-v it spends
        /// (RendezvousDvShare) can't cover the full brachistochrone. The game's GenericTransferTime_d is a minimum-
        /// energy Hohmann estimate meant for slow logistics: it put Titan -> Jupiter at ~4,500 days, which the alien
        /// strike fleet actually flies in ~5 months (2039 probe, 2026-10-17). Calibration (2026-10-21, six joins to
        /// Titan, 13-51 AU): the planner spent 74-83% of each fleet's delta-v; at 0.8 the estimate is within ~10% of the
        /// flown time (at the old 0.6 it was 20-40% long).
        /// </summary>
        internal static float Time(TISpaceFleetState mover, Vector3d? from, Vector3d? to)
        {
            if (mover == null || from == null || to == null) return float.PositiveInfinity;
            double d = (from.Value - to.Value).magnitude;
            if (d < 1e6) return 0f;
            double share = Math.Min(0.95, Math.Max(0.1, Plugin.Cfg.RendezvousDvShare.Value));
            double a = GameRead.Safe(() => mover.cruiseAcceleration_mps2), dv = share * GameRead.Safe(() => mover.currentDeltaV_mps);
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

        /// <summary>
        /// A better-placed anchor for the goal, or null to keep the current one. <paramref name="decided"/> is false while
        /// no muster point can gather the need within RendezvousMaxJoinDays (e.g. before escalation frees more fleets);
        /// the caller then asks again on a later tick instead of locking in a choice made on too little.
        /// </summary>
        public static TISpaceFleetState Choose(TIFactionState faction, FactionGoal_AttackWithFleet goal, TISpaceFleetState anchor, float need, Func<TISpaceFleetState, bool> releasable, out bool decided)
        {
            decided = true;
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
                float strike = last + Time(c, point, targetPos);
                // Need not reachable within MaxJoinDays from here: rank by how much force it can gather, then by time.
                // (The 2039 aliens could free only ~12k of a ~17k need anywhere; joiners recruited meanwhile are in
                // flight and no longer candidates, so the choice must be made on the first recruiting tick.)
                if (have < need) return 100000f * (1f - have / need) + strike;
                return strike;
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
            if (float.IsInfinity(bestScore)) { decided = false; return null; }   // no usable positions at all: ask again later
            bool move = best != anchor && bestScore < current * Plugin.Cfg.RendezvousGain.Value;
            Session.Journal?.Write(new
            {
                t = GameRead.Now(), kind = "rendezvous", goalId = GameRead.Id(goal), target = GameRead.Name(target), targetSystem = GameRead.System(target),
                need, anchor = anchor.displayName, anchorAt = GameRead.Body(anchor), anchorDays = float.IsInfinity(current) ? -1f : (float)Math.Round(current),
                chosen = best.displayName, chosenAt = GameRead.Body(best), chosenDays = float.IsInfinity(bestScore) ? -1f : (float)Math.Round(bestScore), moved = move, candidates = rows,
                anchorToTarget = Debug(anchor, target),
            });
            return move ? best : null;
        }
    }
}
