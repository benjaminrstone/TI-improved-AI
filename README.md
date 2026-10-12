# TI Improved AI

A Terra Invicta mod (BepInEx 5 + Harmony, plus JSON templates) that makes the alien opponent play to win within in-universe constraints, rather than holding the idiot ball.

Status: research and design. See `docs/` as it fills in.

- `tools/saves/timeline.py` — offline save analysis: alien vs player ship counts, fleet mass, fleet sizes over a series of campaign saves.
- `_ref/decompiled/` — decompiled game source (gitignored; regenerate with `ilspycmd -p -o _ref/decompiled "<TI>/TerraInvicta_Data/Managed/Assembly-CSharp.dll"`).
