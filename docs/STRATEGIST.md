# The strategist: a stronger model directs each faction

In a game with a human player, a stronger model (Claude Opus, run locally through headless Claude Code) sets the strategy of every non-player faction every three months. Between checkpoints the game AI (vanilla plus this mod) plays the faction under those directives. Directives re-weight choices the AI could make anyway. They never grant resources, intel or units.

It is off by default (`[Strategist] Enabled`), and `[General] VanillaAI=true` turns it off as well. Code: `src/ImprovedAI/Strategist/`. Watcher: `tools/strategist.ps1`, with the model's instructions in `tools/strategist/prompt.md`.

## Decisions

- **Factions:** all non-player factions, meaning the aliens and every AI human faction. The player's faction is never directed.
- **Pause:** the game pauses while the strategist thinks.
- **Transport:** local files. The mod writes briefs to a folder, a local watcher runs the model and writes directives back. No API key in the mod.
- **What the player sees:** a faction's intent, only through a relationship with that faction (see "What the player sees").
- **Cadence:** every 3 months.

## The checkpoint loop (`Checkpoint.cs`)

1. **When.** On the first new day of a campaign whose sidecar records no checkpoint, then on the 1st of a month once `MonthsBetween` (default 3) months have passed since the last one. The harness command `ai.strategist checkpoint` forces one.
2. **Brief out.** The mod pauses the game, remembers the speed, and writes `briefs/<date>.json` to the exchange folder. It first deletes any directives file already there for that date (left by a reload or by another campaign in the same folder), so a stale answer is never applied.
3. **Wait.** It holds the game paused and looks for `directives/<date>.json` once a second.
4. **Directives in.** Each entry for a directed faction replaces that faction's whole directive in the sidecar (`<save>.improvedai.json`), so directives survive save and load. Entries under other names are ignored. A faction missing from the file keeps its previous directive.
5. **Resume.** The player gets the intel notifications they are entitled to, and the game resumes at the previous speed.
6. **Timeout.** After `TimeoutMinutes` real minutes (default 20, 0 waits indefinitely) the game plays on under the previous directives, or under the mod alone if there are none. A timeout still counts as the checkpoint, and so does a directives file that fails to parse: the next checkpoint comes after the full interval.

**Exchange folder:** `[Strategist] Folder`. When that is empty: `<run folder>\strategist` in an unattended harness run, otherwise `BepInEx\ImprovedAI-strategist` in the game copy. It holds `briefs\`, `directives\`, `responses\` (the model's raw answers) and `watcher.log`.

**Journal:** `strategist` (`ev`: `brief`, `applied` with the directives, `timeout`, `bad-directives`), `strategist-intel`, `strategist-defend`.

## The brief (`Brief.cs`)

One file per checkpoint: date, campaign years, the player's faction, and one section per directed faction. Each section holds what that faction knows, through its own AI's perception. Hab counts and locations are treated as known to everyone (they are visible on the game map).

| Faction | Contents |
|---|---|
| The aliens | Doctrine, escalation state and desperation, terminus start and progress, contempt per human faction, exotics (stock, drip, war chest), base count, fleet SCV in total and by system, the pooled strikes in progress (target, system, SCV needed and present). Per human faction: alien hate, habs by system, and fleets by system as the aliens remember them (`ThreatMemory`). |
| An AI human faction | Persona, self-assessment, resource stocks, habs and fleets by system, ship and councilor counts, its patron if it is a client. Per other human faction: hate, at war, non-aggression pact, habs by system, and fleets by system as perceived (vanilla's system fleet strengths × the faction's perceived-strength factor). The alien threat in the same form. |

The brief has no account of what happened since the last checkpoint. The model's only memory is its own previous directive, which the watcher passes back to it.

## Directives (`Directives.cs`)

Every field is optional. A missing field leaves the AI's own judgement in place. Values are clamped when read. Faction names are template names (`ResistCouncil`, `AlienCouncil` and so on) and system names are matched as they appear in the brief, both without regard to case.

| Field | For | Range | Effect | Hook |
|---|---|---|---|---|
| `intent` | all | free text | What the faction means to do. Journaled, and shown to the player under the intel rules. | `Intel.cs` |
| `posture` | aliens | −1 cautious to +1 aggressive | At posture p: attack superiority × (1 − 0.25p), home reserve × (1 − 0.4p), raid and capture slots × (1 + 0.5p), arrival fight ratio × (1 − 0.15p). | `AI/Dials.cs` |
| `strikeTargets` | aliens | first 5 systems count | Pooled strikes pick their focus goal in these systems first, in list order. A longer list was no priority at all (one campaign's list grew to 30 systems), hence the cap. | `AI/ForcePooling.cs` |
| `spare` | aliens | first 12 systems count | No pooled strike and no raid in these systems. | `AI/ForcePooling.cs`, `AI/RaidPlanner.cs` |
| `raidFactions` | aliens | faction names | Raid scores against these factions' habs × 2. | `AI/RaidPlanner.cs` |
| `warChestMonths` | aliens | 0 to 36 | Replaces `ExoticsWarChestMonths`: the exotics kept back outside escalation, in months of drip. | `AI/Economy.cs` |
| `rivals` | AI humans | faction names | Adds 1.5 to the monthly hate change toward each, on top of the personality terms, and favours hostile councilor missions against them. | `Human/Personality.cs`, `Human/PersonalityActions.cs` |
| `friends` | AI humans | faction names | Subtracts 1.5 from the monthly hate change toward each (never below −1.5 a month), and steers hostile missions away from them. | same |
| `missionFocus` | AI humans | 0 to 1, default 0.5 | Strength of the mission steer: payoff × (1 + 0.8 × focus) against rivals, × (1 − 0.8 × focus) against friends. | `Human/PersonalityActions.cs` |
| `defend` | AI humans | systems | In each listed system where the faction has a hab, a fleet defence goal of importance at least 17 at its main hab there (the station with the most shipyards, else the body of its highest-tier base). Vanilla only defends the primary hab or a system that enemy fleets have already entered. | `DefendDirective.cs` |

## The watcher (`tools/strategist.ps1`)

- It watches the exchange folder for briefs without a matching directives file.
- For each brief it makes one headless Claude Code call per faction, in parallel, with no tools. Each call sees only the game header, that faction's own section of the brief and that faction's own previous directive, so factions cannot coordinate on knowledge none of them has.
- An answer must contain one JSON object. A call is tried twice. A faction with no valid answer is left out of the file and keeps its previous directive.
- Parameters: `-Folder`, `-Model` (default `opus`), `-PollSeconds`, `-Once`, `-StopAfterMinutes`, `-Harness`.

## What the player sees (`Intel.cs`)

After each checkpoint the player gets a notification with a faction's intent if that faction:
- has a non-aggression pact with the player, or
- has an intel-sharing treaty with the player, or
- is the player's client (`Human/Personality.cs`), or
- has a councilor the player has turned.

The first two stand in for "allied": the game has no formal alliance. `[Strategist] ShowIntel` turns the notifications off. In unattended harness runs the intel is journaled only. `[ext]`

## Running it

- **In a game you play:** `tools\playtest.ps1 -Fixture <name> -Strategist` (or `-SavePath <save>`; `-StrategistModel` picks the model). It turns the strategist on for that launch only, starts the watcher in a minimized window and stops it when you quit. The exchange folder is the session's `strategist` folder under `lab\results`. Turning it on in the plugin's .cfg also works, but `deploy.ps1` resets that file.
- **In the harness:** start `tools\strategist.ps1 -Harness` before the run. It follows the newest run under `lab\results`, and each run keeps its exchange files in `lab\results\<run>\strategist`. Probe: `strategist-2039`. While the strategist thinks, the harness holds the Autopilot off so the clock stays stopped.

## Known limits

- **No digest.** The brief carries the current state only, not the battles, losses and treaties since the last checkpoint.
- **No event triggers.** Checkpoints come by the calendar only, not on escalation, a lost system or a declaration of war.
- **Fixed ranges.** Directive values are clamped to the ranges above at every difficulty. They are not tied to the difficulty's dial values.
- **Pause length.** The game stays paused for as long as the parallel calls take (seven factions in a standard game).
- **`defend` is a goal, not an order.** It raises a defence goal. Which fleets answer it is still vanilla's assignment.
- **Quality is unjudged.** One 15-year campaign ran 61 checkpoints without a timeout, with a passive player. Whether each faction stays in character, and whether directives change outcomes, has not been measured.

## Not built

- Research focus, build mix and resource priorities as directive fields.
- Councilor mission types as a directive field (only the rival and friend steer exists).
- Direct orders for individual fleets (`Harness/Orders.cs` has move and merge for harness scripts only).
- A direct API call from the plugin (decided against: transport is local).

## Next steps

1. Judge the directives over a long run with a player that acts (`ai.control ResistCouncil ai` or `ai.bot`).
2. Add the digest of events since the last checkpoint to each brief, from the decision journal.
3. Add event-triggered checkpoints.
4. Add levers: research focus, build mix, councilor mission types.
5. Watch the gang-up on the player. In the one long campaign, 5 of 6 human factions named the Resistance a rival in 2039, where it was the runaway leader, and none did in 2026 to 2028.

**Open question for the user:** whether a non-aggression pact or an intel-sharing treaty is too wide a reading of "allied" for intel.
