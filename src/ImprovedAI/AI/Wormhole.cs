using System;
using HarmonyLib;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;
using UnityEngine;

namespace ImprovedAI.AI
{
    /// <summary>
    /// The widening wormhole: P2's soft clock (DESIGN "Two clocks"). The Hydras slowly widen the wormhole, so the
    /// exotics drip grows over the campaign: the principled, visible cause of alien escalation over time, and the other
    /// half of the exotics-upkeep balance (user, 2026-10-13: bases and warships compete; widening is part of the balance).
    ///
    /// v1: the drip multiplier (TIGlobalConfig.AI_GetExoticsMultiplier, read only for alien exotics income) is scaled by
    /// W = 1 + rate(difficulty) x calendar campaign years, capped. Calendar years, because vanilla already multiplies
    /// the drip by the alien progression speed. Builds on, does not replace, vanilla's setup ramp (alienSetupStartIncome
    /// -> EndIncome, which scales all alien hab income in the Dark Skies scenarios).
    /// Later: strikes at the terminus set the widening back (sidecar), and the terminus project (the hard clock).
    /// </summary>
    internal static class Wormhole
    {
        public static bool On => Plugin.Cfg.UseWidening;

        public static float CampaignYears
        {
            get { try { return Mathf.Max(0f, TITimeState.CampaignDuration_years_Exact()); } catch { return 0f; } }
        }

        public static float RatePerYear => Mathf.Max(0f, Dials.ByDifficulty(Plugin.Cfg.WideningPerYear.Value, 0.05f));

        /// <summary>Current widening factor on the exotics drip (1 = vanilla).</summary>
        public static float Widening => On ? Mathf.Min(Mathf.Max(1f, Plugin.Cfg.WideningMax.Value), 1f + RatePerYear * CampaignYears) : 1f;
    }

    [HarmonyPatch(typeof(TIGlobalConfig), nameof(TIGlobalConfig.AI_GetExoticsMultiplier))]
    internal static class WideningDrip
    {
        private static void Postfix(ref float __result)
        {
            if (!Wormhole.On) return;
            try { __result *= Wormhole.Widening; }
            catch (Exception e) { Plugin.Log.LogWarning($"[Wormhole] {e.Message}"); }
        }
    }
}
