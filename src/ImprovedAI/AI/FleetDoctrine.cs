using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Tasks;

namespace ImprovedAI.AI
{
    /// <summary>
    /// M2 step 4: fleet doctrines. The doctrine's fleet axes bias what the alien shipyards design and build, within what
    /// the aliens have unlocked. How vanilla designs alien ships (FINDINGS / M2-PLAN step 4):
    ///  - the ROLE locks the weapon class: short-range roles score lasers x1e7, medium-range magnetic/particle, long-range
    ///    missile/plasma, every other class /1e7 (TIShipWeaponTemplate.GetCuratedDesignScore);
    ///  - one design is kept per role x size x exotics category, the one with the best TemplateSpaceCombatValue;
    ///  - the build pick draws a role from the goal's secondary-role weights and a size from aiValues.fleetSmalls/Mediums,
    ///    then takes the best-SCV design of that role and size.
    /// So the levers are:
    ///  - weapon focus: the doctrine's class gets WeaponFocusOdds against the role's own class while designing, and a
    ///    design armed with it counts for more when designs are compared ("fit"), so it survives keep/retire and is built;
    ///  - hull philosophy: the build-pick size weights; Motherships also gives huge hulls a fit bonus;
    ///  - engagement style: Artillery / Brawler reweight long- / short-range roles; Assault adds capture slots (Dials).
    ///    Carrier has no effect yet: vanilla aliens have no fighters outside surface-to-orbit squadrons.
    /// The fit factor only applies while the aliens compare designs (design coroutine, build pick), never in combat
    /// evaluation, and never inside the designer itself.
    /// </summary>
    internal static class FleetDoctrine
    {
        public static bool On => Plugin.Cfg.UseFleetDoctrine && AlienStrategy.Current != null;

        /// <summary>&gt;0 while the alien designer is building a design (DesignAlienShip).</summary>
        [ThreadStatic] internal static int Designing;
        /// <summary>&gt;0 while the aliens compare designs (design keep/retire, build pick).</summary>
        [ThreadStatic] internal static int Judging;

        public static string Bucket(TIShipWeaponTemplate w)
        {
            if (w == null) return null;
            if (w.isMissileWeapon) return nameof(WeaponFocus.Missiles);
            if (w.isLaserWeapon) return nameof(WeaponFocus.Beams);
            if (w.isParticleWeapon) return nameof(WeaponFocus.Particles);
            if (w.isMagneticGunWeapon || w.isNavalGunWeapon || w.isPlasmaWeapon) return nameof(WeaponFocus.Kinetics);
            return w.weaponClass.ToString();
        }

        public static bool Matches(TIShipWeaponTemplate w, WeaponFocus focus) => focus != WeaponFocus.Balanced && Bucket(w) == focus.ToString();

        /// <summary>Share of a design's attack weapons in the doctrine's class (0 for Balanced or an unarmed design).</summary>
        public static float WeaponShare(TISpaceShipTemplate t, WeaponFocus focus)
        {
            if (focus == WeaponFocus.Balanced) return 0f;
            var attack = t.allWeaponTemplates.Where(w => w != null && w.attackMode).ToList();
            return attack.Count == 0 ? 0f : attack.Count(w => Matches(w, focus)) / (float)attack.Count;
        }

        /// <summary>How well a design fits the doctrine: 1 = neutral; never below 1 (a bonus, not a penalty).</summary>
        public static float Fit(TISpaceShipTemplate t)
        {
            if (t == null) return 1f;
            float fit = 1f + Dials.WeaponFitBonus * WeaponShare(t, Dials.WeaponFocus);
            if (Dials.MothershipFitBonus > 0f && t.hullTemplate != null && t.hullTemplate.hugeHull) fit *= 1f + Dials.MothershipFitBonus;
            return fit;
        }

        private static readonly HashSet<ShipRole> LongRange = new HashSet<ShipRole> { ShipRole.LL_Intruder, ShipRole.ML_Standoff, ShipRole.SL_Defender };
        private static readonly HashSet<ShipRole> ShortRange = new HashSet<ShipRole> { ShipRole.LS_Penetrator, ShipRole.MS_Strike, ShipRole.SS_Interceptor };

        public static float RoleWeight(ShipRole role)
        {
            switch (Dials.Engagement)
            {
                case EngagementStyle.Artillery: return LongRange.Contains(role) ? Dials.EngagementRoleWeight : 1f;
                case EngagementStyle.Brawler: return ShortRange.Contains(role) ? Dials.EngagementRoleWeight : 1f;
                default: return 1f;
            }
        }

        /// <summary>Ask the alien shipyards to redesign (the design coroutine runs at the next monthly check).</summary>
        public static void RequestRedesign(string why)
        {
            if (!On) return;
            var alien = GameStateManager.AlienFaction();
            if (alien == null) return;
            bool neutral = Dials.WeaponFocus == WeaponFocus.Balanced && Dials.Hulls == HullPhilosophy.Balanced && Dials.Engagement == EngagementStyle.Balanced;
            if (neutral) return;
            alien.updateShipDesignsFlag = true;
            Session.Journal?.Write(new { t = GameRead.Now(), kind = "redesign", why });
        }
    }

    // ---- Designer: the doctrine's weapon class competes with the role's own class ----

    [HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.DesignAlienShip))]
    internal static class FleetDoctrineDesignScope
    {
        private static void Prefix() => FleetDoctrine.Designing++;
        private static Exception Finalizer(Exception __exception) { FleetDoctrine.Designing--; return __exception; }
    }

    [HarmonyPatch(typeof(TIShipWeaponTemplate), nameof(TIShipWeaponTemplate.GetCuratedDesignScore))]
    internal static class FleetDoctrineWeaponScore
    {
        // The role multiplies its own classes by 1e7 and divides the rest by 1e7: 1e14 x odds puts the doctrine's class
        // at about `odds` to one against the role's choice (at equal raw DPS score).
        private static void Postfix(TIShipWeaponTemplate __instance, ref float __result)
        {
            if (FleetDoctrine.Designing <= 0 || !FleetDoctrine.On || !__instance.attackMode) return;
            if (!FleetDoctrine.Matches(__instance, Dials.WeaponFocus)) return;
            float odds = Dials.WeaponFocusOdds;
            if (odds > 0f && __result > 0f) __result *= 1e14f * odds;
        }
    }

    // ---- Judging designs: the keep/retire rule in the design coroutine and the build pick ----

    [HarmonyPatch]
    internal static class FleetDoctrineCoroutineScope
    {
        private static FieldInfo _faction;

        private static MethodBase TargetMethod() => AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(AIDailyFactionPlanner), "DesignShipsCoroutine"));

        private static void Prefix(object __instance, out bool __state)
        {
            __state = false;
            if (!FleetDoctrine.On) return;
            if (_faction == null) _faction = AccessTools.GetDeclaredFields(__instance.GetType()).FirstOrDefault(f => f.FieldType == typeof(TIFactionState));
            if (_faction?.GetValue(__instance) is TIFactionState f && f.IsAlienFaction) { FleetDoctrine.Judging++; __state = true; }
        }

        private static Exception Finalizer(Exception __exception, bool __state)
        {
            if (__state) FleetDoctrine.Judging--;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.GetDesiredShipToBuild))]
    internal static class FleetDoctrineBuildPick
    {
        private struct Saved { public bool active; public float smalls, mediums; }

        private static void Prefix(TIFactionState __instance, out Saved __state)
        {
            __state = default;
            if (!FleetDoctrine.On || __instance == null || !__instance.IsAlienFaction) return;
            __state = new Saved { active = true, smalls = __instance.aiValues.fleetSmalls, mediums = __instance.aiValues.fleetMediums };
            var w = Dials.BuildSizeWeights;
            __instance.aiValues.fleetSmalls *= w.smalls;
            __instance.aiValues.fleetMediums *= w.mediums;
            FleetDoctrine.Judging++;
        }

        /// <summary>What the build picker returned this month, by size (diagnostic: picks vs ships actually built).</summary>
        internal static readonly Dictionary<string, int> PickedSizes = new Dictionary<string, int>();

        private static void Postfix(TIFactionState __instance, TISpaceShipTemplate __result)
        {
            if (__result == null || __instance == null || !__instance.IsAlienFaction) return;
            string k = FleetComposition.SizeOf(__result);
            PickedSizes[k] = PickedSizes.TryGetValue(k, out var n) ? n + 1 : 1;
        }

        private static Exception Finalizer(Exception __exception, TIFactionState __instance, Saved __state)
        {
            if (__state.active)
            {
                __instance.aiValues.fleetSmalls = __state.smalls;
                __instance.aiValues.fleetMediums = __state.mediums;
                FleetDoctrine.Judging--;
            }
            return __exception;
        }
    }

    [HarmonyPatch(typeof(TISpaceShipTemplate), nameof(TISpaceShipTemplate.TemplateSpaceCombatValue))]
    internal static class FleetDoctrineFit
    {
        private static void Postfix(TISpaceShipTemplate __instance, ref float __result)
        {
            if (FleetDoctrine.Judging <= 0 || FleetDoctrine.Designing > 0 || __result <= 0f || !FleetDoctrine.On) return;
            __result *= FleetDoctrine.Fit(__instance);
        }
    }

    // ---- Engagement style: which combat roles alien fleet goals ask for ----

    [HarmonyPatch]
    internal static class FleetDoctrineRoles
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
            typeof(FactionGoal_Fleet).Assembly.GetTypes()
                .Where(t => typeof(FactionGoal_Fleet).IsAssignableFrom(t) && !t.IsAbstract)
                .Select(t => AccessTools.DeclaredMethod(t, nameof(FactionGoal_Fleet.GetSecondaryShipRoles)))
                .Where(m => m != null && !m.IsAbstract);

        private static void Postfix(FactionGoal_Fleet __instance, ref Dictionary<ShipRole, float> __result)
        {
            if (__result == null || !FleetDoctrine.On || __instance?.faction == null || !__instance.faction.IsAlienFaction) return;
            if (Dials.Engagement != EngagementStyle.Artillery && Dials.Engagement != EngagementStyle.Brawler) return;
            if (!__result.Keys.Any(r => FleetDoctrine.RoleWeight(r) != 1f)) return;
            // Vanilla returns shared (sometimes static) dictionaries: hand back a reweighted copy, never mutate them.
            __result = __result.ToDictionary(kv => kv.Key, kv => kv.Value * FleetDoctrine.RoleWeight(kv.Key));
        }
    }
}
