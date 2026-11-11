using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;
using UnityEngine;

namespace ImprovedAI.AI
{
    /// <summary>
    /// The alien economy.
    /// Mine what you lack: vanilla picks alien base sites by percent change in income, mostly gross revenue; stockpiles
    /// and ship-construction spend never enter it. Here a site's score is multiplied by the need for what it yields
    /// (the material mix of the live fleet against what is held).
    /// Exotics upkeep: every alien base and capital hull costs exotics each month, and vanilla's fixed base cap becomes
    /// what the wormhole drip plus a slice of the stockpile can carry. The scripted base goals (Jupiter, belt,
    /// Neptune -> Uranus -> Saturn) still bypass the cap, as in vanilla.
    /// </summary>
    internal static class Economy
    {
        public static bool MineOn => Plugin.Cfg.UseMineWhatYouLack;
        public static bool UpkeepOn => Plugin.Cfg.UseExoticsUpkeep;

        internal static readonly FactionResource[] Basic = { FactionResource.Water, FactionResource.Volatiles, FactionResource.Metals, FactionResource.NobleMetals, FactionResource.Fissiles };

        private static readonly Dictionary<TIFactionState, (string date, Dictionary<FactionResource, float> need)> _need = new Dictionary<TIFactionState, (string, Dictionary<FactionResource, float>)>();

        public static void Reset() { _need.Clear(); }

        /// <summary>Need-weighted site scoring applies to this faction (the aliens, or an AI human faction when enabled).</summary>
        public static bool NeedScoringFor(TIFactionState f) =>
            f != null && (f.IsAlienFaction ? MineOn : Plugin.Cfg.UseHumanMineWhatYouLack && Human.FleetCompetence.AIHuman(f));

        /// <summary>Material mix of what the aliens field: summed construction cost of their live ships, as shares.</summary>
        public static Dictionary<FactionResource, float> SpendShares(TIFactionState alien)
        {
            var sum = Basic.ToDictionary(r => r, r => 0f);
            var byDesign = alien.fleets.Where(f => f?.ships != null).SelectMany(f => f.ships).Select(s => s?.template).Where(t => t != null).GroupBy(t => t);
            foreach (var g in byDesign)
            {
                TIResourcesCost cost;
                // Fresh and uncached: the cached cost includes propellant (mostly water), which is fuel, not fleet.
                try { cost = g.Key.spaceResourceConstructionCost(false, null, includePropellant: false, skipConstructionTime: true, updateWithoutCaching: true); } catch { continue; }
                int n = g.Count();
                foreach (var rv in cost.resourceCosts)
                    if (sum.ContainsKey(rv.resource) && rv.value > 0f) sum[rv.resource] += rv.value * n;
            }
            float total = sum.Values.Sum();
            return total <= 0f ? Basic.ToDictionary(r => r, r => 1f / Basic.Length) : sum.ToDictionary(kv => kv.Key, kv => kv.Value / total);
        }

        /// <summary>
        /// Need weight per basic resource, in [1/max, max]: (share of spend x total basic stock) / own stock.
        /// Recomputed once per game day at most.
        /// </summary>
        public static Dictionary<FactionResource, float> Need(TIFactionState alien)
        {
            string today = GameRead.Now();
            if (_need.TryGetValue(alien, out var c) && c.date == today) return c.need;
            var shares = SpendShares(alien);
            var stock = Basic.ToDictionary(r => r, r => Mathf.Max(0f, alien.GetCurrentResourceAmount(r)));
            float total = stock.Values.Sum();
            float max = Mathf.Max(1f, Plugin.Cfg.NeedWeightMax.Value);
            var need = Basic.ToDictionary(r => r, r => Mathf.Clamp(shares[r] * total / Mathf.Max(1f, stock[r]), 1f / max, max));
            _need[alien] = (today, need);
            return need;
        }

        /// <summary>Production-weighted mean need of what a site yields (1 when it yields nothing basic).</summary>
        public static float SiteFactor(TIFactionState alien, TIHabSiteState site)
        {
            if (site == null) return 1f;
            var need = Need(alien);
            float prod = 0f, weighted = 0f;
            foreach (var r in Basic)
            {
                float p = Mathf.Max(0f, site.GetMonthlyProduction(r));
                prod += p;
                weighted += p * need[r];
            }
            return prod <= 0f ? 1f : weighted / prod;
        }

        public static float ShipUpkeep(TISpaceShipTemplate t)
        {
            if (t?.hullTemplate == null) return 0f;
            if (t.hullTemplate.hugeHull) return Plugin.Cfg.HugeShipExoticsUpkeep.Value;
            return t.size == ShipSize.Large ? Plugin.Cfg.CapitalShipExoticsUpkeep.Value : 0f;
        }

        public static int Bases(TIFactionState alien) => alien.habs.Count(h => h != null && h.IsBase);

        public static float MonthlyUpkeep(TIFactionState alien, out float bases, out float ships)
        {
            bases = Bases(alien) * Plugin.Cfg.BaseExoticsUpkeep.Value;
            ships = alien.fleets.Where(f => f?.ships != null).SelectMany(f => f.ships).Sum(s => ShipUpkeep(s?.template));
            return bases + ships;
        }

        private static float _slack; private static string _slackDate;

        /// <summary>
        /// (drip - terminus draw + hoard income - upkeep) / drip, cached per game day: below 0 the aliens are paying more
        /// than the wormhole brings. The build picker reads it: capital hulls carry upkeep, so a deficit shifts the mix
        /// to smaller hulls.
        /// </summary>
        public static float ExoticsSlack(TIFactionState alien)
        {
            string today = GameRead.Now();
            if (_slackDate == today) return _slack;
            float drip = Drip(alien);
            // The terminus investment is a commitment like upkeep (taken first each month), so it counts against the slack.
            _slack = drip <= 0f ? -1f : (drip - TerminusDraw(drip) + HoardIncome(alien) - MonthlyUpkeep(alien, out _, out _)) / drip;
            _slackDate = today;
            return _slack;
        }

        /// <summary>Weight multiplier on large hulls in the build pick: 1 with slack >= 0 or when escalated, down to 0.25 in deficit.</summary>
        public static float LargeHullBuildFactor(TIFactionState alien)
        {
            if (!UpkeepOn || Escalation.Escalated) return 1f;
            float s = ExoticsSlack(alien);
            if (s >= 0f) return 1f;
            // In deficit with the stock at or below the war chest, no new capital hulls at all.
            // A tiny weight, not 0: the picker divides by it.
            if (WarChestOn && alien.GetCurrentResourceAmount(FactionResource.Exotics) <= WarChest(alien)) return 0.01f;
            return Mathf.Clamp(1f + Plugin.Cfg.ExoticsPressureGain.Value * s, 0.25f, 1f);
        }

        /// <summary>Gross monthly exotics income (the wormhole drip, before upkeep).</summary>
        public static float Drip(TIFactionState alien) => Mathf.Max(0f, GameRead.Safe(() => alien.GetMonthlyIncome(FactionResource.Exotics)));

        // The war chest (ExoticsWarChestMonths of drip): the command spends its hoard down to it and lives within the
        // drip below that. Escalated, the chest is spent freely.
        public static bool WarChestOn => UpkeepOn && Plugin.Cfg.ExoticsWarChest.Value;

        public static float WarChest(TIFactionState alien) =>
            WarChestOn ? Mathf.Max(0f, Strategist.Directives.WarChestMonths ?? Plugin.Cfg.ExoticsWarChestMonths.Value) * Drip(alien) : 0f;

        /// <summary>
        /// The hoard above the war chest, spread over ExoticsHoardMonths: what the command may run its upkeep above the
        /// drip by. It shrinks to 0 as the stock nears the chest, so capital-hull building tightens before the chest is hit.
        /// </summary>
        public static float HoardIncome(TIFactionState alien)
        {
            if (!WarChestOn) return 0f;
            float above = Mathf.Max(0f, alien.GetCurrentResourceAmount(FactionResource.Exotics) - WarChest(alien));
            return above / Mathf.Max(1f, Plugin.Cfg.ExoticsHoardMonths.Value);
        }

        /// <summary>
        /// How many bases the aliens can sustain. The budget is a share of the drip (the rest is kept for exotic warships
        /// and the growth of capital-ship upkeep) plus a slow slice of the stockpile, minus current capital-ship upkeep.
        /// </summary>
        public static int ExoticsBaseCap(TIFactionState alien)
        {
            float perBase = Plugin.Cfg.BaseExoticsUpkeep.Value;
            if (perBase <= 0f) return 999;
            MonthlyUpkeep(alien, out _, out float ships);
            float stockSlice = Mathf.Max(0f, alien.GetCurrentResourceAmount(FactionResource.Exotics)) / Mathf.Max(1f, Plugin.Cfg.ExoticsStockHorizonMonths.Value);
            float budget = Dials.SustainableDripShare * Drip(alien) + stockSlice - ships;
            return Mathf.Clamp(Mathf.FloorToInt(budget / perBase), 0, Plugin.Cfg.MaxAlienBasesCeiling.Value);
        }

        /// <summary>What the terminus takes from the drip each month while it is being built (0 otherwise).</summary>
        public static float TerminusDraw(float drip) =>
            Terminus.On && AlienStrategy.Current.terminusStarted != null && !AlienStrategy.Current.terminusComplete
                ? Mathf.Clamp01(Plugin.Cfg.TerminusDripShare.Value) * drip : 0f;

        /// <summary>
        /// Short of exotics: the stock is at or below the war chest and the slack is under RetireMargin. New bases are
        /// refused then (NoBasesWhenExoticsShort).
        /// </summary>
        public static bool ExoticsShort(TIFactionState alien) =>
            WarChestOn && !Escalation.Escalated && alien.GetCurrentResourceAmount(FactionResource.Exotics) <= WarChest(alien)
            && ExoticsSlack(alien) < Mathf.Clamp01(Plugin.Cfg.RetireMargin.Value);

        /// <summary>
        /// With the exotics stock spent, unpaid upkeep would cost nothing, so capital hulls (large and huge) are retired,
        /// oldest first, until ship upkeep fits what the drip leaves after the terminus investment and base upkeep.
        /// Fleets in combat and attacks under way keep their ships. Removal is vanilla's non-combat DestroyShip (as for
        /// crash-downs).
        /// </summary>
        private static void RetireOldestCapitalHulls(TIFactionState alien, float drip, float baseUpkeep, float shipUpkeep)
        {
            float terminus = TerminusDraw(drip);
            // A margin below the exact fit, so the stock rebuilds instead of sitting at 0.
            float allowed = Mathf.Max(0f, (drip - terminus - baseUpkeep) * (1f - Mathf.Clamp01(Plugin.Cfg.RetireMargin.Value)));
            if (shipUpkeep <= allowed) return;
            var candidates = alien.fleets.Where(f => TIGameState.Valid(f) && f.ships != null && !f.inCombatOrWaitingForCombat
                                                     && !(f.AssignedGoal() is FactionGoal_AttackWithFleet a && a.InProgress()))
                .SelectMany(f => f.ships).Where(s => s != null && ShipUpkeep(s.template) > 0f)
                .OrderBy(s => s.launchDate == null ? double.MinValue : s.launchDate.DifferenceInDays(TITimeState.Now()))   // launch - now: oldest (most negative) first
                .ToList();
            float excess = shipUpkeep - allowed, saved = 0f;
            // At most RetireMaxShare of the capital hulls held, per month, so a large cut is spread over many months.
            int capitalHulls = alien.fleets.Where(f => TIGameState.Valid(f) && f.ships != null).SelectMany(f => f.ships).Count(x => x != null && ShipUpkeep(x.template) > 0f);
            int cap = Mathf.Max(1, Mathf.CeilToInt(Mathf.Clamp01(Plugin.Cfg.RetireMaxShare.Value) * capitalHulls));
            var retired = new List<object>();
            foreach (var s in candidates)
            {
                if (saved >= excess || retired.Count >= cap) break;
                float u = ShipUpkeep(s.template);
                string name = s.displayName, design = s.template?.displayName, fleet = s.fleet?.displayName, at = GameRead.Body(s.fleet), launched = GameRead.Date(s.launchDate);
                try { s.DestroyShip(killPersonnel: false, null); }
                catch (Exception e) { Plugin.Log.LogWarning($"[Economy] retire {name}: {e.Message}"); continue; }
                saved += u;
                retired.Add(new { ship = name, design, fleet, at, launched, upkeep = u });
            }
            Plugin.Log.LogInfo($"[Economy] exotics spent: retired {retired.Count} capital hulls ({saved:F1}/month of {excess:F1} over)");
            Session.Journal?.Write(new
            {
                t = GameRead.Now(), kind = "retire", count = retired.Count, cap, capitalHulls, saved, excess, allowed, drip, terminus, baseUpkeep, shipUpkeepBefore = shipUpkeep,
                ships = retired.Take(40).ToList(),
            });
        }

        /// <summary>
        /// Monthly: charge the upkeep (when on) and journal the economy. The record is also written on vanilla, with what
        /// the mod would weigh and cap, so baselines compare directly.
        /// </summary>
        public static void Monthly()
        {
            var alien = GameStateManager.AlienFaction();
            if (alien == null || !Plugin.Cfg.DecisionJournal.Value && !UpkeepOn) return;
            Reset();
            float charged = 0f, upkeep = 0f, baseUp = 0f, shipUp = 0f;
            float drip = Drip(alien);
            int cap = ExoticsBaseCap(alien);
            upkeep = MonthlyUpkeep(alien, out baseUp, out shipUp);
            if (UpkeepOn)
            {
                charged = Mathf.Min(upkeep, Mathf.Max(0f, alien.GetCurrentResourceAmount(FactionResource.Exotics)));
                if (charged > 0f) alien.SubtractFromCurrentResource(charged, FactionResource.Exotics, true, "ImprovedAI exotics upkeep");
                if (Plugin.Cfg.RetireCapitalHulls.Value && alien.GetCurrentResourceAmount(FactionResource.Exotics) <= 0.5f)
                {
                    RetireOldestCapitalHulls(alien, drip, baseUp, shipUp);
                    upkeep = MonthlyUpkeep(alien, out baseUp, out shipUp);
                }
            }
            Session.Journal?.Write(new
            {
                t = GameRead.Now(),
                kind = "economy",
                mine = MineOn, upkeepOn = UpkeepOn,
                need = Need(alien).ToDictionary(kv => kv.Key.ToString(), kv => (float)Math.Round(kv.Value, 2)),
                shares = SpendShares(alien).ToDictionary(kv => kv.Key.ToString(), kv => (float)Math.Round(kv.Value, 3)),
                stock = Basic.Concat(new[] { FactionResource.Exotics }).ToDictionary(r => r.ToString(), r => (float)Math.Round(alien.GetCurrentResourceAmount(r))),
                drip, widening = (float)Math.Round(Wormhole.Widening, 3), campaignYears = (float)Math.Round(Wormhole.CampaignYears, 2),
                upkeep, baseUpkeep = baseUp, shipUpkeep = shipUp, charged,
                warChest = (float)Math.Round(WarChest(alien)), slack = (float)Math.Round(ExoticsSlack(alien), 3),
                largeFactor = (float)Math.Round(LargeHullBuildFactor(alien), 3), warChestHeld = ExoticsWarChestScope.Held,
                bases = Bases(alien),
                cap,
                // Alien cost computations this month that lacked a space resource (vanilla would price Earth boost +
                // money for it), and how many of those the mod refused.
                earthShortfalls = NoEarthPurchases.Shortfalls, earthBlocked = NoEarthPurchases.Blocked,
                boost = (float)Math.Round(alien.GetCurrentResourceAmount(FactionResource.Boost), 1),
                boostIncome = (float)Math.Round(GameRead.Safe(() => alien.GetMonthlyIncome(FactionResource.Boost)), 1),
                money = (float)Math.Round(alien.GetCurrentResourceAmount(FactionResource.Money), 1),
                moneyIncome = (float)Math.Round(GameRead.Safe(() => alien.GetMonthlyIncome(FactionResource.Money)), 1),
            });
            NoEarthPurchases.Shortfalls = NoEarthPurchases.Blocked = 0;
            ExoticsWarChestScope.Held = 0;
        }
    }

    /// <summary>
    /// The scripted base goals (Io, the belt, the outer planets) bypass the exotics base cap, so new bases would take
    /// back whatever upkeep a hull retirement frees. While the aliens are short of exotics (Economy.ExoticsShort), no
    /// new FoundBase goal is accepted, scripted or not. Goals already under way go on.
    /// </summary>
    [HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.AddGoal))]
    internal static class NoBasesWhenExoticsShort
    {
        internal static int Refused;

        private static bool Prefix(TIFactionState __instance, TIFactionGoalState prospectiveGoal, ref TIFactionGoalState __result)
        {
            if (!(prospectiveGoal is FactionGoal_FoundBase) || __instance == null || !__instance.IsAlienFaction) return true;
            if (!Plugin.Cfg.NoBasesWhenShort.Value || !Economy.ExoticsShort(__instance)) return true;
            Refused++;
            if (Refused <= 20 || Refused % 50 == 0)
                Session.Journal?.Write(new { t = GameRead.Now(), kind = "base-refused", n = Refused, target = GameRead.Body(prospectiveGoal.target()), imp = prospectiveGoal.importance });
            __result = null;
            return false;
        }
    }

    /// <summary>
    /// The war chest in the build pick: while the aliens pick a ship to build (GetDesiredShipToBuild, opened and closed by
    /// FleetDoctrineBuildPick), their exotics read as the stock above the war chest, unless escalated. Vanilla's pick
    /// drops designs whose exotics cost exceeds that amount (x its build share), and designs new ships without exotics
    /// when it reads 0, so exotic-tier builds pause at the chest instead of spending it.
    /// </summary>
    [HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.GetCurrentResourceAmount))]
    internal static class ExoticsWarChestScope
    {
        [ThreadStatic] internal static TIFactionState Alien;
        [ThreadStatic] internal static float Reserve;
        /// <summary>Picks this month that read a reduced stock (diagnostic, monthly economy record).</summary>
        internal static int Held;

        internal static void Open(TIFactionState alien)
        {
            if (!Economy.WarChestOn || Escalation.Escalated) return;
            float reserve = Economy.WarChest(alien);
            if (reserve <= 0f) return;
            Reserve = reserve;
            Alien = alien;
            Held++;
        }

        internal static void Close() { Alien = null; }

        private static void Postfix(TIFactionState __instance, FactionResource resourceType, ref float __result)
        {
            if (Alien == null || resourceType != FactionResource.Exotics || !ReferenceEquals(__instance, Alien)) return;
            __result = Mathf.Max(0f, __result - Reserve);
        }
    }

    /// <summary>
    /// No buying from Earth. Vanilla prices a missing space resource as boost + money from Earth's surface for any
    /// faction (TIResourcesCost.GetBoostSubstitutedCost: ship builds, refits, repairs, hab modules). With the switch on,
    /// the alien cost stays the plain space-resource cost, so a shortfall means waiting (or mining). Shortfalls are
    /// counted either way, for the monthly "economy" record.
    /// </summary>
    [HarmonyPatch(typeof(TIResourcesCost), nameof(TIResourcesCost.GetBoostSubstitutedCost))]
    internal static class NoEarthPurchases
    {
        internal static int Shortfalls, Blocked;

        private static bool Prefix(TIResourcesCost __instance, TIFactionState faction, List<ResourceValue> availableResources, ref TIResourcesCost __result)
        {
            if (faction == null || !faction.IsAlienFaction) return true;
            bool short_ = false;
            try
            {
                foreach (var rv in __instance.resourceCosts)
                {
                    if (rv.value <= 0f || TIResourcesCost.irreplaceableSpaceResources.Contains(rv.resource) || !TIResourcesCost.replaceableSpaceResources.Contains(rv.resource)) continue;
                    float have = availableResources == null ? faction.GetCurrentResourceAmount(rv.resource)
                        : availableResources.Where(a => a.resource == rv.resource).Select(a => a.value).FirstOrDefault();
                    if (have < rv.value) { short_ = true; break; }
                }
            }
            catch { }
            if (short_) Shortfalls++;
            if (!Plugin.Cfg.UseNoEarthPurchases) return true;
            if (short_) Blocked++;
            __result = new TIResourcesCost(__instance);
            return false;
        }
    }

    /// <summary>Base-site and mine scoring: multiply by the site's need factor.</summary>
    [HarmonyPatch(typeof(AIEvaluators), nameof(AIEvaluators.EvaluateHabModule_PercentChange))]
    internal static class MineWhatYouLackPercent
    {
        private static void Postfix(TIFactionState faction, TIGameState location, TIHabModuleTemplate moduleTemplate, bool moduleComparison, ref float __result)
        {
            if (!Economy.NeedScoringFor(faction) || moduleComparison || __result <= 0f) return;
            if (moduleTemplate == null || !moduleTemplate.mine || !(location is TIHabSiteState site)) return;
            try { __result *= Economy.SiteFactor(faction, site); }
            catch (Exception e) { Plugin.Log.LogWarning($"[Economy] site factor: {e.Message}"); }
        }
    }

    /// <summary>The other site scorer (choosing a site on a body; the scripted Neptune/Uranus/Saturn goals).</summary>
    [HarmonyPatch(typeof(AIEvaluators), nameof(AIEvaluators.EvaluateHabSite))]
    internal static class MineWhatYouLackSite
    {
        private static void Postfix(TIFactionState faction, TIHabSiteState habSite, ref float __result)
        {
            if (!Economy.NeedScoringFor(faction) || habSite == null || __result <= 0f) return;
            try { __result *= Economy.SiteFactor(faction, habSite); }
            catch (Exception e) { Plugin.Log.LogWarning($"[Economy] site factor: {e.Message}"); }
        }
    }

    /// <summary>
    /// The planner's "resource base when upkeep is insecure" rule (AlienHabPlanner.ManageHabGoals) founds a base on the
    /// site producing most of the insecure resources, defaulting to water when none is insecure, and its insecurity
    /// counts only ship maintenance (propellant = water). Inside that planner, the list the rule uses becomes: the
    /// vanilla-insecure resources the aliens are not swimming in, plus the resources they truly lack (need >= threshold).
    /// The planner asks twice: first through HasUpkeepInsecurityInTheFuture (gates a 1.5x cap overshoot; left alone),
    /// then for the rule's list (rewritten). Call order is fixed in build 1.0.53b.
    /// </summary>
    [HarmonyPatch(typeof(PavonisInteractive.TerraInvicta.Tasks.AlienHabPlanner), nameof(PavonisInteractive.TerraInvicta.Tasks.AlienHabPlanner.ManageHabGoals))]
    internal static class NeedDrivenResourceBaseScope
    {
        [ThreadStatic] internal static int Calls;
        [ThreadStatic] internal static bool Active;

        private static void Prefix(TIFactionState faction)
        {
            Active = Economy.MineOn && faction != null && faction.IsAlienFaction;
            Calls = 0;
        }

        private static Exception Finalizer(Exception __exception) { Active = false; return __exception; }
    }

    [HarmonyPatch(typeof(AIEvaluators), nameof(AIEvaluators.ResourcesExperiencingUpkeepInsecurityInTheFuture))]
    internal static class NeedDrivenResourceBase
    {
        private static void Postfix(TIFactionState faction, ref IEnumerable<FactionResource> __result)
        {
            if (!NeedDrivenResourceBaseScope.Active || ++NeedDrivenResourceBaseScope.Calls < 2 || faction == null) return;
            try
            {
                var need = Economy.Need(faction);
                float threshold = Plugin.Cfg.ResourceBaseNeedThreshold.Value;
                var vanilla = __result.ToList();
                var list = vanilla.Where(r => !need.TryGetValue(r, out var w) || w >= 1f)
                    .Union(Economy.Basic.Where(r => need[r] >= threshold)).Distinct().ToList();
                if (list.Count == 0) return;
                __result = list;
                if (!list.SequenceEqual(vanilla))
                    Session.Journal?.Write(new { t = GameRead.Now(), kind = "resource-base-need", vanilla = vanilla.Select(r => r.ToString()), now = list.Select(r => r.ToString()) });
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[Economy] resource-base rule: {e.Message}"); }
        }
    }

    /// <summary>The fixed alien base cap (maxAlienBaseGoals_*) becomes what exotics can carry. Only AlienHabPlanner reads it.</summary>
    [HarmonyPatch(typeof(TIGlobalConfig), nameof(TIGlobalConfig.GetMaxAlienBases))]
    internal static class ExoticsBaseCap
    {
        private static void Postfix(ref float __result)
        {
            if (!Economy.UpkeepOn) return;
            var alien = GameStateManager.AlienFaction();
            if (alien == null) return;
            try { __result = Economy.ExoticsBaseCap(alien); }
            catch (Exception e) { Plugin.Log.LogWarning($"[Economy] base cap: {e.Message}"); }
        }
    }
}
