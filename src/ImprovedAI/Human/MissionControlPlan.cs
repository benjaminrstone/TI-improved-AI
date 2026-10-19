using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using PavonisInteractive.TerraInvicta.Tasks;
using UnityEngine;

namespace ImprovedAI.Human
{
    /// <summary>
    /// Mission control: the hard cap on AI human fleets (2039 probe, 2026-10-19). Generic MC sat at 0 all year for five
    /// of six AI factions: their habs consume all of it, ships need MC too, and money, boost and metals piled up.
    /// User, 2026-10-19: "grow MC much harder, and reserve MC for the fleet. Retiring weak habs is usually not optimal:
    /// develop a hab enough to have an MC building and positive resource flow, or don't found it."
    ///  - MC buildings: vanilla builds standard modules only with generic MC available (HumanHabPlanner
    ///    BuildHabModules, HabMayBeUpgraded), so a starved faction can never build the buildings that would end the
    ///    starvation. Monthly, a starved faction first upgrades Operations Centers (4 MC) to Command Centers (10), with
    ///    the MC offline during the 240-day build capped, then starts the best MC building it can afford on habs that
    ///    have none (one per hab, a game limit). Administration buildings are excluded (see IsMCBuilding).
    ///  - Fleet reserve: while warships are wanted, FOUNDING new habs may only use generic MC beyond FleetMCReserve x MC
    ///    income (the founding checks and the found-hab MC indulgence see the reserve). Expansion waits; new MC goes to
    ///    ships first. Existing habs are never retired.
    /// </summary>
    internal static class MissionControlPlan
    {
        [ThreadStatic] internal static TIFactionState FoundingScope;

        public static float Reserve(TIFactionState f) =>
            Plugin.Cfg.UseHumanFleetMCReserve && FleetCompetence.AIHuman(f) && FleetCompetence.WantsWarships(f)
                ? Mathf.Max(0f, Plugin.Cfg.HumanFleetMCReserve.Value) * Mathf.Max(0f, GameRead.Safe(() => f.MissionControlIncome))
                : 0f;

        /// <summary>
        /// MC buildings in the sense of this plan: modules that produce MC without a boost upkeep, i.e. the Operations
        /// Center (4 MC, $30/month) and Command Center (10 MC, $100/month). The Administration buildings cost boost upkeep
        /// and give little MC (1-2): they pay off through their percent bonus to a hab's production, on very high-value
        /// habs or in Earth orbit when starved for CP cap (user, 2026-10-19), which is a separate rule (not built yet).
        /// </summary>
        private static bool IsMCBuilding(TIHabModuleTemplate m) =>
            m != null && m.missionControl > 0 && GameRead.Safe(() => m.MonthlySupportCost(FactionResource.Boost, false)) <= 0f;

        private static bool HasMCBuilding(TIHabState h) =>
            h.AllModules().Any(m => m != null && !m.destroyed && IsMCBuilding(m.moduleTemplate));

        private static float MoneyUpkeep(TIHabModuleTemplate m, TIFactionState f, TIHabState h) =>
            Mathf.Max(0f, GameRead.Safe(() => m.MonthlySupportCost(FactionResource.Money, true, f, h)));

        /// <summary>Monthly: MC buildings on developed habs of starved AI human factions.</summary>
        public static void Monthly()
        {
            if (!Plugin.Cfg.UseHumanMCBuildings) return;
            foreach (var f in GameStateManager.AllHumanFactions())
            {
                if (!FleetCompetence.AIHuman(f) || GameRead.Safe(() => f.AI_GenericMissionControlAvailable) > 0f) continue;
                int started = 0, max = Math.Max(0, Plugin.Cfg.HumanMCBuildingsPerMonth.Value);
                var log = new List<string>();
                var why = new Dictionary<string, int>();
                void Why(string k) => why[k] = why.TryGetValue(k, out var n) ? n + 1 : 1;
                bool hasOps = false, hasCmd = false;
                try
                {
                    hasOps = f.completedProjects.Any(p => p.dataName == "Project_OperationsCenter");
                    hasCmd = f.completedProjects.Any(p => p.dataName == "Project_CommandCenter");
                }
                catch { }
                // Money upkeep must be carried: net monthly money income after all upkeep added this pass, or a stock
                // covering MCUpkeepMonths of it (Ops Center $30/month, Command Center $100/month).
                float netMoney = GameRead.Safe(() => f.GetMonthlyIncome(FactionResource.Money));
                float stock = f.GetCurrentResourceAmount(FactionResource.Money), added = 0f;
                float months = Mathf.Max(1f, Plugin.Cfg.HumanMCUpkeepMonths.Value);
                // First: upgrade MC buildings (Operations Center 4 MC -> Command Center 10 MC). In 2039 the player's 65 MC
                // buildings averaged ~7 MC, the AI factions' ~4: nearly all Ops Centers. +6 MC each, within the
                // one-MC-building-per-hab limit; needs the module upgrade unlocked and allowed (tier-3 hab), the extra
                // power, and the extra money upkeep carried.
                // The old module produces nothing during the 240-day build, so MC offline in upgrades is capped at
                // MCUpgradeOfflineShare x MC income (counts under-construction MC buildings that upgrade from one).
                float offline = 0f, offlineCap = Mathf.Max(0f, Plugin.Cfg.HumanMCUpgradeOffline.Value) * Mathf.Max(0f, GameRead.Safe(() => f.MissionControlIncome));
                try
                {
                    offline = f.habs.Where(x => x != null).SelectMany(x => x.AllModules())
                        .Where(m => m != null && !m.destroyed && m.underConstruction && IsMCBuilding(m.moduleTemplate) && IsMCBuilding(m.moduleTemplate.UpgradesFrom))
                        .Sum(m => (float)m.moduleTemplate.UpgradesFrom.missionControl);
                }
                catch { }
                try
                {
                    foreach (var h in f.habs.Where(x => x != null && !x.decommissioning).OrderByDescending(x => x.tier).ToList())
                    {
                        if (started >= max) break;
                        var mod = h.AllModules().FirstOrDefault(m => m != null && !m.destroyed && !m.underConstruction && IsMCBuilding(m.moduleTemplate));
                        if (mod == null) continue;
                        if (offline + mod.moduleTemplate.missionControl > offlineCap) { Why("upgradeOffline"); continue; }
                        TIHabModuleTemplate up = null;
                        try { if (mod.CanUpgrade(f)) up = mod.moduleTemplate.UpgradeModuleTemplate(f, checkUnlocked: true); } catch { }
                        if (up == null || !IsMCBuilding(up) || up.missionControl <= mod.moduleTemplate.missionControl) { Why("noUpgrade"); continue; }
                        float power = GameRead.Safe(() => h.NetPower(includeUnderConstruction: true, includeDeactivated: true));
                        if (power + (up.power - mod.moduleTemplate.power) < 0) { Why("upgradePower"); continue; }
                        float extra = MoneyUpkeep(up, f, h) - MoneyUpkeep(mod.moduleTemplate, f, h);
                        if (!(netMoney - added - extra >= 0f || stock >= (added + extra) * months)) { Why("upgradeUpkeep"); continue; }
                        var cost = up.CostFromSpace(f, h, isUpgrade: true, substituteBoost: false, 0, dontRecalculateIncome: true);
                        if (!cost.CanAfford_AI(f, null, null, 15)) { Why("upgradeAfford"); continue; }
                        string from = mod.moduleTemplate.dataName;   // the action replaces the module at once
                        int fromMC = mod.moduleTemplate.missionControl;
                        f.playerControl.StartAction(new BuildHabModuleAction(up, mod.sector, mod.slot, cost));
                        added += Mathf.Max(0f, extra);
                        offline += fromMC;
                        started++;
                        log.Add($"{from}->{up.dataName}@{h.displayName}");
                    }
                }
                catch (Exception e) { Plugin.Log.LogWarning($"[HumanAI] MC upgrades {f.templateName}: {e.Message}"); }
                try
                {
                    foreach (var h in f.habs.Where(x => x != null && !x.decommissioning && x.coreModule != null && !x.coreModule.underConstruction)
                                            .OrderByDescending(x => x.tier).ToList())
                    {
                        if (started >= max) break;
                        var slots = h.AvailableSlots();
                        if (slots == null || slots.Count == 0) { Why("noSlot"); continue; }
                        // One MC building per hab is a game limit for everyone (user, 2026-10-19).
                        if (HasMCBuilding(h)) { Why("hasMC"); continue; }
                        float power = GameRead.Safe(() => h.NetPower(includeUnderConstruction: true, includeDeactivated: true));
                        var allowed = h.AllowedModules(f).Where(IsMCBuilding).ToList();
                        if (allowed.Count == 0) { Why("notAllowed"); continue; }
                        var powered = allowed.Where(m => m.power >= 0 || power + m.power >= 0).ToList();
                        if (powered.Count == 0) { Why("power"); continue; }
                        var upkept = powered.Where(m => { float up = MoneyUpkeep(m, f, h); return netMoney - added - up >= 0f || stock >= (added + up) * months; }).ToList();
                        if (upkept.Count == 0) { Why("upkeep"); continue; }
                        var module = upkept.OrderByDescending(m => m.missionControl).FirstOrDefault(m => slots.Any(s => s.IsModuleValidForSlot(m)));
                        if (module == null) { Why("slotType"); continue; }
                        var slot = slots.First(s => s.IsModuleValidForSlot(module));
                        var cost = module.CostFromSpace(f, h, isUpgrade: false, substituteBoost: false);
                        if (!cost.CanAfford_AI(f, null, null, 15)) { Why("afford"); continue; }
                        f.playerControl.StartAction(new BuildHabModuleAction(module, slot.sector, slot.slot, cost));
                        added += MoneyUpkeep(module, f, h);
                        started++;
                        log.Add($"{module.dataName}@{h.displayName}");
                    }
                }
                catch (Exception e) { Plugin.Log.LogWarning($"[HumanAI] MC buildings {f.templateName}: {e.Message}"); }
                if (Plugin.Cfg.DecisionJournal.Value)
                    Session.Journal?.Write(new { t = GameRead.Now(), kind = "human-mc-building", faction = GameRead.Faction(f), started, modules = log, why, hasOps, hasCmd, netMoney, stock, offline, offlineCap });
            }
        }
    }

    /// <summary>Scope: HumanHabPlanner.FoundHabs (founding new habs) sees the fleet MC reserve.</summary>
    [HarmonyPatch(typeof(HumanHabPlanner), nameof(HumanHabPlanner.FoundHabs))]
    internal static class FleetMCReserveScope
    {
        private static void Prefix(TIFactionState faction) => MissionControlPlan.FoundingScope = faction;
        private static Exception Finalizer(Exception __exception) { MissionControlPlan.FoundingScope = null; return __exception; }
    }

    [HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.AI_GenericMissionControlAvailable), MethodType.Getter)]
    internal static class FleetMCReserveGeneric
    {
        private static void Postfix(TIFactionState __instance, ref int __result)
        {
            if (MissionControlPlan.FoundingScope != __instance || __instance == null) return;
            float r = MissionControlPlan.Reserve(__instance);
            if (r > 0f) __result = Mathf.Max(0, __result - Mathf.CeilToInt(r));
        }
    }

    /// <summary>The found-hab MC indulgence (systems with 4+ sites) doesn't override the fleet reserve.</summary>
    [HarmonyPatch(typeof(FactionGoal_FoundHab), nameof(FactionGoal_FoundHab.GrantMissionControlIndulgence), MethodType.Getter)]
    internal static class FleetMCReserveIndulgence
    {
        private static void Postfix(FactionGoal_FoundHab __instance, ref bool __result)
        {
            if (!__result) return;
            var f = __instance?.faction;
            if (f == null || MissionControlPlan.FoundingScope != f) return;
            float r = MissionControlPlan.Reserve(f);
            if (r > 0f && GameRead.Safe(() => (float)f.AI_GenericMissionControlAvailable) <= 0f) __result = false;
        }
    }
}
