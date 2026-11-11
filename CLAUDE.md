# TI Improved AI

A Terra Invicta mod that makes the opposition constrained but competent: it plays to win within in-universe limits instead of holding the idiot ball. Aliens first; the AI human factions and an LLM strategist are further tracks.

**New session? Read `docs/HANDOFF.md` first** (state, what is not known, next steps, gotchas).

| Doc | What it covers |
|---|---|
| `docs/DESIGN.md` | Vision, pillars P1–P6, user decisions (authoritative) |
| `docs/TESTING.md` | How to run probes, soaks and playtests; what a run produces |
| `docs/FINDINGS.md` | How the vanilla alien AI works, with code refs |
| `docs/HUMAN-AI.md` | How the vanilla human AI works, its bugs, what the mod changes |
| `docs/STRATEGIST.md` | The strategist loop and its directives |
| `docs/EXPLOITS.md` | Exploit catalogue and status |
| `docs/DIALS.md` | Which way each dial should move with difficulty; scenario pacing levers |

## Layout

- **`src/ImprovedAI/`**: BepInEx 5 + Harmony plugin (net472). Patches are applied once the main menu exists (`Plugin.cs`).
  - `AIConfig.cs`: every switch and dial. `[General] VanillaAI=true` turns all behaviour changes off; code checks the `Use*` accessors.
  - `AI/`: alien behaviour. Dials that a doctrine, escalation or the strategist can scale are read through `AI/Dials.cs`, never from the config directly.
  - `Human/`: the AI human factions (never the player's faction).
  - `Strategist/`: briefs, directives, the checkpoint loop, player intel.
  - `Fixes/`: vanilla bug fixes.
  - `Diagnostics/`: the journal, metrics and logs every run produces. `Observer.cs` is the daily and monthly clock that drives all the mod's periodic logic.
  - `Harness/`: the unattended runner and its `ai.*` commands.
- **`tools/`**: `deploy.ps1`, `run.ps1`, `probes.ps1`, `soaks.ps1`, `playtest.ps1`, `strategist.ps1`, and the Python analysis scripts (`probe.py`, `summarize.py`, `compare.py`, `aggregate.py`).
- **`lab/`**: `probes/*.json`, `fixtures.json`, `results/` (gitignored run output).
- **`_ref/decompiled/`**: decompiled game source (gitignored; build 1.0.53b). Regenerate after a game patch: `ilspycmd -p -o _ref/decompiled "<TI>/TerraInvicta_Data/Managed/Assembly-CSharp.dll"`.

## Rules

- **Separate from the Honorverse mod** (`../TI - HH mod`). Read its tooling for patterns only. Never write there, share code or config with it, or touch the Steam install's BepInEx or Mods.
- **Run only on the game copy** (`C:\Games\TI-ImprovedAI`, outside OneDrive). Harness scratch is `C:\Games\TI-ImprovedAI-harness`. `deploy.ps1` refuses the Steam install.
- **Never write to the user's saves** (`Documents/My Games/TerraInvicta/Saves`). The harness stages copies and redirects the save folder, profile and autosaves.
- **Save-compatible.** New state goes in the `<save>.improvedai.json` sidecar; the save file is never modified.
- **Probe first; never conclude from one run.** A change starts with a probe that shows the problem on vanilla. Runs are non-deterministic: balance claims need repeated runs (`soaks.ps1` + `aggregate.py`). The regression check is `tools\probes.ps1 flow-2034, flow-2039`.
- **Lore.** Expanding it is allowed; flag any conflict with game text to the user explicitly. Tag ideas `[lore]` / `[ext]` in docs.
- **Commits.** Commit and push to `main` as work lands (`benjaminrstone/TI-improved-AI`).

## Conventions

- **Code speaks for itself.** A comment states a fact the code cannot: the vanilla behaviour being worked around (with the game method), an invariant, a unit. No dates, run names, attributions, version history or plans in comments.
- **Docs say what is true now.** No progress log: git has the history. A measurement goes in a doc only when a future run will be compared against it or it explains a decision; say how many runs it rests on.
- **No backwards compatibility.** There is one user and no release. Rename, restructure and delete freely; don't keep switches or code paths for old behaviour.
- **A new dial needs a reason to exist.** Prefer a constant until something actually varies it (a doctrine, a difficulty level, a probe).
- **Windows.** Edit files that contain Windows paths with the Edit tool, not shell heredocs (escape mangling).
