using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ImprovedAI
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.improvedai.terrainvicta";
        public const string Name = "ImprovedAI";
        public const string Version = "0.0.1";

        internal static ManualLogSource Log;
        internal static AIConfig Cfg;
        internal static Harmony Harmony;

        private void Awake()
        {
            Log = Logger;
            Cfg = new AIConfig(Config);
            Harmony = new Harmony(Guid);

            // Harness mode is decided by run.json before the game bootstraps, because it must redirect the save
            // folder before the player profile is read.
            Harness.CampaignHarness.EarlyInit();

            // Patches are applied once the main menu exists (Bootstrap), never here: the plugin loads before the game
            // bootstraps, and detouring methods on game types can run their static constructors too early.
            gameObject.AddComponent<Bootstrap>();
            Log.LogInfo($"{Name} {Version} loaded (harness: {Harness.CampaignHarness.Active}).");
        }

        /// <summary>Patch every [HarmonyPatch] class in this assembly, one class at a time so a broken patch doesn't take down the rest.</summary>
        internal static void ApplyPatches()
        {
            int failed = 0;
            foreach (var type in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
            {
                try { Harmony.CreateClassProcessor(type).Patch(); }
                catch (Exception e)
                {
                    failed++;
                    Log.LogError($"Patch class {type.FullName} failed: {e.InnerException?.Message ?? e.Message}");
                }
            }
            int count = 0;
            foreach (var m in Harmony.GetPatchedMethods())
            {
                count++;
                Log.LogDebug($"Patched {m.DeclaringType?.FullName}.{m.Name}");
            }
            Log.LogInfo($"{count} methods patched, {failed} patch classes failed.");
        }
    }

    /// <summary>Waits for the main menu, applies patches, then hosts the per-frame observers.</summary>
    internal class Bootstrap : MonoBehaviour
    {
        private bool _patched;

        private void Update()
        {
            if (_patched) return;
            if (FindObjectOfType<StartMenuController>() == null) return;
            _patched = true;
            Plugin.ApplyPatches();
            gameObject.AddComponent<Diagnostics.Observer>();
            if (Harness.CampaignHarness.Active)
                gameObject.AddComponent<Harness.CampaignHarness>();
        }
    }
}
