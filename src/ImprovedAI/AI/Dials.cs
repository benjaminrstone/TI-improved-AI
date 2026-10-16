using System;
using UnityEngine;

namespace ImprovedAI.AI
{
    /// <summary>
    /// The effective value of every behaviour dial: the config value (DIALS.md) scaled by the current alien doctrine
    /// (M2 step 2). AI code reads dials only through here, so one table defines what each doctrine means and a
    /// per-difficulty curve can later be layered in the same place.
    /// With [Strategy] off (or no doctrine yet) every multiplier is 1 and the raw config values apply.
    /// </summary>
    internal static class Dials
    {
        private struct Mod
        {
            public float superiority, poolMargin, reserve, raidSlots, captureSlots, raidImportance, hateWeight, arrivalFight;
            public static Mod One => new Mod { superiority = 1, poolMargin = 1, reserve = 1, raidSlots = 1, captureSlots = 1, raidImportance = 0, hateWeight = 1, arrivalFight = 1 };
        }

        /// <summary>
        /// First-pass doctrine table (docs/M2-PLAN.md step 2). Multipliers on the config dials; raidImportance is additive.
        /// Behaviour that needs new code (expansion, councilor ops, ship design, rally points) arrives in later steps.
        /// </summary>
        private static Mod ForFocus(StrategicFocus f)
        {
            var m = Mod.One;
            switch (f)
            {
                case StrategicFocus.FortressIndustrial: m.superiority = 1.25f; m.reserve = 1.6f; m.raidSlots = 0.5f; m.captureSlots = 0.5f; m.raidImportance = -2; break;
                case StrategicFocus.Bastion: m.superiority = 1.3f; m.reserve = 2f; m.raidSlots = 0; m.captureSlots = 0.5f; m.raidImportance = -3; break;
                case StrategicFocus.PuppetMaster: m.raidSlots = 0.5f; m.captureSlots = 0.5f; m.raidImportance = -1; break;
                case StrategicFocus.QualityOverQuantity: m.superiority = 1.2f; m.poolMargin = 1.2f; break;
                case StrategicFocus.Xenoformer: m.raidSlots = 0.5f; m.captureSlots = 0.75f; break;
                case StrategicFocus.LandGrab: m.captureSlots = 1.5f; break;
                case StrategicFocus.Raider: m.raidSlots = 2f; m.captureSlots = 2f; m.raidImportance = 2; m.hateWeight = 1.5f; m.superiority = 0.9f; break;
                case StrategicFocus.EarthPresence: m.arrivalFight = 0.85f; break;
                case StrategicFocus.Hoarder: m.poolMargin = 1.6f; m.superiority = 1.5f; m.reserve = 0.6f; m.raidSlots = 0.5f; break;
                case StrategicFocus.Strangler: m.raidSlots = 2f; m.captureSlots = 2f; m.raidImportance = 1; break;
                case StrategicFocus.DivideAndRule: m.hateWeight = 2.5f; break;
                case StrategicFocus.Blitz: m.superiority = 0.75f; m.reserve = 0.5f; m.raidSlots = 1.5f; m.arrivalFight = 0.8f; break;
                case StrategicFocus.HunterKiller: m.raidSlots = 0.5f; m.captureSlots = 0.5f; m.arrivalFight = 0.85f; break;
            }
            return m;
        }

        private static Mod Current()
        {
            var d = AlienStrategy.Current?.doctrine;
            if (d == null || !Plugin.Cfg.UseStrategy) return Mod.One;
            var m = ForFocus(d.focus);
            // Political posture: a puppet-leaning command spends less of its fleet on raids.
            float posture = 1.25f - 0.5f * Mathf.Clamp01(d.puppetry);
            m.raidSlots *= posture;
            m.captureSlots *= posture;
            // Engagement style: an assault-minded command keeps more marine captures going.
            if (Plugin.Cfg.UseFleetDoctrine && d.engagement == EngagementStyle.Assault) m.captureSlots *= Plugin.Cfg.AssaultCaptureSlots.Value;
            // P4 escalation: a war they could lose. Loss aversion relaxes, reserves are committed, raids multiply.
            if (Escalation.Escalated)
            {
                float e = Mathf.Clamp(Plugin.Cfg.EscalationBoldness.Value, 0f, 0.9f);
                m.superiority *= 1f - e;              // 0.25 -> x0.75
                m.arrivalFight *= 1f - 0.6f * e;      // -> x0.85
                m.reserve *= 1f - 2f * e;             // -> x0.5
                m.raidSlots *= 1f + 2f * e;           // -> x1.5
                m.captureSlots *= 1f + 2f * e;
                m.raidImportance += 2;
                m.hateWeight *= 1f + 2f * e;
            }
            return m;
        }

        /// <summary>Share of the exotics drip the base cap may commit to upkeep; escalation diverts part of it to war.</summary>
        public static float SustainableDripShare => Mathf.Clamp01(Plugin.Cfg.SustainableDripShare.Value * (Escalation.Escalated ? Plugin.Cfg.EscalationDripShareFactor.Value : 1f));

        // ---- Fleet doctrine (M2 step 4). Neutral (1 / Balanced) when [Fleet] FleetDoctrine is off. ----

        private static Doctrine FleetAxes => Plugin.Cfg.UseFleetDoctrine ? AlienStrategy.Current?.doctrine : null;

        public static WeaponFocus WeaponFocus => FleetAxes?.weapons ?? WeaponFocus.Balanced;
        public static HullPhilosophy Hulls => FleetAxes?.hulls ?? HullPhilosophy.Balanced;
        public static EngagementStyle Engagement => FleetAxes?.engagement ?? EngagementStyle.Balanced;

        public static float WeaponFocusOdds => Mathf.Max(0f, Plugin.Cfg.WeaponFocusOdds.Value);
        public static float WeaponFitBonus => Mathf.Max(0f, Plugin.Cfg.WeaponFitBonus.Value);
        public static float MothershipFitBonus => Hulls == HullPhilosophy.Motherships ? Mathf.Max(0f, Plugin.Cfg.MothershipFitBonus.Value) : 0f;
        public static float EngagementRoleWeight => Mathf.Max(0f, Plugin.Cfg.EngagementRoleWeight.Value);

        /// <summary>
        /// Multipliers on vanilla aiValues.fleetSmalls / fleetMediums (the Large weight is fixed at 1 in vanilla).
        /// Swarm shifts the build mix one bias factor per size step toward small hulls; Capital and Motherships toward large.
        /// </summary>
        public static (float smalls, float mediums) BuildSizeWeights
        {
            get
            {
                float b = Mathf.Max(1f, Plugin.Cfg.HullSizeBias.Value);
                switch (Hulls)
                {
                    case HullPhilosophy.Swarm: return (b * b, b);
                    case HullPhilosophy.Capital:
                    case HullPhilosophy.Motherships: return (1f / (b * b), 1f / b);
                    default: return (1f, 1f);
                }
            }
        }

        public static float AttackSuperiority => Plugin.Cfg.AlienAttackSuperiority.Value * Current().superiority;
        public static float ShortcutSuperiority => Plugin.Cfg.ShortcutSuperiority.Value * Mathf.Min(1f, Current().superiority);
        public static float PoolMargin => Plugin.Cfg.PoolMargin.Value * Current().poolMargin;
        public static float PoolReserveFraction => Mathf.Clamp01(Plugin.Cfg.PoolReserveFraction.Value * Current().reserve);
        public static int MaxRaidGoals => Math.Max(0, (int)Math.Round(Plugin.Cfg.MaxRaidGoals.Value * Current().raidSlots));
        public static int MaxCaptureGoals => Math.Max(0, (int)Math.Round(Plugin.Cfg.MaxCaptureGoals.Value * Current().captureSlots));
        public static int RaidImportance => Mathf.Clamp(Plugin.Cfg.RaidImportance.Value + (int)Current().raidImportance, 1, 19);
        public static float RaidHateWeight => Plugin.Cfg.RaidHateWeight.Value * Current().hateWeight;
        public static float ArrivalFightRatio => Plugin.Cfg.ArrivalFightRatio.Value * Current().arrivalFight;

        // ---- Per-difficulty dials (vanilla difficulty 1 Cinematic .. 4 Brutal) ----

        internal static int Difficulty
        {
            get { try { return Mathf.Clamp(PavonisInteractive.TerraInvicta.TIGlobalValuesState.GlobalValues.difficulty, 1, 4); } catch { return 2; } }
        }

        /// <summary>Picks this difficulty's entry from a "C,N,V,B" config list; falls back to the Normal entry, then to <paramref name="fallback"/>.</summary>
        internal static float ByDifficulty(string list, float fallback)
        {
            var parts = (list ?? "").Split(',');
            float Parse(int i) => i < parts.Length && float.TryParse(parts[i].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : float.NaN;
            float x = Parse(Difficulty - 1);
            if (float.IsNaN(x)) x = Parse(1);
            return float.IsNaN(x) ? fallback : x;
        }

        /// <summary>Starting contempt before vanilla's learned respect is applied: arrogant on easy, wary on hard.</summary>
        public static float StartingContempt => Mathf.Clamp01(Plugin.Cfg.StartingContempt.Value + ByDifficulty(Plugin.Cfg.ContemptDifficultyOffset.Value, 0f));
        /// <summary>Multiplier on contempt losses: how fast the aliens learn respect.</summary>
        public static float ContemptLearning => Mathf.Max(0f, ByDifficulty(Plugin.Cfg.ContemptLearning.Value, 1f));

        /// <summary>Snapshot for the journal, so every run records the effective dials it played with.</summary>
        public static object Snapshot() => new
        {
            AttackSuperiority, ShortcutSuperiority, PoolMargin, PoolReserveFraction, MaxRaidGoals, MaxCaptureGoals, RaidImportance, RaidHateWeight, ArrivalFightRatio,
            Difficulty, StartingContempt, ContemptLearning, Escalated = Escalation.Escalated, SustainableDripShare,
            WeaponFocus = WeaponFocus.ToString(), Hulls = Hulls.ToString(), Engagement = Engagement.ToString(),
            WeaponFocusOdds, WeaponFitBonus, MothershipFitBonus, EngagementRoleWeight,
            BuildSmalls = BuildSizeWeights.smalls, BuildMediums = BuildSizeWeights.mediums,
        };
    }
}
