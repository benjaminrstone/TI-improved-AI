using System;
using System.IO;
using ImprovedAI.Util;

namespace ImprovedAI.Diagnostics
{
    /// <summary>Where diagnostics for the current campaign session go. One folder per loaded game (or per harness run).</summary>
    internal static class Session
    {
        public static string Dir { get; private set; }
        public static Jsonl Journal { get; private set; }
        public static Jsonl Metrics { get; private set; }
        private static StreamWriter _aiLog;

        public static void Start()
        {
            End();
            // A play session may open several games in one launch: one subfolder per loaded game.
            if (Harness.CampaignHarness.Interactive) Dir = Path.Combine(Harness.CampaignHarness.OutDir, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            else if (Harness.CampaignHarness.Active) Dir = Harness.CampaignHarness.OutDir;
            else
            {
                string root = string.IsNullOrEmpty(Plugin.Cfg.LogDir.Value) ? AIConfig.DefaultLogRoot : Plugin.Cfg.LogDir.Value;
                Dir = Path.Combine(root, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            }
            Directory.CreateDirectory(Dir);
            if (Plugin.Cfg.DecisionJournal.Value) Journal = new Jsonl(Path.Combine(Dir, "journal.jsonl"));
            if (Plugin.Cfg.Metrics.Value) Metrics = new Jsonl(Path.Combine(Dir, "metrics.jsonl"));
            Plugin.Log.LogInfo($"[Diagnostics] Session output: {Dir}");
        }

        private static readonly object AILogLock = new object();

        /// <summary>Any thread: the mission planner logs from its own (PatchGuard).</summary>
        public static void AILog(string line)
        {
            if (Dir == null) return;
            try
            {
                lock (AILogLock)
                {
                    if (_aiLog == null)
                        _aiLog = new StreamWriter(new FileStream(Path.Combine(Dir, "ai-log.txt"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
                    _aiLog.WriteLine(line);
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[AILog] {e.Message}"); }
        }

        public static void End()
        {
            Journal?.Dispose(); Journal = null;
            Metrics?.Dispose(); Metrics = null;
            lock (AILogLock) { _aiLog?.Dispose(); _aiLog = null; }
        }
    }
}
