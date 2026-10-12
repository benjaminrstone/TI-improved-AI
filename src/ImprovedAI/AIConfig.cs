using System.IO;
using BepInEx;
using BepInEx.Configuration;

namespace ImprovedAI
{
    /// <summary>
    /// Every feature sits behind its own switch so probes and A/B runs can toggle it. The harness can override any
    /// entry for one run (run.json "config"), without writing the .cfg.
    /// </summary>
    internal class AIConfig
    {
        public readonly ConfigFile File;
        public readonly ConfigEntry<bool> AILog;
        public readonly ConfigEntry<bool> DecisionJournal;
        public readonly ConfigEntry<bool> Metrics;
        public readonly ConfigEntry<string> LogDir;

        public AIConfig(ConfigFile cfg)
        {
            File = cfg;
            AILog = cfg.Bind("Diagnostics", "AILog", true,
                "Write the game's own AI trace (TIFactionState.LogAI, a no-op in vanilla) to ai-log.txt.");
            DecisionJournal = cfg.Bind("Diagnostics", "DecisionJournal", true,
                "Write alien decisions (goals, fleet transfers, habs founded/lost) to journal.jsonl, one line per event.");
            Metrics = cfg.Bind("Diagnostics", "Metrics", true,
                "Write one metrics row per game month (resources, habs, fleets, hate, alien goals) to metrics.jsonl.");
            LogDir = cfg.Bind("Diagnostics", "LogDir", "",
                "Folder for diagnostics output. Empty = BepInEx/ImprovedAI-logs/<session>. The harness overrides this per run.");
        }

        /// <summary>Apply section → key → value overrides for this run only (not saved).</summary>
        public void ApplyOverrides(System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>> overrides)
        {
            if (overrides == null) return;
            File.SaveOnConfigSet = false;
            foreach (var section in overrides)
                foreach (var kv in section.Value)
                {
                    var def = new ConfigDefinition(section.Key, kv.Key);
                    if (File.TryGetEntry<bool>(def, out var b)) b.Value = bool.Parse(kv.Value);
                    else if (File.TryGetEntry<float>(def, out var f)) f.Value = float.Parse(kv.Value, System.Globalization.CultureInfo.InvariantCulture);
                    else if (File.TryGetEntry<int>(def, out var i)) i.Value = int.Parse(kv.Value);
                    else if (File.TryGetEntry<string>(def, out var s)) s.Value = kv.Value;
                    else Plugin.Log.LogWarning($"Config override for unknown entry [{section.Key}] {kv.Key}");
                }
        }

        internal static string DefaultLogRoot => Path.Combine(Paths.BepInExRootPath, "ImprovedAI-logs");
    }
}
