using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using PavonisInteractive.TerraInvicta.Tasks;

namespace ImprovedAI.Fixes
{
    /// <summary>
    /// Vanilla bug: the alien branch of AIDailyFactionPlanner.DesignShipsCoroutine asks for hull "AlienBattleCruiser"
    /// (SS_Interceptor, slot k=3: the exotics medium interceptor). The data name is "AlienBattlecruiser" and the template
    /// lookup is case-sensitive, so it gets null and falls back to the lightest valid hull. The other three requests in
    /// the same method spell it correctly. Fix: the misspelt literal is replaced by a call that returns the right name
    /// (or the original one when the fix is off, so the switch works at runtime).
    /// </summary>
    [HarmonyPatch]
    internal static class AlienBattlecruiserName
    {
        private const string Wrong = "AlienBattleCruiser", Right = "AlienBattlecruiser";
        internal static int Replaced;

        private static MethodBase TargetMethod() => AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(AIDailyFactionPlanner), "DesignShipsCoroutine"));

        public static string HullName() => Plugin.Cfg.UseAlienBattlecruiserFix ? Right : Wrong;

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var call = AccessTools.Method(typeof(AlienBattlecruiserName), nameof(HullName));
            foreach (var ins in instructions)
            {
                if (ins.opcode == System.Reflection.Emit.OpCodes.Ldstr && ins.operand as string == Wrong)
                {
                    Replaced++;
                    yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, call).MoveLabelsFrom(ins).MoveBlocksFrom(ins);
                    continue;
                }
                yield return ins;
            }
            Plugin.Log.LogInfo($"[Fixes] AlienBattlecruiserName: replaced {Replaced} literal(s).");
        }
    }
}
