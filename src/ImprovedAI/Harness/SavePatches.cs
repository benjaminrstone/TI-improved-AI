using HarmonyLib;
using PavonisInteractive.TerraInvicta;

namespace ImprovedAI.Harness
{
    /// <summary>
    /// In harness runs the save folder is already redirected away from the user's saves; this additionally skips the
    /// game's ~100 MB autosaves (every mission phase, before every combat, autopilot saves) for speed.
    /// </summary>
    [HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.SaveAllGameStates))]
    internal static class SavePatches
    {
        internal static int Blocked;

        private static bool Prefix(string filepath, ref bool __result)
        {
            if (!CampaignHarness.Active || CampaignHarness.Run.allowSaves || CampaignHarness.AllowSaveNow) return true;
            Blocked++;
            __result = true;
            return false;
        }
    }
}
