using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using ImprovedAI.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PavonisInteractive.TerraInvicta;

namespace ImprovedAI.AI
{
    public enum StrategicFocus { FortressIndustrial, Bastion, PuppetMaster, QualityOverQuantity, Xenoformer, LandGrab, Raider, EarthPresence, Hoarder, Strangler, DivideAndRule, Blitz, HunterKiller }
    public enum WeaponFocus { Balanced, Missiles, Beams, Particles, Kinetics }
    public enum HullPhilosophy { Balanced, Swarm, Capital, Motherships }
    public enum EngagementStyle { Balanced, Artillery, Brawler, Assault, Carrier }

    public class Doctrine
    {
        [JsonConverter(typeof(StringEnumConverter))] public StrategicFocus focus;
        /// <summary>0 = force (fleets), 1 = puppetry (councilors, Servants, the Alien Administration).</summary>
        public float puppetry;
        /// <summary>0 = centralised command (rally at home), 1 = forward command (rally near the front).</summary>
        public float forwardCommand;
        [JsonConverter(typeof(StringEnumConverter))] public WeaponFocus weapons;
        [JsonConverter(typeof(StringEnumConverter))] public HullPhilosophy hulls;
        [JsonConverter(typeof(StringEnumConverter))] public EngagementStyle engagement;

        public override string ToString() => $"{focus} / puppetry {puppetry:F2} / forward {forwardCommand:F2} / {weapons} {hulls} {engagement}";
    }

    /// <summary>Everything the mod adds to one campaign. Stored in a sidecar beside the saves (AlienStrategy).</summary>
    public class StrategyState
    {
        public int version = 1;
        public string campaignKey;
        /// <summary>derived | sidecar | forced</summary>
        public string source;
        public Doctrine doctrine;
        /// <summary>Per human faction template name, 0..1.</summary>
        public Dictionary<string, float> contempt = new Dictionary<string, float>();
        public List<string> history = new List<string>();
        /// <summary>Doctrinal shame, 0..threshold; past the threshold the command abandons its strategic focus.</summary>
        public float shame;
        /// <summary>How many times the focus has been abandoned this campaign (seeds the next choice).</summary>
        public int shifts;
        /// <summary>Results under the current doctrine, one row per game month (rolling window, DoctrineShift).</summary>
        public List<LedgerMonth> ledger = new List<LedgerMonth>();
        /// <summary>How close the Hydra command thinks it is to losing, 0..1 (Escalation).</summary>
        public float desperation;
        public bool escalated;
        public string escalatedSince;
        public int calmMonths;
        /// <summary>Dates (yyyy-MM-dd) of alien habs lost to humans, kept for 12 months.</summary>
        public List<string> habLosses = new List<string>();
        /// <summary>The terminus: start date, progress 0..1, milestones announced so far.</summary>
        public string terminusStarted;
        public float terminusProgress;
        public int terminusMilestones;
        public bool terminusComplete;
        /// <summary>Proxy faction template -> month (yyyy-MM) of the last alien-initiated trade (ProxyTrade).</summary>
        public Dictionary<string, string> lastProxyTrade = new Dictionary<string, string>();
        /// <summary>Human factions (Human/Personality): client faction template -> patron faction template.</summary>
        public Dictionary<string, string> clients = new Dictionary<string, string>();
        /// <summary>Human factions: grudge memory "victim>actor" (decayed sum of injury hate), per personality half-life.</summary>
        public Dictionary<string, float> grudges = new Dictionary<string, float>();
        /// <summary>The strategist's directives in force, per faction template, and the last checkpoint date.</summary>
        public Dictionary<string, Strategist.Directive> directives = new Dictionary<string, Strategist.Directive>();
        public string lastCheckpoint;
        /// <summary>Goal IDs of the open raids the RaidPlanner created.</summary>
        public HashSet<int> raidGoals = new HashSet<int>();
        /// <summary>Attack goal IDs whose muster point has been chosen, and the escalation state it was chosen under (ForcePooling).</summary>
        public HashSet<int> stagedGoals = new HashSet<int>();
        public bool stagedEscalated;
        /// <summary>Attack goal ID -> the date (yyyy-MM-dd) its pooled force became complete at the muster point.</summary>
        public Dictionary<int, string> musterStart = new Dictionary<int, string>();
        /// <summary>What each human ship has shown the aliens, per ship ID (ThreatMemory).</summary>
        public Dictionary<int, List<ThreatObs>> threat = new Dictionary<int, List<ThreatObs>>();
    }

    public class LedgerMonth
    {
        public string month;
        public int alienShipsLost, humanShipsKilled, alienHabsLost, humanHabsKilled;
    }

    /// <summary>
    /// Chooses the alien doctrine for a campaign and persists the mod's state in sidecars, one per saved moment:
    /// &lt;save folder&gt;/ImprovedAI/&lt;campaign&gt;/&lt;game time&gt;.json, written whenever the game saves. A sidecar is found by
    /// the loaded game's own time, not by the save's file name, because the game renames its autosaves (Autosave to
    /// Autosave2 to Autosave3, TIMissionPhaseState.ContinueNewMissionPhase) and a player may rename or copy saves.
    /// Without a sidecar (new game, save made without the mod) the doctrine is derived deterministically from
    /// CampaignKey. The save file itself is never touched.
    /// With VanillaAI on there is no state, but a campaign's sidecar is carried through saves unchanged, so the
    /// campaign picks up where it was when VanillaAI is turned off again.
    /// </summary>
    internal static class AlienStrategy
    {
        public static StrategyState Current { get; private set; }
        internal static string LastLoadedSave;
        private static string _carried;

        private static readonly JsonSerializerSettings Json = new JsonSerializerSettings { Formatting = Formatting.None };

        /// <summary>Doctrine pool per vanilla difficulty (1 Cinematic .. 4 Brutal); harsher doctrines only on higher levels.</summary>
        internal static List<StrategicFocus> Pool(int difficulty)
        {
            var pool = new List<StrategicFocus> { StrategicFocus.FortressIndustrial, StrategicFocus.Bastion, StrategicFocus.PuppetMaster, StrategicFocus.QualityOverQuantity, StrategicFocus.Xenoformer };
            if (difficulty >= 2) pool.AddRange(new[] { StrategicFocus.LandGrab, StrategicFocus.Raider, StrategicFocus.EarthPresence, StrategicFocus.Hoarder, StrategicFocus.Strangler, StrategicFocus.DivideAndRule });
            if (difficulty >= 3) pool.AddRange(new[] { StrategicFocus.Blitz, StrategicFocus.HunterKiller });
            return pool;
        }

        internal static string CampaignKey()
        {
            // TIGlobalValuesState.realWorldCampaignStart is the real-world time the campaign was created: identical in
            // every save of one campaign, different between campaigns.
            var t = TIGlobalValuesState.GlobalValues.realWorldCampaignStart;
            string created = t == null ? "?" : $"{t.year:D4}{t.month:D2}{t.day:D2}T{t.hour:D2}{t.minute:D2}{t.second:D2}.{t.millisecond:D3}";
            return $"{GameStateManager.Time().template?.dataName}|{created}";
        }

        internal static int StableHash(string s)
        {
            unchecked
            {
                int h = (int)2166136261;
                foreach (char c in s ?? "") h = (h ^ c) * 16777619;
                return h;
            }
        }

        internal static Doctrine Derive(string key, int difficulty)
        {
            var rng = new Random(StableHash(key));
            var pool = Pool(difficulty);
            return new Doctrine
            {
                focus = pool[rng.Next(pool.Count)],
                puppetry = (float)Math.Round(rng.NextDouble(), 2),
                forwardCommand = (float)Math.Round(rng.NextDouble(), 2),
                weapons = (WeaponFocus)rng.Next(Enum.GetValues(typeof(WeaponFocus)).Length),
                hulls = (HullPhilosophy)rng.Next(Enum.GetValues(typeof(HullPhilosophy)).Length),
                engagement = (EngagementStyle)rng.Next(Enum.GetValues(typeof(EngagementStyle)).Length),
            };
        }

        /// <summary>Where the sidecar of the game as it stands now belongs, given any save in the same folder.</summary>
        private static string SidecarPath(string save, string key)
        {
            if (string.IsNullOrEmpty(save)) return null;
            var t = TITimeState.Now();
            return Path.Combine(Path.GetDirectoryName(save), "ImprovedAI", StableHash(key).ToString("x8"),
                $"{t.year:D4}{t.month:D2}{t.day:D2}-{t.hour:D2}{t.minute:D2}{t.second:D2}.json");
        }

        /// <summary>Called by the Observer when a campaign becomes ready (after a load or a new game).</summary>
        public static void OnCampaignReady()
        {
            Current = null;
            _carried = null;
            string key = CampaignKey();
            int difficulty = TIGlobalValuesState.GlobalValues.difficulty;
            string side = SidecarPath(LastLoadedSave, key);
            try
            {
                if (side != null && File.Exists(side))
                {
                    string text = File.ReadAllText(side);
                    var s = JsonConvert.DeserializeObject<StrategyState>(text, Json);
                    if (s == null || s.campaignKey != key) Plugin.Log.LogWarning($"[Strategy] Sidecar {side} belongs to another campaign ({s?.campaignKey} vs {key}); deriving.");
                    else if (Plugin.Cfg.UseStrategy) { s.source = "sidecar"; Current = s; }
                    else _carried = text;
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[Strategy] Bad sidecar {side}: {e.Message}; deriving."); }
            if (!Plugin.Cfg.UseStrategy) return;

            if (Current == null)
                Current = new StrategyState { campaignKey = key, source = "derived", doctrine = Derive(key, difficulty) };
            Plugin.Log.LogInfo($"[Strategy] Doctrine ({Current.source}): {Current.doctrine}");
            Disposition.OnCampaignReady();
            Session.Journal?.Write(new { t = GameRead.Now(), kind = "doctrine", ev = "loaded", Current.source, difficulty, doctrine = Current.doctrine, key, dials = Dials.Snapshot() });
            FleetDoctrine.RequestRedesign("doctrine loaded");
        }

        /// <summary>Probe/debug override: "focus=Raider weapons=Missiles puppetry=0.2 ...".</summary>
        public static string Force(string spec)
        {
            if (Current == null) return "! no strategy state (is VanillaAI on, or no campaign loaded?)";
            var d = Current.doctrine;
            foreach (var kv in spec.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = kv.Split('=');
                if (p.Length != 2) return "! bad doctrine spec " + kv;
                switch (p[0].ToLowerInvariant())
                {
                    case "focus": d.focus = (StrategicFocus)Enum.Parse(typeof(StrategicFocus), p[1], true); break;
                    case "weapons": d.weapons = (WeaponFocus)Enum.Parse(typeof(WeaponFocus), p[1], true); break;
                    case "hulls": d.hulls = (HullPhilosophy)Enum.Parse(typeof(HullPhilosophy), p[1], true); break;
                    case "engagement": d.engagement = (EngagementStyle)Enum.Parse(typeof(EngagementStyle), p[1], true); break;
                    case "puppetry": d.puppetry = float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture); break;
                    case "forward": d.forwardCommand = float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture); break;
                    default: return "! unknown doctrine axis " + p[0];
                }
            }
            Current.source = "forced";
            Current.history.Add($"{GameRead.Now()} forced: {d}");
            Session.Journal?.Write(new { t = GameRead.Now(), kind = "doctrine", ev = "forced", doctrine = d, dials = Dials.Snapshot() });
            FleetDoctrine.RequestRedesign("doctrine forced");
            return "> doctrine " + d;
        }

        internal static void WriteSidecar(string save)
        {
            string text = Current != null ? JsonConvert.SerializeObject(Current, Json) : _carried;
            if (text == null || string.IsNullOrEmpty(save)) return;
            try
            {
                string side = SidecarPath(save, CampaignKey());
                Directory.CreateDirectory(Path.GetDirectoryName(side));
                File.WriteAllText(side, text);
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[Strategy] Could not write sidecar for {save}: {e.Message}"); }
        }
    }

    /// <summary>Remember which save is being loaded (every load path goes through SaveStructure.Load).</summary>
    [HarmonyPatch(typeof(SaveStructure), nameof(SaveStructure.Load))]
    internal static class StrategyLoadHook
    {
        private static void Prefix(string filepath) => AlienStrategy.LastLoadedSave = filepath;
    }

    /// <summary>Write the sidecar whenever the game really saves (not when the harness blocked the save).</summary>
    [HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.SaveAllGameStates))]
    internal static class StrategySaveHook
    {
        private static void Postfix(string filepath, bool __result)
        {
            if (!__result) return;
            if (Harness.CampaignHarness.Active && !Harness.CampaignHarness.Run.allowSaves && !Harness.CampaignHarness.AllowSaveNow) return;
            AlienStrategy.WriteSidecar(filepath);
        }
    }
}
