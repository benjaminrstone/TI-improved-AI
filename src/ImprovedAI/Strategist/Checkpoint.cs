using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using ImprovedAI.AI;
using ImprovedAI.Diagnostics;
using Newtonsoft.Json;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Systems.GameTime;
using UnityEngine;

namespace ImprovedAI.Strategist
{
    /// <summary>The file the strategist writes back: directives per faction template.</summary>
    public class DirectiveFile
    {
        public string checkpoint;
        public Dictionary<string, Directive> factions = new Dictionary<string, Directive>();
    }

    /// <summary>
    /// The checkpoint loop (docs/STRATEGIST.md; user decisions 2026-10-22). Every MonthsBetween months (and once right
    /// after a game loads without directives): pause the game, write briefs/&lt;date&gt;.json to the exchange folder, wait
    /// for directives/&lt;date&gt;.json from the local watcher (tools/strategist.ps1), validate and store it in the sidecar,
    /// show the player the intent of factions they have intel on, and resume at the previous speed. If nothing arrives
    /// within TimeoutMinutes, play on under the previous directives.
    /// </summary>
    internal class Checkpoint : MonoBehaviour
    {
        public static bool Waiting { get; private set; }
        private static string _pending;
        private static float _since, _lastPoll;
        private static int _resumeSpeed = 1;
        private static bool _hooked;

        public static string Folder
        {
            get
            {
                string f = Plugin.Cfg.StrategistFolder.Value;
                if (!string.IsNullOrEmpty(f)) return f;
                if (Harness.CampaignHarness.Active && !Harness.CampaignHarness.Interactive && !string.IsNullOrEmpty(Harness.CampaignHarness.Run?.outDir))
                    return Path.Combine(Harness.CampaignHarness.Run.outDir, "strategist");
                return Path.Combine(Paths.BepInExRootPath, "ImprovedAI-strategist");
            }
        }

        private void Awake()
        {
            if (_hooked) return;
            _hooked = true;
            Observer.OnNewDay += OnNewDay;
            Observer.OnCampaignReady += () => { Waiting = false; _pending = null; };
        }

        private static void OnNewDay(TIDateTime now)
        {
            if (!Directives.On || Waiting) return;
            var s = AlienStrategy.Current;
            if (s == null) return;
            bool due;
            if (string.IsNullOrEmpty(s.lastCheckpoint)) due = true;   // first time in this campaign: set initial directives
            else
            {
                var last = DateTime.Parse(s.lastCheckpoint);
                int months = (now.year - last.Year) * 12 + now.month - last.Month;
                due = now.day == 1 && months >= Mathf.Max(1, Plugin.Cfg.StrategistMonths.Value);
            }
            if (due) Begin();
        }

        /// <summary>Pause, write the brief, start waiting.</summary>
        public static string Begin()
        {
            try
            {
                var player = GameControl.control?.activePlayer;
                string date = GameRead.Now();
                string dir = Folder;
                Directory.CreateDirectory(Path.Combine(dir, "briefs"));
                Directory.CreateDirectory(Path.Combine(dir, "directives"));
                string path = Path.Combine(dir, "briefs", date + ".json");
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(Brief.Build(player), Formatting.Indented));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                var gtm = GameTimeManager.Singleton;
                _resumeSpeed = gtm != null && gtm.currentSpeedIndex > 0 ? gtm.currentSpeedIndex : 1;
                gtm?.SetSpeed(0, false);
                _pending = date;
                _since = Time.realtimeSinceStartup;
                Waiting = true;
                Plugin.Log.LogInfo($"[Strategist] checkpoint {date}: brief written to {path}; game paused, waiting for directives");
                Session.Journal?.Write(new { t = date, kind = "strategist", ev = "brief", path });
                return "> strategist checkpoint " + date;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[Strategist] checkpoint: {e}");
                Waiting = false;
                return "! strategist checkpoint failed: " + e.Message;
            }
        }

        private void Update()
        {
            if (!Waiting) return;
            float now = Time.realtimeSinceStartup;
            var gtm = GameTimeManager.Singleton;
            if (gtm != null && gtm.currentSpeedIndex != 0) gtm.SetSpeed(0, false);   // the game stays paused while the strategist thinks
            if (now - _lastPoll < 1f) return;
            _lastPoll = now;
            string path = Path.Combine(Folder, "directives", _pending + ".json");
            if (File.Exists(path)) { Apply(path); return; }
            float timeout = Plugin.Cfg.StrategistTimeoutMinutes.Value;
            if (timeout > 0f && now - _since > timeout * 60f)
            {
                Plugin.Log.LogWarning($"[Strategist] no directives for {_pending} after {timeout} min; playing on under the previous ones");
                Session.Journal?.Write(new { t = GameRead.Now(), kind = "strategist", ev = "timeout", checkpoint = _pending });
                Finish();
            }
        }

        private static void Apply(string path)
        {
            try
            {
                var file = JsonConvert.DeserializeObject<DirectiveFile>(File.ReadAllText(path));
                var s = AlienStrategy.Current;
                if (s.directives == null) s.directives = new Dictionary<string, Directive>();
                var player = GameControl.control?.activePlayer;
                var directed = GameStateManager.AllFactions().Where(f => f != null && f != player && (f.IsAlienFaction || (f.player != null && f.player.isAI)))
                    .ToDictionary(f => f.templateName, f => f);
                var applied = new List<string>();
                var ignored = new List<string>();
                foreach (var kv in file?.factions ?? new Dictionary<string, Directive>())
                {
                    if (kv.Value == null || !directed.ContainsKey(kv.Key)) { ignored.Add(kv.Key); continue; }
                    kv.Value.set = _pending;
                    s.directives[kv.Key] = kv.Value;
                    applied.Add(kv.Key);
                }
                s.lastCheckpoint = _pending;
                Plugin.Log.LogInfo($"[Strategist] {_pending}: applied directives for {string.Join(", ", applied)}{(ignored.Count > 0 ? "; ignored " + string.Join(", ", ignored) : "")}");
                Session.Journal?.Write(new { t = GameRead.Now(), kind = "strategist", ev = "applied", checkpoint = _pending, applied, ignored, directives = applied.ToDictionary(k => k, k => s.directives[k]) });
                Intel.Show(player, applied.Select(k => directed[k]).ToList());
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[Strategist] directives {path}: {e.Message}");
                Session.Journal?.Write(new { t = GameRead.Now(), kind = "strategist", ev = "bad-directives", checkpoint = _pending, error = e.Message });
                if (AlienStrategy.Current != null) AlienStrategy.Current.lastCheckpoint = _pending;   // don't re-ask at once
            }
            Finish();
        }

        private static void Finish()
        {
            Waiting = false;
            _pending = null;
            GameTimeManager.Singleton?.SetSpeed(_resumeSpeed, pushBeyondCap: Harness.CampaignHarness.Active && !Harness.CampaignHarness.Interactive);
        }
    }
}
