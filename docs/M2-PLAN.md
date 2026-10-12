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

## Step 2: Doctrine axes as dial modifiers

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

## Step 4: Fleet doctrines (design preferences)

Bias the alien ship designer and the build picker toward the doctrine's weapon class, hull size and engagement style, within what the aliens have unlocked.

**Probe:** in a soak, the fleet composition (weapon classes, hull mix) differs measurably between two forced doctrines.

## Step 5: Disposition: contempt × hate

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

## Validation gates

| Step | Gate |
|---|---|
| 1 | Doctrine is chosen and stable across reloads of the same save; sidecar round-trips |
| 2 | Two forced doctrines produce measurably different journals (goal mix, attack tempo, pooling) |
| 3 | e5 passes |
| 4 | Fleet composition differs by doctrine |
| 5 | Contempt probe passes; soak curves show the arc |
| All | Regression probes (e1, e3, e9) stay green; 3-run soaks show no exceptions or stalls |
