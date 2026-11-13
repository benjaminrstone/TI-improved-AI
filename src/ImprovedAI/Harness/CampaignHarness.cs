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
    /// Unattended strategic-layer runner, enabled by a run.json that tools/run.ps1 drops into the plugin folder. It
    /// loads a save, runs the clock at max speed with the game's own Autopilot clearing prompts and auto-resolving
    /// combat, executes setup commands, records diagnostics, and quits after N game days.
    /// Asserts are evaluated afterwards by tools/probe.py over the run's output.
    /// The play install (tools/play.ps1) instead keeps a standing home.json there: every launch of it, with or without
    /// the launcher, is an interactive session with its own save folder.
    /// </summary>
    internal class CampaignHarness : MonoBehaviour
    {
        /// <summary>A new campaign, started from the main menu as the New Game screen does.</summary>
        public class NewGame
        {
            /// <summary>The player's faction (template name, e.g. CooperateCouncil for the Academy).</summary>
            public string faction = "ResistCouncil";
            /// <summary>1 Cinematic .. 4 Brutal.</summary>
            public int difficulty = 2;
            /// <summary>The Accelerated Campaign preset; otherwise the long campaign's defaults.</summary>
            public bool accelerated = true;
            /// <summary>Scenario meta-template name (the game's default when empty).</summary>
            public string scenario;
        }

        public class RunRequest
        {
            public string name = "run";
            /// <summary>Absolute path of the save to load (a copy; the launcher never hands us the user's original).</summary>
            public string save;
            /// <summary>Start a new campaign instead of loading a save.</summary>
            public NewGame newGame;
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
            /// A person plays: the save-folder isolation and diagnostics stay, but no Autopilot, no speed forcing, no
            /// stall handling, no quit; saves allowed (into savesDir). `save` is optional: without it the player uses
            /// the (isolated) main menu.
            /// </summary>
            public bool interactive;
            /// <summary>Show the mod's player notifications as a real play does (an unattended run otherwise only journals them).</summary>
            public bool notices;
        }

        public static bool Interactive => Active && Run.interactive;
        /// <summary>An unattended run that must not raise popups.</summary>
        public static bool Quiet => Active && !Run.interactive && !Run.notices;
        internal static CampaignHarness Instance { get; private set; }

        public static bool Active { get; private set; }
        public static RunRequest Run { get; private set; }
        public static string OutDir => Run?.outDir;
        internal static bool AllowSaveNow;

        private static string RunFile => Path.Combine(Paths.PluginPath, "ImprovedAI", "run.json");
        private static string HomeFile => Path.Combine(Paths.PluginPath, "ImprovedAI", "home.json");

        /// <summary>Called from Plugin.Awake, before the game reads its profile.</summary>
        public static void EarlyInit()
        {
            bool home = !File.Exists(RunFile);
            if (home && !File.Exists(HomeFile)) return;
            try
            {
                Run = JsonConvert.DeserializeObject<RunRequest>(File.ReadAllText(home ? HomeFile : RunFile));
                if (home)
                {
                    // home.json names the folder that holds every session; each launch gets its own.
                    Run.interactive = true;
                    Run.save = null;
                    if (!string.IsNullOrEmpty(Run.outDir)) Run.outDir = Path.Combine(Run.outDir, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                }
                if ((string.IsNullOrEmpty(Run.save) && Run.newGame == null && !Run.interactive) || string.IsNullOrEmpty(Run.outDir) || string.IsNullOrEmpty(Run.savesDir))
                    throw new Exception("run.json needs save or newGame, outDir and savesDir");
                if (Run.interactive) Run.allowSaves = true;
                Directory.CreateDirectory(Run.outDir);
                Directory.CreateDirectory(Run.savesDir);
                // Everything the game reads or writes "in the save folder" (profile, autosaves, autopilot saves,
                // combat autosaves) goes to savesDir for this launch.
                string dir = Run.savesDir.Replace("\\", "/").TrimEnd('/') + "/";
                AccessTools.Field(typeof(CreateSaveFileScrollList), "savedGamesPath").SetValue(null, dir);
                Active = true;
                Plugin.Cfg.ApplyOverrides(Run.config);
                Plugin.Log.LogInfo($"[Harness] Run '{Run.name}': save={Run.save ?? (Run.newGame != null ? "new game" : null)} days={Run.runDays} out={Run.outDir} saves={dir}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[Harness] Bad {(home ? "home.json" : "run.json")}, harness disabled: {e}");
                Active = false;
                // A run was requested but the save folder may not be redirected: report and quit instead of carrying on
                // against the user's real saves (or idling at the menu until the launcher's timeout).
                if (Run != null && !string.IsNullOrEmpty(Run.outDir))
                {
                    try
                    {
                        Directory.CreateDirectory(Run.outDir);
                        File.WriteAllText(Path.Combine(Run.outDir, "results.json"), Jsonl.Serialize(new { endReason = "error: bad run.json: " + e.Message, save = Run.save }, indented: true));
                    }
                    catch { }
                }
                Application.Quit();
            }
        }

        private enum Phase { Menu, Loading, Running, Done }
        private Phase _phase = Phase.Menu;
        private float _menuSeenAt = -1f, _loadRequestedAt, _readyAt, _lastHeartbeat, _lastDateChangeAt;
        private TIDateTime _startDate;
        private string _lastDate;
        private int _stalls, _consecutiveStalls, _unsticks, _resumes, _reloads;
        private float _leftCampaignAt = -1f;
        private string _reloadSave;
        private object _beforeReload;
        private (int ships, float best) _threatBeforeReload;
        private string _shot;
        private float _shotAt;
        private int _shots;
        private float _endGameAt = -1f;

        private float _lastResume = -100f;
        private bool _autopilotHeld;
        private float _lastUnstick;
        private readonly List<string> _exceptions = new List<string>();
        private int _exceptionCount;
        private readonly List<string> _terminal = new List<string>();
        private readonly List<string> _scheduled = new List<string>();
        private Autopilot _autopilot;

        /// <summary>
        /// With notices shown, the popup that ends the campaign is closed as a player closes it: once. The Autopilot
        /// presses Close on every frame while the box is up, and each press starts another return to the main menu.
        /// </summary>
        public static void CloseEndGameNotice()
        {
            if (Instance == null || !Active || Run.interactive || !Run.notices) return;
            if (Instance._autopilot != null) Instance._autopilot.Activated = false;
            Instance._endGameAt = Time.realtimeSinceStartup + 8f;
        }

        /// <summary>
        /// With notices shown, capture the screen a few seconds from now (what the player is looking at). The Autopilot,
        /// which would dismiss a popup on its first frame, is held off until then. A second notice within those seconds
        /// takes the first one's place.
        /// </summary>
        public static void ShootSoon(string what)
        {
            if (Instance == null || !Active || Run.interactive || !Run.notices) return;
            Instance._shot = what;
            Instance._shotAt = Time.realtimeSinceStartup + 4f;
            if (Instance._autopilot != null) Instance._autopilot.Activated = false;
        }

        private void Start()
        {
            Instance = this;
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
                if (_shot != null && Time.realtimeSinceStartup >= _shotAt)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(Run.outDir, $"notice-{++_shots}-{_shot}.png"));
                    _shot = null;
                    _shotAt = Time.realtimeSinceStartup;
                }
                else if (_shot == null && _shotAt > 0f && Time.realtimeSinceStartup - _shotAt > 1f)
                {
                    // The capture is taken at the end of its frame; then the Autopilot may clear the popup.
                    _shotAt = 0f;
                    if (Strategist.Checkpoint.Waiting) _autopilotHeld = true;   // Tick lets it go when the checkpoint ends
                    else if (_autopilot != null && _endGameAt < 0f) _autopilot.Activated = true;
                }
                if (_endGameAt > 0f && Time.realtimeSinceStartup >= _endGameAt)
                {
                    _endGameAt = Time.realtimeSinceStartup + 2f;
                    var screen = FindObjectOfType<NotificationScreenController>();
                    var held = screen == null ? null : Traverse.Create(screen).Field("heldNewsItems").GetValue<List<NotificationQueueItem>>();
                    if (held != null && held.Count > 0 && screen.singleAlertBox.activeInHierarchy)
                    {
                        // Notices queued ahead of the last one are closed first, one at a time.
                        bool last = held[0].triggerEndGame;
                        Plugin.Log.LogInfo($"[Harness] Closing notice '{held[0].itemHeadline}'{(last ? " (ends the campaign)" : "")}");
                        screen.CloseButtonPressed();
                        if (last) _endGameAt = -1f;
                    }
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
            if (Run.newGame != null) { StartNewGame(menu); return; }

            var loader = Resources.FindObjectsOfTypeAll<LoadMenuController>().FirstOrDefault(x => x.gameObject.scene.isLoaded);
            if (loader == null) { Finish("error: LoadMenuController not found"); return; }
            if (!File.Exists(Run.save)) { Finish("error: save not found: " + Run.save); return; }
            Plugin.Log.LogInfo($"[Harness] Loading {Run.save}");
            _phase = Phase.Loading;
            _loadRequestedAt = Time.realtimeSinceStartup;
            loader.LoadSaveFilePath(Run.save);
        }

        /// <summary>Set the New Game screen's own controls and press its launch button.</summary>
        private void StartNewGame(StartMenuController menu)
        {
            var ng = Run.newGame;
            var t = Traverse.Create(menu);
            if (!string.IsNullOrEmpty(ng.scenario))
            {
                var option = Resources.FindObjectsOfTypeAll<NewGameOptionController>().FirstOrDefault(o => o.gameObject.scene.isLoaded && o.gameObject.name == "Scenario");
                var options = option == null ? null : Traverse.Create(option).Field("templateOptions").GetValue<List<TIMetaTemplate>>();
                int at = options?.FindIndex(o => o.dataName == ng.scenario) ?? -1;
                if (at < 0) { Finish($"error: no scenario '{ng.scenario}'; have: {string.Join(", ", options?.Select(o => o.dataName) ?? new string[0])}"); return; }
                option.optionDropdown.value = at;
                menu.UpdateStartOptions("Scenario", options[at]);
            }
            var factions = t.Field("currentAllowedFactions").GetValue<List<TIFactionTemplate>>();
            int idx = factions?.FindIndex(f => f.dataName == ng.faction) ?? -1;
            if (idx < 0) { Finish($"error: no player faction '{ng.faction}'; have: {string.Join(", ", factions?.Select(f => f.dataName) ?? new string[0])}"); return; }
            menu.newCampaignChooseFactionDropdown.value = idx;
            menu.OnFactionOptionSelected();
            menu.selectDifficultyDropdown.value = Mathf.Clamp(ng.difficulty, 1, 4) - 1;
            menu.OnDifficultyChanged();
            Plugin.Log.LogInfo($"[Harness] New game: {ng.faction}, difficulty {ng.difficulty}, {(ng.accelerated ? "accelerated" : "long")} campaign, scenario {t.Field("currentStartOptions").GetValue<Dictionary<string, string>>()?["Scenario"]}");
            _phase = Phase.Loading;
            _loadRequestedAt = Time.realtimeSinceStartup;
            if (ng.accelerated) menu.OnLaunchAcceleratedCampaignClicked(); else menu.OnLaunchLongCampaignClicked();
        }

        private void OnCampaignReady()
        {
            if (Run.interactive)
            {
                // Setup commands once, on the first campaign the player opens; nothing else is automated.
                if (_phase == Phase.Done) return;
                _phase = Phase.Done;
                Plugin.Log.LogInfo($"[Harness] Campaign ready at {GameRead.Now()}.");
                RunCommands();
                return;
            }
            if (_phase != Phase.Loading) return;
            _phase = Phase.Running;
            float now = Time.realtimeSinceStartup;
            _lastHeartbeat = _lastDateChangeAt = now;
            _lastDate = GameRead.Now();
            Plugin.Log.LogInfo($"[Harness] Campaign ready at {_lastDate} after {now - _loadRequestedAt:F0}s load.");

            // Unattended: no exception dialog (we log them), keep running without focus, uncap the frame rate.
            GameControl.handlingException = true;
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Run.targetFrameRate;

            if (_reloadSave == null)
            {
                _readyAt = now;
                _startDate = TITimeState.Now();
                RunCommands();
            }
            else
            {
                FactionControl.HandBackAfterLoad();
                var after = Continuity();
                string a = Jsonl.Serialize(_beforeReload), b = Jsonl.Serialize(after);
                // The memory has taken one more sample since the load (ships finished since the last daily one join
                // it), and the game's SCV of a ship can differ a little across a save and load, so remembered ships
                // and strength may only have grown.
                var (ships, threat) = AI.ThreatMemory.Totals();
                bool same = a == b && ships >= _threatBeforeReload.ships && threat >= 0.98f * _threatBeforeReload.best;
                Session.Journal?.Write(new { t = _lastDate, kind = "reload", n = _reloads, seconds = (float)Math.Round(now - _loadRequestedAt), same, memoryGB = MemoryGB(), threatBefore = _threatBeforeReload.best, threatAfter = threat, shipsBefore = _threatBeforeReload.ships, shipsAfter = ships, before = a == b ? null : _beforeReload, after = a == b ? null : after });
                try { File.Delete(_reloadSave); } catch (Exception e) { Plugin.Log.LogWarning($"[Harness] {_reloadSave}: {e.Message}"); }
                _reloadSave = null;
                _beforeReload = null;
            }

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
                // "@N command" runs N game days after the start (see RunScheduled).
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

        /// <summary>
        /// "ai.reload": save, then load that save in the running game, as a player who reloads mid-campaign does. The
        /// save is renamed before it is loaded, as the game renames its autosaves. The run carries on from the loaded
        /// game; a "reload" journal record says whether the mod's state came back the same.
        /// </summary>
        public string Reload()
        {
            if (_phase != Phase.Running) return "! not running";
            var loader = Resources.FindObjectsOfTypeAll<LoadMenuController>().FirstOrDefault(x => x.gameObject.scene.isLoaded);
            if (loader == null) return "! LoadMenuController not found";
            string written = Path.Combine(Run.outDir, "reload.gz");
            string path = Path.Combine(Run.outDir, $"reload-{++_reloads}.gz");
            _beforeReload = Continuity();
            _threatBeforeReload = AI.ThreatMemory.Totals();
            AllowSaveNow = true;
            bool saved = false;
            try { FactionControl.SaveAsHuman(() => saved = GameStateManager.SaveAllGameStates(written, doNotOpenSaveMenu: true) && File.Exists(written)); }
            finally { AllowSaveNow = false; }
            if (!saved) return "! save failed";
            File.Move(written, path);
            if (_autopilot != null) { _autopilot.Activated = false; Destroy(_autopilot.gameObject); _autopilot = null; }
            _reloadSave = path;
            _phase = Phase.Loading;
            _loadRequestedAt = Time.realtimeSinceStartup;
            loader.LoadSaveFilePath(path);
            return "> reloading from " + path;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct ProcessMemory
        {
            public uint cb, pageFaults;
            public UIntPtr peakWorkingSet, workingSet, peakPagedPool, pagedPool, peakNonPagedPool, nonPagedPool, pagefile, peakPagefile, privateBytes;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [System.Runtime.InteropServices.DllImport("psapi.dll")]
        private static extern bool GetProcessMemoryInfo(IntPtr process, out ProcessMemory counters, uint size);

        /// <summary>
        /// The game process's private memory. Each load of a save in a running game adds to it.
        /// Unity's Mono reports 0 for System.Diagnostics.Process.PrivateMemorySize64, so this asks Windows.
        /// </summary>
        private static double MemoryGB()
        {
            try
            {
                uint size = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(ProcessMemory));
                return GetProcessMemoryInfo(GetCurrentProcess(), out var m, size) ? Math.Round(m.privateBytes.ToUInt64() / 1e9, 1) : 0;
            }
            catch { return 0; }
        }

        /// <summary>What must be the same after a load as before the save.</summary>
        private static object Continuity()
        {
            var s = AI.AlienStrategy.Current;
            var alien = GameStateManager.AlienFaction();
            return new
            {
                date = GameRead.Now(),
                strategy = s == null ? null : Newtonsoft.Json.Linq.JObject.FromObject(s).Properties().Where(p => p.Name != "source" && p.Name != "threat").ToDictionary(p => p.Name, p => p.Value.ToString(Formatting.None)),
                escalated = AI.Escalation.Escalated,
                dials = AI.Dials.Snapshot(),
                alienFleets = alien?.fleets.Count,
                humanShips = GameStateManager.AllHumanFactions().Sum(h => h.fleets.Sum(f => f.ships?.Count ?? 0)),
            };
        }

        private void Tick()
        {
            var gtm = GameTimeManager.Singleton;
            float now = Time.realtimeSinceStartup;
            if (!Observer.Running)
            {
                // The campaign is over (credits, main menu): nothing more will happen.
                if (_leftCampaignAt < 0f) _leftCampaignAt = now;
                else if (now - _leftCampaignAt > 20f) Finish("campaign-ended");
                return;
            }
            _leftCampaignAt = -1f;
            string date = GameRead.Now();
            if (Strategist.Checkpoint.Waiting)
            {
                // Paused on purpose while the strategist thinks: not a stall, not to be resumed here, and the Autopilot
                // (which presses play) is held off.
                if (_autopilot != null && _autopilot.Activated) { _autopilot.Activated = false; _autopilotHeld = true; }
                if (gtm != null && gtm.currentSpeedIndex != 0) gtm.SetSpeed(0, false);
                _lastDateChangeAt = now;
                _consecutiveStalls = 0;
                return;
            }
            if (_autopilotHeld && _autopilot != null) { _autopilot.Activated = true; _autopilotHeld = false; }
            if (gtm != null && gtm.currentSpeedIndex != 0 && gtm.currentSpeedIndex != Run.speedIndex)
                gtm.SetSpeed(Run.speedIndex, pushBeyondCap: true);
            if (date != _lastDate) { _lastDate = date; _lastDateChangeAt = now; _consecutiveStalls = 0; }
            else if (now - _lastDateChangeAt > 20f && now - _lastUnstick > 20f && BlockingPrompts().Contains("faction:PromptSelectCouncilorMissions"))
            {
                // The Autopilot sometimes fails to confirm the mission phase; do what the button does.
                _lastUnstick = now;
                _unsticks++;
                Plugin.Log.LogInfo($"[Harness] Finalizing councilor missions for the stuck mission phase at {date}");
                var p = GameControl.control.activePlayer;
                p.playerControl.StartAction(new PavonisInteractive.TerraInvicta.Actions.FinalizeCouncilorMissions(p));
            }
            else if (gtm != null && gtm.currentSpeedIndex == 0 && now - _lastDateChangeAt > 10f && now - _lastResume > 10f
                     && !TIPromptQueueState.anyActivePlayerBlockingPrompt)
            {
                // Paused with nothing blocking: an event notification (e.g. "fleet returns to operations") paused the
                // clock. An AI-run player faction raises many of these. Resume rather than wait for the stall handler.
                _lastResume = now;
                _resumes++;
                gtm.SetSpeed(Run.speedIndex, pushBeyondCap: true);
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
            if (_phase != Phase.Running) return;   // a scheduled reload is under way
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
                // Unity-logged exceptions plus those PatchGuard swallowed in our own patches.
                exceptions = _exceptionCount + PatchGuard.Count,
                guarded = PatchGuard.Count,
                stalls = _stalls,
                unsticks = _unsticks,
                resumes = _resumes,
                reloads = _reloads,
                memoryGB = MemoryGB(),
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
                    try { FactionControl.SaveAsHuman(() => GameStateManager.SaveAllGameStates(Path.Combine(Run.outDir, "final.gz"), doNotOpenSaveMenu: true)); }
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
