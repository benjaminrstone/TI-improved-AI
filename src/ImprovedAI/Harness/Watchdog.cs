using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ImprovedAI.Util;

namespace ImprovedAI.Harness
{
    /// <summary>
    /// A frozen main thread writes nothing: no heartbeat, no stall, no result, and the run sits until its time cap.
    /// This thread notices that frames have stopped and aborts the main thread. The abort surfaces as a
    /// ThreadAbortException where the thread was stuck, re-raised through every catch on the way up, so the first
    /// trace the harness sees is the place; the run then ends with the result "frozen".
    /// Unattended runs only: a person's game also stops drawing frames when its window is in the background.
    /// </summary>
    internal static class Watchdog
    {
        private const int PollSeconds = 5;
        /// <summary>
        /// Two minutes: longer than any single frame of a healthy run (loading a save in-process is the longest).
        /// Counted in polls, not on a clock, so a machine that slept is not taken for a frozen game.
        /// </summary>
        private const int FrozenPolls = 24;

        private static long _frames;
        private static Thread _main;
        private static double _coresBusy;
        private static string _trace;

        /// <summary>The main thread was aborted for being frozen.</summary>
        public static volatile bool Tripped;

        /// <summary>Call on the main thread.</summary>
        public static void Start(string outDir, string save)
        {
            _main = Thread.CurrentThread;
            new Thread(() => Watch(outDir, save)) { IsBackground = true, Name = "ImprovedAI watchdog" }.Start();
        }

        public static void Frame() => Interlocked.Increment(ref _frames);

        /// <summary>An exception seen after the abort; the first is the innermost, where the thread was stuck.</summary>
        public static void Saw(string trace) => Interlocked.CompareExchange(ref _trace, trace, null);

        /// <summary>
        /// For results.json; null when the run did not freeze. coresBusy near 1 is a thread spinning, near 0 the main
        /// thread waiting on something.
        /// </summary>
        public static object Report() => Tripped ? new { coresBusy = _coresBusy, trace = _trace } : null;

        private static void Watch(string outDir, string save)
        {
            long seen = -1;
            int silent = 0;
            while (true)
            {
                Thread.Sleep(PollSeconds * 1000);
                long frames = Interlocked.Read(ref _frames);
                if (frames != seen) { seen = frames; silent = 0; continue; }
                if (++silent < FrozenPolls) continue;
                try
                {
                    var process = Process.GetCurrentProcess();
                    var before = process.TotalProcessorTime;
                    Thread.Sleep(PollSeconds * 1000);
                    process.Refresh();
                    _coresBusy = Math.Round((process.TotalProcessorTime - before).TotalSeconds / PollSeconds, 2);
                    Plugin.Log.LogError($"[Harness] No frame for {silent * PollSeconds} s: the main thread is frozen ({_coresBusy} cores busy). Aborting it for its stack trace, then ending the game.");
                    Tripped = true;
                    _main.Abort();
                    // Time for the harness to write the full result, if the main thread comes back to it.
                    Thread.Sleep(30000);
                    string results = Path.Combine(outDir, "results.json");
                    if (!File.Exists(results))
                        File.WriteAllText(results, Jsonl.Serialize(new { endReason = "frozen", save, frozen = Report() }, indented: true));
                }
                catch (ThreadAbortException) { return; }   // the game is quitting
                catch (Exception e) { Plugin.Log.LogError($"[Harness] watchdog: {e}"); }
                Process.GetCurrentProcess().Kill();
            }
        }
    }
}
