using System;
using System.Collections.Generic;
using System.Linq;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using UnityEngine;

namespace ImprovedAI.AI
{
    /// <summary>
    /// The aliens use their human proxies as a supply line (user, 2026-10-17). Earth goods reach them through the
    /// pro-alien factions (Servants, Protectorate; maybe the Academy later), never through Earth's open market
    /// (Economy.NoEarthPurchases). Vanilla allows such trades but no AI trade with the aliens happened in 6 x 2-year
    /// soaks: nothing starts them. Here the alien command makes the offer itself.
    ///  - Partners: pro-alien human factions (ideology x &lt; 0) that are not the active player and that the aliens are
    ///    not at war with.
    ///  - Wanted: what the aliens lack (Economy need weight >= ProxyTradeNeed: in practice metals and noble metals), up to
    ///    vanilla's own per-trade cap on the partner (TradeAI.GetMaximumTradeQuantity).
    ///  - Paid: what the aliens are swimming in (need &lt;= 0.5: water, volatiles, fissiles), up to their own cap, until
    ///    the partner gains ProxyTradeMargin x what it gives up, priced by vanilla's TradeAI.GetTradeValue. Resources
    ///    only: vanilla's agreement builder can also move habs, control points and orgs, and a hab handed to the aliens
    ///    is destroyed.
    ///  - Cadence: every ProxyTradeIntervalMonths per partner; monthly for Puppet master and Divide and rule.
    /// Honest dealing [lore]: the Hydras never trick a partner; a deal the partner would not gain from is not offered.
    /// The player can see and cut the line: weaken or flip the proxies, or strip what they can give.
    /// </summary>
    internal static class ProxyTrade
    {
        public static bool On => Plugin.Cfg.UseProxyTrade && AlienStrategy.Current != null;

        private static float Gain(TIFactionState f, FactionResource r, float q) => TradeAI.GetTradeValue(f, r, q);
        private static float Loss(TIFactionState f, FactionResource r, float q) => -TradeAI.GetTradeValue(f, r, -q);

        private static bool AtWar(TIFactionState alien, TIFactionState h)
        {
            try { return alien.GoalsOfType(GoalType.WarOnFaction).Any(g => g.target()?.ref_faction == h); } catch { return false; }
        }

        public static void Monthly()
        {
            if (!On) return;
            var alien = GameStateManager.AlienFaction();
            if (alien == null) return;
            var s = AlienStrategy.Current;
            string month = GameRead.Now()?.Substring(0, 7);
            int interval = Math.Max(1, Plugin.Cfg.ProxyTradeIntervalMonths.Value);
            var focus = s.doctrine?.focus;
            if (focus == StrategicFocus.PuppetMaster || focus == StrategicFocus.DivideAndRule) interval = 1;

            foreach (var h in GameStateManager.AllHumanFactions())
            {
                if (h == null || !h.proAlien || h.isActivePlayer || AtWar(alien, h)) continue;
                string k = GameRead.Faction(h);
                if (s.lastProxyTrade.TryGetValue(k, out var last) && MonthsBetween(last, month) < interval) continue;
                try { if (TryTrade(alien, h)) s.lastProxyTrade[k] = month; }
                catch (Exception e) { Plugin.Log.LogWarning($"[ProxyTrade] {k}: {e.Message}"); }
            }
        }

        private static int MonthsBetween(string a, string b)
        {
            try
            {
                int ya = int.Parse(a.Substring(0, 4)), ma = int.Parse(a.Substring(5, 2)), yb = int.Parse(b.Substring(0, 4)), mb = int.Parse(b.Substring(5, 2));
                return (yb - ya) * 12 + (mb - ma);
            }
            catch { return 999; }
        }

        private static bool TryTrade(TIFactionState alien, TIFactionState partner)
        {
            TradeAI.PrepareCachesForTrading(alien, partner);
            var need = Economy.Need(alien);
            float wantNeed = Plugin.Cfg.ProxyTradeNeed.Value, margin = Plugin.Cfg.ProxyTradeMargin.Value;
            var wantRes = Economy.Basic.Where(r => need[r] >= wantNeed && partner.UnlockedResource(r) && alien.UnlockedResource(r)).ToList();
            var payRes = Economy.Basic.Where(r => need[r] <= 0.5f && partner.UnlockedResource(r) && alien.UnlockedResource(r)).ToList();
            if (wantRes.Count == 0 || payRes.Count == 0) return false;

            for (float scale = 1f; scale >= 0.25f; scale *= 0.5f)
            {
                var want = wantRes.Select(r => (r, q: Mathf.Floor(TradeAI.GetMaximumTradeQuantity(partner, r) * scale))).Where(x => x.q >= 10f).ToList();
                if (want.Count == 0) return false;
                float partnerGives = want.Sum(x => Loss(partner, x.r, x.q));
                float alienGets = want.Sum(x => Gain(alien, x.r, x.q));

                // Pay in surplus, in chunks, cheapest-to-us first, until the partner comes out ahead by the margin.
                var pay = new Dictionary<FactionResource, float>();
                float partnerGets = 0f;
                foreach (var r in payRes.OrderBy(r => need[r]))
                {
                    float cap = Mathf.Floor(TradeAI.GetMaximumTradeQuantity(alien, r));
                    float step = Mathf.Max(10f, Mathf.Floor(cap / 10f));
                    while (partnerGets < partnerGives * margin && (pay.TryGetValue(r, out var q0) ? q0 : 0f) + step <= cap)
                    {
                        pay[r] = (pay.TryGetValue(r, out var q) ? q : 0f) + step;
                        partnerGets = pay.Sum(x => Gain(partner, x.Key, x.Value));
                    }
                    if (partnerGets >= partnerGives * margin) break;
                }
                float alienGives = pay.Sum(x => Loss(alien, x.Key, x.Value));
                bool ok = partnerGets >= partnerGives * margin && alienGets > alienGives;
                Session.Journal?.Write(new
                {
                    t = GameRead.Now(), kind = "proxy-trade", partner = GameRead.Faction(partner), ok, scale,
                    want = want.ToDictionary(x => x.r.ToString(), x => x.q), pay = pay.ToDictionary(x => x.Key.ToString(), x => x.Value),
                    partnerGives, partnerGets, alienGives, alienGets,
                });
                if (!ok) continue;

                var alienOffer = new TradeOffer(alien);
                alienOffer.resourceValues = pay.Select(x => new ResourceValue(x.Key, x.Value)).ToList();
                var partnerOffer = new TradeOffer(partner);
                partnerOffer.resourceValues = want.Select(x => new ResourceValue(x.r, x.q)).ToList();
                alien.playerControl.StartAction(new DiplomacyTradeAction(alien, partner, alienOffer, partnerOffer, 1f));
                return true;
            }
            return false;
        }
    }
}
