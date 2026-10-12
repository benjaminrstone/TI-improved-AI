using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;

namespace ImprovedAI.AI
{
    /// <summary>
    /// Alien attacks are sized to win decisively instead of to just beat the defenders' current SCV (EXPLOITS E3).
    ///
    /// Vanilla (FactionGoal_AttackWithFleet.ComputeDesiredFleetCombatValueForAttack) wants defenders x ~1.4 + a small
    /// bonus, counts only fleets within 1000 km of the target, and launches the moment a fleet reaches that number:
    /// median launch ratio 1.05-1.21 in the vanilla probes. Here, for the alien faction attacking a human target:
    ///   need = (remembered SCV of the defenders + 0.5 x other same-faction fleets in the target's system)
    ///          x perceived-strength factor x AlienAttackSuperiority + 1
    /// and the desired value is raised to at least that. Vanilla's own number still applies when it is higher.
    /// The in-system "spontaneous attack" threshold is raised to the same superiority.
    /// </summary>
    [HarmonyPatch(typeof(FactionGoal_AttackWithFleet), nameof(FactionGoal_AttackWithFleet.ComputeDesiredFleetCombatValueForAttack))]
    internal static class DecisiveForce
    {
        private static void Postfix(TIFactionState faction, TIGameState target, bool onlyConsiderTarget, bool isReinforcement, ref float __result)
        {
            if (!Plugin.Cfg.UseDecisiveForce || faction == null || !faction.IsAlienFaction || onlyConsiderTarget) return;
            if (target == null || target.deleted || target.ref_faction == null || target.ref_faction.IsAlienFaction || target.ref_system == null) return;
            float need = DefenderThreat(faction, target, isReinforcement) * Dials.AttackSuperiority + 1f;
            if (need > __result) __result = need;
        }

        /// <summary>What the aliens expect to fight at the target: remembered defenders plus half of the rest of that faction's fleets in the system.</summary>
        internal static float DefenderThreat(TIFactionState alien, TIGameState target, bool isReinforcement = false)
        {
            var enemy = target.ref_faction;
            var counted = new HashSet<TISpaceFleetState>();
            float total = 0f;

            if (target.ref_hab != null && target.ref_hab.IsStation)
                total += target.ref_hab.PerceivedAggregateDefensiveScore_Station(alien);

            IEnumerable<TISpaceFleetState> primary = null;
            if (target.isSpaceFleetState)
                primary = isReinforcement ? Enumerable.Repeat(target.ref_fleet, 1) : TIFactionState.GetDefenders(target.ref_fleet);
            else if (target is TISpaceObjectState so)
                primary = TIFactionState.GetDefenders(so);
            if (primary != null)
                foreach (var f in primary)
                    if (f != null && counted.Add(f)) total += ThreatMemory.Remembered(f);

            foreach (var f in enemy.fleets)
                if (f != null && f.ref_system == target.ref_system && counted.Add(f))
                    total += 0.5f * ThreatMemory.Remembered(f);

            return total * alien.GetPerceivedEnemyFleetStrengthFactor(enemy);
        }
    }

    /// <summary>Aliens already in the target's system attack "spontaneously" at 1.25x in vanilla; hold them to the same superiority.</summary>
    [HarmonyPatch(typeof(AIEvaluators), nameof(AIEvaluators.GetMinimumSuperiorityForSpontaniousAttack))]
    internal static class DecisiveForceSpontaneous
    {
        private static void Postfix(TIFactionState faction, ref float __result)
        {
            if (Plugin.Cfg.UseDecisiveForce && faction != null && faction.IsAlienFaction && __result < Dials.AttackSuperiority)
                __result = Dials.AttackSuperiority;
        }
    }

    /// <summary>
    /// Vanilla lets an alien fleet already in the target's system launch below the goal's desired strength if it beats
    /// GetPerceivedEnemySpaceAssetStrength_AndItsDefenders (only defenders right at the target) by 1.25x. That is how a
    /// fleet fresh from a win at Earth attacked a station with a docked swarm nearby at 1/3 of the needed strength and
    /// was wiped out (EXPLOITS E14). Hold that shortcut to the full expected threat at the target.
    /// </summary>
    [HarmonyPatch(typeof(FactionGoal_AttackWithFleet), nameof(FactionGoal_AttackWithFleet.ReadyForTransferToTarget))]
    internal static class NoUnderstrengthShortcut
    {
        private static void Postfix(FactionGoal_AttackWithFleet __instance, TISpaceFleetState fleet, ref bool __result)
        {
            if (!__result || !Plugin.Cfg.UseDecisiveForce || fleet == null || !__instance.faction.IsAlienFaction) return;
            float scv = fleet.SpaceCombatValue();
            if (scv >= __instance.desiredFleetCombatValue) return;              // the normal launch rule; leave it
            var target = __instance.target();
            if (target == null || target.ref_faction == null || target.ref_faction.IsAlienFaction) return;
            float need = DecisiveForce.DefenderThreat(__instance.faction, target) * Dials.ShortcutSuperiority;
            if (scv < need) __result = false;
        }
    }

    /// <summary>Perceived strength of a human fleet, as the aliens see it, uses threat memory.</summary>
    [HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.GetPerceivedEnemyFleetStrength))]
    internal static class PerceivedStrengthMemory
    {
        private static void Postfix(TIFactionState __instance, TISpaceFleetState enemyFleet, ref float __result)
        {
            if (!ThreatMemory.Enabled || !__instance.IsAlienFaction || enemyFleet?.faction == null || enemyFleet.faction.IsAlienFaction) return;
            float remembered = __instance.GetPerceivedEnemyFleetStrengthFactor(enemyFleet.faction) * ThreatMemory.Remembered(enemyFleet);
            if (remembered > __result) __result = remembered;
        }
    }
}
