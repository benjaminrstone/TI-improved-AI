# The strategist: a model that directs a faction (design, 2026-10-22)

**Goal (user, 2026-10-22).** In a real game with a human player, a stronger model (Opus) sets goals and strategy for an AI faction a few times a game-year. Between those checkpoints the game AI (vanilla plus this mod) runs the faction according to the instructions. Testing tools 1–3 (AI control, scripted orders, the rule-based player bot) are the first steps towards it.

Status: **v0 being built (2026-10-22)**, following the decisions below.

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

## Decisions (user, 2026-10-22)
1. **Which factions:** all non-player factions: the aliens and every AI human faction.
2. **Pause:** the game pauses while the strategist thinks.
3. **Transport:** local. The mod writes briefs to a folder; a local watcher runs the strategist (Claude Code in headless mode) and writes the directives back. No API key in the mod.
4. **What the player sees:** a faction's intent, only if that faction is allied with the player, is the player's client, or has a councilor the player has turned.
5. **Cadence:** every 3 months (event triggers later).

## v0 as built (2026-10-22)
- **Directives** (`Strategist/Directives.cs`, schema above): aliens `posture`, `strikeTargets`, `spare`, `raidFactions`, `warChestMonths`; human AI factions `rivals`, `friends`, `defend`, `missionFocus`; every faction an `intent`. Stored in the sidecar.
- **Executor hooks:** posture scales the alien dials (`Dials`); strike targets order the pooling focus and spared systems are skipped (`ForcePooling`, `RaidPlanner`); raid factions double raid scores; the war chest takes the directive's months (`Economy`); rivals and friends add ±1.5 hate a month (`Personality` jealousy) and steer hostile missions (`PersonalityActions`); defend systems get a defence goal of importance 17 at the faction's main hab there (`Strategist/DefendDirective.cs`).
- **Brief** (`Strategist/Brief.cs`): per faction, through its own AI's perception (aliens: threat memory; humans: vanilla's system fleet strengths x their perceived-strength factor). Hab locations count as known.
- **Checkpoint loop** (`Strategist/Checkpoint.cs`): right after a game loads without directives, then every 3 months on the 1st. Pauses the game, writes `briefs/<date>.json`, keeps the game paused until `directives/<date>.json` arrives (or `TimeoutMinutes`, default 20, then plays on under the previous directives), applies it, shows intel, resumes.
- **Watcher** (`tools/strategist.ps1`, instructions `tools/strategist/prompt.md`): one headless Claude Code call per faction, in parallel, **each seeing only its own faction's brief and previous directive**, so the factions can't coordinate on knowledge none of them has. Raw answers go to `responses/<date>.md`.
- **Intel** (`Strategist/Intel.cs`): the player gets a notification with a faction's intent if that faction has a non-aggression pact or an intel-sharing treaty with them ("allied": the game has no formal alliance), is their client, or has a councilor they turned.

### Using it in a game on the game copy
`tools\playtest.ps1 -Fixture <name> -Strategist` (or a `-SavePath`): turns the strategist on for that launch only, starts the watcher in a minimized window, and stops it when you quit. The exchange folder (briefs, directives, raw answers, watcher log) is the session's `strategist` folder under `labesults`. The game pauses at each checkpoint for as long as the seven parallel calls take. (Turning it on in the plugin's .cfg also works, but `deploy.ps1` resets that file.)

In the harness: probe `strategist-2039`, with `tools\strategist.ps1 -Harness` running alongside (it follows the newest run; each run keeps its exchange files in `lab\results\<run>\strategist`). While the strategist thinks, the harness holds the Autopilot off so the clock stays stopped.

## Next steps
1. Judge the directives' quality over a long run (does each faction stay in character; do directives move outcomes).
2. A digest of what happened since the last checkpoint in each brief (battles, losses, treaties), from the decision journal.
3. Event-triggered checkpoints (escalation, a lost system, war declared).
4. More levers: research focus, build mix, councilor mission types.
