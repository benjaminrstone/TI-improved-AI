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
        public readonly ConfigEntry<bool> Disposition;
        public readonly ConfigEntry<float> StartingContempt;
        public readonly ConfigEntry<string> ContemptDifficultyOffset;
        public readonly ConfigEntry<string> ContemptLearning;
        public readonly ConfigEntry<bool> DoctrineShift;
        public readonly ConfigEntry<int> ShameWindowMonths;
        public readonly ConfigEntry<float> ShameHabWeight;
        public readonly ConfigEntry<float> ShameMinActivity;
        public readonly ConfigEntry<float> ShameTolerance;
        public readonly ConfigEntry<float> ShameRate;
        public readonly ConfigEntry<float> ShameDecay;
        public readonly ConfigEntry<float> ShameThreshold;
        public readonly ConfigEntry<float> ShiftOppositeWeight;
        public readonly ConfigEntry<bool> FleetDoctrine;
        public readonly ConfigEntry<float> WeaponFocusOdds;
        public readonly ConfigEntry<float> WeaponFitBonus;
        public readonly ConfigEntry<float> HullSizeBias;
        public readonly ConfigEntry<float> MothershipFitBonus;
        public readonly ConfigEntry<float> EngagementRoleWeight;
        public readonly ConfigEntry<float> AssaultCaptureSlots;
        public readonly ConfigEntry<bool> MineWhatYouLack;
        public readonly ConfigEntry<float> NeedWeightMax;
        public readonly ConfigEntry<float> ResourceBaseNeedThreshold;
        public readonly ConfigEntry<bool> NoEarthPurchases;
        public readonly ConfigEntry<bool> ExoticsUpkeep;
        public readonly ConfigEntry<float> BaseExoticsUpkeep;
        public readonly ConfigEntry<float> CapitalShipExoticsUpkeep;
        public readonly ConfigEntry<float> HugeShipExoticsUpkeep;
        public readonly ConfigEntry<float> ExoticsStockHorizonMonths;
        public readonly ConfigEntry<int> MaxAlienBasesCeiling;
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
            Disposition = cfg.Bind("Strategy", "Disposition", true,
                "Per-human-faction alien contempt (falls when the faction beats alien expectations); scales attack sizing against that faction.");
            StartingContempt = cfg.Bind("Strategy", "StartingContempt", 0.8f, "Initial alien contempt for every human faction (0..1), at Normal difficulty.");
            ContemptDifficultyOffset = cfg.Bind("Strategy", "ContemptDifficultyOffset", "0.15,0,-0.1,-0.2",
                "Added to StartingContempt per difficulty (Cinematic,Normal,Veteran,Brutal): arrogant on easy, wary on hard (user, 2026-10-12).");
            ContemptLearning = cfg.Bind("Strategy", "ContemptLearning", "0.7,1,1.25,1.5",
                "Multiplier on contempt LOSSES per difficulty (Cinematic,Normal,Veteran,Brutal): how fast the aliens learn respect.");
            DoctrineShift = cfg.Bind("Strategy", "DoctrineShift", true,
                "Doctrinal shame (P6): a strategic focus that keeps losing its exchanges is abandoned, late, for another.");
            ShameWindowMonths = cfg.Bind("Strategy", "ShameWindowMonths", 6, "Months of results the command judges its doctrine on.");
            ShameHabWeight = cfg.Bind("Strategy", "ShameHabWeight", 5f, "A hab lost or destroyed counts as this many ships in the exchange.");
            ShameMinActivity = cfg.Bind("Strategy", "ShameMinActivity", 4f, "Below this many ship-equivalents lost + destroyed in the window, the doctrine is not judged.");
            ShameTolerance = cfg.Bind("Strategy", "ShameTolerance", 0.55f, "Loss share (lost / (lost + destroyed)) above which shame grows.");
            ShameRate = cfg.Bind("Strategy", "ShameRate", 1f, "Shame per month = (loss share - tolerance) x this.");
            ShameDecay = cfg.Bind("Strategy", "ShameDecay", 0.05f, "Shame lost per month when the doctrine is not failing.");
            ShameThreshold = cfg.Bind("Strategy", "ShameThreshold", 1f, "Shame at which the focus is abandoned.");
            ShiftOppositeWeight = cfg.Bind("Strategy", "ShiftOppositeWeight", 3f, "Weight of opposite-temperament foci (aggressive <-> defensive) when shifting.");
            FleetDoctrine = cfg.Bind("Fleet", "FleetDoctrine", true,
                "Bias alien ship designs and build picks by the doctrine's weapon focus, hull philosophy and engagement style (M2 step 4).");
            WeaponFocusOdds = cfg.Bind("Fleet", "WeaponFocusOdds", 3f,
                "Odds of the doctrine's weapon class against the role's own preferred class, when a mount fits it (vanilla roles lock weapon class).");
            WeaponFitBonus = cfg.Bind("Fleet", "WeaponFitBonus", 1f,
                "When the aliens compare designs (keep/retire, build pick), a design armed entirely with the doctrine's class counts as (1 + this) x its combat value.");
            HullSizeBias = cfg.Bind("Fleet", "HullSizeBias", 2f,
                "Build-pick size bias: Swarm favours small hulls by this factor per size step, Capital and Motherships favour large hulls.");
            MothershipFitBonus = cfg.Bind("Fleet", "MothershipFitBonus", 0.5f,
                "Motherships doctrine: a huge-hull design counts as (1 + this) x its combat value when designs are compared.");
            EngagementRoleWeight = cfg.Bind("Fleet", "EngagementRoleWeight", 3f,
                "Artillery multiplies the weight of long-range combat roles by this; Brawler, short-range roles.");
            AssaultCaptureSlots = cfg.Bind("Fleet", "AssaultCaptureSlots", 1.5f,
                "Assault engagement style multiplies the marine-capture raid slots by this.");
            MineWhatYouLack = cfg.Bind("Economy", "MineWhatYouLack", true,
                "Alien base sites are scored by the need for what they yield (what the fleet is built from vs what is stockpiled), not mostly gross income.");
            NeedWeightMax = cfg.Bind("Economy", "NeedWeightMax", 4f, "Need weights per resource are clamped to [1/this, this].");
            ResourceBaseNeedThreshold = cfg.Bind("Economy", "ResourceBaseNeedThreshold", 2f,
                "The planner's 'resource base when upkeep is insecure' rule targets resources at or above this need weight (instead of defaulting to water).");
            NoEarthPurchases = cfg.Bind("Economy", "NoEarthPurchases", true,
                "Aliens can't substitute boost + money from Earth for missing space resources (user, 2026-10-13): they build from what they mine.");
            ExoticsUpkeep = cfg.Bind("Economy", "ExoticsUpkeep", true,
                "Alien bases and capital hulls cost exotics each month, and the fixed alien base cap becomes what the exotics drip and stockpile can carry.");
            BaseExoticsUpkeep = cfg.Bind("Economy", "BaseExoticsUpkeep", 3f, "Exotics per alien base per month.");
            CapitalShipExoticsUpkeep = cfg.Bind("Economy", "CapitalShipExoticsUpkeep", 0.5f, "Exotics per large-hull alien ship per month.");
            HugeShipExoticsUpkeep = cfg.Bind("Economy", "HugeShipExoticsUpkeep", 2f, "Exotics per huge-hull alien ship (mothership) per month.");
            ExoticsStockHorizonMonths = cfg.Bind("Economy", "ExoticsStockHorizonMonths", 60f,
                "The base cap counts the exotics stockpile as an extra monthly budget of stock / this.");
            MaxAlienBasesCeiling = cfg.Bind("Economy", "MaxAlienBasesCeiling", 40, "Hard ceiling on the exotics-based alien base cap.");
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
        public bool UseDisposition => UseStrategy && Disposition.Value;
        public bool UseFleetDoctrine => UseStrategy && FleetDoctrine.Value;
        public bool UseDoctrineShift => UseStrategy && DoctrineShift.Value;
        public bool UseMineWhatYouLack => !VanillaAI.Value && MineWhatYouLack.Value;
        public bool UseExoticsUpkeep => !VanillaAI.Value && ExoticsUpkeep.Value;
        public bool UseNoEarthPurchases => !VanillaAI.Value && NoEarthPurchases.Value;
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
