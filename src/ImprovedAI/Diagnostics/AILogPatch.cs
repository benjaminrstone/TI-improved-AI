using HarmonyLib;
using PavonisInteractive.TerraInvicta;

namespace ImprovedAI.Diagnostics
{
    /// <summary>
    /// TIFactionState.LogAI is an empty stub in shipping builds, but the AI still calls it from ~38 places (wars, habs
    /// founded, projects, DumpGoals/DumpShipyards...). Route it to ai-log.txt.
    /// Caveat: Mono may inline an empty static method at some call sites; those calls can't be recovered this way.
    /// </summary>
    [HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.LogAI))]
    internal static class AILogPatch
    {
        internal static int Calls;

        private static void Prefix(string logEntry)
        {
            Calls++;
            if (!Plugin.Cfg.AILog.Value) return;
            string date;
            try { date = TITimeState.Now()?.ToCustomDateString() ?? "?"; } catch { date = "?"; }
            Session.AILog($"[{date}] {logEntry}");
        }
    }
}
