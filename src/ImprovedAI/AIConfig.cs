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
        public readonly ConfigEntry<bool> Strategy;
        public readonly ConfigEntry<bool> ThreatMemory;
        public readonly ConfigEntry<float> ThreatMemoryDays;
        public readonly ConfigEntry<bool> DecisiveForce;
        public readonly ConfigEntry<float> AlienAttackSuperiority;
        public readonly ConfigEntry<bool> ForcePooling;
        public readonly ConfigEntry<float> PoolMargin;
        public readonly ConfigEntry<float> PoolReserveFraction;
        public readonly ConfigEntry<float> AttackMaxRatioFloor;
        public readonly ConfigEntry<float> ShortcutSuperiority;
        public readonly ConfigEntry<bool> StanceCourage;
        public readonly ConfigEntry<float> ArrivalFightRatio;
        public readonly ConfigEntry<float> DefendHabHopelessRatio;
        public readonly ConfigEntry<bool> RaidPlanner;
        public readonly ConfigEntry<int> MaxRaidGoals;
        public readonly ConfigEntry<int> RaidImportance;
        public readonly ConfigEntry<float> RaidBombardMultiplier;
        public readonly ConfigEntry<float> RaidHateWeight;
        public readonly ConfigEntry<bool> RaidCaptures;
        public readonly ConfigEntry<int> MaxCaptureGoals;
        public readonly ConfigEntry<bool> AlienMarineFix;

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
            Strategy = cfg.Bind("Strategy", "Enabled", true,
                "Alien strategy state (doctrine, later contempt/clocks), derived per campaign and kept in a <save>.improvedai.json sidecar.");
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
            PoolReserveFraction = cfg.Bind("Commitment", "PoolReserveFraction", 0.25f,
                "Pooling may draw defenders from a system only beyond max(local human threat x superiority, this fraction of the aliens' own force there).");
            AttackMaxRatioFloor = cfg.Bind("Commitment", "AttackMaxRatioFloor", 1.5f,
                "Floor on an alien attack goal's max fleet/desired ratio (vanilla can drop it to 0, splitting waiting fleets).");
            ShortcutSuperiority = cfg.Bind("Commitment", "ShortcutSuperiority", 1.0f,
                "A fleet already in the target's system may attack below its goal's desired strength only at this ratio to the full expected threat (~even odds).");
            StanceCourage = cfg.Bind("Combat", "StanceCourage", true,
                "Aliens don't evade when defending their own habs (unless hopeless) or when an attack arrives at ~even odds.");
            ArrivalFightRatio = cfg.Bind("Combat", "ArrivalFightRatio", 0.9f,
                "An alien attack fleet fights instead of evading at this own/enemy strength ratio or better.");
            DefendHabHopelessRatio = cfg.Bind("Combat", "DefendHabHopelessRatio", 0.25f,
                "Alien fleets defending their own base/station evade only below this strength ratio.");
            RaidPlanner = cfg.Bind("Targeting", "RaidPlanner", true,
                "Every 7 days the aliens add attack goals on the most valuable, least defended human bases/stations anywhere (not only in alien turf).");
            MaxRaidGoals = cfg.Bind("Targeting", "MaxRaidGoals", 4, "Maximum raid goals the planner keeps open at once.");
            RaidImportance = cfg.Bind("Targeting", "RaidImportance", 16, "Importance (0-20) of raid goals; vanilla impudent-asset attacks use ~15.");
            RaidHateWeight = cfg.Bind("Targeting", "RaidHateWeight", 1f,
                "Raid score multiplier per war-threshold of alien hate toward the target's faction (capped at 4x the threshold).");
            RaidCaptures = cfg.Bind("Targeting", "RaidCaptures", true,
                "Raids on undefended bases become alien CaptureHab goals (marine assault; for aliens a capture destroys the base).");
            MaxCaptureGoals = cfg.Bind("Targeting", "MaxCaptureGoals", 6, "Maximum open marine-capture raids on undefended bases (separate from MaxRaidGoals).");
            AlienMarineFix = cfg.Bind("Fixes", "AlienMarineValue", true,
                "Fix vanilla bug: the alien Salamander pod lists Crashdown before Assault, so its marine value reads 0 and aliens can never assault a base.");
            RaidBombardMultiplier = cfg.Bind("Targeting", "RaidBombardMultiplier", 2f, "Raid goals want this multiple of vanilla's desired bombardment value.");
        }

        public bool UseStrategy => !VanillaAI.Value && Strategy.Value;
        public bool UseThreatMemory => !VanillaAI.Value && ThreatMemory.Value;
        public bool UseDecisiveForce => !VanillaAI.Value && DecisiveForce.Value;
        public bool UseForcePooling => !VanillaAI.Value && ForcePooling.Value;
        public bool UseRaidPlanner => !VanillaAI.Value && RaidPlanner.Value;
        public bool UseStanceCourage => !VanillaAI.Value && StanceCourage.Value;
        public bool UseAlienMarineFix => !VanillaAI.Value && AlienMarineFix.Value;
        public bool UseRaidCaptures => UseRaidPlanner && RaidCaptures.Value && UseAlienMarineFix;

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
