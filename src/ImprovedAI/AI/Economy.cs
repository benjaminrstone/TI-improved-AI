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
    /// M2 step 6: the alien economy (DESIGN P2 "Mine what you lack", "Exotics are the imported edge").
    ///
    /// Mine what you lack. Vanilla picks alien base sites by percent change in income, mostly gross revenue: stockpiles
    /// and ship-construction spend never enter it, so metals and nobles are spent to nothing while water and volatiles
    /// pile up past 400k (FINDINGS). Here each basic resource gets a NEED weight: what the aliens actually spend it on
    /// (the material mix of their live fleet) against what they hold. A site's score is multiplied by the
    /// production-weighted mean need of what it yields, so a metal-rich site outranks another ice moon.
    ///
    /// Exotics upkeep instead of a fixed base cap. Every alien base costs exotics each month, and so does every capital
    /// hull; the wormhole drip pays for it. The base cap becomes what the drip plus a slice of the stockpile can carry.
    /// Bases and warships now compete for the same drip, and the 7.8k-exotics hoard of the 2039 save becomes expansion.
    /// The scripted base goals (Jupiter, belt, Neptune -> Uranus -> Saturn) still bypass the cap, as in vanilla.
    /// </summary>
    internal static class Economy
    {
        public static bool MineOn => Plugin.Cfg.UseMineWhatYouLack;
        public static bool UpkeepOn => Plugin.Cfg.UseExoticsUpkeep;

        internal static readonly FactionResource[] Basic = { FactionResource.Water, FactionResource.Volatiles, FactionResource.Metals, FactionResource.NobleMetals, FactionResource.Fissiles };

        private static Dictionary<FactionResource, float> _need;
        private static string _needDate;

        public static void Reset() { _need = null; _needDate = null; }

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
        /// A resource the fleet is made of but the stockpile lacks scores high; one that only piles up scores low.
        /// Recomputed once per game day at most.
        /// </summary>
        public static Dictionary<FactionResource, float> Need(TIFactionState alien)
        {
            string today = GameRead.Now();
            if (_need != null && _needDate == today) return _need;
            var shares = SpendShares(alien);
            var stock = Basic.ToDictionary(r => r, r => Mathf.Max(0f, alien.GetCurrentResourceAmount(r)));
            float total = stock.Values.Sum();
            float max = Mathf.Max(1f, Plugin.Cfg.NeedWeightMax.Value);
            _need = Basic.ToDictionary(r => r, r => Mathf.Clamp(shares[r] * total / Mathf.Max(1f, stock[r]), 1f / max, max));
            _needDate = today;
            return _need;
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

        // ---- Exotics upkeep ----

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

        /// <summary>Gross monthly exotics income (the wormhole drip, before upkeep).</summary>
        public static float Drip(TIFactionState alien) => Mathf.Max(0f, GameRead.Safe(() => alien.GetMonthlyIncome(FactionResource.Exotics)));

        /// <summary>How many bases the drip plus a slice of the stockpile can carry, after capital-ship upkeep.</summary>
        public static int ExoticsBaseCap(TIFactionState alien)
        {
            float perBase = Plugin.Cfg.BaseExoticsUpkeep.Value;
            if (perBase <= 0f) return 999;
            MonthlyUpkeep(alien, out _, out float ships);
            float stockSlice = Mathf.Max(0f, alien.GetCurrentResourceAmount(FactionResource.Exotics)) / Mathf.Max(1f, Plugin.Cfg.ExoticsStockHorizonMonths.Value);
            float budget = Drip(alien) + stockSlice - ships;
            return Mathf.Clamp(Mathf.FloorToInt(budget / perBase), 0, Plugin.Cfg.MaxAlienBasesCeiling.Value);
        }

        /// <summary>
        /// Monthly: charge the upkeep (when on) and record the economy (need weights, cap, upkeep, stocks). The record is
        /// also written on vanilla, with what the mod WOULD weigh and cap, so baselines compare directly.
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
            }
            Session.Journal?.Write(new
            {
                t = GameRead.Now(),
                kind = "economy",
                mine = MineOn, upkeepOn = UpkeepOn,
                need = Need(alien).ToDictionary(kv => kv.Key.ToString(), kv => (float)Math.Round(kv.Value, 2)),
                shares = SpendShares(alien).ToDictionary(kv => kv.Key.ToString(), kv => (float)Math.Round(kv.Value, 3)),
                stock = Basic.Concat(new[] { FactionResource.Exotics }).ToDictionary(r => r.ToString(), r => (float)Math.Round(alien.GetCurrentResourceAmount(r))),
                drip, upkeep, baseUpkeep = baseUp, shipUpkeep = shipUp, charged,
                bases = Bases(alien),
                cap,
            });
        }
    }

    /// <summary>Alien base-site and mine scoring: multiply by the site's need factor.</summary>
    [HarmonyPatch(typeof(AIEvaluators), nameof(AIEvaluators.EvaluateHabModule_PercentChange))]
    internal static class MineWhatYouLackPercent
    {
        private static void Postfix(TIFactionState faction, TIGameState location, TIHabModuleTemplate moduleTemplate, bool moduleComparison, ref float __result)
        {
            if (!Economy.MineOn || faction == null || !faction.IsAlienFaction || moduleComparison || __result <= 0f) return;
            if (moduleTemplate == null || !moduleTemplate.mine || !(location is TIHabSiteState site)) return;
            try { __result *= Economy.SiteFactor(faction, site); }
            catch (Exception e) { Plugin.Log.LogWarning($"[Economy] site factor: {e.Message}"); }
        }
    }

    /// <summary>The other alien site scorer (choosing a site on a body; the scripted Neptune/Uranus/Saturn goals).</summary>
    [HarmonyPatch(typeof(AIEvaluators), nameof(AIEvaluators.EvaluateHabSite))]
    internal static class MineWhatYouLackSite
    {
        private static void Postfix(TIFactionState faction, TIHabSiteState habSite, ref float __result)
        {
            if (!Economy.MineOn || faction == null || !faction.IsAlienFaction || habSite == null || __result <= 0f) return;
            try { __result *= Economy.SiteFactor(faction, habSite); }
            catch (Exception e) { Plugin.Log.LogWarning($"[Economy] site factor: {e.Message}"); }
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
