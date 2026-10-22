using System;
using System.Collections.Generic;
using System.Linq;
using ImprovedAI.AI;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;
using UnityEngine;

namespace ImprovedAI.Strategist
{
    /// <summary>
    /// What each non-player faction knows at a checkpoint (docs/STRATEGIST.md, brief v0), compact enough to read whole.
    /// Fog of war through each AI's own perception: the aliens see human fleets through their threat memory; a human
    /// faction sees other fleets through vanilla's system fleet strengths scaled by its perceived-strength factor (what its
    /// own AI uses). Hab counts and locations are treated as known (they are visible on the game map).
    /// </summary>
    internal static class Brief
    {
        private static float R(float v) => (float)Math.Round(v);

        public static object Build(TIFactionState player)
        {
            var humans = GameStateManager.AllHumanFactions().Where(h => h != null && h.IsActiveHumanFaction).ToList();
            var alien = GameStateManager.AlienFaction();
            var factions = new Dictionary<string, object>();
            if (alien != null) factions[alien.templateName] = Alien(alien, humans);
            foreach (var f in humans.Where(h => h != player && h.player != null && h.player.isAI)) factions[f.templateName] = HumanBrief(f, humans, alien);
            return new
            {
                date = GameRead.Now(),
                campaignYears = (float)Math.Round(GameRead.Safe(() => TITimeState.CampaignDuration_years_Exact()), 1),
                player = player?.templateName,
                schema = "docs/STRATEGIST.md (directive schema v0)",
                factions,
            };
        }

        private static Dictionary<string, float> FleetsBySystem(TIFactionState f) =>
            f.fleets.Where(x => TIGameState.Valid(x)).GroupBy(x => GameRead.System(x) ?? "?")
                .ToDictionary(g => g.Key, g => R(g.Sum(x => GameRead.Safe(() => x.SpaceCombatValue()))));

        private static Dictionary<string, int> HabsBySystem(TIFactionState f) =>
            f.habs.Where(h => TIGameState.Valid(h)).GroupBy(h => GameRead.System(h) ?? "?").ToDictionary(g => g.Key, g => g.Count());

        private static Dictionary<string, float> Stock(TIFactionState f, params FactionResource[] rs) =>
            rs.ToDictionary(r => r.ToString(), r => R(f.GetCurrentResourceAmount(r)));

        private static object Alien(TIFactionState a, List<TIFactionState> humans)
        {
            var s = AlienStrategy.Current;
            var pooling = ForcePooling.Pooling.Where(g => g != null).Select(g => new
            {
                target = GameRead.Name(g.target()), system = GameRead.System(g.target()),
                need = R(g.desiredFleetCombatValue * Dials.PoolMargin), present = R(GameRead.Safe(() => g.assignedFleet?.SpaceCombatValue() ?? 0f)),
            }).ToList();
            return new
            {
                role = "the alien (Hydra) command",
                doctrine = s?.doctrine?.ToString(),
                escalated = s?.escalated, desperation = s == null ? (float?)null : (float)Math.Round(s.desperation, 2),
                terminus = s?.terminusStarted == null ? null : new { started = s.terminusStarted, progress = (float)Math.Round(s.terminusProgress, 3) },
                contempt = s?.contempt,
                exotics = new { stock = R(a.GetCurrentResourceAmount(FactionResource.Exotics)), drip = (float)Math.Round(Economy.Drip(a), 1), warChest = R(Economy.WarChest(a)) },
                bases = Economy.Bases(a),
                fleetScv = R(a.fleets.Sum(x => GameRead.Safe(() => x.SpaceCombatValue()))),
                fleetsBySystem = FleetsBySystem(a),
                pooling,
                // The human factions as the aliens perceive them: habs (visible), fleets as remembered (threat memory).
                humans = humans.ToDictionary(h => h.templateName, h => (object)new
                {
                    hate = R(GameRead.Safe(() => a.GetFactionHate(h))),
                    habsBySystem = HabsBySystem(h),
                    perceivedFleetsBySystem = h.fleets.Where(x => TIGameState.Valid(x)).GroupBy(x => GameRead.System(x) ?? "?")
                        .ToDictionary(g => g.Key, g => R(g.Sum(x => ThreatMemory.Remembered(x)))),
                }),
                directiveFields = "intent, posture (-1..1), strikeTargets [systems], spare [systems], raidFactions [factions], warChestMonths (0..36)",
            };
        }

        private static object HumanBrief(TIFactionState f, List<TIFactionState> humans, TIFactionState alien)
        {
            Dictionary<TISpaceObjectState, Dictionary<TIFactionState, float>> strengths = null;
            try { strengths = AIEvaluators.SystemFleetStrengths; } catch { }
            Dictionary<string, float> Perceived(TIFactionState other)
            {
                var d = new Dictionary<string, float>();
                if (strengths == null || other == null) return d;
                float k = GameRead.Safe(() => f.GetPerceivedEnemyFleetStrengthFactor(other));
                foreach (var kv in strengths)
                    if (kv.Value != null && kv.Value.TryGetValue(other, out var v) && v > 0f) d[GameRead.Name(kv.Key) ?? "?"] = R(v * k);
                return d;
            }
            return new
            {
                role = "an AI human faction",
                persona = ImprovedAI.Human.Personality.Of(f).ToString(),
                selfAssessment = f.selfAssessement.ToString(),
                resources = Stock(f, FactionResource.Money, FactionResource.Influence, FactionResource.Operations, FactionResource.Boost,
                    FactionResource.Water, FactionResource.Volatiles, FactionResource.Metals, FactionResource.NobleMetals, FactionResource.Fissiles),
                habsBySystem = HabsBySystem(f),
                fleetsBySystem = FleetsBySystem(f),
                ships = f.fleets.Sum(x => x.ships?.Count ?? 0),
                councilors = f.councilors?.Count ?? 0,
                patron = ImprovedAI.Human.Personality.PatronOf(f)?.templateName,
                relations = humans.Where(h => h != f).ToDictionary(h => h.templateName, h => (object)new
                {
                    hate = R(GameRead.Safe(() => f.GetFactionHate(h))),
                    atWar = SafeBool(() => f.AI_AtWarWithFaction(h)),
                    nap = SafeBool(() => f.HasNAP(h)),
                    habsBySystem = HabsBySystem(h),
                    perceivedFleets = Perceived(h),
                }),
                alienThreat = alien == null ? null : new { hate = R(GameRead.Safe(() => f.GetFactionHate(alien))), perceivedFleets = Perceived(alien) },
                directiveFields = "intent, rivals [factions], friends [factions], defend [systems], missionFocus (0..1)",
            };
        }

        private static bool SafeBool(Func<bool> fn) { try { return fn(); } catch { return false; } }
    }
}
