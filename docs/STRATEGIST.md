# The strategist: a model that directs a faction (design, 2026-10-22)

**Goal (user, 2026-10-22).** In a real game with a human player, a stronger model (Opus) sets goals and strategy for an AI faction a few times a game-year. Between those checkpoints the game AI (vanilla plus this mod) runs the faction according to the instructions. Testing tools 1–3 (AI control, scripted orders, the rule-based player bot) are the first steps towards it.

Status: **design only.** Nothing here is built yet beyond the building blocks listed in "What exists".

## The loop

```
checkpoint (quarterly, or on a big event)
  -> brief:      the mod writes what the faction knows, in a compact form
  -> strategist: the model reads the brief and its previous intent, writes directives
  -> executor:   the mod validates the directives and applies them through the hooks the AI already reads
  -> the game AI plays on under them until the next checkpoint
```

### 1. The brief (state exporter)
What the faction knows, not the truth. For the aliens: their perceived threats (ThreatMemory), known human assets and fleets, their own economy (exotics, bases, upkeep), doctrine, contempt, escalation, terminus. For a human AI faction: resources, habs, fleets, research, councilors, relations and hate, threats. Plus a digest of what happened since the last checkpoint (from the decision journal: strikes, losses, trades, wars), and the strategist's own previous intent, so it can judge its plan.

### 2. Directives (what the strategist may set)
Bounded levers that re-weight choices the AI could make anyway. **No cheating:** no resources, intel or units it doesn't have, and nothing outside the faction's legal actions.

| Lever | Hook it drives today |
|---|---|
| Doctrine and posture dials (attack superiority, raid limits, pool margin, reserves) | `Dials`, within the difficulty's bounds |
| Strike priorities (which system or hab to pool against next; systems not to touch) | `ForcePooling` focus choice, `RaidPlanner` weights |
| Defence priorities (systems to hold, release budgets) | pooling release budget, defend goal importance |
| Economy (resources to mine, build-size mix, exotics spending vs war chest) | `Economy` need weights, build pick, war chest |
| Human factions: who is the enemy, who to court, trade stance | `Personality` weights, market and treaty rules |
| Research focus | research priority hook (to add) |
| Councilor mission emphasis | mission payoff bias (`PersonalityActions`) |
| Direct orders for a few key fleets (rare) | `Harness/Orders` (move, merge), moved into a runtime service |
| Intent (free text) | journaled; optionally shown to the player as intel or flavour `[ext]` |

The executor clamps every value to its legal range and logs what it applied.

### 3. Cadence and triggers
A checkpoint every quarter (configurable). Extra checkpoints on big events: escalation, a lost system, war declared, a failed strike, the terminus begun. Between checkpoints the directives persist in the save's sidecar (`<save>.improvedai.json`), so they survive save and load.

### 4. Transport: how the model is reached
| Option | How | Trade-offs |
|---|---|---|
| A. Offline, in the harness | A run stops at the checkpoint; I read the brief and write directives; the next run continues from that save (`-SavePath` already carries the sidecar). | No live game. Slow (a load per checkpoint), but needs no new infrastructure. The place to design the brief and the directives. |
| B. File handshake, live | The mod writes the brief to a folder and either pauses or plays on under the old directives; a local agent (a Claude Code session, or a small script) answers with a directives file. | Works in your real game. Needs a watcher running. Pause vs play-on is a choice. |
| C. Direct API call from the mod | The plugin calls the Claude API itself. | Simplest to use, but an API key sits in the mod config, plus network access and cost per checkpoint. |

Proposed path: build A first (it reuses the harness), then B for real games. C only if B proves clumsy.

### 5. Fairness and fallback
- The brief respects fog of war (the faction's own intel).
- If no directive arrives, the faction plays on under the last one, and the mod alone under none.
- Directives can't exceed the difficulty's bounds, so difficulty still sets how tight the constraints are.

## What exists (building blocks, 2026-10-22)
- `ai.control <faction> ai|human`: hand a faction to the game's AI, or back.
- `ai.move`, `ai.merge` (`Harness/Orders.cs`): real orders through the game's own transfer and merge operations, journaled.
- `ai.bot` (`Harness/PlayerBot.cs`): a rule-based player (reinforce threatened systems, merge idle fleets).
- The decision journal, monthly metrics and the alien strategy sidecar: the raw material of the brief and the storage for directives.

## Decisions for the user
1. Which faction does the strategist direct first: the aliens, a human AI faction, or both?
2. In a live game, does the game pause while the strategist thinks, or play on under the previous directives?
3. Transport: a local agent watching a folder (B), or an API key in the mod (C)?
4. Should the player ever see the strategist's intent (as intel reports or flavour), or should it stay hidden?
5. Cadence: quarterly plus big events, or something else?

## Next concrete steps
1. Directive schema v0 (JSON) and an executor for 3–4 levers (strike priorities, posture dials, economy mix), with a harness command to load a directives file.
2. Brief v0 for the aliens, built from the journal and the strategy sidecar.
3. A harness checkpoint loop (option A): run to the checkpoint, write the brief, wait for a directives file, continue. One consolidated run per scenario.
