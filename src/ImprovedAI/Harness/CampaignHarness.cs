using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using ImprovedAI.Diagnostics;
using ImprovedAI.Util;
using Newtonsoft.Json;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Assets;
using PavonisInteractive.TerraInvicta.Debugging;
using PavonisInteractive.TerraInvicta.Systems.Bootstrap;
using PavonisInteractive.TerraInvicta.Systems.GameTime;
using UnityEngine;

#pragma warning disable CS0649 // run.json fields are set by the JSON deserializer
namespace ImprovedAI.Harness
{
    /// <summary>
    /// Unattended strategic-layer runner (L1 probes / L2 soaks). Enabled by a run.json that tools/run.ps1 drops into
    /// the plugin folder. It loads a save, runs the clock at max speed with the game's own Autopilot clearing prompts
    /// and auto-resolving combat, executes setup commands, records diagnostics, and quits after N game days.
    /// Asserts are evaluated afterwards by tools/probe.py over the run's output, so the plugin stays dumb.
    /// </summary>
    internal class CampaignHarness : MonoBehaviour
    {
        public class RunRequest
        {
            public string name = "run";
            /// <summary>Absolute path of the save to load (a copy; the launcher never hands us the user's original).</summary>
            public string save;
            public double runDays = 30;
            public float maxRealSeconds = 3600f;
            public int speedIndex = 5;
            public bool quitOnFinish = true;
            /// <summary>Absolute output folder for this run (journal, metrics, ai-log, results, heartbeat).</summary>
            public string outDir;
            /// <summary>Absolute folder the game treats as its save folder for this launch (keeps the user's saves untouched).</summary>
            public string savesDir;
            /// <summary>Debug-terminal commands to run once the campaign is ready (e.g. "addresource Money,1000").</summary>
            public List<string> commands = new List<string>();
            /// <summary>Config overrides for this run only: section → key → value.</summary>
            public Dictionary<string, Dictionary<string, string>> config;
            /// <summary>Let the game write autosaves (100 MB each; off by default for speed).</summary>
            public bool allowSaves;
            /// <summary>Save the final state as &lt;outDir&gt;/final.gz.</summary>
            public bool finalSave;
            public float heartbeatSeconds = 10f;
            /// <summary>Real seconds without the game date advancing before we call it a stall (screenshot + note).</summary>
            public float stallSeconds = 90f;
            public int targetFrameRate = -1;
            /// <summary>Give up after this many stalls in a row (each is stallSeconds without the date moving).</summary>
            public int maxConsecutiveStalls = 4;
            /// <summary>
            /// Chapter playtest (tools/playtest.ps1): keep the save-folder isolation and diagnostics, but a person plays.
            /// No Autopilot, no speed forcing, no stall handling, no quit; saves allowed (into savesDir). `save` is
            /// optional: without it the player loads from the (isolated) load menu.
            /// </summary>
            public bool interactive;
        }

        public static bool Interactive => Active && Run.interactive;

        public static bool Active { get; private set; }
        public static RunRequest Run { get; private set; }
        public static string OutDir => Run?.outDir;
        internal static bool AllowSaveNow;

        private static string RunFile => Path.Combine(Paths.PluginPath, "ImprovedAI", "run.json");

        /// <summary>Called from Plugin.Awake, before the game reads its profile.</summary>
        public static void EarlyInit()
        {
            if (!File.Exists(RunFile)) return;
            try
            {
                Run = JsonConvert.DeserializeObject<RunRequest>(File.ReadAllText(RunFile));
                if ((string.IsNullOrEmpty(Run.save) && !Run.interactive) || string.IsNullOrEmpty(Run.outDir) || string.IsNullOrEmpty(Run.savesDir))
                    throw new Exception("run.json needs save, outDir and savesDir");
                if (Run.interactive) Run.allowSaves = true;
                Directory.CreateDirectory(Run.outDir);
                Directory.CreateDirectory(Run.savesDir);
                // Everything the game reads or writes "in the save folder" (profile, autosaves, autopilot saves,
                // combat autosaves) goes to savesDir for this launch.
                string dir = Run.savesDir.Replace("\\", "/").TrimEnd('/') + "/";
                AccessTools.Field(typeof(CreateSaveFileScrollList), "savedGamesPath").SetValue(null, dir);
                Active = true;
                Plugin.Cfg.ApplyOverrides(Run.config);
                Plugin.Log.LogInfo($"[Harness] Run '{Run.name}': save={Run.save} days={Run.runDays} out={Run.outDir} saves={dir}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[Harness] Bad run.json, harness disabled: {e}");
                Active = false;
                // Unattended runs must not idle at the menu until the launcher's timeout: report and quit.
                if (Run != null && !Run.interactive && !string.IsNullOrEmpty(Run.outDir))
                {
                    try
                    {
                        Directory.CreateDirectory(Run.outDir);
                        File.WriteAllText(Path.Combine(Run.outDir, "results.json"), Jsonl.Serialize(new { endReason = "error: bad run.json: " + e.Message, save = Run.save }, indented: true));
                    }
                    catch { }
                    Application.Quit();
                }
            }
        }

        private enum Phase { Menu, Loading, Running, Done }
        private Phase _phase = Phase.Menu;
        private float _menuSeenAt = -1f, _loadRequestedAt, _readyAt, _lastHeartbeat, _lastDateChangeAt;
        private TIDateTime _startDate;
        private string _lastDate;
        private int _stalls, _consecutiveStalls, _unsticks;
        private float _lastUnstick;
        private readonly List<string> _exceptions = new List<string>();
        private int _exceptionCount;
        private readonly List<string> _terminal = new List<string>();
        private readonly List<string> _scheduled = new List<string>();
        private Autopilot _autopilot;

        private void Start()
        {
            Application.logMessageReceived += OnLog;
            Observer.OnCampaignReady += OnCampaignReady;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            Observer.OnCampaignReady -= OnCampaignReady;
        }

        private void OnLog(string condition, string stack, LogType type)
        {
            if (type != LogType.Exception) return;
            _exceptionCount++;
            if (_exceptions.Count < 30)
                _exceptions.Add(condition + " @ " + (stack ?? "").Split('\n').FirstOrDefault());
        }

        private void Update()
        {
            try
            {
                if (Run.interactive)
                {
                    // A person plays: only the optional first load is automated.
                    if (_phase == Phase.Menu && !string.IsNullOrEmpty(Run.save)) PollMenu();
                    return;
                }
                switch (_phase)
                {
                    case Phase.Menu: PollMenu(); break;
                    case Phase.Loading:
                        if (Time.realtimeSinceStartup - _loadRequestedAt > 600f) Finish("error: load timed out");
                        break;
                    case Phase.Running: Tick(); break;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[Harness] {e}");
                Finish("error: " + e.Message);
            }
        }

        private void PollMenu()
        {
            var menu = FindObjectOfType<StartMenuController>();
            if (menu == null) { _menuSeenAt = -1f; return; }
            if (_menuSeenAt < 0f) { _menuSeenAt = Time.realtimeSinceStartup; return; }
            if (Time.realtimeSinceStartup - _menuSeenAt < 3f) return;

            var loader = Resources.FindObjectsOfTypeAll<LoadMenuController>().FirstOrDefault(x => x.gameObject.scene.isLoaded);
            if (loader == null) { Finish("error: LoadMenuController not found"); return; }
            if (!File.Exists(Run.save)) { Finish("error: save not found: " + Run.save); return; }
            Plugin.Log.LogInfo($"[Harness] Loading {Run.save}");
            _phase = Phase.Loading;
            _loadRequestedAt = Time.realtimeSinceStartup;
            loader.LoadSaveFilePath(Run.save);
        }

        private void OnCampaignReady()
        {
            if (Run.interactive)
            {
                // Setup commands once, on the first campaign the player opens; nothing else is automated.
                if (_phase == Phase.Done) return;
                _phase = Phase.Done;
                Plugin.Log.LogInfo($"[Harness] Playtest campaign ready at {GameRead.Now()}.");
                RunCommands();
                return;
            }
            if (_phase != Phase.Loading) return;
            _phase = Phase.Running;
            _readyAt = _lastHeartbeat = _lastDateChangeAt = Time.realtimeSinceStartup;
            _startDate = TITimeState.Now();
            _lastDate = GameRead.Date(_startDate);
            Plugin.Log.LogInfo($"[Harness] Campaign ready at {_lastDate} after {_readyAt - _loadRequestedAt:F0}s load.");

            // Unattended: no exception dialog (we log them), keep running without focus, uncap the frame rate.
            GameControl.handlingException = true;
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Run.targetFrameRate;

            RunCommands();

            _autopilot = new GameObject("ImprovedAI-Autopilot").AddComponent<Autopilot>();
            _autopilot.IgnoreExeptions = true;
            _autopilot.Activated = true;
        }

        private void RunCommands()
        {
            if (Run.commands == null || Run.commands.Count == 0) return;
            var gameCommands = new List<string>();
            foreach (var raw in Run.commands)
            {
                // "@N command" runs N game days after the start (see OnNewDay).
                string cmd = raw.Trim();
                if (cmd.StartsWith("@")) { _scheduled.Add(cmd); continue; }
                Plugin.Log.LogInfo($"[Harness] command: {cmd}");
                _terminal.Add("$ " + cmd);
                if (!HarnessCommands.TryRun(cmd, _terminal)) gameCommands.Add(cmd);
            }
            if (gameCommands.Count == 0) return;
            Terminal terminal = null;
            try { terminal = GlobalInstaller.container.Resolve<Terminal>(); }
            catch (Exception e) { Plugin.Log.LogError($"[Harness] No terminal: {e.Message}"); }
            if (terminal == null) return;
            Action<string> outp = s => _terminal.Add("> " + s);
            Action<string> err = s => _terminal.Add("! " + s);
            terminal.controller.OnOutput += outp;
            terminal.controller.OnOutputError += err;
            try
            {
                foreach (var cmd in gameCommands)
                {
                    try { terminal.controller.ParseCommand(cmd); }
                    catch (Exception e) { _terminal.Add("! " + e.Message); }
                }
            }
            finally
            {
                terminal.controller.OnOutput -= outp;
                terminal.controller.OnOutputError -= err;
            }
        }

        private void RunScheduled()
        {
            if (_scheduled.Count == 0) return;
            double days = DaysElapsed();
            foreach (var line in _scheduled.ToList())
            {
                int sp = line.IndexOf(' ');
                if (sp < 0 || !double.TryParse(line.Substring(1, sp - 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var at)) { _scheduled.Remove(line); continue; }
                if (days < at) continue;
                _scheduled.Remove(line);
                string cmd = line.Substring(sp + 1).Trim();
                Plugin.Log.LogInfo($"[Harness] scheduled (day {days:F0}): {cmd}");
                _terminal.Add($"$ [day {days:F0}] {cmd}");
                if (!HarnessCommands.TryRun(cmd, _terminal))
                {
                    try { GlobalInstaller.container.Resolve<Terminal>().controller.ParseCommand(cmd); }
                    catch (Exception e) { _terminal.Add("! " + e.Message); }
                }
            }
        }

        private double DaysElapsed()
        {
            try { return TITimeState.Now().DifferenceInDays(_startDate); } catch { return 0; }
        }

        private void Tick()
        {
            var gtm = GameTimeManager.Singleton;
            if (gtm != null && gtm.currentSpeedIndex != 0 && gtm.currentSpeedIndex != Run.speedIndex)
                gtm.SetSpeed(Run.speedIndex, pushBeyondCap: true);

            float now = Time.realtimeSinceStartup;
            string date = GameRead.Now();
            if (date != _lastDate) { _lastDate = date; _lastDateChangeAt = now; _consecutiveStalls = 0; }
            else if (now - _lastDateChangeAt > 20f && now - _lastUnstick > 20f && BlockingPrompts().Contains("faction:PromptSelectCouncilorMissions"))
            {
                // The Autopilot sometimes fails to confirm the mission phase (seen at 2039-08-01); do what the button does.
                _lastUnstick = now;
                _unsticks++;
                Plugin.Log.LogInfo($"[Harness] Finalizing councilor missions for the stuck mission phase at {date}");
                var p = GameControl.control.activePlayer;
                p.playerControl.StartAction(new PavonisInteractive.TerraInvicta.Actions.FinalizeCouncilorMissions(p));
            }
            else if (now - _lastDateChangeAt > Run.stallSeconds)
            {
                _stalls++;
                _lastDateChangeAt = now;
                string shot = Path.Combine(Run.outDir, $"stall-{_stalls}.png");
                ScreenCapture.CaptureScreenshot(shot);
                var prompts = BlockingPrompts();
                Plugin.Log.LogWarning($"[Harness] Stall #{_stalls} at {date} (speed idx {gtm?.currentSpeedIndex}); prompts: {string.Join(", ", prompts)}; screenshot {shot}");
                Session.Journal?.Write(new { t = date, kind = "stall", n = _stalls, speed = gtm?.currentSpeedIndex, prompts });
                if (++_consecutiveStalls >= Run.maxConsecutiveStalls) { Finish("stalled: " + string.Join(", ", prompts)); return; }
            }

            if (now - _lastHeartbeat >= Run.heartbeatSeconds)
            {
                _lastHeartbeat = now;
                File.WriteAllText(Path.Combine(Run.outDir, "heartbeat.json"), Jsonl.Serialize(Status("running")));
            }

            RunScheduled();
            if (DaysElapsed() >= Run.runDays) Finish("days-elapsed");
            else if (now - _readyAt > Run.maxRealSeconds) Finish("real-time-limit");
        }

        /// <summary>The active player's pending prompts (what the Autopilot failed to clear), for stall diagnosis.</summary>
        private static List<string> BlockingPrompts()
        {
            var names = new List<string>();
            try
            {
                var q = GameStateManager.PromptQueue();
                foreach (var pr in q.activePlayerFactionPromptList) names.Add("faction:" + pr.name);
                foreach (var pr in q.activePlayerNationPromptList) names.Add("nation:" + pr.name);
                names.Add("anyActivePlayerBlocking=" + TIPromptQueueState.anyActivePlayerBlockingPrompt);
            }
            catch (Exception e) { names.Add("error:" + e.Message); }
            return names;
        }

        private object Status(string state)
        {
            double days = DaysElapsed();
            float real = Time.realtimeSinceStartup - _readyAt;
            return new
            {
                state,
                name = Run.name,
                start = GameRead.Date(_startDate),
                date = GameRead.Now(),
                gameDays = Math.Round(days, 2),
                realSeconds = Math.Round(real, 1),
                gameDaysPerRealMinute = real > 1 ? Math.Round(days / (real / 60.0), 2) : 0,
                speedIndex = GameTimeManager.Singleton?.currentSpeedIndex,
                fps = Math.Round(1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 1),
                exceptions = _exceptionCount,
                stalls = _stalls,
                unsticks = _unsticks,
            };
        }

        private void Finish(string reason)
        {
            if (_phase == Phase.Done) return;
            var prev = _phase;
            _phase = Phase.Done;
            Plugin.Log.LogInfo($"[Harness] Finished: {reason}");
            if (_autopilot != null) _autopilot.Activated = false;
            object metrics = null;
            if (prev == Phase.Running)
            {
                try { metrics = Diagnostics.Metrics.Snapshot(); Session.Metrics?.Write(metrics); } catch (Exception e) { Plugin.Log.LogWarning($"[Harness] final metrics: {e.Message}"); }
                if (Run.finalSave)
                {
                    AllowSaveNow = true;
                    try { GameStateManager.SaveAllGameStates(Path.Combine(Run.outDir, "final.gz"), doNotOpenSaveMenu: true); }
                    catch (Exception e) { Plugin.Log.LogWarning($"[Harness] final save: {e.Message}"); }
                    AllowSaveNow = false;
                }
            }
            var result = new
            {
                status = prev == Phase.Running ? Status("finished") : null,
                endReason = reason,
                save = Run.save,
                aiLogCalls = AILogPatch.Calls,
                savesBlocked = SavePatches.Blocked,
                exceptionSamples = _exceptions,
                terminal = _terminal,
            };
            try { File.WriteAllText(Path.Combine(Run.outDir, "results.json"), Jsonl.Serialize(result, indented: true)); }
            catch (Exception e) { Plugin.Log.LogError($"[Harness] results: {e}"); }
            Session.End();
            if (Run.quitOnFinish) Application.Quit();
        }
    }
}
