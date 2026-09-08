# Gameplay tuning follow-up — 2026-09-06

Uncommitted working tree on main `1235579`; existing user changes retained.

- The held LMB + RMB **line** now reads `linePillarHeight` from the existing
  `EarthPillarWaveProfile`, default 3.15 m. Its custom Inspector draws
  **Линия столбиков — ЛКМ + ПКМ / Высота, м** above **Волна**. The old
  `crestHeight` controls the separate travelling wave, not this line. Editing
  line height does not trigger the wave Inspector's other-value synchronization.
- Existing authored Locomotion prevents base-layer regeneration during
  `EarthHumanoidMotionSetup.UpgradeController`. The user is assigning their own
  movement clips; those assignments were not replaced by this follow-up.
- MMA Kick import was truncated at frame 50 of the actual 122-frame take.
  Full source is restored (2.033 s); baked quick kicks use its native extension
  and recovery. Finisher retains one authored revolution without double-adding
  the full source's recovery turn. The Animator asset references stay in place.
- Sonar pulses travel 40 m over 4 s. Sixteen bounded slots prevent running
  emissions from overwriting pulses before they reach distant opponents;
  emission spacing, thin/dim fronts and one-second mode fade remain unchanged.
- Cracked walls retain their fitting baked meshes/colliders. Same-wall and
  slow settling contacts cannot recursively damage bonds. Ordinary hits remove
  at most 34% of a bond per event; further hits are needed for detachment.
- Drawn walls fit the original finite face, including width and all four
  embedded base corners. Thin supports use a smaller measured embed depth.
  Whole walls and connected pieces cannot be moved with MMB; detached pieces
  and structure repair/disassembly retain their routes.
- Stone response uses reduced mass and incoming contact speed. Tiny stones
  cannot trigger the repeated-hit ragdoll accumulation; heavier/faster stones
  produce stronger bounded impulse and scaled HP damage. Ordinary pooled
  stones no longer inherit an unarmed typed projectile's stale classification.

## Evidence

`BuildReports/GameplayTuningEdit.json`: **64/64 EditMode**, 00:39:10 UTC.
Includes finite-face/thin-support fitting, bond cap, stone mass/speed/HP response,
full kick source and sonar GPU/authority tests.

`BuildReports/GameplayTuningPlay.json`: **7/7 PlayMode**, 00:39:50 UTC.
Includes fitting fracture volume, real attached-cell stability and repeated hits,
anchored-wall MMB rejection, health deduplication, pebble/boulder response,
production five-beat combo and wave-timed enemy occlusion.

Final finisher adjustment: `GameplayTuningAnimationPlay.json`, **1/1 PlayMode**,
00:43:38 UTC. Source bake reports 360 degrees for finisher and full natural
recovery for normal kick. Left/right gameplay contact frames inspected.

These are focused behavior checks, not an exhaustive gameplay or GPU benchmark.
The prior short-kick guard-return contract in QUICK_STONE_COMBO is superseded
by this full-source recovery. Original unrelated mantle/startup gates remain open.
