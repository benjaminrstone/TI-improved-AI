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
        /// <summary>Set by the watcher when no faction could be answered (its Claude login expired, say).</summary>
        public string error;
        public Dictionary<string, Directive> factions = new Dictionary<string, Directive>();
    }

    /// <summary>
    /// The checkpoint loop. Every MonthsBetween months (and once right after a game loads without directives): pause the
    /// game, write briefs/&lt;date&gt;.json to the exchange folder, wait for directives/&lt;date&gt;.json from the local
    /// watcher (tools/strategist.ps1), validate and store it in the sidecar, show the player the intent of factions they
    /// have intel on, and resume at the previous speed. While it waits the player sees why the game is paused and, after
    /// a short while, a button to resume. If nothing arrives within TimeoutMinutes, or the player resumes, play goes on
    /// under the previous directives.
    /// </summary>
    internal class Checkpoint : MonoBehaviour
    {
        public static bool Waiting { get; private set; }
        private static string _pending;
        private static float _since, _lastPoll;
        private static int _resumeSpeed = 1;
        private static bool _hooked;
        private static GUIStyle _box, _button;
        private static Texture2D _boxFill;
        private const float ResumeButtonAfterSeconds = 30f;
        private const float FailureShownSeconds = 20f;
        private static string _failure;
        private static float _failureAt;

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
                // An answer left from an earlier visit to this date (a reload, another campaign in the same folder)
                // would be applied at once. The watcher answers any brief that has no directives file.
                File.Delete(Path.Combine(dir, "directives", date + ".json"));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                var gtm = GameTimeManager.Singleton;
                _resumeSpeed = gtm != null && gtm.currentSpeedIndex > 0 ? gtm.currentSpeedIndex : 1;
                gtm?.SetSpeed(0, false);
                _pending = date;
                _since = Time.realtimeSinceStartup;
                Waiting = true;
                Harness.CampaignHarness.ShootSoon("checkpoint");
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

        private void Update() => PatchGuard.Run(Poll, "Checkpoint.Update");

        private void Poll()
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
            if (timeout > 0f && now - _since > timeout * 60f) GiveUp("timeout");
        }

        private static void GiveUp(string why, string error = null)
        {
            Plugin.Log.LogWarning($"[Strategist] no directives for {_pending} ({why}{(error != null ? ": " + error : "")}); playing on under the previous ones");
            Session.Journal?.Write(new { t = GameRead.Now(), kind = "strategist", ev = why, checkpoint = _pending, error });
            if (AlienStrategy.Current != null) AlienStrategy.Current.lastCheckpoint = _pending;   // wait for the next checkpoint, don't re-ask tomorrow
            Finish();
        }

        private void OnGUI() => PatchGuard.Run(Notice, "Checkpoint.OnGUI");

        private void Notice()
        {
            if (Harness.CampaignHarness.Quiet) return;
            float sinceFailure = Time.realtimeSinceStartup - _failureAt;
            bool failed = !Waiting && _failure != null && sinceFailure < FailureShownSeconds;
            if (!Waiting && !failed) return;
            if (_box == null || _boxFill == null)
            {
                // An opaque panel: the map's labels show through Unity's default box.
                _boxFill = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                _boxFill.SetPixel(0, 0, new Color(0.05f, 0.07f, 0.1f, 0.94f));
                _boxFill.Apply();
                _box = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
                _box.normal.background = _boxFill;
                _box.normal.textColor = Color.white;
                _button = new GUIStyle(GUI.skin.button);
            }
            float k = Mathf.Max(1f, Screen.height / 1080f);   // sized for 1080p, larger on larger screens
            _box.fontSize = Mathf.RoundToInt(18f * k);
            _button.fontSize = Mathf.RoundToInt(15f * k);
            float waited = Time.realtimeSinceStartup - _since;
            var r = new Rect(Screen.width / 2f - 300f * k, 96f * k, 600f * k, 72f * k);
            if (failed)
            {
                r.height = 108f * k;
                GUI.Label(r, $"The strategist did not answer: {_failure}\nThe other factions carry on under their last orders.", _box);
                return;
            }
            GUI.Label(r, $"Paused: the other factions are planning their next moves ({waited:F0} s).\nThe game resumes by itself.", _box);
            if (waited > ResumeButtonAfterSeconds && GUI.Button(new Rect(r.xMax - 150f * k, r.yMax + 4f * k, 150f * k, 30f * k), "Resume now", _button)) GiveUp("resumed-by-player");
        }

        private static void Apply(string path)
        {
            try
            {
                var file = JsonConvert.DeserializeObject<DirectiveFile>(File.ReadAllText(path));
                if (!string.IsNullOrEmpty(file?.error))
                {
                    _failure = file.error.Length > 160 ? file.error.Substring(0, 160) : file.error;
                    _failureAt = Time.realtimeSinceStartup;
                    Harness.CampaignHarness.ShootSoon("strategist-failed");
                    GiveUp("failed", file.error);
                    return;
                }
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
