using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Tasks;

namespace ImprovedAI.AI
{
    /// <summary>
    /// Alien willingness to fight, once a battle is joined (user, 2026-10-12: "never lose a ship" is too cautious).
    ///  - Defending their own base or station (there are Hydras aboard): never evade unless hopelessly outmatched.
    ///  - An attack fleet that arrives to find ~even odds fights rather than turning around and burning fuel home.
    /// Applied after vanilla's StratCombatInitStrategy.SelectStance, only to replace an Evade.
    /// </summary>
    [HarmonyPatch(typeof(StratCombatInitStrategy), nameof(StratCombatInitStrategy.SelectStance))]
    internal static class StanceCourage
    {
        private static void Postfix(TIFactionState faction, TISpaceCombatState combatState, Dictionary<TINationState, PlannedFighters> fighterPlan, ref CombatStance __result)
        {
            if (__result != CombatStance.Evade || !Plugin.Cfg.UseStanceCourage || faction == null || !faction.IsAlienFaction || combatState == null) return;
            var allowed = combatState.allowedStances[faction];

            float mine = 0f, theirs = 0f;
            TISpaceFleetState own = null;
            foreach (var f in combatState.fleets)
            {
                if (f == null) continue;
                if (f.faction == faction) { own = f; mine += f.SpaceCombatValue(); }
                else theirs += faction.GetPerceivedEnemyFleetStrength(f);
            }
            bool ourHab = combatState.hab != null && combatState.hab.ref_faction != null && combatState.hab.ref_faction.permanentAlly(faction);
            if (combatState.hab != null)
            {
                if (ourHab) mine += combatState.hab.SpaceCombatValue();
                else theirs += combatState.hab.SpaceCombatValue();
            }
            if (fighterPlan != null) mine += fighterPlan.Values.Sum(x => x.count * x.fighter.TemplateSpaceCombatValue());
            float ratio = mine / UnityEngine.Mathf.Max(1f, theirs);

            CombatStance? stand = null;
            if (ourHab && ratio >= Plugin.Cfg.DefendHabHopelessRatio.Value)
                stand = CombatStance.Defend;
            else if (own?.AssignedGoal() is FactionGoal_AttackWithFleet && ratio >= Plugin.Cfg.ArrivalFightRatio.Value)
                stand = allowed.Contains(CombatStance.Pursue) ? CombatStance.Pursue : CombatStance.Defend;

            if (stand.HasValue && allowed.Contains(stand.Value))
            {
                Diagnostics.Session.Journal?.Write(new { t = Diagnostics.GameRead.Now(), kind = "stance", from = "Evade", to = stand.Value.ToString(), ratio, ourHab, fleet = own?.displayName, hab = combatState.hab?.displayName });
                __result = stand.Value;
            }
        }
    }
}
