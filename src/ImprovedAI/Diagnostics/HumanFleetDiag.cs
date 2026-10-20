using System;
using System.Collections.Generic;
using System.Linq;
using PavonisInteractive.TerraInvicta;

namespace ImprovedAI.Diagnostics
{
    /// <summary>
    /// Why AI human factions build so few warships (docs/HUMAN-AI.md §6b; vanilla 2034 soaks: 0.3-2.4k SCV each vs the
    /// aliens' ~35k). One "human-fleet" record per AI human faction per month, to tell the candidate causes apart:
    ///  1. demand: fleet goals' desired strength (sized from the faction's own fleet; aliens count only at war);
    ///  2. the mission-control gate on queuing ships (AI_GenericMissionControlAvailable);
    ///  3. no war on the alien faction;
    ///  4. hab-first scheduling: the saving target, shipyard queues.
    /// Runs on vanilla too.
    /// </summary>
    internal static class HumanFleetDiag
    {
        public static void Monthly()
        {
            var alien = GameStateManager.AlienFaction();
            foreach (var f in GameStateManager.AllHumanFactions())
            {
                if (f == null) continue;   // the player too (player = true), as the yardstick
                try { Session.Journal?.Write(Row(f, alien)); }
                catch (Exception e) { Plugin.Log.LogWarning($"[HumanFleetDiag] {f.templateName}: {e.Message}"); }
            }
        }

        private static T Try<T>(Func<T> f) { try { return f(); } catch { return default; } }

        private static object Row(TIFactionState f, TIFactionState alien)
        {
            var goals = f.factionGoals.Values.SelectMany(l => l).OfType<FactionGoal_Fleet>().Where(g => g != null && g.importance > 0).ToList();
            var byType = goals.GroupBy(g => g.GetGoalType().ToString()).ToDictionary(gr => gr.Key, gr => new
            {
                n = gr.Count(),
                desired = (float)Math.Round(gr.Sum(g => GameRead.Safe(() => g.desiredFleetCombatValue)), 0),
                assigned = (float)Math.Round(gr.Sum(g => GameRead.Safe(() => g.assignedFleet?.SpaceCombatValue() ?? 0f)), 0),
                wantShips = gr.Count(g => { try { return g.NeedsShipsOrdered(); } catch { return false; } }),
                maxImp = gr.Max(g => g.importance),
            });
            var save = f.AISavingTarget;
            bool atWarWithAliens = false;
            try { atWarWithAliens = f.GoalsOfType(GoalType.WarOnFaction).Any(g => g.target()?.ref_faction == alien); } catch { }
            int queued = 0;
            try { queued = f.nShipyardQueues.Values.Sum(q => q.Count); } catch { }
            return new
            {
                t = GameRead.Now(),
                kind = "human-fleet",
                f = GameRead.Faction(f),
                player = f.isActivePlayer,
                mcUsage = GameRead.Safe(() => f.GetMissionControlUsage()),
                mcFromHabs = GameRead.Safe(() => f.GetMissionControlContributionFromHabs()),
                mcBuildings = f.habs.Sum(h => h.AllModules().Count(m => m?.moduleTemplate != null && m.moduleTemplate.missionControl > 0 && !m.destroyed)),
                habs = f.habs.Count,
                scv = (float)Math.Round(f.fleets.Sum(x => GameRead.Safe(() => x.SpaceCombatValue())), 0),
                fleets = f.fleets.Count,
                ships = f.fleets.Sum(x => x.ships?.Count ?? 0),
                mcIncome = GameRead.Safe(() => f.MissionControlIncome),
                mcGenericAvailable = GameRead.Safe(() => f.AI_GenericMissionControlAvailable),
                shipyards = GameRead.Safe(() => f.nShipyardQueues.Count),
                queued,
                saving = save != null && save.active ? new
                {
                    what = save.desiredPurchase?.dataName, goal = save.relatedGoal?.GetGoalType().ToString(), imp = save.relatedGoal?.importance,
                    cost = Try(() => save.GetResourcesToSave().resourceCosts.Where(x => x.value > 0f).ToDictionary(x => x.resource.ToString(), x => (float)Math.Round(x.value))),
                    banked = Try(() => save.bankedResources.ToDictionary(x => x.Key.ToString(), x => (float)Math.Round(x.Value))),
                    canSave = Try(() => save.CanSaveFor),
                } : null,
                stock = Try(() => ((FactionResource[])Enum.GetValues(typeof(FactionResource))).Where(r => f.UnlockedResource(r) || r == FactionResource.Money)
                    .ToDictionary(r => r.ToString(), r => (float)Math.Round(f.GetCurrentResourceAmount(r)))),
                income = Try(() => new[] { FactionResource.Water, FactionResource.Volatiles, FactionResource.Metals, FactionResource.NobleMetals, FactionResource.Fissiles, FactionResource.Antimatter, FactionResource.Exotics, FactionResource.Boost }
                    .ToDictionary(r => r.ToString(), r => (float)Math.Round(GameRead.Safe(() => f.GetMonthlyIncome(r)), 1))),
                atWarWithAliens,
                alienHate = alien == null ? (float?)null : GameRead.Safe(() => f.GetFactionHate(alien)),
                money = (float)Math.Round(f.GetCurrentResourceAmount(FactionResource.Money)),
                boost = (float)Math.Round(f.GetCurrentResourceAmount(FactionResource.Boost)),
                metals = (float)Math.Round(f.GetCurrentResourceAmount(FactionResource.Metals)),
                goals = byType,
            };
        }
    }
}
