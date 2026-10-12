# TI Improved AI

A Terra Invicta mod that makes the alien opponent constrained but competent: it plays to win within in-universe limits instead of holding the idiot ball.

**New session? Read `docs/HANDOFF.md` first.** It has the current state, open questions for the user, and next steps. Then, as needed:

| Doc | What it covers |
|---|---|
| `docs/DESIGN.md` | Vision, pillars P1–P6, user decisions (authoritative) |
| `docs/M2-PLAN.md` | Current milestone, step by step, with gates |
| `docs/PROGRESS.md` | What is built and what the probes and soaks showed |
| `docs/FINDINGS.md` | How vanilla AI works, with code refs; vanilla bugs found |
| `docs/EXPLOITS.md` | Exploit catalogue and probe status |
| `docs/DIALS.md` | Every tunable and its intended difficulty direction; scenario pacing levers |
| `docs/TESTING.md`, `docs/HARNESS.md` | Test layers and how the harness works |

## Layout

- **`src/ImprovedAI/`**: BepInEx 5 + Harmony plugin (net472). Patches are applied once the main menu exists (`Plugin.cs` Bootstrap).
  - `AI/`: alien behaviour changes. Every one sits behind a config switch, and `[General] VanillaAI=true` turns them all off.
    - `ThreatMemory`, `DecisiveForce` (+ E14 shortcut guard), `ForcePooling`, `StanceCourage`
    - `RaidPlanner` (+ captures, per-target failure memory)
    - `AlienStrategy` (doctrine + sidecar), `Dials` (doctrine-scaled dial accessors; **AI code reads dials only through `Dials`**), `Disposition` (contempt)
  - `Fixes/`: vanilla bug fixes (`AlienMarineValue`).
  - `Diagnostics/`: AI log revival, decision journal (`journal.jsonl`), monthly metrics, combat journal, observer clock.
  - `Harness/`: unattended campaign runner (`run.json`), probe setup commands (`HarnessCommands.cs`: `ai.foundoutposts`, `ai.spawnfleet`, `ai.drainammo`, `ai.doctrine`, `@N` scheduling).
- **`tools/`**:
  - `deploy.ps1`; `run.ps1` (one run: `-Probe`/`-Fixture`/`-SavePath`, `-Vanilla`, `-Days`, `-FinalSave`)
  - `probes.ps1` (several probes); `soaks.ps1` (repeated vanilla/mod soaks)
  - `probe.py` (asserts + info), `summarize.py` (story of one run), `compare.py` (two runs side by side), `aggregate.py` (means over repeated soaks)
  - `saves/timeline.py` (offline save analysis)
- **`lab/`**: `probes/*.json` (exploit and gate probes), `fixtures.json` (user saves used as fixtures), `results/` (gitignored run output).
- **`_ref/decompiled/`**: decompiled game source (gitignored; build 1.0.53b of 2026-09-12). Regenerate after a game patch: `ilspycmd -p -o _ref/decompiled "<TI>/TerraInvicta_Data/Managed/Assembly-CSharp.dll"`.

## Rules

- **Separate from the Honorverse mod** (`../TI - HH mod`). Read its tooling for patterns only. Never write there, share code or config with it, or touch the Steam install's BepInEx/Mods.
- **Run only on the game copy.** This project runs on `C:\Games\TI-ImprovedAI` (outside OneDrive). Harness scratch is `C:\Games\TI-ImprovedAI-harness`. `deploy.ps1` refuses the Steam install.
- **Never write to the user's saves** (`Documents/My Games/TerraInvicta/Saves`). The harness stages copies and redirects the save folder, profile and autosaves.
- **Probe first.** Every AI change starts with a probe that shows the problem on vanilla. **Never conclude balance from one run**: runs are non-deterministic, so use `soaks.ps1` + `aggregate.py`.
- **Keep it alien-only and save-compatible.** New state goes in the `<save>.improvedai.json` sidecar; the save file is never modified.
- **Lore.** Expanding it is allowed; flag any conflict with game text to the user explicitly. Tag ideas `[lore]` / `[ext]` in docs.
- **Commits.** Commit and push to `main` as work lands (the user's GitHub repo `benjaminrstone/TI-improved-AI`).
- **Escaping.** Edit files with Windows paths via the Edit tool, not Python heredoc replaces (escape mangling). See the gotchas in `docs/HANDOFF.md`.
