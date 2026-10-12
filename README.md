# TI Improved AI

A Terra Invicta mod (BepInEx 5 + Harmony, plus JSON templates) that makes the alien opponent play to win within in-universe constraints, rather than holding the idiot ball.

Status: M0 (infrastructure). Design is in `docs/DESIGN.md`; the harness is in `docs/HARNESS.md`.

- `src/ImprovedAI/` — BepInEx 5 + Harmony plugin: diagnostics (AI log, decision journal, metrics) and the campaign harness.
- `tools/run.ps1`, `tools/probes.ps1` — run probes on the isolated game copy; `tools/probe.py` evaluates asserts.
- `lab/probes/` — exploit probes (written to fail on vanilla); `lab/fixtures.json` — fixture saves.

- `tools/saves/timeline.py` — offline save analysis: alien vs player ship counts, fleet mass, fleet sizes over a series of campaign saves.
- `_ref/decompiled/` — decompiled game source (gitignored; regenerate with `ilspycmd -p -o _ref/decompiled "<TI>/TerraInvicta_Data/Managed/Assembly-CSharp.dll"`).
