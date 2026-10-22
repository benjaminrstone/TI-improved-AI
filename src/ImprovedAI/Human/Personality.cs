using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ImprovedAI.AI;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using PavonisInteractive.TerraInvicta.Tasks;
using UnityEngine;

namespace ImprovedAI.Human
{
    /// <summary>
    /// Faction personalities and the gang-up web (docs/HUMAN-AI.md §9; user, 2026-10-19: keep gang-up-on-the-leader but
    /// smarter; anti-alien factions should feel very different; clients/minor allies of the leader among compatible
    /// factions). Replaces vanilla's human-to-human jealousy (ADFP.JealousyAndDeescalation: flat +1..+2.25 a month toward
    /// the strongest faction) with three per-personality terms:
    ///  - lead: the vanilla base scaled by the leader's strength over the runner-up's (0 in a close race, above vanilla
    ///    for a runaway); Initiative balances a close race and bandwagons a runaway instead;
    ///  - grudges: monthly hate from remembered injuries (missions, space, war), decayed at the personality's half-life;
    ///  - alien: while the aliens press, hate toward a leader at war with them is damped (anti-alien) or amplified
    ///    (pro-alien), graded by the pressure.
    /// Clients: a compatible weaker AI faction attaches to a strong patron (the player included): no jealousy either way,
    /// a non-aggression pact allowed (vanilla never allows one with the strongest faction). Injuries, war, the client
    /// becoming a contender or the patron falling behind break it. Everything else of the vanilla function is kept.
    /// </summary>
    internal static class Personality
    {
        internal enum Persona { Resist, HF, Exodus, Initiative, Academy, Protectorate, Servants, Other }

        internal sealed class Profile
        {
            public float Lead = 1f, GrudgeWeight = 1f, GrudgeHalfLife = 6f, AlienDamp, AlienAmp;
            /// <summary>Share of standing hate toward a human faction forgiven per month (vanilla: a flat -1 that
            /// hundreds of hate never notice, so late-game hate only ratchets up). Hate settles near injuries/forgive.</summary>
            public float Forgive = 0.03f;
            public bool Bandwagon;
            public float BreakGrudge = 4f;   // as a patron: client relations break at this grudge either way
        }

        private static readonly Dictionary<Persona, Profile> Profiles = new Dictionary<Persona, Profile>
        {
            // The coalition-builder: pragmatic, won't sabotage an anti-alien leader while the aliens press.
            [Persona.Resist] = new Profile { Lead = 1f, GrudgeWeight = 1f, GrudgeHalfLife = 6f, AlienDamp = 0.8f, Forgive = 0.03f },
            // The zealot: jealous, remembers, sees softness as betrayal; a harsh patron.
            [Persona.HF] = new Profile { Lead = 1.4f, GrudgeWeight = 1.5f, GrudgeHalfLife = 18f, AlienDamp = 0.3f, BreakGrudge = 1f, Forgive = 0f },
            // The isolationist: wants to be left alone; retaliates, then lets go.
            [Persona.Exodus] = new Profile { Lead = 0.4f, GrudgeWeight = 1.5f, GrudgeHalfLife = 2f, AlienDamp = 0.7f, Forgive = 0.15f },
            // The opportunist: balances a close race, bandwagons a runaway; profit first.
            [Persona.Initiative] = new Profile { Lead = 1f, GrudgeWeight = 1f, GrudgeHalfLife = 4f, Bandwagon = true, Forgive = 0.06f },
            [Persona.Academy] = new Profile { Lead = 1f, GrudgeWeight = 1f, GrudgeHalfLife = 6f, AlienAmp = 0.2f, Forgive = 0.04f },
            [Persona.Protectorate] = new Profile { AlienAmp = 0.6f, Forgive = 0.02f },
            [Persona.Servants] = new Profile { AlienAmp = 0.6f, Forgive = 0.02f },
            [Persona.Other] = new Profile(),
        };

        public static Persona Of(TIFactionState f)
        {
            switch (f?.ideology?.ideology)
            {
                case FactionIdeology.Resist: return Persona.Resist;
                case FactionIdeology.Destroy: return Persona.HF;
                case FactionIdeology.Escape: return Persona.Exodus;
                case FactionIdeology.Exploit: return Persona.Initiative;
                case FactionIdeology.Cooperate: return Persona.Academy;
                case FactionIdeology.Appease: return Persona.Protectorate;
                case FactionIdeology.Submit: return Persona.Servants;
                default: return Persona.Other;
            }
        }

        public static Profile ProfileOf(TIFactionState f) => Profiles[Of(f)];

        // ---- per-day cache: strengths and alien pressure -------------------------------------------------------------

        private static string _day;
        private static Dictionary<TIFactionState, float> _strength = new Dictionary<TIFactionState, float>();
        private static float _pressure;

        private static void Refresh()
        {
            string day = GameRead.Now();
            if (day == _day) return;
            _day = day;
            _strength = GameStateManager.AllHumanFactions().Where(x => x != null && x.IsActiveHumanFaction)
                .ToDictionary(x => x, x => Mathf.Max(0.001f, GameRead.Safe(() => x.GetFactionStrengthEstimate())));
            float p = 0f;
            try
            {
                p = AIEvaluators.ShouldAliensGoLoud() ? 0.4f : 0.1f;
                if (Escalation.Escalated) p += 0.4f;
                var alien = GameStateManager.AlienFaction();
                float alienScv = alien?.fleets.Sum(x => GameRead.Safe(() => x.SpaceCombatValue())) ?? 0f;
                float humanScv = GameStateManager.AllHumanFactions().Where(x => x != null).Sum(f => f.fleets.Sum(x => GameRead.Safe(() => x.SpaceCombatValue())));
                if (alienScv > humanScv) p += 0.2f;
            }
            catch { }
            _pressure = Mathf.Clamp01(p);
        }

        public static float Strength(TIFactionState f) { Refresh(); return f != null && _strength.TryGetValue(f, out var s) ? s : 0.001f; }

        /// <summary>The alien threat as the humans feel it, 0..1 (quiet phase 0.1, loud 0.4, +0.4 escalated, +0.2 alien fleet superiority).</summary>
        public static float AlienPressure { get { Refresh(); return _pressure; } }

        /// <summary>The faction's strength over the strongest other active human faction.</summary>
        public static float LeadRatio(TIFactionState f)
        {
            Refresh();
            float best = _strength.Where(kv => kv.Key != f).Select(kv => kv.Value).DefaultIfEmpty(0.001f).Max();
            return Strength(f) / Mathf.Max(0.001f, best);
        }

        public static TIFactionState Strongest()
        {
            Refresh();
            return _strength.Count == 0 ? null : _strength.OrderByDescending(kv => kv.Value).First().Key;
        }

        // ---- the three terms ---------------------------------------------------------------------------------------

        /// <summary>Lead factor: 0 below a 1.15 lead, 1 at 2x, 1.5 at 2.43x. Bandwagon: rises to 1 at 1.5x, then falls to 0.15 by 2.5x.</summary>
        public static float LeadFactor(float r, bool bandwagon)
        {
            if (!bandwagon) return Mathf.Clamp((r - 1.15f) / 0.85f, 0f, 1.5f);
            if (r <= 1.5f) return Mathf.Clamp01((r - 1f) / 0.5f);
            return Mathf.Max(0.15f, 1f - (r - 1.5f) * 0.85f);
        }

        /// <summary>Alien-aware multiplier on jealousy toward a leader at war with the aliens.</summary>
        public static float AlienMultiplier(TIFactionState faction, TIFactionState leader)
        {
            var alien = GameStateManager.AlienFaction();
            bool fighting = false;
            try { fighting = alien != null && (alien.AI_AtWarWithFaction(leader) || leader.AI_AtWarWithFaction(alien)); } catch { }
            if (!fighting) return 1f;
            var p = ProfileOf(faction);
            float pressure = AlienPressure;
            if (faction.antiAlien)
            {
                float damp = p.AlienDamp;
                // Humanity First has no patience for "collaborators and cowards" in the lead.
                if (Of(faction) == Persona.HF && (Of(leader) == Persona.Academy || Of(leader) == Persona.Exodus)) damp = 0f;
                return Mathf.Max(0f, 1f - damp * pressure);
            }
            if (faction.proAlien && leader.antiAlien) return 1f + p.AlienAmp * pressure;
            return 1f;
        }

        private static string Key(TIFactionState victim, TIFactionState actor) => $"{victim.templateName}>{actor.templateName}";

        public static float Grudge(TIFactionState victim, TIFactionState actor)
        {
            var s = AlienStrategy.Current;
            if (s?.grudges == null || victim == null || actor == null) return 0f;
            return s.grudges.TryGetValue(Key(victim, actor), out var g) ? g : 0f;
        }

        /// <summary>From the hate ledger: an injury (missions, space, war) raised the victim's hate toward the actor.</summary>
        internal static void OnInjury(TIFactionState victim, TIFactionState actor, float delta)
        {
            if (!Plugin.Cfg.UseHumanPersonalities || delta <= 0f) return;
            var s = AlienStrategy.Current;
            if (s == null) return;
            if (s.grudges == null) s.grudges = new Dictionary<string, float>();
            string k = Key(victim, actor);
            s.grudges[k] = (s.grudges.TryGetValue(k, out var g) ? g : 0f) + delta;
        }

        // ---- clients ----------------------------------------------------------------------------------------------

        public static TIFactionState PatronOf(TIFactionState client)
        {
            var s = AlienStrategy.Current;
            if (!Plugin.Cfg.UseHumanClients || s?.clients == null || client == null) return null;
            return s.clients.TryGetValue(client.templateName, out var p) ? GameStateManager.AllHumanFactions().FirstOrDefault(x => x?.templateName == p) : null;
        }

        public static bool ClientPair(TIFactionState a, TIFactionState b) =>
            a != null && b != null && (PatronOf(a) == b || PatronOf(b) == a);

        /// <summary>Who may be whose client (docs/HUMAN-AI.md §9, user answers 2026-10-19).</summary>
        public static bool Compatible(TIFactionState client, TIFactionState patron, out string why)
        {
            why = null;
            var c = Of(client); var p = Of(patron);
            switch (c)
            {
                case Persona.Exodus: return p == Persona.Resist || p == Persona.Academy || p == Persona.HF;
                case Persona.Academy: return p == Persona.Exodus || p == Persona.Resist;
                case Persona.Initiative:
                    if (LeadRatio(patron) < 2f) { why = "noRunaway"; return false; }
                    return true;   // profit first: pro-alien patrons too
                case Persona.Resist:
                    // Only under severe alien escalation: anti-alien unity.
                    if (!(Escalation.Escalated && AlienPressure >= 0.8f)) { why = "noEscalation"; return false; }
                    return p == Persona.HF || p == Persona.Exodus;
                default: return false;   // Humanity First is never a client; the pro-alien factions keep the vanilla feel
            }
        }

        private static bool AtWar(TIFactionState a, TIFactionState b)
        {
            try { return a.AI_AtWarWithFaction(b) || b.AI_AtWarWithFaction(a); } catch { return false; }
        }

        private static float BreakGrudge(TIFactionState patron) => ProfileOf(patron).BreakGrudge;

        /// <summary>Monthly: decay grudges, keep or break client relations, form new ones and offer the pact.</summary>
        public static void Monthly()
        {
            if (!Plugin.Cfg.UseHumanPersonalities) return;
            var s = AlienStrategy.Current;
            if (s == null) return;
            if (s.grudges == null) s.grudges = new Dictionary<string, float>();
            if (s.clients == null) s.clients = new Dictionary<string, string>();
            var humans = GameStateManager.AllHumanFactions().Where(x => x != null).ToList();
            foreach (var k in s.grudges.Keys.ToList())
            {
                var victim = humans.FirstOrDefault(x => x.templateName == k.Split('>')[0]);
                float half = victim == null ? 6f : Mathf.Max(0.5f, ProfileOf(victim).GrudgeHalfLife);
                float v = s.grudges[k] * Mathf.Pow(0.5f, 1f / half);
                if (v < 0.05f) s.grudges.Remove(k); else s.grudges[k] = v;
            }
            Forgiveness(humans);
            if (!Plugin.Cfg.UseHumanClients) return;
            var strongest = Strongest();
            float top = Strength(strongest);
            foreach (var c in humans.Where(x => FleetCompetence.AIHuman(x) && x.IsActiveHumanFaction))
            {
                var patron = PatronOf(c);
                if (patron != null)
                {
                    string brk = null;
                    if (!patron.IsActiveHumanFaction) brk = "patronGone";
                    else if (AtWar(c, patron)) brk = "war";
                    else if (Grudge(c, patron) >= BreakGrudge(patron) || Grudge(patron, c) >= BreakGrudge(patron)) brk = "injury";
                    else if (Strength(c) > 0.75f * Strength(patron)) brk = "contender";
                    else if (Strength(patron) < 0.7f * top) brk = "patronBehind";
                    else if (!Compatible(c, patron, out var w)) brk = w ?? "incompatible";
                    if (brk == null) { OfferPact(c, patron); continue; }
                    s.clients.Remove(c.templateName);
                    Journal(c, patron, "break", brk);
                    continue;
                }
                var best = humans.Where(x => x != c && x.IsActiveHumanFaction
                        && Strength(x) >= 0.85f * top && Strength(c) <= 0.6f * Strength(x)
                        && !AtWar(c, x) && Grudge(c, x) < 2f && Grudge(x, c) < 2f
                        && Compatible(c, x, out _))
                    .OrderByDescending(Strength).FirstOrDefault();
                if (best == null) continue;
                s.clients[c.templateName] = best.templateName;
                Journal(c, best, "form", null);
                OfferPact(c, best);
            }
        }

        /// <summary>Monthly: AI humans forgive a share of standing hate toward other human factions (Exodus 15%,
        /// Initiative 6%, Academy 4%, Resistance 3%, pro-alien 2%, Humanity First never).</summary>
        private static void Forgiveness(List<TIFactionState> humans)
        {
            foreach (var f in humans.Where(FleetCompetence.AIHuman))
            {
                float share = ProfileOf(f).Forgive;
                if (share <= 0f) continue;
                foreach (var o in humans)
                {
                    if (o == f) continue;
                    float h = GameRead.Safe(() => f.GetFactionHate(o));
                    if (h <= 1f) continue;
                    try { f.GainFactionHate(o, -share * h, cantConflagrate: true, "Personality forgiveness", randomize: false); }
                    catch { }
                }
            }
        }

        /// <summary>AI patron: the client offers a non-aggression pact the vanilla way (a treaty trade). A player
        /// patron proposes it from the diplomacy screen; the client accepts (see the CanTradeNAP patches).</summary>
        private static void OfferPact(TIFactionState client, TIFactionState patron)
        {
            if (!patron.player.isAI) return;
            try
            {
                if (client.HasNAP(patron) || !client.CanTradeNAP(patron) || !patron.CanTradeNAP(client)) return;
                var a = new TradeOffer(client) { treatyType = TradeOffer.TreatyType.NAP };
                var b = new TradeOffer(patron) { treatyType = TradeOffer.TreatyType.NAP };
                client.playerControl.StartAction(new DiplomacyTradeAction(client, patron, a, b, 1f));
                Journal(client, patron, "pact", null);
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[Personality] pact {client.templateName}->{patron.templateName}: {e.Message}"); }
        }

        private static void Journal(TIFactionState client, TIFactionState patron, string what, string why)
        {
            Plugin.Log.LogInfo($"[Personality] {what} {client.templateName} -> {patron.templateName} {why}");
            if (Plugin.Cfg.DecisionJournal.Value)
                Session.Journal?.Write(new
                {
                    t = GameRead.Now(), kind = "human-client", what, why,
                    client = GameRead.Faction(client), patron = GameRead.Faction(patron),
                    clientStrength = (float)Math.Round(Strength(client), 1), patronStrength = (float)Math.Round(Strength(patron), 1),
                    lead = (float)Math.Round(LeadRatio(patron), 2), pressure = AlienPressure,
                });
        }

        // ---- the jealousy function ---------------------------------------------------------------------------------

        /// <summary>
        /// ADFP.JealousyAndDeescalation for an AI human toward a human faction, with vanilla's jealousy block replaced by
        /// the personality terms. Vanilla order is kept: periodic -1 decay, shared-enemy de-escalation, then the
        /// jealousy block (clamped at 0), the victory-objective +1; then the grudge term.
        /// </summary>
        internal static float Jealousy(TIFactionState faction, TIFactionState enemy, bool generalDeescalation, bool processPeriodicChange)
        {
            if (faction.permanentAlly(enemy)) return 0f;
            var mt = faction.GetMostThreateningEnemyHumanFaction();
            var mt2 = enemy.GetMostThreateningEnemyHumanFaction();
            bool gate = enemy.player.isAI || TINationState.GetIdeologicalDistance(faction.ideology, enemy.ideology) >= TemplateManager.global.AI_GangUpOnLeaderBehavior_MinIdeologicalDistance_Difficulty();
            bool client = ClientPair(faction, enemy);
            float num = 0f;
            bool periodic = false;
            if (mt != enemy || generalDeescalation)
            {
                periodic = processPeriodicChange;
                if (mt != null && mt == mt2)
                {
                    num -= 1f;
                    if ((faction.proAlien && mt.antiAlien) || (faction.antiAlien && mt.proAlien)) num -= 1f;
                }
            }
            if (gate)
            {
                if (mt == enemy && !client)
                {
                    var p = ProfileOf(faction);
                    float b = 0f;
                    switch (faction.selfAssessement)
                    {
                        case FactionSelfAssessment.Even: b = 1f; break;
                        case FactionSelfAssessment.Losing: b = 1.75f; break;
                        case FactionSelfAssessment.LosingBig: b = 2.25f; break;
                    }
                    float j = b * LeadFactor(LeadRatio(enemy), p.Bandwagon) * p.Lead;
                    if ((faction.veryProAlien && enemy.veryAntiAlien) || (faction.veryAntiAlien && enemy.veryProAlien)) j += 1.4f;
                    j *= AlienMultiplier(faction, enemy);
                    if ((faction.proAlien && enemy.proAlien) || (faction.antiAlien && enemy.antiAlien))
                        j -= TITimeState.CampaignDuration_years_Exact() < 7f ? 3f : 0.75f;
                    num = Mathf.Max(0f, num + j);
                }
                if (!faction.permanentAlly(enemy) && enemy.unlockedVictoryObjective) num += 1f;
            }
            // Grudges: an injured faction keeps hating the actor for a while (per personality). Not between a client and
            // its patron: the injury itself breaks the relation (and the pact, through vanilla's rising-hate check).
            if (!client) num += 0.1f * ProfileOf(faction).GrudgeWeight * Grudge(faction, enemy);
            // The strategist's rivals and friends (docs/STRATEGIST.md): +/-1.5 hate a month, on top of the personality.
            int stance = Strategist.Directives.Stance(faction, enemy);
            if (stance != 0) num = Mathf.Max(stance < 0 ? -1.5f : 0f, num + 1.5f * stance);
            if (periodic) faction.GainFactionHate(enemy, -1f, cantConflagrate: true, "Periodic hate");
            return num;
        }
    }

    [HarmonyPatch(typeof(AIDailyFactionPlanner), nameof(AIDailyFactionPlanner.JealousyAndDeescalation))]
    internal static class PersonalityJealousy
    {
        private static bool Prefix(TIFactionState faction, TIFactionState enemyFaction, bool generalDeescalation, bool processPeriodicChange, ref float __result)
        {
            if (!Plugin.Cfg.UseHumanPersonalities || faction == null || enemyFaction == null || faction.IsAlienFaction || enemyFaction.IsAlienFaction
                || faction.player == null || !faction.player.isAI) return true;
            try { __result = Personality.Jealousy(faction, enemyFaction, generalDeescalation, processPeriodicChange); return false; }
            catch (Exception e) { Plugin.Log.LogWarning($"[Personality] jealousy {faction.templateName}: {e.Message}"); return true; }
        }
    }

    /// <summary>A client and its patron may hold a non-aggression pact although the patron is the strongest faction
    /// (vanilla: never with mostPowerfulHumanEnemy).</summary>
    [HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.CanTradeNAP))]
    internal static class PersonalityClientNAP
    {
        private static void Postfix(TIFactionState __instance, TIFactionState otherFaction, ref bool __result)
        {
            if (__result || !Plugin.Cfg.UseHumanClients || !Personality.ClientPair(__instance, otherFaction)) return;
            try
            {
                if (__instance.HasNAP(otherFaction) || __instance.permanentAlly(otherFaction)) return;
                if (__instance.AI_AtWarWithFaction(otherFaction) || otherFaction.AI_AtWarWithFaction(__instance)) return;
                if (!new FactionGoal_NonAggressionPact(__instance, 4, otherFaction).ValidNewGoal()) return;
                __result = true;
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(AIEvaluators), nameof(AIEvaluators.GetWillingnessToTradeNAP))]
    internal static class PersonalityClientNAPWillingness
    {
        private static void Postfix(TIFactionState faction, TIFactionState otherFaction, ref int __result)
        {
            if (__result < 1 && Plugin.Cfg.UseHumanClients && Personality.ClientPair(faction, otherFaction) && faction.CanTradeNAP(otherFaction)) __result = 1;
        }
    }
}
