using System;
using System.Collections.Generic;
using System.Linq;
using ImprovedAI.AI;
using PavonisInteractive.TerraInvicta;
using UnityEngine;

namespace ImprovedAI.Strategist
{
    /// <summary>
    /// What the strategist may set for one faction (docs/STRATEGIST.md, schema v0). Every field is optional; missing means
    /// "no change from the AI's own judgement". Values re-weight choices the AI could make anyway: no resources, intel or
    /// units are ever granted. Stored in the sidecar (StrategyState.directives), so they survive save and load.
    /// </summary>
    public class Directive
    {
        /// <summary>Free text: what the faction means to do. Journaled; shown to the player only under Intel's rules.</summary>
        public string intent;
        /// <summary>Checkpoint date this directive was set (yyyy-MM-dd).</summary>
        public string set;

        // ---- the aliens ----
        /// <summary>-1 cautious .. +1 aggressive: scales attack superiority, reserves, raid and capture slots, fight-on-arrival.</summary>
        public float? posture;
        /// <summary>Systems to pool strikes against, in priority order (e.g. "Jupiter", "Mars").</summary>
        public List<string> strikeTargets;
        /// <summary>Systems not to attack or raid.</summary>
        public List<string> spare;
        /// <summary>Faction templates whose assets raids should favour (x2 score).</summary>
        public List<string> raidFactions;
        /// <summary>War chest size in months of exotics drip (0..36).</summary>
        public float? warChestMonths;

        // ---- human AI factions ----
        /// <summary>Faction templates to treat as rivals: hate rises each month, hostile missions favoured.</summary>
        public List<string> rivals;
        /// <summary>Faction templates to court: hate falls each month, hostile missions avoided.</summary>
        public List<string> friends;
        /// <summary>Systems to hold: a high-priority fleet defence goal at the faction's main hab there.</summary>
        public List<string> defend;
        /// <summary>0..1: how strongly rivals/friends steer councilor missions (default 0.5).</summary>
        public float? missionFocus;
    }

    /// <summary>Read side of the directives, for the AI hooks. Every accessor is neutral when no directive applies.</summary>
    internal static class Directives
    {
        public static bool On => Plugin.Cfg.UseStrategist;

        private static Dictionary<string, Directive> All => AlienStrategy.Current?.directives;

        public static Directive For(TIFactionState f)
        {
            if (!On || f == null) return null;
            var all = All;
            return all != null && all.TryGetValue(f.templateName, out var d) ? d : null;
        }

        private static bool Has(List<string> list, string name) =>
            list != null && name != null && list.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));

        private static int Rank(List<string> list, string name)
        {
            if (list == null || name == null) return int.MaxValue;
            int i = list.FindIndex(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
            return i < 0 ? int.MaxValue : i;
        }

        // ---- aliens ----
        public static float AlienPosture => Mathf.Clamp(For(GameStateManager.AlienFaction())?.posture ?? 0f, -1f, 1f);
        // Caps (15-year campaign, 2026-10-23: the strike list grew to 30 systems, which is no priority at all).
        internal const int MaxStrikeTargets = 5, MaxSpare = 12;
        public static int StrikeRank(string system) => Rank(For(GameStateManager.AlienFaction())?.strikeTargets?.Take(MaxStrikeTargets).ToList(), system);
        public static bool Spared(string system) => Has(For(GameStateManager.AlienFaction())?.spare?.Take(MaxSpare).ToList(), system);
        public static float RaidFactionWeight(TIFactionState target) => Has(For(GameStateManager.AlienFaction())?.raidFactions, target?.templateName) ? 2f : 1f;
        public static float? WarChestMonths
        {
            get { var m = For(GameStateManager.AlienFaction())?.warChestMonths; return m.HasValue ? Mathf.Clamp(m.Value, 0f, 36f) : (float?)null; }
        }

        // ---- humans ----
        /// <summary>+1 rival, -1 friend, 0 neither.</summary>
        public static int Stance(TIFactionState f, TIFactionState other)
        {
            var d = For(f);
            if (d == null || other == null) return 0;
            if (Has(d.rivals, other.templateName)) return 1;
            if (Has(d.friends, other.templateName)) return -1;
            return 0;
        }

        public static float MissionFocus(TIFactionState f) => Mathf.Clamp01(For(f)?.missionFocus ?? 0.5f);

        public static IEnumerable<string> DefendSystems(TIFactionState f) => For(f)?.defend ?? Enumerable.Empty<string>();
    }
}
