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
    // ---- Doctrine axes (DESIGN P3; doctrines are combinations of axes, agreed 2026-10-12) ----
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

    /// <summary>Everything the mod adds to the alien mind for one campaign. Stored in a sidecar next to the save.</summary>
    public class StrategyState
    {
        public int version = 1;
        public string campaignKey;
        /// <summary>derived | sidecar | forced</summary>
        public string source;
        public Doctrine doctrine;
        /// <summary>Per human faction template name, 0..1 (DESIGN "Disposition"); filled in by M2 step 5.</summary>
        public Dictionary<string, float> contempt = new Dictionary<string, float>();
        public List<string> history = new List<string>();
    }

    /// <summary>
    /// M2 step 1 (docs/M2-PLAN.md). Chooses the alien doctrine for a campaign and persists it.
    /// - With a sidecar (&lt;save&gt;.improvedai.json, written whenever the game saves) the state is loaded as saved.
    /// - Without one (old save, new game, save made without the mod) the doctrine is DERIVED deterministically from the
    ///   campaign (start template + start date + alien faction ID), so the same save always yields the same doctrine.
    /// The save file itself is never touched: vanilla can always load it.
    /// </summary>
    internal static class AlienStrategy
    {
        public static StrategyState Current { get; private set; }
        internal static string LastLoadedSave;

        private static readonly JsonSerializerSettings Json = new JsonSerializerSettings { Formatting = Formatting.Indented };

        /// <summary>Doctrine pool per vanilla difficulty (1 Cinematic .. 4 Brutal); harsher doctrines only on higher levels (user, 2026-10-12).</summary>
        internal static List<StrategicFocus> Pool(int difficulty)
        {
            var pool = new List<StrategicFocus> { StrategicFocus.FortressIndustrial, StrategicFocus.Bastion, StrategicFocus.PuppetMaster, StrategicFocus.QualityOverQuantity, StrategicFocus.Xenoformer };
            if (difficulty >= 2) pool.AddRange(new[] { StrategicFocus.LandGrab, StrategicFocus.Raider, StrategicFocus.EarthPresence, StrategicFocus.Hoarder, StrategicFocus.Strangler, StrategicFocus.DivideAndRule });
            if (difficulty >= 3) pool.AddRange(new[] { StrategicFocus.Blitz, StrategicFocus.HunterKiller });
            return pool;
        }

        internal static string CampaignKey()
        {
            var time = GameStateManager.Time();
            var start = time.template;
            var now = TITimeState.Now();
            var alien = GameStateManager.AlienFaction();
            // The start date is "now - daysInCampaign"; combined with the template and the alien faction's ID this is
            // stable for the whole life of a campaign and differs between campaigns.
            int startDay = now.year * 372 + now.month * 31 + now.day - time.daysInCampaign;
            return $"{start?.dataName}|{startDay}|{GameRead.Id(alien)}";
        }

        private static int StableHash(string s)
        {
            unchecked
            {
                int h = (int)2166136261;
                foreach (char c in s) h = (h ^ c) * 16777619;
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

        private static string SidecarPath(string save) => string.IsNullOrEmpty(save) ? null : save + ".improvedai.json";

        /// <summary>Called by the Observer when a campaign becomes ready (after a load or a new game).</summary>
        public static void OnCampaignReady()
        {
            Current = null;
            if (!Plugin.Cfg.UseStrategy) return;
            string key = CampaignKey();
            int difficulty = TIGlobalValuesState.GlobalValues.difficulty;
            string side = SidecarPath(LastLoadedSave);
            try
            {
                if (side != null && File.Exists(side))
                {
                    var s = JsonConvert.DeserializeObject<StrategyState>(File.ReadAllText(side), Json);
                    if (s != null && s.campaignKey == key) { s.source = "sidecar"; Current = s; }
                    else Plugin.Log.LogWarning($"[Strategy] Sidecar {side} belongs to another campaign ({s?.campaignKey} vs {key}); deriving.");
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[Strategy] Bad sidecar {side}: {e.Message}; deriving."); }

            if (Current == null)
                Current = new StrategyState { campaignKey = key, source = "derived", doctrine = Derive(key, difficulty) };
            Plugin.Log.LogInfo($"[Strategy] Doctrine ({Current.source}): {Current.doctrine}");
            Session.Journal?.Write(new { t = GameRead.Now(), kind = "doctrine", ev = "loaded", Current.source, difficulty, doctrine = Current.doctrine, key });
        }

        /// <summary>Probe/debug override: "focus=Raider weapons=Missiles puppetry=0.2 ...".</summary>
        public static string Force(string spec)
        {
            if (Current == null) return "! no strategy state (is [Strategy] Enabled on and a campaign loaded?)";
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
            Session.Journal?.Write(new { t = GameRead.Now(), kind = "doctrine", ev = "forced", doctrine = d });
            return "> doctrine " + d;
        }

        internal static void WriteSidecar(string save)
        {
            if (Current == null || string.IsNullOrEmpty(save)) return;
            try { File.WriteAllText(SidecarPath(save), JsonConvert.SerializeObject(Current, Json)); }
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
            if (!__result || !Plugin.Cfg.UseStrategy) return;
            if (Harness.CampaignHarness.Active && !Harness.CampaignHarness.Run.allowSaves && !Harness.CampaignHarness.AllowSaveNow) return;
            AlienStrategy.WriteSidecar(filepath);
        }
    }
}
