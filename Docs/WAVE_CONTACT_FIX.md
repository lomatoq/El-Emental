# Stable wave, contact effects and small-stone collision — 2026-09-04

Dirty working tree on `d2174eded114dd022e4a9c442abadda7a0e44555`.
The user's latest scene was saved first. The unchanged wave profile is backed up
in `BuildReports/WaveContact/Before/`: rise 3 s, settle 1 s, hold .5 s, retreat 3 s,
speed 6.04 m/s, push 115, tilt 12.52 degrees, height 1.55 m. No scene rebuild.

## Stable fracture

### September 4 follow-up: overlapping rows and user-controlled phase length

This supersedes the speed-cap/previous-row-retreat rule recorded below. The user
explicitly rejected waiting for an earlier row to descend. The cast now samples
one travelling pulse: `height(d,t) = curve(t - (d-firstDistance)/speed)`.
Each row's delay depends only on its fixed distance and the authored speed;
phase durations do not slow propagation. Immutable geometry, common cast frame,
contained bevels and whole-cast pool reservation remain in place.

Reference algorithms: [Catlike Coding, Waves](https://catlikecoding.com/unity/tutorials/flow/waves/)
and [GPU Gems, Effective Water Simulation](https://developer.nvidia.com/gpugems/gpugems/part-i-natural-effects/chapter-1-effective-water-simulation-physical-models).
Their common spatial/time phase is adapted to a finite pulse using the existing
five phase curves. Horizontal Gerstner displacement is deliberately not applied
to these fixed fracture footprints.

The top Inspector field **Длина фазы волны, м** is a direct authoring control:
`length = speed * (rise + settle + hold + retreat)`. Editing it scales the four
timings proportionally within their existing limits, preserving curve keys and
speed. The actual seconds remain visible and individually editable; there is no
hidden timing multiplier or additional runtime scheduling dependency. Preparation
keeps its separate duration. Changing speed or an individual timing updates the
displayed length. Existing saved settings are not automatically retuned.

Current focused evidence is listed at the top of PROJECT_TECHNICAL_STATE.md.

### September 4 follow-up: rendered seams and dark dust

The earlier immutable-mesh test missed a placement defect: animated tilt selected
different lowest vertices and the placement helper snapped those vertices to the
cell centre, moving the whole rock sideways despite unchanged mesh vertices. A new
production regression reproduced **1.724 m** of projected vertex drift with the
saved long timings. Shared polygon cells now retain their initial rotation, unit
scale in both physics and presentation. The optional translation tremor path remains;
rotation is removed from the shared fracture. Detached/standalone crests retain their tilt controls. The
Inspector explains that distinction; the user's curve and timing asset is unchanged.

Dense dust used `URP/Particles/Lit`. A nine-particle overlap lost **0.516** mean RGB
when direct lighting disappeared, producing dark smoke. The same shared material
now uses `URP/Particles/Unlit`, preserving its existing tint, alpha texture, soft
particle fade and ordinary alpha blend. All effects referencing it receive the fix.
The lookdev builder also selects Unlit so a future rebuild cannot restore Lit.
Cosmetic chips still composite before dust; opaque world geometry still occludes it.

`Elemental > QA > Run Wave Stability PlayMode Tests` checks projected vertices over
the complete cast, plus actual URP pixels for dense dust with/without direct light
and dust/fragment/world occlusion. Baseline: **0/2 passed**, UTC `11:16:49`, saved in
`BuildReports/WaveStability/Before/`. This supersedes the earlier claim that an
unchanged mesh alone established a stable visible fracture.

### Moving crest and partition lifetime

The user clarified that rotation and literal shape substitution are separate issues.
The rotation regression does not by itself establish the cause of substitution.
An additional lifetime gap allowed a subsequent small cast into already-retreated
slots while the original outer rows were still visible. The complete wave now keeps
its cast reservation until its last row finishes; the PlayMode test explicitly tries
both a small wave and a one-column crest during that partially released state.

Production height no longer uses the topology generator's stationary middle-row
Gaussian crest. Each prepared cell reaches the charged crest height when the same
outward-moving crest reaches its fixed distance. `EarthWaveTravelSchedule` derives
arrival delays from distance and authored phases: by the next row's peak, the
preceding row has reached at least 60% of its retreat phase. Long phase durations
therefore reduce actual propagation speed; the existing speed slider is now explicitly
a maximum. All saved phase seconds and curves remain unchanged. With the saved
3 / 1 / .5 / 3 s phases, the tested short crown takes 23.1 s to propagate.
The runtime exposes actual travel time, speed and total duration for diagnostics.

The previous scoped 2/2 PlayMode run at UTC `11:28:29` verified progressing peak
locations and 712 declining-height samples behind the crest; collision-pair checks
were added afterwards and require their own fresh result below.

### Intersecting neighbouring columns

The added production pair test reproduced **0.85537 m** of convex penetration
(`WaveStabilityPlay` 1/2, UTC `11:31:18`; baseline in
`BuildReports/WaveStability/BeforeOverlapFix/`). The flat Voronoi partition was
placed using independent radial/sampled surface normals. Sharing those normals alone
did **not** fix the 0.85 m overlap. Pure 2D and cooked flat-frame collider probes both
had zero overlaps, isolating the remaining fault to runtime placement. The generic
surface-placement solver pins the lowest vertex to the requested point; that point
is the **centre** of a wave cell. A cube changing from -1 to +1 degree snapped sideways
by **0.982395 m**. `ResolveFullRisePlacement` now uses only its normal-direction
correction and preserves the cell's tangent coordinates. Its returned support point
is adjusted consistently and still has the same authored embed depth.

Production wave cells share the original
cast up and tangent frame. Their tangent positions match the original partition;
terrain/sphere sampling changes height only. Bevel vertices are also constrained
inside their original cell's side planes during preparation. A new cast is rejected
while any earlier anchored wave/crest columns remain, including partially retired
fields. Already-detached physical stones continue their normal physics lifecycle.

Focused EditMode **20/20 passed**, UTC `11:34:40`, including long/short travelling
crest timing and acute bevel containment with both polygon windings.
Final focused EditMode **21/21 passed**, UTC `11:42:19`, also verifies that switching
the lowest vertex never recentres the cell, while retaining correct ground seating.

Final production `WaveStabilityPlay` **2/2 passed**, UTC `2026-09-04T11:43:39Z`:
**21,646 collision-pair checks / 0 penetration**, **1,249,221 projected-vertex checks /
0 drift**, 7,872 immutable mesh checks, moving peak delay samples 4.079 / 11.132 /
18.039 s, 703 descending samples and successful rejection of partial-field reuse.
Contact feedback covered 93 cells with 17,032 contact events / 1,182 bursts;
`Elemental.Wave.SurfaceContact` peaked at **1.9917 ms** in this Editor run. Dense dust
lighting delta is **0**, and opaque-wall occlusion still passes. These are focused
Editor results, not standalone performance certification. Rise/retreat captures
are in `BuildReports/WaveContact/` and dense dust pixels in `BuildReports/DustCompositing/`.

Wave and crest casts now reserve their complete capacity before scheduling.
A busy pool rejects the new cast with `PoolExhausted`; it never steals live cells,
rewrites their meshes, or invalidates a grabbed piece. This keeps the existing
bounded physics budget. Capacity becomes reusable after the cells finish.

Production wave cells retain the same horizontal footprint through their depth.
The previous geological belly and tapered bottom changed the visible ground
cross-section during a slow lift even without mesh regeneration. Generic ground
stones keep their existing shape family. The wave's mesh is prepared once per cast;
only its rigid pose changes during the animation, with the existing authored curves.

## Contact effects

`EarthWaveSurfaceContactSolver` intersects the unchanged mesh with its ground plane
in local coordinates, using caller-owned buffers. The runtime emits spatial cues at
those intersections at approximately 8 Hz, staggered by stable cell ID. Eight-point
bursts mark emergence and retreat; four-point dust/chip streams follow moving
contact. No stream appears when the mesh has cleared the plane or stopped rising.
A fast retreat that crosses between samples uses the last valid contact positions.

Surface cues have a dedicated bounded queue so the ordinary 1.5 m impact merge
cannot collapse neighbouring cell contacts into one central puff. They share the
existing per-frame particle budget and use rotating fair allocation. General impact
merging and its eight-event budget are unchanged.

Terrain extraction burst increased from 64/16 to 220/56 dust/chips; anchored decor
extraction uses 192/48. A short six-point extraction stream follows the hole while
the lifted stone still intersects the surface. These cues use the larger fracture
dust layer with increased cloud size; material tint remains authoritative. Dust
and cosmetic chips retain the established compositing order.

## Matching small stones and gravity grab

Natural wall stones previously kept the much larger original structural collider.
Their new collider comes from their visible geometry. A bounded 64-point hull keeps
all six axis extrema and at most 124 triangular faces, avoiding PhysX's 255-face
limit and partial-hull fallback. The cache is prepared with the wall; no collider
mesh is generated by its collision callback.

Gravity grip now estimates occupied volume from actual collider geometry, independent
of canonical mass. Tiny clusters use a correspondingly small orbit; the profile's
orbit radius remains the upper bound. PhysX still resolves actual contact; collision
is not disabled to make the cluster look tighter.

Actual large-rock/arena splitting is documented separately in
[convex-volume fracture](CONTAINED_FRACTURE_FIX.md): children derive from the source
volume rather than inscribed generic templates.

## Verification entry points

- `Elemental > QA > Run Wave Contact EditMode Tests`: plane sections, constant
  cross-section at five depths, 96 distinct nearby contact sources within budget,
  tiny-cluster packing, material event regressions and previous wave repair tests.
- `Elemental > QA > Run Wave Contact PlayMode Tests`: long saved-profile wave,
  unchanged mesh references/vertices/generations throughout, busy cast rejection,
  contact-plane positions, ascent/retreat feedback, extraction stream, all 40
  natural wall collider bounds, production combat and gravity session regressions.
- Reports/captures: `BuildReports/WaveContactEdit.*`, `WaveContactPlay.*`, and
  `BuildReports/WaveContact/{Latest.json,Rise.png,Retreat.png}`.

Performance scope is Editor-only. Source partition preparation and wall hulls have
cold costs; the existing wave launch-time preparation spike remains a separate risk.
These focused checks do not establish whole-project or standalone performance.

Production report `WaveContactPlay.json`, UTC `2026-09-04T10:22:37Z`: **4/4 passed**.
The long-wave scenario recorded **2,784 unchanged mesh checks**, **5,037 contact
events**, **379 surface bursts** from **93 contacting cells**, and maximum contact
plane error **0.000001815 m**. The extraction rim emitted **360 dust / 90 chips**
in its 0.4-second test interval, in addition to the separate commit burst. All **40
wall stone collider bounds** match their visible stones, within the face budget.
`Elemental.Wave.SurfaceContact` peaked at **5.9642 ms** in this Editor run; that is
the aggregate marker sample, not a per-cell figure. Rise/retreat captures were reviewed.
The later cold-hull nearest-distance cache optimization is covered by the additional
complex-small-stone EditMode regression; it changes preparation cost, not the contract.

Final `WaveContactEdit.json`, UTC `2026-09-04T10:25:17Z`: **16/16 passed**, including
the complex-small-stone hull extrema and PhysX face-budget regression after that optimization.
The saved wave profile remains byte-identical to the pre-change backup.
