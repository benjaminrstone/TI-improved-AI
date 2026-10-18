using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using PavonisInteractive.TerraInvicta.Tasks;

namespace ImprovedAI.Human
{
    /// <summary>
    /// The human AI track, step 1 (user, 2026-10-18): four vanilla bugs, verified by reading the code (docs/HUMAN-AI.md §6).
    /// Each is behind its own [HumanAI] switch, and VanillaAI turns them all off.
    /// </summary>
    internal static class HumanFixes
    {
        public static bool AI(TIFactionState f) => f != null && !f.IsAlienFaction && !f.isActivePlayer;
    }

    /// <summary>
    /// H1: direct investment never fires. ManageNations builds the influence budget as
    /// (influence * councilors.Count == maxCouncilSize) ? 0.2f : 0.1f, an operator-precedence slip, so the budget is
    /// the literal 0.1 or 0.2 influence and every direct-invest priority (all cost influence) is unaffordable.
    /// The intended budget is influence * (full council ? 0.2 : 0.1). It is applied on the first AttemptFundPriority
    /// call of each pass (the dictionary is passed by ref and spent down call by call).
    /// </summary>
    [HarmonyPatch(typeof(AIDailyFactionPlanner), nameof(AIDailyFactionPlanner.AttemptFundPriority))]
    internal static class FixDirectInvestBudget
    {
        private static readonly Dictionary<TIFactionState, string> Fixed = new Dictionary<TIFactionState, string>();

        private static void Prefix(TIFactionState faction, ref Dictionary<FactionResource, float> resourcesAvailable)
        {
            if (!Plugin.Cfg.UseFixDirectInvest || !HumanFixes.AI(faction) || resourcesAvailable == null) return;
            string today = GameRead.Now();
            if (Fixed.TryGetValue(faction, out var d) && d == today) return;   // already fixed for this pass
            Fixed[faction] = today;
            if (!resourcesAvailable.TryGetValue(FactionResource.Influence, out var inf) || inf > 1f) return;   // not the broken value
            float share = faction.councilors.Count == faction.maxCouncilSize ? 0.2f : 0.1f;
            resourcesAvailable[FactionResource.Influence] = faction.GetCurrentResourceAmount(FactionResource.Influence) * share;
        }
    }

    /// <summary>Diagnostic: every direct investment by an AI human faction.</summary>
    [HarmonyPatch(typeof(DirectInvestAction), MethodType.Constructor, new[] { typeof(TIFactionState), typeof(TINationState), typeof(PriorityType), typeof(float) })]
    internal static class DirectInvestJournal
    {
        private static void Postfix(TIFactionState faction, TINationState nation, PriorityType priority, float IPs)
        {
            if (!HumanFixes.AI(faction) || !Plugin.Cfg.DecisionJournal.Value) return;
            Session.Journal?.Write(new { t = GameRead.Now(), kind = "direct-invest", faction = GameRead.Faction(faction), nation = nation?.displayName, priority = priority.ToString(), ips = IPs });
        }
    }

    /// <summary>
    /// H2: "losing big" is unreachable. AnalyzeDeficiencies_Human tests Losing (strength &lt; 0.8 x the most powerful human
    /// enemy) before LosingBig (&lt; 0.5 x), so LosingBig only comes from the full-Earth-orbit case. Afterwards, a faction
    /// marked Losing whose strength is below half its strongest rival's becomes LosingBig (vanilla's own estimate).
    /// </summary>
    [HarmonyPatch(typeof(AIDailyFactionPlanner), "AnalyzeDeficiencies_Human")]
    internal static class FixLosingBig
    {
        private static void Postfix(TIFactionState faction, bool useAltMethod)
        {
            if (!Plugin.Cfg.UseFixLosingBig || !useAltMethod || faction == null || faction.selfAssessement != FactionSelfAssessment.Losing) return;
            var rival = faction.mostPowerfulHumanEnemy;
            if (rival == null) return;
            try
            {
                if (faction.GetFactionStrengthEstimate() < 0.5f * rival.GetFactionStrengthEstimate())
                    faction.selfAssessement = FactionSelfAssessment.LosingBig;
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[HumanAI] LosingBig: {e.Message}"); }
        }
    }

    /// <summary>
    /// H3: org buying stops at the first conflict. PurchaseOrgs walks its ranked candidates and BREAKS (where continue
    /// was meant) as soon as a candidate's org or councilor has already been used this pass, so it buys about one org
    /// per weekly run. Re-running the pass after it bought something (at most MaxOrgBuyPasses) continues past the
    /// conflict without editing IL: each pass re-ranks from the new state and the same per-org budget rules apply.
    /// </summary>
    [HarmonyPatch(typeof(AIDailyFactionPlanner), "PurchaseOrgs")]
    internal static class FixOrgBuying
    {
        [ThreadStatic] private static bool _inRerun;
        private static MethodInfo _method;

        private static void Prefix(TIFactionState faction, out int __state) => __state = SafeCount(faction);

        private static void Postfix(TIFactionState faction, Dictionary<TIOrgState, TICouncilorState> missionCriticalTransfers, List<TIMissionTemplate> requiredMissions,
            List<TIMissionTemplate> missingRequiredMissions, ref Dictionary<TICouncilorState, Dictionary<FactionResource, float>> councilorIncomes, bool chasingHydra,
            int factionWars, bool controllingNeutralPowers, List<TICouncilorState> criticalAdminNeed, int __state)
        {
            if (_inRerun || !Plugin.Cfg.UseFixOrgBuying || !HumanFixes.AI(faction)) return;
            int before = __state, now = SafeCount(faction), passes = 0, max = Math.Max(0, Plugin.Cfg.MaxOrgBuyPasses.Value);
            if (_method == null) _method = AccessTools.Method(typeof(AIDailyFactionPlanner), "PurchaseOrgs");
            _inRerun = true;
            try
            {
                while (now > before && passes < max)
                {
                    before = now;
                    var args = new object[] { faction, missionCriticalTransfers, requiredMissions, missingRequiredMissions, councilorIncomes, chasingHydra, factionWars, controllingNeutralPowers, criticalAdminNeed };
                    _method.Invoke(null, args);
                    councilorIncomes = (Dictionary<TICouncilorState, Dictionary<FactionResource, float>>)args[4];
                    now = SafeCount(faction);
                    passes++;
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[HumanAI] org buying: {e.Message}"); }
            finally { _inRerun = false; }
            if (passes > 0 && Plugin.Cfg.DecisionJournal.Value)
                Session.Journal?.Write(new { t = GameRead.Now(), kind = "org-buy-rerun", faction = GameRead.Faction(faction), passes, orgs = now - __state });
        }

        private static int SafeCount(TIFactionState f)
        {
            try { return f?.GetAllOrgs()?.Count ?? 0; } catch { return 0; }
        }
    }

    /// <summary>
    /// H4: the mission risk relaxation is dead. The AIMissionEntry constructor sets
    /// acceptableMinimumSuccess = 0.4 x riskAversion - min(payoff / 80000, 0.2) before payoff is assigned, so high-value
    /// missions never get their lower threshold. Recomputed here from the final payoff, with vanilla's own rules
    /// (non-objective floor 0.15, early-campaign caution). Expected utility doesn't use the threshold, so it stands.
    /// </summary>
    [HarmonyPatch(typeof(AIMissionEntry), MethodType.Constructor, new[] {
        typeof(AICouncilorMissionPlanner), typeof(TIMissionTemplate), typeof(TICouncilorState), typeof(TIGameState), typeof(float), typeof(List<TIMissionTemplate>),
        typeof(List<TIMissionTemplate>), typeof(bool), typeof(float), typeof(List<CampaignMilestone>), typeof(bool), typeof(float), typeof(List<TIFactionState>),
        typeof(TIRegionState), typeof(float), typeof(float), typeof(bool), typeof(float) })]
    internal static class FixMissionRisk
    {
        private static void Postfix(AIMissionEntry __instance, float riskAversion, bool objective_, float campaignDuration_years)
        {
            if (!Plugin.Cfg.UseFixMissionRisk || !HumanFixes.AI(__instance?.faction)) return;
            float v = 0.4f * riskAversion - UnityEngine.Mathf.Min(__instance.payoff / 80000f, 0.2f);
            if (!objective_)
            {
                v = UnityEngine.Mathf.Max(v, 0.15f);
                if (campaignDuration_years < 0.25f) v += 0.5f - campaignDuration_years * 2f;
            }
            __instance.acceptableMinimumSuccess = v;
        }
    }
}
