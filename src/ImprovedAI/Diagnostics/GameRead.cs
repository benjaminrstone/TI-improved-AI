using System;
using PavonisInteractive.TerraInvicta;

namespace ImprovedAI.Diagnostics
{
    /// <summary>Defensive accessors for reading game state from diagnostics code (never throw into the game).</summary>
    internal static class GameRead
    {
        public static int Id(TIGameState s) => s == null ? 0 : s.ID.GetHashCode();

        public static string Name(TIGameState s)
        {
            try { return s?.displayName; } catch { return null; }
        }

        /// <summary>The natural body a state sits at or orbits (moon, asteroid, planet), else its space body.</summary>
        public static string Body(TIGameState s)
        {
            try { return s?.ref_naturalSpaceObject?.displayName ?? s?.ref_spaceBody?.displayName; } catch { return null; }
        }

        public static string System(TIGameState s)
        {
            try { return s?.ref_system?.displayName; } catch { return null; }
        }

        public static string Faction(TIFactionState f) => f?.templateName;

        public static string Date(TIDateTime d) => d == null ? null : $"{d.year:D4}-{d.month:D2}-{d.day:D2}";

        public static string Now()
        {
            try { return Date(TITimeState.Now()); } catch { return null; }
        }

        public static float Safe(Func<float> f)
        {
            try { return f(); } catch { return float.NaN; }
        }

        /// <summary>True while a strategic campaign (not skirmish) is loaded and running.</summary>
        public static bool InCampaign()
        {
            try
            {
                return GameControl.loadcycle100 && GameControl.control != null && !GameControl.control.skirmishMode
                       && GameControl.control.activePlayer != null && GameStateManager.AlienFaction() != null;
            }
            catch { return false; }
        }
    }
}
