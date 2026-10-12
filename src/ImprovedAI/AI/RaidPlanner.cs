using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;

namespace ImprovedAI.AI
{
    /// <summary>
    /// The aliens raid the human economy wherever it is weakly held (EXPLOITS E5, and pressure on turtles, E10).
    ///
    /// Vanilla only targets "impudent" assets inside alien turf (Jupiter and beyond, or systems with alien bases) and adds
    /// at most one base and one station target per month (AIDailyFactionPlanner.AliensCheckGoals). In the vanilla probe, 15
    /// undefended mining outposts in the main belt drew zero attack goals in six months.
    ///
    /// Every 7 game days, for each human faction the aliens are at war with, score that faction's bases and stations:
    ///   value  = (1 + tier) x 2 if it has an active mine x 1.5 if it has a shipyard
    ///   threat = the hab's own SCV + remembered SCV of that faction's fleets in the same system
    ///   score  = value / (1 + threat / 200) x (1 + RaidHateWeight x hate / war-threshold hate)
    /// so at low hate the aliens go for the most valuable targets, and a faction they hate intensely has its assets
    /// prioritised anywhere (user, 2026-10-12).
    /// and add AttackWithFleet goals (requiresWar, so vanilla drops them if the war ends) for the best targets not already
    /// under attack, keeping at most MaxRaidGoals of our raids open. Vanilla then sizes, builds for and launches them.
    /// </summary>
    internal static class RaidPlanner
    {
        private static readonly HashSet<int> _ours = new HashSet<int>();
        internal static bool IsRaid(TIFactionGoalState g) => g != null && _ours.Contains(GameRead.Id(g));
        private static int _day;

        public static void Reset() { _ours.Clear(); _day = 0; }

        public static void Daily(TIDateTime now)
        {
            if (!Plugin.Cfg.UseRaidPlanner || _day++ % 7 != 0) return;
            var alien = GameStateManager.AlienFaction();
            if (alien == null || !AIEvaluators.ShouldAliensGoLoud()) return;

            var attackGoals = alien.GoalsOfType(GoalType.AttackWithFleet).OfType<FactionGoal_AttackWithFleet>().ToList();
            var live = new HashSet<int>(attackGoals.Where(g => g.importance > 0).Select(g => GameRead.Id(g)));
            _ours.RemoveWhere(id => !live.Contains(id));
            int slots = Plugin.Cfg.MaxRaidGoals.Value - _ours.Count;
            if (slots <= 0) return;

            var targeted = new HashSet<TIGameState>(attackGoals.Where(g => g.importance > 0).Select(g => g.target()).Where(t => t != null));
            var enemies = alien.GoalsOfType(GoalType.WarOnFaction).OfType<FactionGoal_WarOnFaction>()
                .Select(g => g.targetFaction).Where(f => f != null && !f.IsAlienFaction).Distinct().ToList();

            var candidates = new List<(TIHabState hab, float score, float value, float threat)>();
            float warHate = UnityEngine.Mathf.Max(1f, TemplateManager.global.alienFactionHateWarValue);
            foreach (var enemy in enemies)
            {
                float hate = GameRead.Safe(() => alien.GetFactionHate(enemy));
                float hateFactor = 1f + Plugin.Cfg.RaidHateWeight.Value * UnityEngine.Mathf.Clamp(float.IsNaN(hate) ? 0f : hate / warHate, 0f, 4f);
                foreach (var hab in enemy.habs)
                {
                    if (hab == null || targeted.Contains(hab) || hab.ref_system == null) continue;
                    if (!hab.IsBase && !hab.IsStation) continue;
                    float value = (1 + hab.tier) * (hab.HasActiveMine ? 2f : 1f) * (hab.CompletedShipyards().Count > 0 ? 1.5f : 1f);
                    float threat = GameRead.Safe(() => hab.SpaceCombatValue())
                                   + enemy.fleets.Where(f => f.ref_system == hab.ref_system).Sum(f => ThreatMemory.Remembered(f));
                    if (float.IsNaN(threat)) continue;
                    candidates.Add((hab, value / (1f + threat / 200f) * hateFactor, value, threat));
                }
            }

            var picks = candidates.OrderByDescending(c => c.score).Take(slots).ToList();
            foreach (var c in picks)
            {
                var goal = alien.AddGoal(new FactionGoal_AttackWithFleet(alien, Plugin.Cfg.RaidImportance.Value, c.hab, requiresWar: true),
                    HandleDuplicateGoalRule.ResetImportanceIfHigher) as FactionGoal_AttackWithFleet;
                if (goal != null) _ours.Add(GameRead.Id(goal));
            }
            if (picks.Count > 0)
                Session.Journal?.Write(new
                {
                    t = GameRead.Now(), kind = "raid", open = _ours.Count,
                    picks = picks.Select(c => new { hab = c.hab.displayName, faction = GameRead.Faction(c.hab.faction), body = GameRead.Body(c.hab), system = GameRead.System(c.hab), c.value, c.threat, c.score }),
                });
        }
    }

    /// <summary>
    /// Vanilla escalates the bombardment it wants after failed attacks, but counts failures per FLEET
    /// (GetFailedAttacksOnTargetCount), and every re-launch is a fresh fleet, so the aliens never learn that a base needs
    /// more. For the aliens, count failures per TARGET: the highest count among all their fleets.
    /// </summary>
    [HarmonyPatch(typeof(FactionGoal_AttackWithFleet), nameof(FactionGoal_AttackWithFleet.GetDesiredBombardmentValue), new[] { typeof(TIFactionState), typeof(TIGameState), typeof(int) })]
    internal static class FailureMemoryPerTarget
    {
        private static void Prefix(TIFactionState bombardingFaction, TIGameState target, ref int failedAttackCount)
        {
            if (!Plugin.Cfg.UseRaidPlanner || bombardingFaction == null || !bombardingFaction.IsAlienFaction || target == null) return;
            foreach (var f in bombardingFaction.fleets)
            {
                int n = 0;
                try { n = f.GetFailedAttacksOnTargetCount(target); } catch { }
                if (n > failedAttackCount) failedAttackCount = n;
            }
        }
    }

    /// <summary>Raids are meant to destroy the target: ask for RaidBombardMultiplier x vanilla's bombardment so one ship isn't sent alone.</summary>
    [HarmonyPatch(typeof(FactionGoal_AttackWithFleet), nameof(FactionGoal_AttackWithFleet.GetDesiredBombardmentValue), new System.Type[0])]
    internal static class RaidBombardment
    {
        private static void Postfix(FactionGoal_AttackWithFleet __instance, ref float __result)
        {
            if (__result > 0f && Plugin.Cfg.UseRaidPlanner && RaidPlanner.IsRaid(__instance)) __result *= Plugin.Cfg.RaidBombardMultiplier.Value;
        }
    }
}
