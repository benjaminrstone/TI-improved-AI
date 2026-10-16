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
  - **Narrow in both directions, and nothing goes back.** The Hydras aren't here for Earth's resources (they want Earth as a bastion), so there is no reason to send anything home. Every ship, Hydra and exotic that comes through is committed to this front for good. The lore text describes gravity as flowing one way through the wormhole. We don't rely on that: the reason nothing goes back is purpose, not physics.
  - **Everything but exotics is local.** Hulls, metals, fuel and bases come from Solar System resources, so alien power depends on alien territory and mining. Their economy is a legitimate target: starve them of metals and they can't build.
  - **Exotics are the imported edge.** They gate everything uniquely alien: exotic armor, advanced weapons, wormhole-tier hulls. Upkeep in exotics follows naturally, so bases and advanced warships compete for the same drip. (The 7.8k-exotic stockpile in the 2039 save suggests the drip isn't binding today.)
  - **Hydra population is a real resource (user, 2026-10-11), but secondary: it binds less and bites less than exotics.**
    - Hatcheries grow it steadily. It is spent crewing ships and staffing habs.
    - It mainly bites through heavy losses: a destroyed crewed fleet costs years of hatchery output. That gives loss aversion an arithmetic basis.
    - It mostly acts as a price in AI decisions ("is this target worth the crews?"), not a hard cap.
    - **Exotics upkeep stays the primary limit** on expansion and force size. Population is a soft second limit, not the replacement for the base cap.
    - Player targets: hatcheries, crewed capital ships, and captives (each one is a Hydra fewer).
  - **The wormhole terminus is the most valuable target in the system.** Threatening it changes everything they do, so it is their best-defended point.
  - **Late-game surge (P4) under this model** is mostly local mobilization: spend stockpiles, divert the whole drip to war, push hatcheries to maximum, strip quiet systems, and accept losses. They are burning their future.
  - The user agreed the four points above and the late-game surge (2026-10-11).
  - **Two clocks** (user, 2026-10-11; LW2 Avatar-style).
    1. **Steady widening (soft clock).** The Hydras are slowly widening the wormhole, so the supply drip grows over time.
       - This is the principled cause of alien escalation over time. It replaces vanilla's opaque year-based ramps (attack-ratio growth, tech timers) with something visible and attackable.
       - Difficulty sets the widening rate, which fits difficulty-as-constraint.
       - Striking the widening works at the terminus slows it.
       - **Build on the existing ramp.** Vanilla already has one: `alienSetupStartIncome` → `alienSetupEndIncome` over `alienSetupDuration_years`, used by the 2003 scenario at 0.15 → 1.5× over 20 years (DIALS.md). Widening should extend that ramp, not replace it.
    2. **The full terminus (hard clock, alien victory).** A huge project to build a full terminus on this side. **If it completes, the Hydras win.**
       - **Visible meter.** It appears on the posture panel once intel is high enough. Before that, the clue is a silence: captives won't discuss it (P6).
       - **Setbacks.** Progress depends on the construction site, which is a major structure the player must eventually assault. Strikes knock the meter back.
       - **Coupled to the drip.** It is funded by the drip, so a wider wormhole means faster progress. Hitting the widening works slows both clocks.
       - **Start (proposal).** It begins when contempt falls far enough that the aliens take humanity seriously and commit to the long game. Its rate follows their economy.
       - **To investigate in M0:** how vanilla ends a campaign in defeat (TIVictoryTemplate, game-over flow), so the mod can hook an alien victory.
- **Mine what you lack (user, 2026-10-11).** The aliens run short of metals while water piles up past a million, and they don't react. That is an AI failure, not a constraint. A Hydra planner that sees "1M water, 0 metals" founds metal mines.
  - **Vanilla tries and fails.** It has a "resource base if upkeep is insecure" rule (`AlienHabPlanner` :136-174) and deficiency tracking (`resourceIncomeDeficiencies`, `AnalyzeDeficiences_Alien`). Metals still sat near zero for five years. Suspects: the base cap, and site scoring that ignores deficits.
  - **Fix.** Weight site selection by projected stock-to-need for each resource (metals, volatiles, water, noble metals, fissiles), with base founding limited by exotics upkeep (P2), not a fixed cap.
  - **It's a tell.** The aliens expand toward what they lack, so a player who reads their stocks can predict their next targets: a metal-rich moon or a belt cluster. Contesting those sites is a real strategy.
- **Loss aversion.** The Hydra administration counts lives. Heavy losses carry a political cost, such as slower decision cadence or a forced pause for that doctrine. The player wins by making attacks expensive, not by baiting them.
- **Species taboos come from trauma (user, 2026-10-11).** The Hydras' own home planet was nuked. That is why they don't use nukes or long-range bombardment of planets. These taboos hold even when breaking them would win; they are not policy choices. Hypothesis: human nuclear use should land emotionally, shifting the aliens from contempt to fury (more aggressive, more willing to take losses, possibly less calculating). Nukes would then win a fight at the cost of a changed opponent.
  - **No biological weapons (user, 2026-10-11).** [lore] The Hydras have a deep-seated fear of biological assault (Project_HydraBiowarfare). They never use bioweapons. Hypothesis: if the aliens learn of human bioweapon research (vanilla's Biowarfare → Kill the Hive path), it hits like nuclear use, an existential threat. That pushes them toward fury, and toward desperation once the path is near completion. They prioritise the labs and the factions pursuing it. This gives the player a visible risk on that victory path.
- **Subjugation, not extermination.** The aliens want Earth's industry and population intact. That limits what they'll bombard.
- **Imperfect intel.** Alien knowledge of human fleets comes through surveillance assets and is good but not perfect. Destroying surveillance and fitting ECM degrade it. This is the foundation that makes deception an intended mechanic (WS5).

### P3. Variety: doctrines

Each campaign draws a hidden **strategic doctrine** and a **fleet doctrine**, weighted by difficulty and start date. Intel can reveal them. They can shift when a doctrine keeps failing. The player notices the shift through observed behaviour and fresh captives (see "Seeing their doctrine" under P5).

**Strategic doctrines** (the user's list, consolidated):
- **Inner-system land grab:** an early fast grab of Mars, Mercury or Venus.
- **Raider:** long-range drop-pod or marine assaults on outposts without marines or point defense (PD). Vanilla never lets the aliens plan hab captures, which is a lever).
- **Early Earth-space presence:** forces fights with exofighters and planetary PD.
- **Hoarder:** saves up for one decisive megafleet.
- **Fortress-industrial:** expands hard and fields defensive fleets only, winning on economy unless stopped.
- **Strangler:** blockades and raids the human economy instead of attacking Earth directly.

**More strategic directions** (brainstorm, user asked to keep exploring, 2026-10-11):
- **Puppet master.** The main effort is on Earth: Servants, mind control, expanding the Alien Administration, capturing nations. Space posture stays defensive. This leans into the mind-control angle.
- **Divide and rule.** Uses Earth's division. Honest, binding pacts with weaker human factions isolate the strongest one. The Hydras can afford honesty because humans betray each other.
- **Bastion.** Fortifies the Jupiter–Saturn line until it's nearly impregnable, and lets the terminus clock run. A counter-turtle that forces the player to attack.
- **Blitz.** An early war before humans can tech up, accepting early losses to cripple human space industry for good. Loss aversion is deliberately overridden, which makes it a gamble the player can make expensive.
- **Hunter-killer.** Destroys human fleets rather than assets: ambushes from silence at transfer windows (P6).
- **Quality over quantity.** Minimal expansion. Exotics are hoarded for wormhole-tier hulls, producing a small, late, terrifying fleet.
- **Xenoformer.** Ground operations and xenoforming on Earth (a vanilla system), with space held defensively.

**Compose axes, don't pick from a list (agreed with the user, 2026-10-11).** A doctrine is a combination of separate dimensions, which gives much more variety than a fixed set:
- strategic focus (the lists above);
- political posture: force versus puppetry;
- command style: centralised versus forward (pherocyte command);
- fleet philosophy (below).

**Rejected: Blockade** (user, 2026-10-11, for player-experience reasons). Keeping humanity gravity-bound attacks the player's ability to play at all, not just their assets. If it ever returns, it is Brutal-only.

**Difficulty gates the doctrine pool** (user, 2026-10-11). The more punishing and aggressive doctrines only occur on higher difficulties. This is consistent with difficulty-as-constraint: on lower difficulties the harsher strategies are not available to the Hydra command. Draft tiers, to tune with soaks:

| Tier | Doctrines added |
|---|---|
| Cinematic | Fortress-industrial, Bastion, Puppet master, Quality over quantity, Xenoformer |
| Normal | Inner-system land grab, Raider, Early Earth presence, Hoarder, Strangler, Divide and rule |
| Veteran / Brutal | Blitz, Hunter-killer. Weights shift toward the aggressive end of every axis |

Each tier includes the doctrines of the tiers below it. **Strangler stays on Normal** because it is the answer to free turtling (E10). It hits remote and economic assets, which the player can defend or contest, not the player's access to space. Watch it in playtests for the same agency problem.

**Initial weights react to the opening.** If humans rush space, Raider and Strangler become more likely. If one faction dominates, Divide and rule does.

**Fleet doctrines:**
- weapon focus: missiles, beams, particle weapons, kinetics, or balanced;
- hull philosophy: swarm, capital, motherships, or balanced;
- engagement style: long-range artillery (outrange and kite), brawler (high-acceleration close-in), boarding/assault (marines, capturing habs; vanilla never lets the aliens plan hab captures), or carrier (alien fighters).

**Counter-design:** fleet doctrine adapts to the human weapon mix it observes, e.g. more PD against missile-heavy players, more armor against beam-heavy players.

**Expansion** is driven by doctrine, by resource need, and by contesting human expansion, not by the fixed Neptune → Uranus → Saturn script. The hard base cap is replaced by the upkeep constraint (P2).

**Stealthy human expansion is a valid style, but it costs something** (user, 2026-10-11). Vanilla aliens see everything within 500 AU. Instead, alien detection of each human asset is a chance, checked each period. The chance rises with:
- the asset's signature: size, activity, drives burning;
- nearness to alien sensors: surveillance stations, fleets, bases;
- the alien detection effort for that faction, which grows with hate and with lower contempt.

ECM on colony ships and on bases lowers the signature. An undetected asset generates no hate and can't be targeted. So a quiet Kuiper colony is achievable, but you pay for it in ECM and modules, and the aliens are actively looking. Once found, it becomes "verified" knowledge (P6). This uses the same machinery as the trust ledger and verification, so it is one system, not two.

### P4. A late-game arc

**Target experience (user, 2026-10-11).** Holding Jupiter or Saturn means the aliens now take you seriously as an opponent. The game doesn't simply get harder. Two things are true at once:
- you are in a much better position;
- the aliens are going all out against you.

The final phase should feel different (higher stakes, decisive battles), not merely tougher.

**Built v1 (2026-10-16, `AI/Escalation.cs`).**
- **Desperation**, judged monthly, from:
  - human habs in the gas-giant systems;
  - gas-giant systems lost outright (humans present, aliens absent);
  - respect (contempt below 0.35);
  - alien habs lost in the last 12 months.
- **At 0.55 the aliens escalate**, and stay escalated until 6 months below 0.4. While escalated:
  - total war becomes possible at once (vanilla waits 20 progression-years on Normal);
  - loss aversion relaxes (bolder attacks, reserves committed, more raids aimed at the hated);
  - half the expansion share of the drip goes to war.
- **Not yet:** an explicit exotics surge into wormhole-tier warships, riskAversion, and the terminus.

- **Change of posture, triggered by real losses.** Losing a gas-giant system, or a threat to the main base, moves the aliens from managing a nuisance to fighting a war they could lose.
  - **They commit everything they have saved:** the wormhole surges using stockpiled exotics, advanced tech is released, total war begins, and they launch concentrated counteroffensives.
  - **Desperation relaxes loss aversion (P2).** They accept fights and casualties they used to avoid. That makes them more dangerous, and it also gives the player the decisive engagements the cautious alien would have refused.
  - **It's justified, not rubber-banding.** The surge spends resources that actually exist, such as the exotics stockpile. There is no hidden bonus. A player who has raided their economy or wormhole meets a weaker surge.
- **A longer human tech ladder.** Extend weapons, armor and radiators past the current ceiling, and add military repeatables, so "turtle and research" is a real strategy with a real cost.
- **Pressure on turtles.** The economic-strangulation and expansion doctrines make sitting at Earth expensive.

### Pherocyte command

Status: [ext], user interested 2026-10-11, to be developed.

**Root.** [lore] Hydra speech is simple, and its meaning and intent travel chemically through pherocytes (Project_HydraLanguage). Radio carries the words but not the intent. [ext] So real command (adaptive, intent-driven decisions) needs a senior Hydra physically present. Fleets far from command run on **standing orders**.

**Command nodes.**
- **Motherships.** Mobile command, using the existing alien hull. They become precious operational assets and the natural decapitation target.
- **The primary base,** and possibly a new alien command module (JSON) on other habs.
- **Possibly alien councilors** for operations on Earth.
- **Range:** the same planetary system or region as a node. That is discrete and easy for the player to read. Pherocytes are physical, so range is about presence, not signal strength.

**In command vs on standing orders.**

| | In command | Standing orders |
|---|---|---|
| Plan | Made with full intent: the WS1 sizing and concentration fixes apply when a plan is made | Executes the plan it last received, rigidly |
| In transit | Re-evaluates; can abort, divert or reinforce | Continues as planned |
| Opportunities | Takes targets of opportunity, retreats intelligently | Follows doctrine-fixed rules (e.g. a fixed retreat threshold) |
| Multi-fleet operations | Coordinated, synchronised arrivals | Not possible; each fleet acts alone |
| Doctrine change | Immediate | Arrives only when command reaches the fleet (see below) |

**Rigid, not dumb.** Standing orders are a good plan executed without adaptation, never the vanilla idiot ball.

**Consequences that fall out of this:**
- **Big coordinated offensives require bringing command forward.** Concentrated strikes at Earth or Mars need a mothership in theatre. That is a strategic decision for the aliens (doctrine: centralised versus forward command), a visible signal to the player, and a target.
- **Decapitation is real counterplay.** Kill the mothership mid-operation and the fleets in that theatre fall back to standing orders: they finish the last plan rigidly, or withdraw per doctrine. The game reports it ("Alien fleets near Mars appear to be operating without direction").
- **Doctrinal inertia becomes physical.** When the Hydra command changes doctrine (shame, see P6), remote fleets keep the old doctrine until command reaches them. That opens a readable window.
- **Concealment interacts with command.** A fleet gone dark can't receive new intent either (P6), so the aliens have to choose between going dark and staying adaptable.

**Tiers.**
- **A (cheap, high value):** a command gate on our adaptive behaviours. Motherships and the primary base are nodes. Standing orders outside their range. Decapitation reverts fleets.
- **B:** doctrine propagation delay, an alien command module, the AI deciding where to forward-deploy command, status in fleet tooltips and on the posture panel.
- **C:** tactical-combat effects: in-command fleets get the better combat AI.
- **Rejected: physical couriers** (user, 2026-10-11).

### Disposition: contempt × hate (the danger meter)

The user agreed this framing on 2026-10-11. The alien attitude toward each human faction has two axes.

- **Hate** is how much they want to hurt you. It is vanilla `factionHate`, unchanged in meaning. It drives war, retaliation and target priority.
- **Contempt** is how little they think you can hurt them. It is new. [lore] The Hydras regard humanity as beneath notice (Project_HydraDiplomacy). It drives:
  - force sizing and concentration;
  - how much verification they bother with;
  - whether they hold reserves;
  - how readily they commit their exotics-tier assets.

| | Low hate | High hate |
|---|---|---|
| **High contempt** | *Management.* Proportional, minimal responses (early game). | *Punitive but careless.* Angry raids sized by underestimation: the window for ambushes. |
| **Low contempt** | *Wary.* Watchful and deterrence-minded; avoids you and keeps pacts. | *Deadly.* Concentrated, verified, all-in. Late game and desperation (P4). |

- **What moves contempt down:** outcomes that beat their expectations. Winning fights they expected to win, kills, holding their territory, demonstrated tech. Surprise is what teaches respect.
- **What moves it back up:** losses that confirm their view.
- **Fury** is a spike in hate plus less calculation. It comes from human nuclear use and discovered deceptions.
- **It's visible.** The player sees an estimate of both axes. Vanilla already shows estimated hate. Reading the meter is part of the game: provoking hate while still held in contempt is the dangerous corner, and a "Wary" alien can be bargained with.
- **Difficulty sets starting contempt and how fast it erodes** (user, 2026-10-12): arrogant and slow to learn on Cinematic, wary and quick to learn on Brutal.

### P5. Legibility and counterplay

The player must be able to read and answer the AI:
- intel reveals doctrine, intent and posture at the right investment;
- alien behaviour follows from its constraints, so a player who understands it can exploit it;
- the decision journal (TESTING.md) shows why the AI did something, for us and optionally for players.

**Seeing their doctrine** (agreed with the user, 2026-10-11). There are three channels.
1. **Observed behaviour.** Fleet compositions, targets and posture visibly change. Intel events report what sensors and wrecks reveal, e.g. "Alien shipyards at Titan have retooled for heavy laser hulls."
2. **Captive interrogation, ongoing.** A captive answers truthfully or goes silent (P6). What they know is frozen at the moment of capture, so they show doctrine *as of capture*. As time passes and the aliens change course, their answers go stale. A fresh captive is worth more than an old one. There are no separate "leadership change" events: a doctrine change shows up as a difference between old and new captives and in observed behaviour.
3. **The alien posture panel** (approved). An intel screen that fills in with intel:
   - doctrine (strategic and fleet), with its source and date ("Strangler, from a captive taken Mar 2033");
   - disposition (contempt × hate);
   - supply state;
   - command nodes;
   - known silences.
   - At low intel it shows guesses or blanks.
   - This is new UI work: a screen plus localization text.

**Two-way intel** (agreed in both directions). How the aliens see you: surveillance, Servants, scouts. You counter by destroying surveillance, purging infiltrators, using ECM and disguise, and staying dark. How you see them: sensors, captives, wreck analysis, turned Servants, traffic near the wormhole. They counter with concealment, purging compromised Servants, and protecting command nodes.

### P6. The information game: Hydras conceal, humans deceive

**Root.** [lore] A Hydra would rather go silent than be caught lying; failed deception is a deep shame (Project_TheirPurpose). The user called this the core of the alien character (2026-10-11). Everything below follows from it. Tags: [lore] = in the game's text, [ext] = our extrapolation.

**The asymmetry.**
- Hydras never feint, bluff, fake a signature or stage a false retreat.
- Hydras do conceal: fleets go dark, staging happens in shadow (behind gas giants, far from human sensors), reserves stay hidden, they ambush from silence, and they withhold information. An ambush from silence is honest: they never said they weren't there.
- Humans deceive: false signatures, decoys, bait refits (WS5).

**Silence is a signal [ext].**
- **Silence has a footprint.** Going dark means radiators in and drives cold, so a dark fleet coasts on a ballistic path. A player with deep-space sensors and some orbital reasoning can work out where it must be.
- **Silence costs flexibility.** A dark fleet can't take new orders (ties to command friction), so it executes the plan it went dark with. Surprise is traded for adaptability.
- **The absence is the tell.** When surveillance traffic or fleet emissions in a region stop, that silence is itself a warning. The game reports it ("Alien activity near Saturn has gone quiet") and the player learns to read it. This is the quiet before the storm, made into a mechanic.

**Honest signals [ext from lore].**
- **Ultimatums are real.** When the Hydras say "withdraw from Callisto or we will attack", they will. An honest opponent tells you where its lines are. The player can call the threat, knowing it's real. Legibility comes nearly free through the existing notification system.
- **Pacts are binding on their side.** Hydras keep their word (vanilla has `FactionGoal_NonAggressionPact`). A human faction that breaks a pact is never trusted again: no more pacts, and maximum verification of its signals.

**The trust ledger [ext].** The Hydras know humans lie. Each human faction carries a trust value in the alien mind.
- **Discovered deceptions lower trust.** A bait refit recognised, a disguise seen through, a decoy unmasked: each one makes the aliens demand fresher, closer verification before they believe anything from that faction.
- **Deception therefore has diminishing returns.** It works the first time and gets harder each time after. This answers the refit-bait loop without banning refits: the first bait may work, the fifth won't.
- **Discovery is also an affront.** A discovered deception may shift disposition toward fury, like nuclear use.

**Verification before commitment [ext].**
- **Two grades of knowledge.** Alien knowledge of a human asset is either *verified* (seen by their own scouts or sensors recently, at a given range) or *reported* (Servant intel or stale data).
- **Uncertainty plus loss aversion means caution.** Unverified targets get a pessimistic estimate: either a bigger fleet or a scout first.
- **Scouting is honest and visible.** A Hydra scout arriving is a tell that an attack is being considered.
- **Counterplay:**
  - **Kill the scout:** this delays the strike, but a blinded Hydra assumes the worst. Blinding them buys time and invites a larger eventual blow.
  - **Let it look:** they see the truth, which may be what you want them to see.
- This is how the user's ECM-disguise idea becomes an intended mechanic: a disguise holds until a verifying look gets close enough, with odds set by time, range and tech.

**Deception by proxy [ext from lore].** Hydras won't lie, but their human Servants and the Alien Administration will, and the Hydras tolerate it. Alien-side deception exists only through humans: false reports, planted intel, Servant councilors. Exposing Servants is the counterplay. This ties into the vanilla councilor game.

**Captives: truth or silence [lore].**
- **They can't lie,** so interrogation yields true answers or silence.
- **The pattern of silences is intelligence.** A report might read "answered: current doctrine is Strangler; silent on: wormhole defenses, Titan." Silence marks what they're hiding: a project, a secret staging area, the widening of the wormhole.
- **"They don't abandon their own", tied to interrogation** (user mildly likes it, 2026-10-11).
  - **The loop.** Captives give ongoing, as-of-capture intel. Fresh captives keep that view current (P5), so the player is pulled to keep capturing Hydras. Each capture, and each captive held, draws alien attention: recovery attempts, and strikes on where captives are held. Intel versus exposure is a risk/reward loop the player controls.
  - **Guard against farmed bait.** Their response obeys loss aversion and verification. They come for their own when the odds are acceptable and go silent otherwise. Response strength scales with how many captives you hold and what those captives know, not with a fixed reflex.
  - **Smallest test first.** A crash-site race on vanilla's existing alien crash sites: alien recovery teams compete with human investigators trying to capture survivors.

**Shame drives doctrinal inertia [ext].**
- **Failure is shameful,** like failed deception, so the Hydra command persists with a failing doctrine longer than is optimal. That is the root of P3's inertia, and an exploitable flaw.
- **Collapse is visible.** Past a threshold the command abandons the doctrine and adopts a new one. Players see this through behaviour and fresh captives, not a dedicated event.
- **One will, no internal factions (user, 2026-10-11).** The Hydras are a single, unified threat. Doctrine changes are one command revising its course, never rival factions. Earth is the divided side. A unified intelligence bending divided humans leans into the mind-control angle (Servants, pherocyte control).
- **Players can read and steer it.** A player who understands this can keep a failing alien doctrine alive by not handing it the decisive defeat that would force the change.

**Pherocyte truth-sense [ext, speculative].** Hydra falsehoods carry a chemical signature [lore]; perhaps Hydras also read human chemistry up close. That would explain why disguises fail at close range and why Hydra ground operations and boarding are hard to fool. It is a possible in-universe justification for "verification needs proximity".

## Workstreams

| WS | Name | Purpose | Depends on | Kind |
|---|---|---|---|---|
| WS0 | Infrastructure | Plugin skeleton, game copy, revived `LogAI`, decision journal, metrics, probe runner, soak runner, save forensics | — | code + tools |
| WS1 | Perception & commitment | Threat memory and potential, concentration, in-flight re-evaluation, retreat, counting reinforcements. Fixes E1–E4, E12, E13. | WS0 | Harmony |
| WS2 | Operations & targeting | Economic targets anywhere, raid sizing, faster reaction, surveillance escort. Fixes E5, E7. | WS0, WS1 | Harmony |
| WS3 | Strategy & doctrines | Doctrine system, contested expansion planner, counter-design, late-game escalation. Fixes E9, E10. | WS1, WS2 | Harmony + JSON |
| WS4 | Constraints & economy | Wormhole flow, exotics upkeep, metals bottleneck, difficulty redefined as constraints. Balances WS1–3 so a smart AI is beatable. | WS1–3, soaks | JSON + Harmony |
| WS5 | Information game (P6) | Verified vs reported alien knowledge, trust ledger, scouting before strikes, dark fleets and silence tells, ultimatums, captives, human deception (ECM signatures and disguise) | WS1 | Harmony + JSON + UI text |
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

- **Lore:** expanding the lore is allowed. Keep it consistent with the game's text. Where a mod idea conflicts with existing lore, that can be acceptable, but call it out explicitly to the user.

- **Scope:** aliens only. The human AI factions stay vanilla for now. Soaks will show whether they get steamrolled.
- **Difficulty:** reuse the vanilla levels (Cinematic, Normal, Veteran, Brutal). Each level sets how tight the constraints are (P2), not how competent the AI is (P1).
- **Test install:** an isolated game copy at `C:\Games\TI-ImprovedAI`.
- **Passive players (2026-10-12).** Punishing a passive player is fine. Every dial we create or turn is tracked in `docs/DIALS.md`, so a smooth difficulty curve can be built across the levels.
- **Willingness to fight (2026-10-12).** "Never lose a ship" is too cautious.
  - Alien fleets defend their bases and stations (there are Hydras aboard), unless the odds are hopeless.
  - Attack fleets that arrive to roughly even odds fight rather than burn fuel going home.
- **Raid targeting (2026-10-12).** Weigh value and hate together. A faction the aliens hate intensely has its assets prioritised anywhere; at lower hate they go for the most valuable targets.
- **Fresh mine spam (2026-10-12).** Leave it as is. Fresh tier-1 mines get a grace period (about 8 months from goal to kill) because raids hit bigger bases first.
- **Contempt by difficulty (2026-10-12).** Yes. Difficulty sets both the starting contempt (higher on easy: arrogant, careless raids) and how fast it erodes (faster on hard). Dials `ContemptDifficultyOffset` and `ContemptLearning` (DIALS.md).
- **Terminus (2026-10-12).** Completing the full terminus is a hard alien victory (game over), LW2 Avatar-style: a visible meter, knocked back by strikes on the construction site.
- **Chapter playtest (2026-10-12).** Build the one-command playtest setup after M2 steps 4 and 6 land, so there is more to feel.
- **No buying from Earth (2026-10-13).** Clarified the same day: Earth counts as local by the wormhole lore. What the aliens lack is access to Earth's open market, not to Earth goods. Their sanctioned channel is their human proxies (trades with the Servants or Protectorate, maybe the Academy later), which the player can see and disrupt. Original decision: The aliens lose vanilla's boost-and-money substitution for missing space resources. They build only from what they mine, plus the exotics drip. Metals become a real constraint, and alien mining a real target.
- **Exotics upkeep (2026-10-13).** Bases and exotic warships should definitely compete for the drip. The widening wormhole is part of that balance, so the upkeep numbers are tuned together with the widening clock, not on their own.
- **Sustainable base cap (2026-10-16).** "We want intelligent aliens." The cap budgets bases and capital-hull upkeep from about 75% of the drip, so the aliens don't over-expand and then starve their exotic warships. Expansion resumes as the wormhole widens.
- **Doctrine selection stays seeded (2026-10-13).** No opening-weighted selection. Variety comes from the campaign seed and from shame shifts.

## Engineering rules

- **Ship structure.** One BepInEx plugin and one JSON mod. Every feature sits behind its own config switch, so A/B tests and probes can toggle them.
- **Mid-game loading.** All AI changes take effect when an existing save loads. State the mod adds (doctrine, threat memory) is stored in a sidecar keyed by the save, or rebuilt on load. **Never break vanilla save loading.**
- **Scope of changes.** Only change alien behaviour unless a change is explicitly meant to affect the human AI factions. Shared code paths, such as `SpaceCombatValue`, are guarded by a "who is asking" context.
- **Probe first.** Any AI change starts with a probe that shows the problem on vanilla.
