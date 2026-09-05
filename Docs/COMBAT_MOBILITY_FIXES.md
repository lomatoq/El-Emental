# Combat and mobility fixes — 2026-09-04

The user's current scene and assets were saved before this pass. The scene and
profile snapshot is in `BuildReports/CombatMobilityFixes/Before/`. This pass builds
on the dirty `d2174ed` working tree; it does not reset the scene or regenerate it.

## Where to tune

The later [wave/contact/collider follow-up](WAVE_CONTACT_FIX.md) adds ground-contact
streams, stronger extraction dust, matching wall collider hulls and geometry-sized
gravity packing. Actual rock/arena children now derive from closed partitions of
their parent's convex volume; see [filled fracture](CONTAINED_FRACTURE_FIX.md).

Wall material correction: the previous `sharedMaterial` assignment replaced only
slot zero and retained `RumbleClay` in slot one. Unity can redraw a single-submesh
stone with that excess slot. Natural wall pieces now receive an exact one-element
`sharedMaterials` array containing `RumbleSandstone`. The saved wall pool's
`Fracture Interior Material` and both wall setup paths also use sandstone.
The runtime regression now checks the entire material array, not just slot zero.
The strengthened production collision/wall test passed **1/1** at UTC
`2026-09-04T08:56:42Z` (`BuildReports/CombatCollisionPlay.json`).

- **Wave animation:** Unity menu `Elemental > Tuning > Wave Animation` selects
  `Assets/Elemental/Content/Profiles/EarthPillarWaveProfile.asset`. The simplified
  Inspector now shows six curves, five phase durations and five main controls.
  See [wave repair](WAVE_REPAIR.md) for the current contract; the earlier long list
  of procedural parameters is no longer the authoring interface.
- **Small shard effects:** `EarthEffectsTuningProfile.asset`, `Materials > Impact
  Rubble` references `Content/Materials/LooseEarthChipVfx.mat`. Particle layer
  `Angular Speed` controls the random rotation speed in degrees per second.
- **Physical stones and natural wall/column breakup:** the scene's
  `EarthRockDebrisPool > Material` references
  `Content/GraphicsV5/Materials/RumbleSandstone.mat`. Shape variants come from the
  same stone library; fracture visuals use a broad bevel fitted to the actual convex.
  Detaching an arena piece preserves its original mesh/material; only a subsequent
  physical split produces contained stone children. See [containment repair](CONTAINED_FRACTURE_FIX.md).
- **Surf wake:** `EarthSurfProfile.asset > Dust and stones around board` exposes
  `Wake Dust Per Meter`, `Wake Chip Multiplier`, `Wake Front Share`.
- **Cumulative structural threshold:** each `EarthArenaStructure` has
  `Cumulative Fracture Impulse` (default 95). The meteor-only floor stays protected.
- **Secondary breakup:** `EarthRockProfile.asset` supplies the existing size/mass
  breakup policy. Collision impulse accumulates until its threshold is reached.

## Runtime changes

Armor projectile collisions now route damage into structures. Weak meaningful
impacts persist between hits on walls, columns and platforms. Attached arena cells route to their structure;
released cells receive independent fatigue and split through the physical debris
pool. Successful splits retire the original matter representation and preserve
mass in targetable children. Exhausting the pool retains the original piece.
Repair cannot recreate a consumed cell while its children remain alive.

Cosmetic shards have independent 3D spawn angles and angular speeds. Surf and
pillar manual chips and physical breakup children also have seeded spin.
Cosmetic shards retain the prior dust compositing fix; physical geometry retains
normal world depth.

Surf emits more stones and dust along the sides, front and deck. Raising and
receiving pillars emit spatial material cues; receiving contact adds dust/chips
and suppresses the landing roll without disabling ordinary landing rolls.

Wave pose is continuous across the premium phase boundaries and is evaluated at
render frequency. Longer anticipation settings also extend cell visibility lead.
Physical collision motion remains on the fixed simulation clock.

## Focused verification

Final results on the current dirty tree: **9/9 EditMode** at UTC
`2026-09-04T08:46:02Z`, **4/4 PlayMode** at UTC `2026-09-04T08:49:26Z`.
The production feedback sample observed a hub maximum of **0.0628 ms** and
presenter maximum of **0.1984 ms**; these are scoped marker observations.

`Elemental > QA > Run Combat Mobility Fixes EditMode Tests` and
`Elemental > QA > Run Combat Mobility Fixes PlayMode Tests` write JSON/XML reports
in `BuildReports/CombatMobilityEdit.*` and `BuildReports/CombatMobilityPlay.*`.
Production captures and scoped profiler observations are in
`BuildReports/EarthMaterialPass/`.

The PlayMode scenarios cover real armor collisions, cumulative hits on attached
and released cells, physical collision breakup away from the arena, mass-preserving
children, no duplicate repair, rounded wall shapes/material, cushioned landing,
full debris pool retention, and the production surf/wave/pillar effects path.
The focused armor probe ignores adjacent walls to hit its selected column.
Production gravity launch measures two ticks of unobstructed integration with
contacts temporarily disabled; the combat test separately exercises real solid
contacts and verifies secondary breakup. These fixtures avoid conflating a
legitimate contact/fragment replacement with a failure to launch.

Existing orphan armor-shell Required Components warnings predate this pass.
An intermittent Unity editor `unexpected guid mismatch` assertion also occurred
during test/domain reload; no C# compilation errors were reported.
Focused tests are evidence for these paths, not a full-project performance or
visual-quality certification.
