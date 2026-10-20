using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Tasks;

namespace ImprovedAI.Human
{
    /// <summary>
    /// Personalities in action, not only in hate (docs/HUMAN-AI.md §9; user, 2026-10-19: anti-alien factions should feel
    /// very different). Two levers:
    ///  - Mission bias: the payoff of hostile councilor missions (AICouncilorMissionPlanner.GetPayoffForMissionTarget)
    ///    is scaled by who acts against whom. Humanity First is zealous against pro-alien factions; the Resistance
    ///    punishes collaborators (very pro-alien); Exodus holds back unless it was hurt by that faction (then it
    ///    retaliates); clients and patrons spare each other.
    ///  - Exodus works toward its escape: vanilla throttles focus on an active objective project to 20% of research
    ///    time since the last objective (ShouldFocusOnObjectiveProject); Exodus keeps the focus.
    /// Metrics: mission hate by victim and actor (human-hate), Exodus objective progress (human-persona).
    /// </summary>
    internal static class PersonalityActions
    {
        /// <summary>Hostile missions: those the non-aggression pact zeroes (FactionGoal_NonAggressionPact), minus Propaganda.</summary>
        internal static readonly HashSet<string> Hostile = new HashSet<string>
        {
            "Coup", "Crackdown", "Purge", "Unrest", "SabotageFacilities", "Assassinate", "Detain", "HostileTakeover",
            "SabotageProject", "StealProject", "Turn", "SeizeSpaceAsset", "ControlSpaceAsset", "SabotageHabModule",
            "EnthrallElites", "EnthrallOrg",
        };

        public static float MissionFactor(TIFactionState actor, TIFactionState target)
        {
            if (actor == null || target == null || actor == target || target.IsAlienFaction) return 1f;
            if (Personality.ClientPair(actor, target)) return 0.2f;
            switch (Personality.Of(actor))
            {
                case Personality.Persona.HF:
                    return target.proAlien ? 1.5f : 1f;
                case Personality.Persona.Resist:
                    return target.veryProAlien ? 1.3f : 1f;
                case Personality.Persona.Exodus:
                    // Left alone, it leaves others alone; hurt, it hits back.
                    return Personality.Grudge(actor, target) >= 2f ? 1.2f : 0.5f;
                default:
                    return 1f;
            }
        }

        internal static readonly Dictionary<string, int> Scaled = new Dictionary<string, int>();

        /// <summary>Monthly: Exodus progress on its objective chain, and how often the mission bias applied.</summary>
        public static void Monthly()
        {
            if (!Plugin.Cfg.UseHumanPersonalities || !Plugin.Cfg.DecisionJournal.Value) return;
            foreach (var f in GameStateManager.AllHumanFactions().Where(x => x != null && Personality.Of(x) == Personality.Persona.Exodus))
            {
                try
                {
                    var active = f.CurrentlyActiveProjects().Where(p => p.AI_projectRole == ProjectRole.Objective).ToList();
                    Session.Journal?.Write(new
                    {
                        t = GameRead.Now(), kind = "human-persona", f = GameRead.Faction(f),
                        objectiveDone = f.completedProjects.Where(p => p.AI_projectRole == ProjectRole.Objective).Select(p => p.dataName).ToList(),
                        objectiveActive = active.Select(p => new { p.dataName, research = (float)Math.Round(GameRead.Safe(() => f.GetProjectProgressByTemplate(p).accumulatedResearch)), cost = p.researchCost }).ToList(),
                        victoryUnlocked = f.unlockedVictoryObjective,
                        missionBias = Scaled.ToDictionary(kv => kv.Key, kv => kv.Value),
                    });
                }
                catch (Exception e) { Plugin.Log.LogWarning($"[PersonalityActions] persona record: {e.Message}"); }
            }
            Scaled.Clear();
        }
    }

    [HarmonyPatch(typeof(AICouncilorMissionPlanner), nameof(AICouncilorMissionPlanner.GetPayoffForMissionTarget))]
    internal static class PersonalityMissionBias
    {
        private static void Postfix(TIFactionState faction, TIMissionTemplate mission, TIGameState target, ref float __result)
        {
            if (__result <= 0f || !Plugin.Cfg.UseHumanPersonalities || !FleetCompetence.AIHuman(faction) || mission == null || target == null) return;
            if (!PersonalityActions.Hostile.Contains(mission.dataName)) return;
            TIFactionState owner = null;
            try { owner = target.ref_faction; } catch { }
            float k = PersonalityActions.MissionFactor(faction, owner);
            if (k == 1f) return;
            __result *= k;
            string key = $"{faction.templateName}>{owner?.templateName}:{(k > 1f ? "up" : "down")}";
            PersonalityActions.Scaled[key] = (PersonalityActions.Scaled.TryGetValue(key, out var n) ? n : 0) + 1;
        }
    }

    [HarmonyPatch(typeof(AIEvaluators), nameof(AIEvaluators.ShouldFocusOnObjectiveProject))]
    internal static class ExodusObjectiveFocus
    {
        private static void Postfix(TIFactionState faction, ref bool __result)
        {
            if (__result || !Plugin.Cfg.UseHumanPersonalities || !FleetCompetence.AIHuman(faction) || Personality.Of(faction) != Personality.Persona.Exodus) return;
            try { if (faction.CurrentlyActiveProjects().Any(p => p.AI_projectRole == ProjectRole.Objective)) __result = true; }
            catch { }
        }
    }
}
