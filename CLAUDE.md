# TI Improved AI

Terra Invicta mod: a constrained, competent alien opponent. Start with `docs/DESIGN.md` (goals, pillars, workstreams). Then read `docs/FINDINGS.md` (how vanilla AI works, with code refs), `docs/TESTING.md` (test layers L0–L5) and `docs/EXPLOITS.md` (exploit catalogue; each exploit becomes a probe).

- This project is separate from the user's Honorverse mod (`../TI - HH mod`). You may read its tooling for patterns (lab harness, BepInEx plugin layout). Never write there, never share code or config with it, and never run against the Steam install's BepInEx while Honorverse owns it. This project runs on its own game copy outside OneDrive (see `docs/TESTING.md`).
- Decompiled source is in `_ref/decompiled/` (gitignored, game build 2026-09-12 = 1.0.53b). Regenerate it after game patches with `ilspycmd -p -o _ref/decompiled "<TI>/TerraInvicta_Data/Managed/Assembly-CSharp.dll"`.
- Saves are gzipped JSON in `Documents/My Games/TerraInvicta/Saves`. `tools/saves/` holds the offline analysis tools. Never write to the user's saves.
- Any AI change starts with a probe that shows the problem on vanilla.
