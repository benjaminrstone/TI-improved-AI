using System;
using System.Collections.Generic;
using System.Linq;
using ImprovedAI.Diagnostics;
using PavonisInteractive.TerraInvicta;

namespace ImprovedAI.Harness
{
    /// <summary>
    /// Scenario-injection commands for probes, for things the game's debug terminal can't do. In a probe's
    /// "commands", lines starting with "ai." run here; everything else goes to the game's terminal.
    ///
    ///   ai.foundoutposts &lt;faction&gt; &lt;region&gt; &lt;count&gt; [mine]
    ///       Found tier-1 outposts (optionally with a mining complex) at free hab sites, one per body.
    ///       region: innerbelt | midbelt | outerbelt | belt | centaur | kbo | body:&lt;name&gt;
    ///   ai.spawnfleet &lt;faction&gt; &lt;system&gt; &lt;count&gt; [missile|best|design:&lt;dataName&gt;]
    ///       Build a fleet of N copies of one of the faction's designs, docked at its hab in that system (or in Earth
    ///       orbit). "missile" = its strongest design armed with missiles (default), "best" = strongest design.
    ///   ai.doctrine focus=Raider weapons=Missiles hulls=Swarm engagement=Assault puppetry=0.2 forward=0.8
    ///       Force (part of) the alien doctrine for this run.
    ///   ai.drainammo &lt;faction&gt; &lt;system&gt;
    ///       Empty every magazine on that faction's fleets in a system (e.g. Earth). Weapons without ammo count as
    ///       inoperable, so missile fleets drop to ~0 SCV: the "unresupplied bait" trick (EXPLOITS E1).
    /// </summary>
    internal static class HarnessCommands
    {
        public static bool TryRun(string line, List<string> output)
        {
            if (!line.StartsWith("ai.", StringComparison.OrdinalIgnoreCase)) return false;
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            try
            {
                switch (parts[0].ToLowerInvariant())
                {
                    case "ai.foundoutposts":
                        output.Add(FoundOutposts(parts[1], parts[2], int.Parse(parts[3]), parts.Length > 4 && parts[4].Equals("mine", StringComparison.OrdinalIgnoreCase)));
                        break;
                    case "ai.spawnfleet":
                        output.Add(SpawnFleet(parts[1], parts[2], int.Parse(parts[3]), parts.Length > 4 ? parts[4] : "missile"));
                        break;
                    case "ai.doctrine":
                        output.Add(AI.AlienStrategy.Force(line.Substring(parts[0].Length).Trim()));
                        break;
                    case "ai.drainammo":
                        output.Add(DrainAmmo(parts[1], parts[2]));
                        break;
                    default:
                        output.Add("! unknown harness command " + parts[0]);
                        break;
                }
            }
            catch (Exception e)
            {
                output.Add($"! {line}: {e.GetType().Name}: {e.Message}");
                Plugin.Log.LogError($"[Harness] {line}: {e}");
            }
            return true;
        }

        private static TIFactionState Faction(string name) =>
            GameStateManager.AllFactions().FirstOrDefault(f => f.templateName.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("no faction " + name);

        private static Func<TISpaceBodyState, bool> Region(string region)
        {
            switch (region.ToLowerInvariant())
            {
                case "innerbelt": return b => b.innerMainBeltAsteroid(false);
                case "midbelt": return b => b.midMainBeltAsteroid(false);
                case "outerbelt": return b => b.outerMainBeltAsteroid(false);
                case "belt": return b => b.innerMainBeltAsteroid(false) || b.midMainBeltAsteroid(false) || b.outerMainBeltAsteroid(false);
                case "centaur": return b => b.centaur(false);
                case "kbo": return b => b.kuiperBeltObject(false);
            }
            if (region.StartsWith("body:", StringComparison.OrdinalIgnoreCase))
            {
                string n = region.Substring(5);
                return b => string.Equals(b.displayName, n, StringComparison.OrdinalIgnoreCase) || string.Equals(b.templateName, n, StringComparison.OrdinalIgnoreCase);
            }
            throw new ArgumentException("unknown region " + region);
        }

        private static string SpawnFleet(string factionName, string system, int count, string pick)
        {
            var faction = Faction(factionName);
            var designs = faction.shipDesigns.Where(d => d != null).ToList();
            bool HasMissiles(TISpaceShipTemplate d) => d.noseWeapons.Concat(d.hullWeapons).Any(w => w?.moduleTemplate?.ref_weapon is TIMissileTemplate);
            TISpaceShipTemplate design;
            // Prefer the faction's own designs; fall back to other human factions' (e.g. a laser/coilgun player has no missile boats).
            var others = GameStateManager.AllHumanFactions().Where(f => f != faction).SelectMany(f => f.shipDesigns.Where(d => d != null)).ToList();
            TISpaceShipTemplate Choose(List<TISpaceShipTemplate> pool)
            {
                if (pick.StartsWith("design:", StringComparison.OrdinalIgnoreCase))
                    return pool.FirstOrDefault(d => string.Equals(d.dataName, pick.Substring(7), StringComparison.OrdinalIgnoreCase));
                return (pick.Equals("best", StringComparison.OrdinalIgnoreCase) ? pool : pool.Where(HasMissiles))
                    .OrderByDescending(d => GameRead.Safe(() => d.TemplateSpaceCombatValue())).FirstOrDefault();
            }
            design = Choose(designs) ?? Choose(others);
            if (design == null)
                throw new ArgumentException($"no {pick} design for {factionName}; designs: " + string.Join("; ", designs.Select(d =>
                    d.dataName + "[" + string.Join(",", d.noseWeapons.Concat(d.hullWeapons).Select(w => w?.moduleTemplate?.ref_weapon?.GetType().Name + ":" + w?.moduleTemplate?.dataName)) + "]")));

            TIGameState location = faction.habs.Where(h => string.Equals(GameRead.System(h), system, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(h => h.IsStation).ThenByDescending(h => h.tier).FirstOrDefault();
            if (location == null && system.Equals("Earth", StringComparison.OrdinalIgnoreCase)) location = GameStateManager.Earth().interfaceOrbits.First();
            if (location == null) throw new ArgumentException($"no {factionName} hab in {system}");

            var ships = new List<TISpaceShipState>();
            for (int i = 0; i < count; i++)
            {
                var ship = (TISpaceShipState)design.CreateGameState();
                ship.InitWithTemplate(design);
                ship.CompleteShipInitialization();
                ship.SetDisplayName(TISpaceAssetState.GetRandomAssetName(ship, faction));
                ships.Add(ship);
            }
            var fleet = TISpaceFleetState.CreateAtRunTime(faction, ships, location, null);
            float scv = GameRead.Safe(() => fleet.SpaceCombatValue());
            Session.Journal?.Write(new { t = GameRead.Now(), kind = "inject", cmd = "spawnfleet", faction = factionName, system, count, design = design.dataName, designName = design.displayName, fleet = fleet.displayName, scv, at = GameRead.Name(location) });
            return $"> spawned {fleet.displayName}: {count} x {design.displayName} ({design.dataName}) at {GameRead.Name(location)}, SCV {scv:F0}";
        }

        private static string DrainAmmo(string factionName, string system)
        {
            var faction = Faction(factionName);
            int fleets = 0, ships = 0;
            float before = 0, after = 0;
            foreach (var fleet in faction.fleets.Where(f => string.Equals(GameRead.System(f), system, StringComparison.OrdinalIgnoreCase)))
            {
                fleets++;
                before += GameRead.Safe(() => fleet.SpaceCombatValue());
                foreach (var ship in fleet.ships)
                {
                    if (ship.ammo == null || ship.ammo.Count == 0) continue;
                    ships++;
                    foreach (var k in ship.ammo.Keys.ToList()) ship.ammo[k] = 0;
                    ship.spaceCombatValueDataDirty = true;
                }
                after += GameRead.Safe(() => fleet.SpaceCombatValue());
            }
            Session.Journal?.Write(new { t = GameRead.Now(), kind = "inject", cmd = "drainammo", faction = factionName, system, fleets, ships, scvBefore = before, scvAfter = after });
            return $"> drained {ships} ships in {fleets} {factionName} fleets at {system}: SCV {before:F0} -> {after:F0}";
        }

        private static string FoundOutposts(string factionName, string region, int count, bool mine)
        {
            var faction = Faction(factionName);
            var inRegion = Region(region);
            var sites = GameStateManager.IterateByClass<TIHabSiteState>()
                .Where(s => s.hab == null && s.parentBody != null && inRegion(s.parentBody))
                .GroupBy(s => s.parentBody)
                .OrderBy(g => g.Key.displayName, StringComparer.Ordinal)
                .Select(g => g.OrderBy(s => s.displayName, StringComparer.Ordinal).First())
                .Take(count)
                .ToList();
            var founded = new List<string>();
            foreach (var site in sites)
            {
                var hab = GameStateManager.CreateNewGameState<TIHabState>();
                hab.InitializeNewHab(faction, site, faction, 1, 0.05f);
                if (mine) hab.ConstructFoundingModule(faction.IsAlienFaction ? "AlienOutpostMiningComplex" : "OutpostMiningComplex", 0, 1, 0.05f);
                founded.Add(GameRead.Body(site));
            }
            Session.Journal?.Write(new { t = GameRead.Now(), kind = "inject", cmd = "foundoutposts", faction = factionName, region, count = founded.Count, mine, bodies = founded });
            return $"> founded {founded.Count}/{count} {(mine ? "mining " : "")}outposts for {factionName} in {region}: {string.Join(", ", founded)}";
        }
    }
}
