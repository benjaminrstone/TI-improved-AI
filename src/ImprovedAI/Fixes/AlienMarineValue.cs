using System.Linq;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;

namespace ImprovedAI.Fixes
{
    /// <summary>
    /// Vanilla bug: TIUtilityModuleTemplate.marineOpsValue returns the module's value only if its FIRST special rule is
    /// Assault or MarineOpsDefenseOnly. The aliens' only assault module, SalamanderTerrorUnitPod, lists
    /// ["Crashdown","Assault",...], so every alien ship has assault value 0 and can never take or destroy a base
    /// (DestroyHabOperation / AssaultHabOperation need AssaultCombatValue > 0). A big part of why undefended asteroid
    /// mines are safe (EXPLOITS E5). Fix: count the value whenever the rules include Assault.
    /// Every human marine module already lists Assault first, so only that alien pod changes.
    /// </summary>
    [HarmonyPatch(typeof(TIUtilityModuleTemplate), nameof(TIUtilityModuleTemplate.marineOpsValue), MethodType.Getter)]
    internal static class AlienMarineValue
    {
        private static void Postfix(TIUtilityModuleTemplate __instance, ref float __result)
        {
            if (__result == 0f && Plugin.Cfg.UseAlienMarineFix && __instance.specialModuleRules != null
                && __instance.specialModuleRules.Contains(SpecialModuleRule.Assault))
                __result = __instance.specialModuleValue;
        }
    }
}
