# Design: a constrained, competent alien opponent

## The goal in one sentence

The aliens should play to win, held back by in-universe constraints rather than by blind spots. Beating them should feel like out-thinking a capable opponent who is working under severe limits, not like exploiting one that isn't trying.

The model is Long War / Long War 2. The enemy has readable habits and weaknesses you can plan around, and those weaknesses come from its situation and its values, not from it ignoring you.

## What's wrong

These are symptoms, gathered from the user, the community survey (EXPLOITS.md) and the save forensics (FINDINGS.md):

1. **It never concentrates.** Mid-game the aliens had 3–6× the player's hulls, spread across 10+ fleets of 10–25 ships, and sent them in waves sized to "just enough".
2. **Its perception is gameable.** It reads the instantaneous combat value (CV) of the fitted loadout and remembers nothing. Bait refits, empty magazines and scarecrow fleets all work.
3. **It doesn't defend its economy, and doesn't threaten yours.** Remote and undefended human expansion is safe. Alien expansion stops at a fixed cap in a fixed order.
4. **It holds resources it can't convert into force.** Metals run dry while 300k+ volatiles and water and thousands of exotics sit unused.
5. **It plays the same game every time:** Kuiper belt → Neptune, Uranus, Saturn moons → Jupiter and belt → waves at Earth.
6. **It has no late-game answer.** Once you hold Jupiter or Saturn, it never escalates, so the last third is mop-up. The human tech tree also runs out: weapons and armor stop at about 0.5M of 3.4M research points.
7. **Turtling is free.** Nothing punishes a player who sits at Earth with a missile swarm and researches straight to late drives.
8. **Station defenses are a trap.** They're outranged, fragile and have zero ECM. Surface bases work only because they resolve in a different combat layer.
9. **Fleet controls are painful** in tactical combat: holding formation and matching velocity. This is a separate track; see WS8.

Several fixes have been proposed: an ECM disguise system, exotics upkeep, more expansion, varied strategies. The user's own principle applies: knowing something is wrong doesn't mean knowing the fix. Each proposal below is treated as a hypothesis aimed at a symptom, and is checked against the cheaper root-cause fix first.

## Pillars

### P1. Competence: remove the blind spots

The AI should never lose to something that a person playing the aliens would see through in two attempts.

- **Threat memory and potential.** Judge an enemy fleet by what it can become before the attack arrives, not only by what it shows now:
  - the best loadout seen on it recently,
  - refits possible at nearby shipyards within the transfer time,
  - resupply,
  - reinforcements that can reach the target in time,
  - STO (surface-to-orbit) fighters.
  - This removes E1, E2 and E4 without nerfing refits for honest uses.
- **Concentration.** Size attacks to win decisively, not to meet a minimum. Gather fleets into one strike and coordinate arrivals. Keep a real reserve. Prefer fewer, larger operations.
- **Re-evaluate in flight.** If the picture changes during a transfer, abort, divert or call reinforcements. Log the decision.
- **Retreat and preserve.** The aliens value their own lives (`protectAlienLife` = 1 is already in their AI values). They should withdraw from losing fights and bring damaged fleets home. This is also a justified weakness the player can use (see P2).
- **Value targets properly.** Economic targets anywhere (mines, shipyards, fuel depots, antimatter plants) are worth attacking. Raids should be sized to the defenses. React within weeks, not once a month.

### P2. Constraints, not handicaps

Difficulty should come from what the aliens are allowed to do, not from how clever they are. Each constraint needs an in-universe reason, in the spirit of the vanilla rule against nukes and kinetic strikes on Earth. Candidates, all to be validated by soaks:

- **The narrow wormhole (user, 2026-10-11).** This is the root constraint. The Hydra empire is vast, but the wormhole only lets small things through: an initial base or base supplies, a few workers, a set of eggs, and then a steady drip of exotics. The aliens in the Solar System are an **expeditionary colony that must bootstrap locally**, not an invading army.
  - **Everything but exotics is local.** Hulls, metals, fuel and bases come from Solar System resources, so alien power depends on alien territory and mining. Their economy is a legitimate target: starve them of metals and they can't build.
  - **Exotics are the imported edge.** They gate everything uniquely alien: exotic armor, advanced weapons, wormhole-tier hulls. Upkeep in exotics follows naturally, so bases and advanced warships compete for the same drip. (The 7.8k-exotic stockpile in the 2039 save suggests the drip isn't binding today.)
  - **Hydra lives are scarce (candidate).** A colony grown from a few workers and eggs can't easily replace crews. That turns loss aversion into arithmetic: a lost fleet costs years of hatchery output. It could replace the arbitrary base cap (bases need staff) and gives the player targets: hatcheries, crewed capital ships, and captives, which reduce their numbers.
  - **The wormhole terminus is the most valuable target in the system.** Threatening it changes everything they do, so it is their best-defended point.
  - **Late-game surge (P4) under this model** is mostly local mobilization: spend stockpiles, divert the whole drip to war, push hatcheries to maximum, strip quiet systems, and accept losses. They are burning their future.
  - **Option: widening the wormhole.** A long, visible project that, if completed, raises throughput sharply. It would give the aliens an endgame clock that the player can delay by striking the terminus.
- **Fix the bottleneck behind stranded resources.** Metals scarcity strangles alien building while other stocks pile up. Decide whether that is a constraint we want (then make the AI work around it: mine metals, raid human metals) or a bug (then let the AI convert its stocks).
- **Loss aversion.** The Hydra administration counts lives. Heavy losses carry a political cost, such as slower decision cadence or a forced pause for that doctrine. The player wins by making attacks expensive, not by baiting them.
- **Species taboos come from trauma (user, 2026-10-11).** The Hydras' own home planet was nuked. That is why they don't use nukes or long-range bombardment of planets. These taboos hold even when breaking them would win; they are not policy choices. Hypothesis: human nuclear use should land emotionally, shifting the aliens from contempt to fury (more aggressive, more willing to take losses, possibly less calculating). Nukes would then win a fight at the cost of a changed opponent.
- **Subjugation, not extermination.** The aliens want Earth's industry and population intact. That limits what they'll bombard.
- **Imperfect intel.** Alien knowledge of human fleets comes through surveillance assets and is good but not perfect. Destroying surveillance and fitting ECM degrade it. This is the foundation that makes deception an intended mechanic (WS5).

### P3. Variety: doctrines

Each campaign draws a hidden **strategic doctrine** and a **fleet doctrine**, weighted by difficulty and start date. Intel can reveal them. They can shift when a doctrine keeps failing, and the shift is signalled through intel events so the player can notice the change.

**Strategic doctrines** (the user's list, consolidated):
- **Inner-system land grab:** an early fast grab of Mars, Mercury or Venus.
- **Raider:** long-range drop-pod or marine assaults on outposts without marines or point defense (PD). Vanilla never lets the aliens plan hab captures, which is a lever).
- **Early Earth-space presence:** forces fights with exofighters and planetary PD.
- **Hoarder:** saves up for one decisive megafleet.
- **Fortress-industrial:** expands hard and fields defensive fleets only, winning on economy unless stopped.
- **Strangler:** blockades and raids the human economy instead of attacking Earth directly.

**Fleet doctrines:**
- weapon focus: missiles, beams, particle weapons, kinetics, or balanced;
- hull philosophy: swarm, capital, motherships, or balanced.

**Counter-design:** fleet doctrine adapts to the human weapon mix it observes, e.g. more PD against missile-heavy players, more armor against beam-heavy players.

**Expansion** is driven by doctrine and by contesting human expansion, not by the fixed Neptune → Uranus → Saturn script. The hard base cap is replaced by the upkeep constraint (P2).

### P4. A late-game arc

**Target experience (user, 2026-10-11).** Holding Jupiter or Saturn means the aliens now take you seriously as an opponent. The game doesn't simply get harder. Two things are true at once:
- you are in a much better position;
- the aliens are going all out against you.

The final phase should feel different (higher stakes, decisive battles), not merely tougher.

- **Change of posture, triggered by real losses.** Losing a gas-giant system, or a threat to the main base, moves the aliens from managing a nuisance to fighting a war they could lose.
  - **They commit everything they have saved:** the wormhole surges using stockpiled exotics, advanced tech is released, total war begins, and they launch concentrated counteroffensives.
  - **Desperation relaxes loss aversion (P2).** They accept fights and casualties they used to avoid. That makes them more dangerous, and it also gives the player the decisive engagements the cautious alien would have refused.
  - **It's justified, not rubber-banding.** The surge spends resources that actually exist, such as the exotics stockpile. There is no hidden bonus. A player who has raided their economy or wormhole meets a weaker surge.
- **A longer human tech ladder.** Extend weapons, armor and radiators past the current ceiling, and add military repeatables, so "turtle and research" is a real strategy with a real cost.
- **Pressure on turtles.** The economic-strangulation and expansion doctrines make sitting at Earth expensive.

### P5. Legibility and counterplay

The player must be able to read and answer the AI:
- intel reveals doctrine, intent and posture at the right investment;
- alien behaviour follows from its constraints, so a player who understands it can exploit it;
- the decision journal (TESTING.md) shows why the AI did something, for us and optionally for players.

## Workstreams

| WS | Name | Purpose | Depends on | Kind |
|---|---|---|---|---|
| WS0 | Infrastructure | Plugin skeleton, game copy, revived `LogAI`, decision journal, metrics, probe runner, soak runner, save forensics | — | code + tools |
| WS1 | Perception & commitment | Threat memory and potential, concentration, in-flight re-evaluation, retreat, counting reinforcements. Fixes E1–E4, E12, E13. | WS0 | Harmony |
| WS2 | Operations & targeting | Economic targets anywhere, raid sizing, faster reaction, surveillance escort. Fixes E5, E7. | WS0, WS1 | Harmony |
| WS3 | Strategy & doctrines | Doctrine system, contested expansion planner, counter-design, late-game escalation. Fixes E9, E10. | WS1, WS2 | Harmony + JSON |
| WS4 | Constraints & economy | Wormhole flow, exotics upkeep, metals bottleneck, difficulty redefined as constraints. Balances WS1–3 so a smart AI is beatable. | WS1–3, soaks | JSON + Harmony |
| WS5 | Intel & deception | Intel-gated alien perception, ECM signatures/disguise as an intended mechanic | WS1 | Harmony + JSON |
| WS6 | Station defenses | Range parity, HP and hitbox, station ECM, siege behaviour | WS0 (combat lab) | JSON + Harmony |
| WS7 | Tech extension | Mk4/5 weapons, armor and radiators; military repeatables; alien escalation to match | WS4 | JSON |
| WS8 | Fleet controls | Station-keeping on a leader ("hold formation"), tighten/loosen formation, pre-battle setup improvements | — | Harmony + UI |

## Milestones

- **M0, see the AI.**
  - Plugin skeleton on the game copy, with `LogAI` revived and the decision journal.
  - The probe runner works and throughput is measured.
  - Probes for E1, E3, E5 and E9 run and fail on vanilla.
  - Save forensics extended.
- **M1, no idiot ball.**
  - WS1 plus the E5 part of WS2.
  - Those probes pass. A soak shows no runaway and no stall.
  - Chapter playtests at "hot war 2031" and "Jupiter assault 2037".
- **M2, a different opponent each game.** WS3 doctrines and WS4 constraints. Soak curves are tuned. Playtests on early chapters.
- **M3, the war they could lose.** WS3 escalation, WS7 tech extension, WS6 stations. Full user campaign.
- **WS8 controls** are independent and can land at any point. It's a good parallel track for a separate session.

## Decisions

The user made these on 2026-10-11:

- **Scope:** aliens only. The human AI factions stay vanilla for now. Soaks will show whether they get steamrolled.
- **Difficulty:** reuse the vanilla levels (Cinematic, Normal, Veteran, Brutal). Each level sets how tight the constraints are (P2), not how competent the AI is (P1).
- **Test install:** an isolated game copy at `C:\Games\TI-ImprovedAI`.

## Engineering rules

- **Ship structure.** One BepInEx plugin and one JSON mod. Every feature sits behind its own config switch, so A/B tests and probes can toggle them.
- **Mid-game loading.** All AI changes take effect when an existing save loads. State the mod adds (doctrine, threat memory) is stored in a sidecar keyed by the save, or rebuilt on load. **Never break vanilla save loading.**
- **Scope of changes.** Only change alien behaviour unless a change is explicitly meant to affect the human AI factions. Shared code paths, such as `SpaceCombatValue`, are guarded by a "who is asking" context.
- **Probe first.** Any AI change starts with a probe that shows the problem on vanilla.
