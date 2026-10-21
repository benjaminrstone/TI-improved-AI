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
        public readonly ConfigEntry<bool> ProxyTrade;
        public readonly ConfigEntry<int> ProxyTradeIntervalMonths;
        public readonly ConfigEntry<float> ProxyTradeNeed, ProxyTradeMargin;
        public readonly ConfigEntry<float> EscalatedMaxSuperiority;
        public readonly ConfigEntry<float> EscalatedJoinReserve;
        public readonly ConfigEntry<float> EscalatedExoticsFitBonus;
        public readonly ConfigEntry<float> ExoticsPressureGain;
        public readonly ConfigEntry<bool> Terminus;
        public readonly ConfigEntry<string> TerminusCost;
        public readonly ConfigEntry<float> TerminusDripShare, TerminusStartContempt, TerminusSetbackBattle, TerminusSetbackHab;
        public readonly ConfigEntry<bool> Escalation;
        public readonly ConfigEntry<float> EscalationThreatHabs, EscalationRespectContempt, EscalationLossHabs, EscalationEscalateAt, EscalationCalmAt, EscalationBoldness, EscalationDripShareFactor;
        public readonly ConfigEntry<int> EscalationCalmMonths;
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
        public readonly ConfigEntry<bool> Widening;
        public readonly ConfigEntry<string> WideningPerYear;
        public readonly ConfigEntry<float> WideningMax;
        public readonly ConfigEntry<bool> ExoticsUpkeep;
        public readonly ConfigEntry<float> BaseExoticsUpkeep;
        public readonly ConfigEntry<float> SustainableDripShare;
        public readonly ConfigEntry<float> CapitalShipExoticsUpkeep;
        public readonly ConfigEntry<float> HugeShipExoticsUpkeep;
        public readonly ConfigEntry<float> ExoticsStockHorizonMonths;
        public readonly ConfigEntry<bool> ExoticsWarChest;
        public readonly ConfigEntry<float> ExoticsWarChestMonths, ExoticsHoardMonths;
        public readonly ConfigEntry<int> MaxAlienBasesCeiling;
        public readonly ConfigEntry<bool> ThreatMemory;
        public readonly ConfigEntry<float> ThreatMemoryDays;
        public readonly ConfigEntry<bool> DecisiveForce;
        public readonly ConfigEntry<float> AlienAttackSuperiority;
        public readonly ConfigEntry<bool> ForcePooling;
        public readonly ConfigEntry<float> PoolMargin;
        public readonly ConfigEntry<float> PoolReserveFraction;
        public readonly ConfigEntry<float> AttackMaxRatioFloor;
        public readonly ConfigEntry<bool> Rendezvous;
        public readonly ConfigEntry<float> RendezvousGain;
        public readonly ConfigEntry<float> RendezvousMaxJoinDays, RendezvousDvShare;
        public readonly ConfigEntry<bool> PoolTimeAware;
        public readonly ConfigEntry<float> MusterDays;
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
        public readonly ConfigEntry<bool> AlienBattlecruiserFix;
        public readonly ConfigEntry<bool> FixDirectInvest, FixLosingBig, FixOrgBuying, FixMissionRisk, FixSavingDeadlock, FixTradeZeroStock, HumanMarketWater;
        public readonly ConfigEntry<int> MaxOrgBuyPasses;
        public readonly ConfigEntry<bool> HumanMissionControl, HumanBoostForShips, HumanMineWhatYouLack, HumanBiggerHulls, HumanQueueUnblock;
        public readonly ConfigEntry<bool> HumanMCBuildings, HumanFleetMCReserveOn, HumanPersonalities, HumanClients, HumanMarket, HumanDrainGuard;
        public readonly ConfigEntry<float> HumanMarketShortStock, HumanMarketSurplusStock, HumanMarketShareOfSurplus, HumanMarketMargin, HumanMarketMaxHate;
        public readonly ConfigEntry<int> HumanMCBuildingsPerMonth;
        public readonly ConfigEntry<float> HumanFleetMCReserve, HumanMCUpkeepMonths, HumanMCUpgradeOffline;
        public readonly ConfigEntry<float> HumanMissionControlValue, HumanBoostReserve, HumanSmallsFactor, HumanMediumsFactor, HumanUpsizeDays;

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
            ProxyTrade = cfg.Bind("Economy", "ProxyTrade", true,
                "The aliens offer resource trades to pro-alien human factions (their supply line from Earth): what they lack for what they have in surplus.");
            ProxyTradeIntervalMonths = cfg.Bind("Economy", "ProxyTradeIntervalMonths", 3, "Months between alien offers to each proxy (Puppet master and Divide and rule: every month).");
            ProxyTradeNeed = cfg.Bind("Economy", "ProxyTradeNeed", 2f, "The aliens ask for resources at or above this need weight.");
            ProxyTradeMargin = cfg.Bind("Economy", "ProxyTradeMargin", 1.15f, "The partner must gain this multiple of what it gives (vanilla trade valuation): honest, attractive deals.");
            EscalatedMaxSuperiority = cfg.Bind("Strategy", "EscalatedMaxSuperiority", 1.2f,
                "Escalated (P4), attack superiority is capped here: a desperate command accepts closer fights (user, 2026-10-17: relax when escalated).");
            EscalatedJoinReserve = cfg.Bind("Strategy", "EscalatedJoinReserve", 0.2f,
                "Escalated, fleets joining a pooled strike keep only this delta-v reserve when picking a trajectory (vanilla 0.5): faster assembly, more propellant spent.");
            EscalatedExoticsFitBonus = cfg.Bind("Strategy", "EscalatedExoticsFitBonus", 0.5f,
                "Escalated (P4 surge): exotics (wormhole-tier) designs count as (1 + this) x their combat value when the aliens compare and pick designs.");
            ExoticsPressureGain = cfg.Bind("Economy", "ExoticsPressureGain", 2f,
                "Exotics-aware shipbuilding: with upkeep above the drip by a fraction s, large hulls weigh (1 - gain x s) in the build pick (min 0.25).");
            Terminus = cfg.Bind("Strategy", "Terminus", true,
                "The full wormhole terminus (hard clock): once begun, funded from the exotics drip; completion is an alien victory (user, 2026-10-12).");
            TerminusCost = cfg.Bind("Strategy", "TerminusCost", "3000,2400,2000,1600",
                "Exotics to complete the terminus, per difficulty (Cinematic,Normal,Veteran,Brutal). Normal: ~8-10 years at 30% of the drip.");
            TerminusDripShare = cfg.Bind("Strategy", "TerminusDripShare", 0.3f, "Share of the monthly exotics drip invested in the terminus once begun.");
            TerminusStartContempt = cfg.Bind("Strategy", "TerminusStartContempt", 0.35f, "The terminus begins when contempt for any human faction falls to this, or on escalation.");
            TerminusSetbackBattle = cfg.Bind("Strategy", "TerminusSetbackBattle", 0.05f, "Progress lost per alien defeat in the terminus (alien HQ) system.");
            TerminusSetbackHab = cfg.Bind("Strategy", "TerminusSetbackHab", 0.1f, "Progress lost per alien hab destroyed in the terminus system.");
            Escalation = cfg.Bind("Strategy", "Escalation", true,
                "P4: when desperate (humans in the gas-giant heartland, systems lost, respect earned, habs lost) the aliens escalate to total war and accept risk.");
            EscalationThreatHabs = cfg.Bind("Strategy", "EscalationThreatHabs", 4f, "Human habs in the gas-giant systems that count as the full threat.");
            EscalationRespectContempt = cfg.Bind("Strategy", "EscalationRespectContempt", 0.35f, "Contempt below which respect starts adding to desperation (0 contempt = full).");
            EscalationLossHabs = cfg.Bind("Strategy", "EscalationLossHabs", 4f, "Alien habs lost to humans in 12 months that count as the full loss.");
            EscalationEscalateAt = cfg.Bind("Strategy", "EscalationEscalateAt", 0.55f, "Desperation at which the aliens escalate.");
            EscalationCalmAt = cfg.Bind("Strategy", "EscalationCalmAt", 0.4f, "Desperation below which an escalated command starts to calm.");
            EscalationCalmMonths = cfg.Bind("Strategy", "EscalationCalmMonths", 6, "Months below CalmAt before escalation ends.");
            EscalationBoldness = cfg.Bind("Strategy", "EscalationBoldness", 0.25f,
                "Escalated: attack superiority x(1-b), arrival fight ratio x(1-0.6b), home reserve x(1-2b), raid/capture slots and hate weight x(1+2b).");
            EscalationDripShareFactor = cfg.Bind("Strategy", "EscalationDripShareFactor", 0.5f, "Escalated: the base cap commits this fraction of its usual drip share (the rest goes to war).");
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
            HullSizeBias = cfg.Bind("Fleet", "HullSizeBias", 3f,
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
            Widening = cfg.Bind("Economy", "Widening", true,
                "The widening wormhole (P2 soft clock): the alien exotics drip grows with campaign years.");
            WideningPerYear = cfg.Bind("Economy", "WideningPerYear", "0.03,0.05,0.07,0.09",
                "Drip growth per calendar campaign year, per difficulty (Cinematic,Normal,Veteran,Brutal): drip x (1 + this x years).");
            WideningMax = cfg.Bind("Economy", "WideningMax", 3f, "Cap on the widening factor.");
            ExoticsUpkeep = cfg.Bind("Economy", "ExoticsUpkeep", true,
                "Alien bases and capital hulls cost exotics each month, and the fixed alien base cap becomes what the exotics drip and stockpile can carry.");
            BaseExoticsUpkeep = cfg.Bind("Economy", "BaseExoticsUpkeep", 2f, "Exotics per alien base per month.");
            SustainableDripShare = cfg.Bind("Economy", "SustainableDripShare", 0.75f,
                "Share of the exotics drip the base cap may commit to upkeep (bases + capital hulls); the rest is kept for exotic warships.");
            CapitalShipExoticsUpkeep = cfg.Bind("Economy", "CapitalShipExoticsUpkeep", 0.5f, "Exotics per large-hull alien ship per month.");
            HugeShipExoticsUpkeep = cfg.Bind("Economy", "HugeShipExoticsUpkeep", 2f, "Exotics per huge-hull alien ship (mothership) per month.");
            ExoticsStockHorizonMonths = cfg.Bind("Economy", "ExoticsStockHorizonMonths", 240f,
                "The base cap counts the exotics stockpile as an extra monthly budget of stock / this.");
            MaxAlienBasesCeiling = cfg.Bind("Economy", "MaxAlienBasesCeiling", 40, "Hard ceiling on the exotics-based alien base cap.");
            ExoticsWarChest = cfg.Bind("Economy", "ExoticsWarChest", true,
                "Outside escalation the aliens keep a war chest of exotics: exotic-tier builds use only the stock above it, and capital-hull building tightens as the hoard above it runs down. Needs ExoticsUpkeep.");
            ExoticsWarChestMonths = cfg.Bind("Economy", "ExoticsWarChestMonths", 12f, "War chest size, in months of the exotics drip.");
            ExoticsHoardMonths = cfg.Bind("Economy", "ExoticsHoardMonths", 60f,
                "Upkeep may run above the drip by (stock above the war chest) / this many months before capital-hull building tightens.");
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
            Rendezvous = cfg.Bind("Commitment", "Rendezvous", true,
                "A pooled strike musters at the best-placed alien fleet by time to strike (slowest needed joiner + transfer to target), not wherever vanilla anchored it.");
            RendezvousGain = cfg.Bind("Commitment", "RendezvousGain", 0.85f, "Re-anchor only if the new muster point's time to strike is below this fraction of the current one.");
            RendezvousMaxJoinDays = cfg.Bind("Commitment", "RendezvousMaxJoinDays", 240f,
                "The join horizon: fleets that can't reach a muster point within this many days don't count toward it (muster choice), and with PoolTimeAware aren't recruited or counted toward a pooled strike.");
            PoolTimeAware = cfg.Bind("Commitment", "PoolTimeAware", true,
                "A pooled strike recruits donors in order of arrival time, only within the join horizon, and counts joiners toward its need only if they arrive within it (far joiners still come, as reinforcements).");
            RendezvousDvShare = cfg.Bind("Commitment", "RendezvousDvShare", 0.8f,
                "Share of a fleet's delta-v the travel-time estimate assumes it spends (calibrated on six 2039 joins: the game's planner spent 74-83%).");
            MusterDays = cfg.Bind("Commitment", "MusterDays", 14f, "Days a complete pooled force musters (coordinating) before setting out.");
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
            FixDirectInvest = cfg.Bind("HumanAI", "FixDirectInvest", true,
                "Vanilla bug H1: AI direct investment never fires (operator precedence makes the influence budget the literal 0.1/0.2).");
            FixLosingBig = cfg.Bind("HumanAI", "FixLosingBig", true, "Vanilla bug H2: 'LosingBig' self-assessment is unreachable (tested after 'Losing').");
            FixOrgBuying = cfg.Bind("HumanAI", "FixOrgBuying", false, "Vanilla bug H3: AI org buying stops at the first conflicting candidate (break instead of continue).");
            MaxOrgBuyPasses = cfg.Bind("HumanAI", "MaxOrgBuyPasses", 3, "Extra org-buying passes after a pass that bought something.");
            FixMissionRisk = cfg.Bind("HumanAI", "FixMissionRisk", true, "Vanilla bug H4: the high-payoff mission risk relaxation reads payoff before it is assigned.");
            FixSavingDeadlock = cfg.Bind("HumanAI", "FixSavingDeadlock", true,
                "Vanilla bug H5: savings banked for a ship queued behind another build (it cannot start for months) starve every ship that could start now. While the target is idle, other ships may tap them.");
            FixTradeZeroStock = cfg.Bind("HumanAI", "FixTradeZeroStock", true,
                "Vanilla bug H6: a faction holding none of a resource values receiving it at almost nothing (divide by zero in TradeAI's storage modifier). AI human valuers only.");
            HumanMissionControl = cfg.Bind("HumanAI", "MissionControl", true,
                "AI humans starved of generic mission control while wanting warships invest in it (MC priority / spaceflight programme) and value MC income more.");
            HumanMissionControlValue = cfg.Bind("HumanAI", "MissionControlValue", 3f, "While starved, AI valuation of MC income is multiplied by this.");
            HumanPersonalities = cfg.Bind("HumanAI", "Personalities", true,
                "Gang-up on the leader by faction personality (docs/HUMAN-AI.md 9): hate toward the strongest faction scales with its lead, grudges from injuries, alien-aware weighting.");
            HumanClients = cfg.Bind("HumanAI", "ClientAllies", true,
                "Compatible weaker factions become clients of the leader (no jealousy either way, a non-aggression pact allowed). Needs Personalities.");
            HumanDrainGuard = cfg.Bind("HumanAI", "DrainGuard", true,
                "While an AI human's net hab flow of metals or noble metals is negative, it doesn't found stations (they can't mine; their modules and crew draw metals). Bases stay allowed.");
            HumanMarket = cfg.Bind("HumanAI", "Market", true,
                "AI humans short of metals or noble metals for warships buy them with money from other AI humans with a surplus (vanilla AIs never trade with each other).");
            HumanMarketWater = cfg.Bind("HumanAI", "MarketWater", true,
                "The market also sells water (propellant is in every ship's build cost; water is the most common ship-queue shortfall in 2034).");
            HumanMarketShortStock = cfg.Bind("HumanAI", "MarketShortStock", 2000f, "A buyer is short below this stock of a ship resource.");
            HumanMarketSurplusStock = cfg.Bind("HumanAI", "MarketSurplusStock", 8000f, "A seller keeps this much; only stock above it is for sale.");
            HumanMarketShareOfSurplus = cfg.Bind("HumanAI", "MarketShareOfSurplus", 0.3f, "Share of a seller's surplus sold in one deal.");
            HumanMarketMargin = cfg.Bind("HumanAI", "MarketMargin", 1.1f, "The seller must gain this times what it gives up (vanilla trade valuation).");
            HumanMarketMaxHate = cfg.Bind("HumanAI", "MarketMaxHate", 40f, "No deals between factions hating each other more than this (or at war).");
            HumanMCBuildings = cfg.Bind("HumanAI", "MCBuildings", true,
                "MC-starved AI humans build the best MC building they can afford on developed habs that have none (vanilla can't: module upgrades need generic MC).");
            HumanMCBuildingsPerMonth = cfg.Bind("HumanAI", "MCBuildingsPerMonth", 4, "MC buildings started or upgraded (Ops -> Command Center) per faction per month at most.");
            HumanMCUpkeepMonths = cfg.Bind("HumanAI", "MCUpkeepMonths", 24f,
                "An MC building's money upkeep must be covered by net money income, or by a stock covering this many months of it.");
            HumanMCUpgradeOffline = cfg.Bind("HumanAI", "MCUpgradeOfflineShare", 0.15f,
                "An Ops Center being upgraded to a Command Center produces no MC for the 240-day build: MC offline in upgrades is capped at this share of MC income.");
            HumanFleetMCReserveOn = cfg.Bind("HumanAI", "FleetMCReserve", true,
                "While warships are wanted, founding new habs may only use generic MC beyond FleetMCReserveShare x MC income (user, 2026-10-19).");
            HumanFleetMCReserve = cfg.Bind("HumanAI", "FleetMCReserveShare", 0.3f, "Share of MC income reserved for warships against new hab founding.");
            HumanBoostForShips = cfg.Bind("HumanAI", "BoostForShips", true,
                "AI humans wanting warships may pay a ship's missing space resources with boost + money (Earth-system yards) down to BoostReserve.");
            HumanBoostReserve = cfg.Bind("HumanAI", "BoostReserve", 150f, "Boost kept in reserve when paying ships from Earth.");
            HumanMineWhatYouLack = cfg.Bind("HumanAI", "MineWhatYouLack", true, "AI human hab sites are scored by need (the aliens' need weighting, per faction).");
            HumanBiggerHulls = cfg.Bind("HumanAI", "BiggerHulls", false,
                "AI human build picks favour larger hulls (scaled size weights) and upsize to a larger design of the same role when affordable.");
            HumanSmallsFactor = cfg.Bind("HumanAI", "SmallsFactor", 0.33f, "Multiplier on vanilla fleetSmalls (Large weight is fixed at 1).");
            HumanMediumsFactor = cfg.Bind("HumanAI", "MediumsFactor", 0.6f, "Multiplier on vanilla fleetMediums.");
            HumanUpsizeDays = cfg.Bind("HumanAI", "UpsizeDays", 120f, "Upsize only to designs the faction can pay for within this many days.");
            HumanQueueUnblock = cfg.Bind("HumanAI", "QueueUnblock", true,
                "One unaffordable ship no longer stops the AI's whole ship-queue loop, nor every other shipyard's start.");
            AlienBattlecruiserFix = cfg.Bind("Fixes", "AlienBattlecruiserName", true,
                "Fix vanilla bug: the alien designer asks for hull 'AlienBattleCruiser' (data name 'AlienBattlecruiser'), so the exotics medium interceptor gets the wrong hull.");
            RaidBombardMultiplier = cfg.Bind("Targeting", "RaidBombardMultiplier", 2f, "Raid goals want this multiple of vanilla's desired bombardment value.");
        }

        public bool UseStrategy => !VanillaAI.Value && Strategy.Value;
        public bool UseDisposition => UseStrategy && Disposition.Value;
        public bool UseFleetDoctrine => UseStrategy && FleetDoctrine.Value;
        public bool UseDoctrineShift => UseStrategy && DoctrineShift.Value;
        public bool UseEscalation => UseStrategy && Escalation.Value;
        public bool UseProxyTrade => UseStrategy && ProxyTrade.Value;
        public bool UseTerminus => UseStrategy && Terminus.Value;
        public bool UseMineWhatYouLack => !VanillaAI.Value && MineWhatYouLack.Value;
        public bool UseExoticsUpkeep => !VanillaAI.Value && ExoticsUpkeep.Value;
        public bool UseNoEarthPurchases => !VanillaAI.Value && NoEarthPurchases.Value;
        public bool UseWidening => !VanillaAI.Value && Widening.Value;
        public bool UseThreatMemory => !VanillaAI.Value && ThreatMemory.Value;
        public bool UseDecisiveForce => !VanillaAI.Value && DecisiveForce.Value;
        public bool UseForcePooling => !VanillaAI.Value && ForcePooling.Value;
        public bool UseRaidPlanner => !VanillaAI.Value && RaidPlanner.Value;
        public bool UseStanceCourage => !VanillaAI.Value && StanceCourage.Value;
        public bool UseAlienMarineFix => !VanillaAI.Value && AlienMarineFix.Value;
        public bool UseAlienBattlecruiserFix => !VanillaAI.Value && AlienBattlecruiserFix.Value;
        public bool UseFixDirectInvest => !VanillaAI.Value && FixDirectInvest.Value;
        public bool UseFixLosingBig => !VanillaAI.Value && FixLosingBig.Value;
        public bool UseFixOrgBuying => !VanillaAI.Value && FixOrgBuying.Value;
        public bool UseFixMissionRisk => !VanillaAI.Value && FixMissionRisk.Value;
        public bool UseFixSavingDeadlock => !VanillaAI.Value && FixSavingDeadlock.Value;
        public bool UseFixTradeZeroStock => !VanillaAI.Value && FixTradeZeroStock.Value;
        public bool UseHumanMissionControl => !VanillaAI.Value && HumanMissionControl.Value;
        public bool UseHumanBoostForShips => !VanillaAI.Value && HumanBoostForShips.Value;
        public bool UseHumanMineWhatYouLack => !VanillaAI.Value && HumanMineWhatYouLack.Value;
        public bool UseHumanBiggerHulls => !VanillaAI.Value && HumanBiggerHulls.Value;
        public bool UseHumanQueueUnblock => !VanillaAI.Value && HumanQueueUnblock.Value;
        public bool UseHumanMCBuildings => !VanillaAI.Value && HumanMCBuildings.Value;
        public bool UseHumanFleetMCReserve => !VanillaAI.Value && HumanFleetMCReserveOn.Value;
        public bool UseHumanPersonalities => !VanillaAI.Value && HumanPersonalities.Value;
        public bool UseHumanClients => UseHumanPersonalities && HumanClients.Value;
        public bool UseHumanMarket => !VanillaAI.Value && HumanMarket.Value;
        public bool UseHumanDrainGuard => !VanillaAI.Value && HumanDrainGuard.Value;
        public bool UseRaidCaptures => UseRaidPlanner && RaidCaptures.Value && UseAlienMarineFix;

        /// <summary>Apply section → key → value overrides for this run only (not saved).</summary>
        public void ApplyOverrides(System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>> overrides)
        {
            if (overrides == null) return;
            File.SaveOnConfigSet = false;
            foreach (var section in overrides)
                foreach (var kv in section.Value)
                {
                    // TryGetEntry<T> throws (not false) when the entry has another type, so go through the untyped entry.
                    var def = new ConfigDefinition(section.Key, kv.Key);
                    if (File.ContainsKey(def)) File[def].SetSerializedValue(kv.Value);
                    else Plugin.Log.LogWarning($"Config override for unknown entry [{section.Key}] {kv.Key}");
                }
        }

        internal static string DefaultLogRoot => Path.Combine(Paths.BepInExRootPath, "ImprovedAI-logs");
    }
}
