# Wall contacts and the two legacy push boulders

## Requested behavior

Real thrown stones should progressively crack and eject snug wall cells while
source-attached cells remain strongest. Wall bevels should have a consistent
physical width and use the sandstone shader's actual vertex-attribute contract.
The two large push boulders should obey the same gravity, mass and fracture rules
as other loose stones.

## Implemented contracts

- `EarthBondImpact.LocalMetricScale` defaults to unit scale for existing consumers.
  Bond distance and inverse-transpose normal evaluation use this metric. A thin
  wall's normalized thickness is no longer treated as equal to its full width.
  Nonfinite/nonpositive metrics reject the impact without changing the graph.
- `EarthWall` uses a 1.6 m contact radius and strength based on the smaller adjacent
  cell's mass, with ordinary damage capped at 0.45 per accepted contact. Source
  anchors retain at least 3x strength and cap damage at 0.16. These values live in
  `EarthWallProfile`, including local strength coefficient 0.8.
- Newly unsupported, unheld cells share 55% of the accepted impulse proportional
  to their total released mass, capped at 5 m/s. The transfer happens after the
  support transition and only once; remaining supported cells stay rigid.
- `ApplyRockContact` deduplicates the same Rigidbody during a 60 ms contact window
  in bounded storage. Intact/fragmented wall callbacks and `MagicExecutor`'s
  collision/sweep callbacks use that same path. The Rigidbody reference is only
  a local physics-callback deduplication key; canonical wall/piece IDs are unchanged.
- Intact walls previously ignored every native collision whose other object was
  not another wall. External dynamic stone contacts now reach wall damage too.
- Wall visuals are regenerated from the same canonical cell mesh using actual
  dimensions and a 15 mm bevel, with restrained deterministic variation. The final
  geometry is clipped inside its collider and normals rebuilt afterward. The
  intact mesh still combines those exact cell meshes. Per-wall generated meshes
  are replaced on dimension change and reused for the same dimensions; storage
  is bounded by the wall pool. Cold work has profiler marker
  `Elemental.Earth.Wall.MetricVisuals`; no new steady-update allocations were added.
- Newly generated wall bevel faces now carry alpha 1.0, which activates the
  sandstone shader's bevel mask; the old builder alpha 0.1 disabled it. The shared
  builder's optional parameter defaults to its old value for other rock families.
  Existing exterior/fresh-cut color channels and shared material identity remain.

## Exact two-object migration

Menu: **Tools > Elemental > Repair Two Push Boulders**.

Entrypoint: `Elemental.Authoring.Editor.EarthPushBoulderMigration.RepairShippingBoulders`.

The idempotent editor repair opens the shipping scene if needed and changes only
`Magic Push Boulders/Light Push Boulder` and `Magic Push Boulders/Heavy Push Boulder`.
It binds the scene gravity world and debris pool, assigns stable IDs `0xD3B00001`
and `0xD3B00002`, derives mass from the existing collider through the shared policy,
adds `EarthDestructibleDecorRock(initiallyAnchored: false)`, and removes the generic
duplicate `PhysicalImpactTarget`. Existing mesh, collider and placement are retained.
`M3EarthCoreSetup.CreatePushBoulder` now creates the same configuration in future
authoring runs. No broad scene rebuild is required.

The previous serialized bodies had gravity disabled and null gravity-world
references, and lacked the destructible owner. Their masses were fixed 55/320
values instead of the current common mass policy.

## Verification and remaining evidence

- EditMode: `Elemental.Tests.EditMode.EarthBondDamageSolverTests` adds metric
  front-face reach/sideways exclusion and invalid-metric rejection.
- EditMode: `Elemental.Tests.EditMode.EarthStoneBevelProfileTests` adds narrow/wide
  metric bevel size, containment and normal validity checks.
- PlayMode: `Elemental.Tests.PlayMode.EarthWallStoneContactTests` includes three
  actual 40 kg / 8 m/s Rigidbody stone contacts, per-contact deduplication,
  progressive fracture, forward ejection momentum and intact source anchors.
  Its second case verifies both repaired boulders' gravity, shared mass and fracture.
- Existing `Elemental.Tests.PlayMode.EarthBakedFractureRuntimeTests` retains the
  before/after narrow/wide silhouette comparison, full cell-volume checks and
  repeated-hit source-support checks.
- AlphaWallsRhythmPlay at 2026-09-06 16:01:04 UTC passed all four wall/boulder
  cases against the installed shared mass policy. Its separate locomotion case
  was still failing and is tracked in AlphaCadencePlay, not counted as a wall failure.
- AlphaPhysicsRhythmEdit passed 41/41 at 16:23:28 UTC, including bond/bevel checks.
- The coordinator ran the targeted migration and saved EarthCoreSlice; the arena
  generator was not run. Narrow/wide cracked captures were inspected in
  BuildReports/WallSilhouette; normals/facets and contained silhouette remain visible.
- No isolated wall CPU/GC profiling claim is made from these correctness tests.
