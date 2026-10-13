using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;

namespace ImprovedAI.Diagnostics
{
    /// <summary>
    /// What the alien fleet is made of (M2 step 4 gate): a monthly "fleet" journal record of live ships and active designs,
    /// and a "ship-built" record per alien ship completed. Weapon classes use the doctrine buckets (Missiles, Beams,
    /// Particles, Kinetics = magnetic/naval/plasma guns), counting attack weapons only (point defense is left out).
    /// </summary>
    internal static class FleetComposition
    {
        public static string SizeOf(TISpaceShipTemplate t)
        {
            try { return t.hullTemplate != null && t.hullTemplate.hugeHull ? "Huge" : t.size.ToString(); } catch { return "?"; }
        }

        public static List<string> AttackWeapons(TISpaceShipTemplate t)
        {
            try { return t.allWeaponTemplates.Where(w => w != null && w.attackMode).Select(AI.FleetDoctrine.Bucket).ToList(); }
            catch { return new List<string>(); }
        }

        private static Dictionary<string, int> Tally(IEnumerable<string> keys) =>
            keys.Where(k => k != null).GroupBy(k => k).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count());

        public static object Describe(TISpaceShipTemplate t) => new
        {
            design = t.dataName,
            role = t.role.ToString(),
            size = SizeOf(t),
            hull = t.hullName,
            exotics = t.requiresExotics,
            weapons = Tally(AttackWeapons(t)),
            scv = (float)Math.Round(GameRead.Safe(() => t.TemplateSpaceCombatValue()), 1),
        };

        public static void Monthly()
        {
            var alien = GameStateManager.AlienFaction();
            if (alien == null) return;
            var ships = alien.fleets.Where(f => f?.ships != null).SelectMany(f => f.ships).Where(s => s?.template != null).Select(s => s.template).ToList();
            var designs = (alien.shipDesigns ?? new List<TISpaceShipTemplate>()).Where(d => d != null && d.combatant && !d.Obsolete(alien)).ToList();
            Session.Journal?.Write(new
            {
                t = GameRead.Now(),
                kind = "fleet",
                ships = ships.Count,
                bySize = Tally(ships.Select(SizeOf)),
                byRole = Tally(ships.Select(x => x.role.ToString())),
                byHull = Tally(ships.Select(x => x.hullName)),
                weapons = Tally(ships.SelectMany(AttackWeapons)),
                designs = designs.Select(Describe).ToList(),
            });
        }
    }

    [HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.RecordShipBuilt))]
    internal static class ShipBuiltJournal
    {
        private static void Postfix(TIFactionState __instance, TISpaceShipTemplate template)
        {
            if (__instance == null || !__instance.IsAlienFaction || template == null || !Plugin.Cfg.DecisionJournal.Value) return;
            try
            {
                Session.Journal?.Write(new
                {
                    t = GameRead.Now(),
                    kind = "ship-built",
                    template.dataName,
                    role = template.role.ToString(),
                    size = FleetComposition.SizeOf(template),
                    hull = template.hullName,
                    weapons = FleetComposition.AttackWeapons(template),
                });
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[FleetComposition] ship-built: {e.Message}"); }
        }
    }
}
