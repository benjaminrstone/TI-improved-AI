using System;
using System.Collections.Generic;
using System.Linq;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using UnityEngine;

namespace ImprovedAI.Human
{
    /// <summary>
    /// Metals are the next fleet bottleneck (docs/HUMAN-AI.md, 2034 2-year soak): the Initiative sat at 0 metals all run
    /// and the Academy near 1k while money idled at $30-100k and Exodus held 24-38k metals. Vanilla AI humans never trade
    /// with each other (0 human-to-human trades in four 2-year runs): nothing starts a deal. Monthly, an AI human that
    /// wants warships and is short of a ship metal (stock below MarketShortStock) buys it with money from another AI
    /// human with a surplus (stock above MarketSurplusStock) that it isn't at war with and doesn't hate much.
    ///  - Quantity: at most MarketShareOfSurplus of the seller's stock above its own surplus line, and vanilla's per-trade
    ///    cap (TradeAI.GetMaximumTradeQuantity).
    ///  - Price: money, priced by vanilla's TradeAI.GetTradeValue, until the seller gains MarketMargin x what it gives
    ///    up; the buyer must value the metal more than the money, and pays at most half its money.
    /// The player is never a party (AI-to-AI only).
    /// </summary>
    internal static class HumanMarket
    {
        private static readonly FactionResource[] Wanted = { FactionResource.Metals, FactionResource.NobleMetals };

        private static float Gain(TIFactionState f, FactionResource r, float q) => TradeAI.GetTradeValue(f, r, q);
        private static float Loss(TIFactionState f, FactionResource r, float q) => -TradeAI.GetTradeValue(f, r, -q);

        private static bool Hostile(TIFactionState a, TIFactionState b)
        {
            try
            {
                if (a.AI_AtWarWithFaction(b) || b.AI_AtWarWithFaction(a)) return true;
                return a.GetFactionHate(b) > Plugin.Cfg.HumanMarketMaxHate.Value || b.GetFactionHate(a) > Plugin.Cfg.HumanMarketMaxHate.Value;
            }
            catch { return true; }
        }

        public static void Monthly()
        {
            if (!Plugin.Cfg.UseHumanMarket) return;
            var ais = GameStateManager.AllHumanFactions().Where(x => FleetCompetence.AIHuman(x) && x.IsActiveHumanFaction).ToList();
            float shortStock = Plugin.Cfg.HumanMarketShortStock.Value, surplusStock = Plugin.Cfg.HumanMarketSurplusStock.Value;
            var sold = new HashSet<(TIFactionState, FactionResource)>();
            foreach (var buyer in ais)
            {
                if (!FleetCompetence.WantsWarships(buyer)) continue;
                foreach (var r in Wanted)
                {
                    if (!buyer.UnlockedResource(r) || buyer.GetCurrentResourceAmount(r) >= shortStock) continue;
                    var sellers = ais.Where(s => s != buyer && s.UnlockedResource(r) && !sold.Contains((s, r))
                                                 && s.GetCurrentResourceAmount(r) > surplusStock && !Hostile(buyer, s))
                                     .OrderByDescending(s => s.GetCurrentResourceAmount(r)).ToList();
                    foreach (var seller in sellers)
                    {
                        try { if (TryBuy(buyer, seller, r)) { sold.Add((seller, r)); break; } }
                        catch (Exception e) { Plugin.Log.LogWarning($"[HumanMarket] {buyer.templateName}<-{seller.templateName} {r}: {e.Message}"); }
                    }
                }
            }
        }

        private static bool TryBuy(TIFactionState buyer, TIFactionState seller, FactionResource r)
        {
            TradeAI.PrepareCachesForTrading(buyer, seller);
            float surplus = seller.GetCurrentResourceAmount(r) - Plugin.Cfg.HumanMarketSurplusStock.Value;
            float q = Mathf.Floor(Mathf.Min(surplus * Plugin.Cfg.HumanMarketShareOfSurplus.Value, TradeAI.GetMaximumTradeQuantity(seller, r)));
            if (q < 50f) return false;
            float sellerGives = Loss(seller, r, q), buyerGets = Gain(buyer, r, q), margin = Plugin.Cfg.HumanMarketMargin.Value;
            float moneyCap = Mathf.Min(buyer.GetCurrentResourceAmount(FactionResource.Money) * 0.5f, TradeAI.GetMaximumTradeQuantity(buyer, FactionResource.Money));
            float step = Mathf.Max(50f, Mathf.Floor(moneyCap / 20f)), money = 0f, sellerGets = 0f;
            while (sellerGets < sellerGives * margin && money + step <= moneyCap)
            {
                money += step;
                sellerGets = Gain(seller, FactionResource.Money, money);
            }
            float buyerGives = Loss(buyer, FactionResource.Money, money);
            bool ok = money > 0f && sellerGets >= sellerGives * margin && buyerGets > buyerGives;
            if (Plugin.Cfg.DecisionJournal.Value)
                Session.Journal?.Write(new
                {
                    t = GameRead.Now(), kind = "human-market", ok, buyer = GameRead.Faction(buyer), seller = GameRead.Faction(seller),
                    resource = r.ToString(), q, money, sellerGives, sellerGets, buyerGives, buyerGets,
                    buyerStock = (float)Math.Round(buyer.GetCurrentResourceAmount(r)), sellerStock = (float)Math.Round(seller.GetCurrentResourceAmount(r)),
                });
            if (!ok) return false;
            var buyerOffer = new TradeOffer(buyer) { resourceValues = new List<ResourceValue> { new ResourceValue(FactionResource.Money, money) } };
            var sellerOffer = new TradeOffer(seller) { resourceValues = new List<ResourceValue> { new ResourceValue(r, q) } };
            buyer.playerControl.StartAction(new DiplomacyTradeAction(buyer, seller, buyerOffer, sellerOffer, 1f));
            return true;
        }
    }
}
