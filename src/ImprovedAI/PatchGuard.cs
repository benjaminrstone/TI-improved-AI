using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using HarmonyLib;

namespace ImprovedAI
{
    /// <summary>
    /// No patch of ours may break the game's own code. Harmony runs a Prefix or Postfix inside the patched game method,
    /// so an exception in one would abort that method for its caller (a faction's daily plan, a save, a combat). Every
    /// Prefix and Postfix in this assembly is itself patched with a finalizer that logs the exception and swallows it:
    /// a failed Postfix leaves the game's result as it was, and a failed Prefix lets the original run.
    /// The plugin's own per-frame code goes through Run for the same reason: the game answers any exception Unity logs
    /// with its crash dialog and a blocked clock (GlobalInstaller.HandleException).
    /// </summary>
    internal static class PatchGuard
    {
        /// <summary>Exceptions swallowed since the game started.</summary>
        internal static int Count;
        private static readonly HashSet<string> Reported = new HashSet<string>();

        internal static int Apply(Harmony harmony, IEnumerable<Type> types)
        {
            var onVoid = new HarmonyMethod(typeof(PatchGuard), nameof(OnVoid));
            var onBool = new HarmonyMethod(typeof(PatchGuard), nameof(OnBool));
            int guarded = 0;
            foreach (var type in types)
            {
                if (type == typeof(PatchGuard) || HarmonyMethodExtensions.GetFromType(type).Count == 0) continue;
                foreach (var m in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (m.Name != "Prefix" && m.Name != "Postfix") continue;
                    try
                    {
                        harmony.Patch(m, finalizer: m.ReturnType == typeof(bool) ? onBool : onVoid);
                        guarded++;
                    }
                    catch (Exception e) { Plugin.Log.LogError($"Could not guard {type.FullName}.{m.Name}: {e.Message}"); }
                }
            }
            return guarded;
        }

        /// <summary>Run a piece of the plugin's own per-frame code; an exception is reported and swallowed.</summary>
        internal static void Run(Action body, string name)
        {
            try { body(); }
            catch (Exception e) { Report(e, name); }
        }

        private static Exception OnVoid(Exception __exception, MethodBase __originalMethod)
        {
            if (__exception != null) Report(__exception, Name(__originalMethod));
            return null;
        }

        /// <summary>A Prefix that failed must not skip the game's method.</summary>
        private static Exception OnBool(Exception __exception, MethodBase __originalMethod, ref bool __result)
        {
            if (__exception == null) return null;
            __result = true;
            Report(__exception, Name(__originalMethod));
            return null;
        }

        private static string Name(MethodBase m) => m?.DeclaringType?.Name + "." + m?.Name;

        private static void Report(Exception e, string name)
        {
            Interlocked.Increment(ref Count);
            bool first;
            lock (Reported) first = Reported.Add(name);
            // The first failure of each patch in full; a patch that fails daily must not flood the log.
            if (first) Plugin.Log.LogError($"[Guard] {name} threw and was skipped (further failures there are counted, not logged): {e}");
        }
    }
}
