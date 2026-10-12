using System;
using PavonisInteractive.TerraInvicta;
using UnityEngine;

namespace ImprovedAI.Diagnostics
{
    /// <summary>Watches the game clock and drives the daily journal diff and the monthly metrics row.</summary>
    internal class Observer : MonoBehaviour
    {
        public static bool Running { get; private set; }
        public static event Action OnCampaignReady;
        public static event Action<TIDateTime> OnNewDay;

        private TIFactionState _player;
        private int _y, _m, _d;

        private void Update()
        {
            if (!GameRead.InCampaign())
            {
                if (Running) { Running = false; _player = null; Session.End(); }
                return;
            }
            var player = GameControl.control.activePlayer;
            TIDateTime now;
            try { now = TITimeState.Now(); } catch { return; }
            if (now == null) return;

            if (!Running || player != _player)
            {
                Running = true;
                _player = player;
                _y = now.year; _m = now.month; _d = now.day;
                Session.Start();
                Plugin.Log.LogInfo($"[Diagnostics] Campaign ready at {GameRead.Date(now)}; player {GameRead.Faction(player)}.");
                AI.ThreatMemory.Reset();
                AI.RaidPlanner.Reset();
                Safe(() => AI.ThreatMemory.Sample(now), "threat memory");
                Safe(DecisionJournal.Init, "journal init");
                Safe(Metrics.Write, "metrics");
                OnCampaignReady?.Invoke();
                return;
            }

            if (now.day == _d && now.month == _m && now.year == _y) return;
            bool newMonth = now.month != _m || now.year != _y;
            _y = now.year; _m = now.month; _d = now.day;
            Safe(() => AI.ThreatMemory.Sample(now), "threat memory");
            Safe(() => AI.RaidPlanner.Daily(now), "raid planner");
            if (Plugin.Cfg.DecisionJournal.Value) Safe(() => DecisionJournal.Daily(), "journal");
            if (newMonth && Plugin.Cfg.Metrics.Value) Safe(Metrics.Write, "metrics");
            try { OnNewDay?.Invoke(now); } catch (Exception e) { Plugin.Log.LogError($"[Diagnostics] OnNewDay: {e}"); }
        }

        private static void Safe(Action a, string what)
        {
            try { a(); } catch (Exception e) { Plugin.Log.LogError($"[Diagnostics] {what}: {e}"); }
        }
    }
}
