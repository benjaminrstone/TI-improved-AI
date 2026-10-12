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
        public readonly ConfigEntry<bool> VanillaAI;
        public readonly ConfigEntry<bool> ThreatMemory;
        public readonly ConfigEntry<float> ThreatMemoryDays;
        public readonly ConfigEntry<bool> DecisiveForce;
        public readonly ConfigEntry<float> AlienAttackSuperiority;
        public readonly ConfigEntry<bool> ForcePooling;
        public readonly ConfigEntry<float> PoolMargin;
        public readonly ConfigEntry<float> DefenderSurplusFactor;
        public readonly ConfigEntry<float> AttackMaxRatioFloor;

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

            VanillaAI = cfg.Bind("General", "VanillaAI", false,
                "Master switch: true turns every AI change off (diagnostics still run). Used for vanilla baselines.");
            ThreatMemory = cfg.Bind("Perception", "ThreatMemory", true,
                "Aliens judge human fleets by their best recent per-ship SCV, not the currently fitted design (defeats bait refits / empty magazines).");
            ThreatMemoryDays = cfg.Bind("Perception", "ThreatMemoryDays", 180f,
                "How many game days a fleet's best showing is remembered.");
            DecisiveForce = cfg.Bind("Commitment", "DecisiveForce", true,
                "Aliens size attacks to win decisively against remembered defenders and nearby fleets, not to just beat current SCV.");
            AlienAttackSuperiority = cfg.Bind("Commitment", "AlienAttackSuperiority", 2.0f,
                "Required ratio of attacking fleet SCV to expected defender strength.");
            ForcePooling = cfg.Bind("Commitment", "ForcePooling", true,
                "Aliens pool idle fleets and surplus defenders into ONE focus attack at a time, instead of one starving fleet per goal.");
            PoolMargin = cfg.Bind("Commitment", "PoolMargin", 1.1f,
                "Pool until the focus fleet reaches this multiple of its goal's desired SCV.");
            DefenderSurplusFactor = cfg.Bind("Commitment", "DefenderSurplusFactor", 1.25f,
                "A defending fleet may be pooled only if its SCV exceeds this multiple of its own goal's desired SCV.");
            AttackMaxRatioFloor = cfg.Bind("Commitment", "AttackMaxRatioFloor", 1.5f,
                "Floor on an alien attack goal's max fleet/desired ratio (vanilla can drop it to 0, splitting waiting fleets).");
        }

        public bool UseThreatMemory => !VanillaAI.Value && ThreatMemory.Value;
        public bool UseDecisiveForce => !VanillaAI.Value && DecisiveForce.Value;
        public bool UseForcePooling => !VanillaAI.Value && ForcePooling.Value;

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
