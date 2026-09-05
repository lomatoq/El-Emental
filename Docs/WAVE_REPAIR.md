# Wave fracture and authoring repair — 2026-09-04

The later [stable-wave/contact follow-up](WAVE_CONTACT_FIX.md) supersedes live
pool reuse, depth-varying wave contours, and the profile snapshot below. These
measurements remain evidence for the earlier oversized-cell repair.

Dirty working tree on `d2174eded114dd022e4a9c442abadda7a0e44555`.
The user's scene and wave values were retained. The pre-edit profile snapshot is
`BuildReports/WaveRepair/Before/EarthPillarWaveProfile.asset`.

## Causes and changes

Sparse wave families generated Voronoi cells over the entire disk. Outer cells
therefore claimed large empty regions: a six-seed probe measured footprints up to
11.38 m (SerpentRidge), 10.77 m (ForkedWave), and 10.88 m (RollingTerraces).
The pure footprint solver now intersects each cell with a bounded octagon around
its generating site, scaled by the existing size class. Centroids are recomputed
after clipping. All six semantic families and small/large size variation remain.

The same pool alternated metre-sized polygon meshes and unit crest meshes without
restoring the latter. Crest scheduling now restores both base render and collider
geometry before scaling. A bounded per-cast impact ledger also prevents overlapping
casts from repeatedly resetting the structural damage gate; walls share that gate.

## Inspector

`Elemental > Tuning > Wave Animation` opens the saved profile. The custom Inspector
also applies to the existing derived EarthWebWaveProfile asset.

- Five height curves with seconds: anticipation, rise, settle, hold, retreat.
- One tilt curve across the entire cycle.
- Five main controls: height, range, propagation speed, push strength, tilt angle.

Curve X is normalized phase time. Height Y is relative to the peak height;
neighbouring phase endpoints are anchored to join without position jumps.
Tilt Y is -1..1 times the chosen tilt angle. Render-time evaluation uses the curves;
pure timing owns phase boundaries, while collision motion stays on the fixed clock.
Premium mode has no added procedural tremor. Legacy serialized data is retained.

Preserved values include height 1.55 m, range 11.2 m, speed 2.25 m/s, push 187,
tilt 6 degrees, and phase durations .055/.829/.145/.055/.3 seconds.

## Evidence

- `BuildReports/WaveRepairEdit.json`: 24/24, UTC 09:26:48; six families across
  six seeds and three charge levels, bounded geometry, size diversity, per-cast
  claims, actual curve evaluation and phase joins, surface-follow regressions.
- `BuildReports/WaveRepairPlay.json`: 1/1, UTC 09:28:29. Six production waves plus
  six crest casts forcing pool reuse; 5,857 visible samples, largest polygon span
  2.133 m, largest unit crest mesh span .931 m.
- `BuildReports/WaveRepair/RepeatedWave.png`: reviewed production capture.
- `BuildReports/WaveRepair/Latest.json`: measured maximum
  `Elemental.Wave.Schedule` 168.865 ms in this Editor test. This includes launch-time
  topology/mesh/collider preparation and is a remaining startup performance risk;
  these checks do not certify frame-time performance or a standalone build.

Rerun via `Elemental > QA > Run Wave Repair EditMode Tests` and
`Run Wave Repair PlayMode Tests`. Earlier project-wide acceptance remains separate.
