using System;
using HarmonyLib;
using PavonisInteractive.TerraInvicta.Tasks;

namespace ImprovedAI.Harness
{
    /// <summary>
    /// "ai.fault on|off": while on, this Prefix and Postfix throw on every call, inside the fleet tick of every AI
    /// faction's daily plan. With PatchGuard working the game carries on as if the patch were not there.
    /// </summary>
    [HarmonyPatch(typeof(AIDailyFactionPlanner), "ManageFleets")]
    internal static class FaultInjection
    {
        internal static bool On;

        private static bool Prefix()
        {
            if (On) throw new InvalidOperationException("injected fault (prefix)");
            return true;
        }

        private static void Postfix()
        {
            if (On) throw new InvalidOperationException("injected fault (postfix)");
        }
    }
}
