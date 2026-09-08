# Gameplay readability repair — September 6

This follow-up supersedes the premium HUD appearance acceptance. It preserves
the user's manually assigned locomotion clips and thresholds.

- Walls: the intact proxy combines the exact rendered fracture cells. The
  production bake uses a shared broad crown and external chamfers for source
  and all forty cells. Narrow walls no longer receive a stretched decorative
  comb, and cracking does not replace their geometry with a plain box.
- HUD: recovered the original pre-redesign sources; fonts increased 12%.
  Navigation moved down four pixels to clear the larger mana text. Provenance:
  [HUD_ORIGINAL_RESTORE](HUD_ORIGINAL_RESTORE.md).
- Paired-mouse drag line: saved `lineGroundOffset=-0.35m`; the user's separate
  pillar height remains 2.9m. Inspector: EarthPillarWaveProfile →
  «Линия столбиков — ЛКМ + ПКМ» → «Смещение от земли, м».
- Sonar: support reacquisition preserves the one-second toggle envelope;
  movement wave gain .58, with existing thin width and two-metre running spacing.
  A persistent Unity shader-array allocation at the former five entries was
  silently truncating newer waves. Versioned sixteen-entry globals remove that
  stale allocation; tests now assert actual capacity and recent wave presence.
- Character: short A/D taps and sustained turns articulate the actual legs.
  Secondary motion now has a slower spring and greater inertia. Real skinned
  plume displacement measures 2.2–2.4cm; belt displacement 4.7–8.8cm across the
  two actors. No locomotion/controller rebuild.
- Charge: added missing live LMB acquisition/wall/pluck accumulation source.
  Earlier onset, maximum FOV +10 degrees and chromatic .30; existing light
  render-only shake restores the camera pose afterwards.

Evidence already completed: wall geometry **6/6 Edit**, narrow/wide intact and
cracked captures inspected, wall repeat-hit test passed. Character tap and
skin-motion tests passed. HUD plus running pixel check **2/2 Play** passed after
the four-pixel spacing correction. Charge **19/19 Edit + 1/1 Play**, actual URP
stack and projection verified; neutral/full-charge captures show 60→70 degrees
and visible edge aberration. Final sonar capacity, LMB route and impact evidence
is recorded below after coordinated runs.

Final sonar capacity run: **3/3 Play** at 08:41 UTC; no array-size warnings,
all three GPU arrays contain sixteen entries and newest wave age stays below
0.8 seconds. Actual running camera reveal increased from 104,204 to 336,338
pixels in the same near-pillar framing. No further brightness increase needed.
Physical LMB-only accumulation **1/1 Play**, +9.606° lens and clean release;
captured edge aberration inspected. Stone response pure checks **53/53 Edit**
cover mass ordering, bounded +40% transfer and viscous return at 30/60/120Hz.

Final impact production run **6/6 Play**, 08:48:08 UTC, includes actual stone
shove, production torso spring, living heavy-hit recovery and health-owned
finishing hits. Medium-hit spring peaks at 2.434 degrees around 0.10s and
settles by 1.2s. The get-up test now waits for the clip's authored contact phase
instead of demanding it during the initial unweighted segment. Runtime recovery
and controller clip assignments were not changed for that assertion.

Final isolated full-body stagger capture rerun: 1/1 Play at 08:51:41 UTC; peak and settled renders inspected. Framing uses actual head/foot bones so oversized effect bounds and orbiting stones cannot spoil the proof.
