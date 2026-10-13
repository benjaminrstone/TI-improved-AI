# M2 plan: a different opponent each game

**Order (agreed 2026-10-12):**
1. The doctrine framework, with the Raider capability as its first real doctrine.
2. The disposition model (contempt × hate).
3. The economy fixes (mine what you lack; base cap → exotics upkeep).

This doc turns DESIGN P2/P3 and the disposition section into code-level steps. Each step lands behind config switches and is proven with probes or soaks before the next one starts.

## Step 1: Alien strategy state and persistence ✅ (2026-10-12)

**Gate passed.**
- The 2031 and 2034 saves of one campaign derive the identical doctrine.
- Reloading a mod-made save reads it back from its sidecar.
- The campaign key is `TIGlobalValuesState.realWorldCampaignStart` (when the campaign was created) plus the start template.
- The user's accel campaign: FortressIndustrial / puppetry 0.62 / forward 0.91 / Particles / Motherships / Balanced.

There is one `AlienStrategy` object per campaign. It holds:
- the doctrine;
- contempt per human faction;
- doctrine-failure bookkeeping;
- later: trust ledger, Hydra population, clocks.

**Persistence: a sidecar JSON next to the save.** `<save>.improvedai.json` is written in a postfix on `GameStateManager.SaveAllGameStates` and read when that save loads.
- If there is no sidecar (an old save, or a fresh start), the doctrine is **derived deterministically** from the campaign: a seed from the campaign start date plus the alien faction's ID. A save made without the mod therefore gets the same doctrine every time it is loaded.
- The save itself is never modified, so vanilla can always load it.

**Harness additions:**
- `ai.doctrine <axis>=<value> …` forces a doctrine for probes.
- The journal records `doctrine` events (chosen, shifted, why).

## Step 2: Doctrine axes as dial modifiers ✅ first pass (2026-10-12)

**Gate passed** (2034 save, 180 days, forced focus):

| | Raider | Bastion |
|---|---|---|
| Raid picks | 22 | 3 |
| Capture goals | 13 | 3 |
| Attack launches | 20 | 9 |
| Bases killed | 3 | 1 |

Still to come in this step: the doctrine-failure ("shame") shift, and the opening-weighted selection.

A doctrine is a combination of axis values (agreed). Each value maps to two kinds of effect.

**Dial multipliers.** These apply to the existing dials (DIALS.md) and the vanilla `AIValues`:
- `AlienAttackSuperiority`
- `PoolReserveFraction`
- `MaxRaidGoals` / `RaidImportance`
- vanilla `riskAversion`, `fleetSmalls` / `fleetMediums`
- the importance of expansion vs attack goals

**Behaviour switches.** These turn planners on or off: the raid planner, Earth-presence goals, hoarding (pool everything for one megastrike), and so on.

| Axis | Values (difficulty-gated, DESIGN P3) | First effects to implement |
|---|---|---|
| Strategic focus | Fortress-industrial, Bastion, Puppet master, Quality over quantity, Xenoformer (all levels); Land grab, Raider, Earth presence, Hoarder, Strangler, Divide and rule (Normal+); Blitz, Hunter-killer (Veteran+) | Raider (step 3), Hoarder (pool-everything megastrike), Fortress-industrial (expansion up, attacks down), Land grab (early inner-planet base goals) |
| Political posture | force ↔ puppetry | Weight of councilor/Earth operations vs fleet goals |
| Command style | centralised ↔ forward | Rally point for pooling: home vs the front. Pherocyte command plugs in here later. |
| Fleet philosophy | weapon focus × hull size × engagement style | Ship-design scoring in `GetDesiredShipToBuild` / `DesignShipsCoroutine` |

**First-pass doctrine table** (built: `AI/Dials.cs`; multipliers on config dials; raid importance is additive). Everything here is a dial, tuned by soaks.

| Focus | Attack superiority | Pool margin | Home reserve | Raid slots | Capture slots | Raid importance | Hate weight | Arrival fight ratio |
|---|---|---|---|---|---|---|---|---|
| Fortress-industrial | ×1.25 | | ×1.6 | ×0.5 | ×0.5 | −2 | | |
| Bastion | ×1.3 | | ×2 | 0 | ×0.5 | −3 | | |
| Puppet master | | | | ×0.5 | ×0.5 | −1 | | |
| Quality over quantity | ×1.2 | ×1.2 | | | | | | |
| Xenoformer | | | | ×0.5 | ×0.75 | | | |
| Land grab | | | | | ×1.5 | | | |
| Raider | ×0.9 | | | ×2 | ×2 | +2 | ×1.5 | |
| Earth presence | | | | | | | | ×0.85 |
| Hoarder | ×1.5 | ×1.6 | ×0.6 | ×0.5 | | | | |
| Strangler | | | | ×2 | ×2 | +1 | | |
| Divide and rule | | | | | | | ×2.5 | |
| Blitz | ×0.75 | | ×0.5 | ×1.5 | | | | ×0.8 |
| Hunter-killer | | | | ×0.5 | ×0.5 | | | ×0.85 |

Political posture also scales raid and capture slots by ×(1.25 − 0.5 × puppetry).

**Selection.**
- Weighted random from the difficulty's pool, with weights shifted by the opening (DESIGN P3).
- Each axis gets a value.
- Logged, and readable through the harness.

**Doctrinal inertia** (P6) is bookkeeping:
- Track the doctrine's results: SCV lost vs destroyed, habs lost vs taken, over a rolling window.
- Past a shame threshold, shift one axis.
- The shift is visible through behaviour now, and through captives and the posture panel later.

## Step 3: The Raider capability (fixes E5)

**Finding.** Bases die only to marines (`DestroyHabOperation` needs `AssaultCombatValue > 0`) or to sustained bombardment. Alien raid fleets carry neither.

**Planned route.** Vanilla never lets the aliens plan hab captures (`SelectHabToCapture` returns null for them). The Raider doctrine turns undefended human bases into alien **capture** goals (`FactionGoal_CaptureHab`), which uses the game's existing assault machinery: marines, assault value, assault success chance.

This is the "drop pods take any outpost without marines or PD" idea.

**Correction (capture survey, 2026-10-12):**
- For the aliens, `TIHabState.CaptureHab` *destroys* the hab; they never own human habs.
- A **vanilla bug** zeroes the alien marine pod's value (Crashdown is listed before Assault), so alien ships could never assault anything.

**Built (v1):**
- `Fixes/AlienMarineValue`.
- The raid planner issues `FactionGoal_CaptureHab` for undefended bases off Earth/Luna.
- `AlienCaptureInProgress` stops vanilla's mission-control check discarding those goals.

**To investigate first:**
- how `FactionGoal_CaptureHab` sizes and staffs fleets;
- whether alien designs carry marines (the ArmyCarrier role);
- whether `AssaultHabOperation` works for aliens;
- what happens to a captured human base (keep, scuttle, convert).

**Probe:** e5-undefended-mines must pass. The aliens take or destroy ≥5 of 15 undefended belt mines within 180 days.

## Step 4: Fleet doctrines (design preferences) ✅ v1 (2026-10-12)

**Gate passed** (2034 save, 180 days, one run each; the design shifts are near-deterministic, the build mix is noisy):

| | Neutral axes | Missiles / Swarm / Artillery | Beams / Capital / Brawler |
|---|---|---|---|
| Ships built (S / M / L) | 44 / 36 / 20% | 57 / 32 / 11% | 25 / 52 / 22% |
| Weapons on ships built | missiles 31%, beams 31% | **missiles 47%** | **beams 38%** |
| Design mounts, missiles / beams / kinetics (start 51 / 123 / 67) | 67 / 106 / 59 | **130** / 101 / 55 | 70 / **168** / **22** |

Weak spot: Brawler barely shifted the role mix (few short-range ships built), because the attack goals' role tables give short-range roles little weight. Capital moved the build mix toward medium rather than large hulls (affordability buckets).

Bias the alien ship designer and the build picker toward the doctrine's weapon class, hull size and engagement style, within what the aliens have unlocked.

**Probe:** in a soak, the fleet composition (weapon classes, hull mix) differs measurably between two forced doctrines.

**How vanilla designs alien ships (survey, 2026-10-12).**
- **The role locks the weapon class.** `GetCuratedDesignScore` multiplies the role's own classes by 1e7 and divides all others by 1e7: short-range roles (LS/MS/SS) get lasers, medium-range (LM/MM/SM) magnetic or particle, long-range (LL/ML/SL) missile or plasma. Within that, the pick is weighted random (`ChooseWeapon`).
- **Mounts limit classes.** Alien missiles fit only single hull mounts; particle weapons only nose mounts. Aliens have no naval guns.
- **One design per role × size × exotics category** survives, the one with the best `TemplateSpaceCombatValue` (`DesignShipsCoroutine` keep and retire rules). Hulls come from a hardcoded table per role.
- **Build pick** (`GetDesiredShipToBuild`): a role drawn from the goal's secondary-role weights, a size drawn from `aiValues.fleetSmalls`/`fleetMediums` (aliens 2/2, Large fixed at 1), then the best-SCV design of that role and size (importance ≥ 10).
- **No alien fighters** outside surface-to-orbit squadrons, so the Carrier style has nothing to bias yet.
- **Vanilla bug (not fixed):** the coroutine asks for hull `AlienBattleCruiser`; the data name is `AlienBattlecruiser`, so that slot falls back to the lightest valid hull.

**Built (v1, `AI/FleetDoctrine.cs`, `[Fleet]` config):**
- **Weapon focus.** While the alien designer runs, the doctrine's class gets `WeaponFocusOdds` (3) against the role's own class wherever a mount fits it. When designs are compared (keep/retire, build pick), a design counts as (1 + `WeaponFitBonus` × share of its attack weapons in the doctrine class) × its SCV, so doctrine designs survive and get built. Kinetics = magnetic, naval and plasma guns.
- **Hull philosophy.** The build-pick size weights shift by `HullSizeBias` (2) per size step: Swarm toward small, Capital and Motherships toward large. Motherships also gives huge hulls a fit bonus (`MothershipFitBonus` 0.5). Vanilla's mothership exclusions (importance < 15, no total-war enemy) stay.
- **Engagement style.** Artillery multiplies long-range role weights by `EngagementRoleWeight` (3), Brawler short-range ones. Assault multiplies capture slots by `AssaultCaptureSlots` (1.5). Carrier: no effect yet.
- A doctrine load or force with any non-balanced fleet axis sets `updateShipDesignsFlag`, so the aliens redesign at the next monthly check.
- **Journal:** a monthly `fleet` record (live ships by size, role, hull and weapon class; active designs) and a `ship-built` record per alien ship. Both run on vanilla too.
- **Probes:** `fleet-swarm-missiles`, `fleet-capital-beams`, `fleet-neutral` (control).

## Step 5: Disposition: contempt × hate ✅ v1 (2026-10-12)

**Built** (`AI/Disposition.cs`):
- contempt per faction in the sidecar, seeded from vanilla's learned `perceivedEnemyFleetStrengthFactor`;
- lessons from battles and hab losses, plus a monthly outer-system presence lesson;
- attack superiority × (1.25 − 0.5 × contempt);
- `disposition` and `contempt` journal records.

**Gate passed:** 2039 Resistance starts at 0.40 and falls to 0.30 in 5 months.

**Still to come:** fury (nukes, discovered deception), verification effort (WS5), and desperation feeding P4 escalation.

**Contempt** is per human faction, from 0 to 1. It starts high (contempt). Its effects:
- **Force sizing:** high contempt means smaller attacks and lower `AlienAttackSuperiority`. These are the careless raids, the player's opening to ambush.
- **Verification effort:** used later in WS5.
- **Reserves and exotics-tier commitment.**

Contempt **falls** when alien expectations are beaten:
- battles lost that they expected to win (the combat journal has the data);
- ships killed;
- alien habs lost;
- a human presence at or beyond Jupiter.

It **rises** slowly with alien victories. Fury (from nukes or discovered deception) is a hate spike plus a lower `riskAversion`.

**Probe:** contempt falls after a forced alien defeat (inject a strong human fleet at an alien base), and attack sizing changes with it.

## Step 6: Economy (start of M3 if M2 runs long)

- **Mine what you lack.** Weight alien hab-site choice by projected stock-to-need for each resource. **Soak:** alien metals no longer sit near zero while water sits at a million.
- **Base cap → exotics upkeep.** Replace `maxAlienBaseGoals_*` with an exotics upkeep per base and per capital ship. **Soak:** bases grow past 13 when exotics allow, and stop when they don't.

**Survey (2026-10-12).**
- **Site choice is the whole mix.** One mine per alien base, and its output is the site's yield × the mine tier, so what a base mines is decided entirely when the site is picked.
- **The site scorer ignores need.** `EvaluateHabModule_PercentChange` is 60% gross revenue. Stockpiles and ship-construction spend never enter it. The deficiency tracker feeds only the other scorer (`EvaluateHabSite`).
- **Upkeep insecurity sees only ship maintenance,** whose propellant is water. So the "resource base if upkeep is insecure" rule flags water, and it defaults to water when nothing is flagged (`AlienHabPlanner` :147-150).
- **The cap binds.** The aliens held 13 bases in every 2034 run, vanilla or mod, with no base-founding goal at all in 270 days. `GetMaxAlienBases` is read only by `AlienHabPlanner.ManageHabGoals` (:67, :136). The Jupiter, belt and Neptune → Uranus → Saturn goals bypass it.
- **No exotics upkeep exists.** Exotics come only from `AlienWormholeFacility` (10/month × the difficulty multiplier 1/2/3/4 × the setup ramp). Exotics can't go negative and have no shortage penalty.
- **Stocks in the fixtures:**
  - metals 100 to 6k;
  - nobles 5.7k (2031) falling to 0.6k (2040);
  - water and volatiles 100k to 450k each;
  - exotics flat at about 7.8k by 2039.
- **Aliens buy what they lack from Earth.** `GetBoostSubstitutedCost` replaces a missing space resource with boost plus money, which is how they keep building on empty metals. This is a [lore] oddity; it is not changed yet.

**Built (v1, `AI/Economy.cs`, `[Economy]` config):**
- **Need weights.** For each basic resource: (its share of the live fleet's material cost, propellant excluded × total basic stock) / own stock, clamped to [¼, 4]. A site's score is multiplied by the production-weighted mean need of what it yields. This applies in both alien scorers.
- **Exotics upkeep.**
  - Each month, 3 exotics per base, 0.5 per large hull and 2 per huge hull are charged.
  - The base cap is ⌊(drip + stock/60 − ship upkeep) / 3⌋, up to a ceiling of 40.
  - The planner's own −4 early phase and its overshoot rules apply to this value.
- **Journal:** a monthly `economy` record (need, shares, stocks, drip, upkeep, cap; also written on vanilla, with nothing charged), `income` per resource in the metrics, and site `yields` on `hab+`.
- **Not done:** the water default in the "insecure upkeep" rule is left alone (it sits in a compiler-generated local function).
- **Probe:** `econ-mine-and-cap`.

## Validation gates

| Step | Gate |
|---|---|
| 1 | Doctrine is chosen and stable across reloads of the same save; sidecar round-trips |
| 2 | Two forced doctrines produce measurably different journals (goal mix, attack tempo, pooling) |
| 3 | e5 passes |
| 4 | Fleet composition differs by doctrine |
| 5 | Contempt probe passes; soak curves show the arc |
| All | Regression probes (e1, e3, e9) stay green; 3-run soaks show no exceptions or stalls |
