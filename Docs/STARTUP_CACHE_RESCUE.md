# Exact scene startup caches

## Fresh Player verification, September 5, 13:35 UTC

Current dirty `d2174ed` fixes an actual Player serialization omission: persistent
script-generated collider meshes carried `DontSaveInBuild` / `DontSaveInEditor`.
The baker and material authoring path now clear those flags on saved cache data.
A standalone-target AssetBundle control preserves all three meshes, including
their readable vertex/index arrays (**3/3**). The rebuilt immutable cache
`867eb2621fa1419990940e4b51fd43cf` contains **1939 plans**.

Fresh Windows Development build succeeded with **0 errors**. Player evidence
`BuildReports/StartupPlayer/20260905-133538-792` confirms **1939 baked plans,
0 misses, 7368/7368 collider cooks**, exact baked planet and completed scatter
with zero rejected candidates. Scene readiness took **1823.27 ms**, cache loading
**15.23 ms**, background cooking **1254.59 ms**, peak main-thread cook poll
**0.20 ms**. App uptime at playable readiness was **5649.99 ms**. These are fresh
process measurements, not a claim that filesystem/GPU caches were cold.

The playable camera PNG was visually reviewed and is correctly rendered. This
run still **fails the loading-cover image gate**: IMGUI was absent from the URP
offscreen camera request, giving an almost-black Bootstrap PNG. A real transient
camera-rendered cover/status has been implemented; a new built Player must prove
that path before overall bootstrap visual acceptance. The measured largest frame
remains **2750.72 ms** during covered startup; the stall is covered, not eliminated.

The following sections retain the earlier investigation history.

Implementation: dirty working tree on `d2174ed`, updated 2026-09-05. The scene caches
have been baked, converted to binary and wired. Focused startup verification passed
**3/3 EditMode and 3/3 PlayMode** in Unity 6000.5.7f1 (timestamps below). A clean,
explicit-menu current-code A/B also completed after the replay-prone entry script was
retired. It establishes an Editor comparison for that revision, not an untouched
historical baseline or a cold Development Player result. A subsequent startup change
now schedules unique convex cooks through `IJobParallelFor` and extends persistent
coverage to debris/hero-fragment source libraries. That change still requires compile,
rebake, focused tests and a fresh menu A/B before its performance is accepted.

## Observed cause and measurement boundary

The saved 55.1 m planet uses resolution 16, 1 m cells and a one-chunk-per-frame
budget. The conservative shell contains 232 chunks. Its lexicographic queue places
the four chunks under the arena at ranks 114, 115, 142 and 143. The imported arena
is immediately available, while the runtime ground is generated later. This is a
code/parameter-derived delay, not a profiler measurement.

The other candidate is `EarthArenaStructure.InitializeRuntime` preparing recursive
convex partitions and render bevels for every distinct architecture piece. The
existing partition cache is owner-local and discarded when the world ends. Pool
warmup, primary architecture bevels, chunk GameObject construction and collider
cooking remain separate costs and must be measured.

## Derived data, never terrain authority

`PlanetBaseMeshCache` stores the exact output of the existing SDF mesher: meshes,
coordinates and canonical chunk hashes. Radius, seed, resolution, cell size, noise,
schema, field and mesher revisions form its runtime identity. Field/mesher revisions
must be bumped when their runtime interpretation changes. The editor output key also
includes source dependency hashes, so rebaking changed generator code produces a
different immutable revision. Materials and scene transforms are retained.

The runtime verifies the complete expected shell before hydrating any chunk. A valid
cache is available from Awake, with no incremental missing terrain. Borrowed meshes
are never cleared or destroyed by a planet. On edit the target slot receives a new
owned mesh; transaction swaps carry ownership flags with both active and staging
slots. A second edit therefore cannot overwrite the old baked base now occupying
the staging slot. Existing atomic visual/collider commit and SDF edit authority remain.

Unassigned, stale, missing, duplicate or extra entries reject the cache. Stale and
invalid assigned caches report an actionable warning; an unassigned cache reports
`BaseCacheStatus` and uses the intentional uncached path. Canonical budgeted meshing
remains available. The readiness boundary covers this path as well as cache hits.

## Convex cache and first interaction

`EarthConvexFractureCacheAsset` stores the existing exact source/count child plans,
including source vertices signature, child collider/render meshes, centers and volumes.
The editor prepares both top-level 3/4-way variants and the next generation selected
by the current break policy and source scale. For persistent debris and hero-fragment
libraries it prepares both supported 3/4-way descendants because those pooled sources
can be resized before impact. Source signatures and cache revision reject stale plans.
Borrowed generated assets are not destroyed by runtime cache disposal.

Coverage includes authored convex MeshColliders belonging to `EarthArenaPiece` and
`EarthDestructibleDecorRock`, plus readable persistent mesh references configured on
`EarthRockDebrisPool` and every `EarthFragmentPool` in the active scene. Unreadable or
nonpersistent configured pool sources fail the bake with an actionable error instead
of silently preserving a first-interaction preparation path. Runtime-generated legacy
sources/primitives retain the cold fallback; `BakedFracturePlanMissCount` exposes every
fallback taken after a valid baked cache was accepted. Primary architecture render
bevel creation is not changed.

Saved geometry is not treated as proof that native cooked collision data survived an
Editor restart/build. `EarthRockDebrisPool` schedules every distinct borrowed child
collider ID once through a non-Burst `IJobParallelFor`. `Update` only polls the handle
and calls `Complete` after `IsCompleted`; it does not synchronously complete the batch
on its scheduling frame. Existing default cooking options and convex binding remain
identical. `PhysicsPrepared` requires pool Awake/Start and completion of the background
batch, so the loading cover remains until all scheduled cooks finish and first
interaction does not intentionally inherit that work. Counters distinguish scheduled,
completed and pending meshes, background wall time, main-thread poll peak and cache
plan misses.

The project currently has `bakeCollisionMeshes: 0`; this change does not silently
alter global build settings. Unity documents that Physics.BakeMesh stores data on the
mesh instance and that sharing requires unchanged geometry, matching cooking options
and compatible transforms. Saved collider prefab/prebake settings are an optional
later measured build optimization, not a replacement for the explicit runtime gate.
[Unity BakeMesh](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics.BakeMesh.html),
[Unity collider preparation](https://docs.unity3d.com/6000.0/Documentation/Manual/physics-optimization-cpu-collider-types.html).

## Loading boundary

`EarthSceneReadinessGate` owns explicit planet, debris pool and input/bot references.
At the start it preserves time scale and component enabled states, pauses simulation
time and those command producers, and draws a simple opaque loading cover. Terrain
Update and bounded physics preparation continue using realtime, so pausing physics
does not stop loading. The gate releases only after `GeometryReady && PhysicsPrepared`
and a physics transform sync, restoring the previous values. No arbitrary delay or
approximate sphere is used. Missing dependencies or timeout retain the cover and
emit an actionable error. Destruction/unload restores the owned pause.

This gate is intended for the one active gameplay world. Simultaneous independent
scene gates are not a supported additive multi-world loading protocol.

## Authoring and safe publication

Invoke `Elemental.Authoring.Editor.StartupCacheBaker.BakeCurrentScene()` outside Play,
or menu **Elemental / World / Bake Startup Caches In Current Scene**. It targets the
currently open scene, requires exactly one planet and debris pool, and does not rebuild
the gameplay scene. Both caches finish before component references are published.
The baker adds/configures one readiness gate, connects input/bot components and saves
the current scene, preserving authored graphics/gameplay settings.

Output is under `Assets/Elemental/Content/StartupCaches`, keyed by canonical data,
source mesh identity/geometry/scale, policy and generator dependencies. An unchanged
bake reuses its validated revision. Writes use a temporary generated asset and move
only after completion; failed temporary outputs are deleted. Previous valid revisions
remain for rollback and are not recursively removed. Complete planet geometry/hash
validation runs in the editor, avoiding an expensive runtime re-bake.

Both generated cache types prefer binary serialization. The baker explicitly makes
the cache ScriptableObject the main asset, so its preference applies to the entire
file including mesh subassets. This reduces text/hex storage and parsing overhead
without changing geometry. Generated binary revisions are rebaked rather than merged.
The first YAML bake contained 1,403 convex plans, 5,436 children and exactly 10,872
meshes (one collider plus one render mesh per child): 399,454,305 bytes on disk and
173,291,688 bytes of vertex payload. The planet contained 232 meshes and
24,726,816 vertex-payload bytes in a 54,398,687-byte YAML asset. These are read-only
file counts before conversion, not runtime memory or measured loading improvements.
[Unity binary serialization](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PreferBinarySerialization.html).

The targeted binary resave produced these actual file sizes, confirmed on disk:

| Generated revision | Binary bytes | Previous YAML bytes |
| --- | ---: | ---: |
| `EarthCoreSliceConvexFracture_747750338d7d43e5cc0b4f7b74af58bd.asset` | 186,450,284 | 399,454,305 |
| `EarthCoreSliceConvexFracture_5e4f65fe0650e5746f960d747374f21d.asset` (post-reimport, currently wired before the coverage rebake) | 186,673,608 | n/a |
| `EarthCoreSlicePlanetBase_a3565256c82028c4ece51550448b8615.asset` | 26,927,020 | 54,398,687 |

This is storage evidence only. Mesh counts and geometry quality were retained;
runtime memory, parsing time and first-hit cost were not measured by this resave.

## Verified focused evidence and remaining checks

Actual report evidence on the dirty working tree:

| Report | UTC completed | Result | Suite duration |
| --- | --- | --- | ---: |
| `BuildReports/StartupCacheEdit.json` | 2026-09-04 16:16:59.3467685 | 3/3 passed, 0 failed | 0.0499866 s |
| `BuildReports/StartupCachePlay.json` | 2026-09-04 16:46:45.0931975 | 3/3 passed, 0 failed | 1.8729399 s |

Suite duration is test-runner time, **not production startup latency**. These results
cover the focused cache fixtures, not the later Blender reexport, production A/B,
whole-scene rendering or a Development Player.

Launch `Elemental.Tests.EditMode.StartupCacheTestLauncher.RunEdit()` and `.RunPlay()`.
Reports: `BuildReports/StartupCacheEdit.json` and `StartupCachePlay.json`.
Focused coverage includes canonical signature rejection, exact borrowed convex plans,
source change rejection, unique native preparation, first-frame full terrain, repeated
and overlapping transaction edits, shared two-instance ownership, unload safety and
cache-miss readiness with command/time restoration. Also rerun existing voxel runtime
and authored fracture/grab/repair regressions after the scene is wired.

Two additional EditMode cases are staged for the background-cooking revision: authored
debris/hero source deduplication and complete 3/4 descendant-plan binding with zero cold
misses. They are not part of the 3/3 historical result above; the next accepted report
must contain 5/5 and scheduled/completed cooking counts must match.

Record scene activation/Awake CPU, request-to-first-visible-frame, geometry-ready time,
physics-ready time, peak/p95/p99 loading and first-interaction frames, GC, mesh counts,
memory and generated asset size. Existing and new markers:

- `Elemental.Earth.Fracture.PrepareConvexCells`
- `Elemental.Earth.Fracture.StartupCooking`
- `Elemental.Voxel.BaseCache.Hydrate`
- `Elemental.Voxel.RenderQueue` / `Elemental.Voxel.ColliderQueue`

Null versus assigned cache references provide a **current-code uncached/cached A/B**,
not the untouched historical baseline: staging allocation timing also changed.
Keep capacities, materials, camera, bots and render settings identical. Restart the
Editor/process for cold runs so native cooked data is not accidentally warmed; report
warm reload separately. A Development Player run is needed before claiming player
startup/persistence performance. Hard behavioral gates are zero visible arena-only
frames, no new covered-source partition/bevel preparation after readiness, and zero
shared base mesh mutation.

`ProductionStartupSample.RunProductionStartupSample(label)` is used by the owning
integration task. Its report distinguishes Editor enter-to-ready
(including reload/activation) from gate Awake-to-ready. Its per-frame sampling begins
after EnteredPlayMode and cannot separately measure the initial Awake stall. No Editor
sample is a substitute for a cold Development Player profile.

### Clean explicit-menu current-code A/B (before background cooking change)

Both runs used the saved clean `EarthCoreSlice` through the dedicated cached/uncached
menu entries, without opening or saving the scene in the command. The uncached run
records exact-reference restoration, unchanged scene SHA-256 and a clean scene after
restore. Reports:

- `BuildReports/EnvironmentAnimationRescue/cached-menu-20260904-175541-646-8ba106/StartupSample.json`
- `BuildReports/EnvironmentAnimationRescue/uncached-menu-20260904-175656-919-80665c/StartupSample.json`

| Metric | Cached | Uncached |
| --- | ---: | ---: |
| Editor enter request to readiness | 49,427.69 ms | 83,215.08 ms |
| Readiness gate Awake to ready | 17,789.62 ms | 45,774.51 ms |
| Exact base hydration | 47.20 ms | 0 ms |
| Baked plans / runtime-prepared plans | 1,403 / 284 | 0 / 1,687 |
| Prepared child collider meshes | 5,436 | 0 |
| Primary / secondary real impact calls | 10.8633 / 11.7264 ms | 7.4716 / 9.8998 ms |
| New plans and measured managed bytes in both impacts | 0 / 0 | 0 / 0 |

For this current-code Editor pair, assigned caches reduced gate time by 27,984.90 ms
(61.1%) and enter-to-ready by 33,787.39 ms (40.6%). This establishes the benefit of
exact cached geometry for that revision. Absolute cached startup remained unacceptable:
the gate still waited 17.79 s while 5,436 unique child meshes were cooked in sequential
main-thread slices, and 284 pool-source plans were still prepared cold. Those measured
residuals are the reason for the staged background-cooking and pool-coverage change.
Run both menu entries again after compile and rebake; do not apply the percentages above
to the new implementation until that pair completes.

### Initial production Editor samples: provisional due to entry-command replay risk

**Protocol limitation discovered after collection:** one MCP RunCommand containing
scene-open/save-copy work repeated after `EarthArmorPiece` warnings and produced
14 snapshot files roughly three seconds apart. This directly establishes command
replay in that tool path, not the number of repetitions during either earlier sample.
Because the old cached/uncached entry scripts also included scene opening and warnings,
their startup intervals may be contaminated. Preserve both reports as diagnostics;
do not calculate or claim a speedup from them. The replacement harness uses explicit
menu entries, unique timestamp labels, a prevalidated clean scene and no scene open/save.
Its uncached variant temporarily changes only cache fields and restores exact prior
references on EnteredEditMode, recording file-hash/dirty-state checks without clearing
dirty state. The clean results are recorded in the preceding section; the older reports
below remain diagnostics only.

`BuildReports/EnvironmentAnimationRescue/cached-01/StartupSample.json` reports
`Complete`, recorded UTC 2026-09-04 17:30:21.0220995. Both actual Game-view captures
were written. This was an existing Editor process, not a measured cold Player launch.

| Metric | Measured result |
| --- | ---: |
| Editor enter request boundary to observed readiness | 50,552.12 ms |
| Readiness gate Awake to ready | 17,978.08 ms |
| Exact base hydration | 166.55 ms |
| Largest budgeted native cooking slice | 3.1331 ms |
| Baked plans / cooked child collider meshes | 1,403 / 5,436 |
| Additional runtime prepared plans at ready | 284 |
| Exact terrain chunks; pending render/collider queues | 232; 0 / 0 |
| Real primary impact call | 10.3484 ms; 0 measured managed bytes; 0 new plans |
| Real secondary fracture call | 13.0943 ms; 0 measured managed bytes; 0 new plans |
| Observed impact frames, maximum / p95 | 45 frames; 45.2403 / 43.1728 ms |

Both calls were accepted on `OuterArch_01_INTACT`, targeting the actual mesh center
of `FR_outer_arch_01_P009` (index 8). The primary impact released four cells; the
secondary fractured one into an expected three physical children. Zero plan-count
delta proves no new partition plan was generated by these two calls; it is not a
separate measurement of every native physics operation or every other fracture case.

The startup-frame list begins with **37,470.13 ms**, then **8,917.58 ms**. All 66
reported deltas sum to 50,337.82 ms. These are `Time.unscaledDeltaTime` observations,
not measured CPU scopes: the first callback reads an already accumulated engine
frame interval that can include the Editor transition before the callback exists.
The first interval exceeds the entire 17,978.08 ms gate lifetime, so it cannot be
attributed wholly to terrain hydration or budgeted cooking within that gate.
Enter-to-ready minus gate duration is approximately 32,574.04 ms; that difference
includes work before gate Awake and the small delay until the harness observes ready.
Do not call the first 37 s observation a normal gameplay frame or a measured 37 s
mesh-cooking operation. Its inclusion in startup maximum/p95 is retained transparently.

The source still performs pool warmup, cold preparation for runtime-generated sources,
primary bevel work and other scene initialization. The 284 additional prepared plans
show remaining cold work, but this sample does not time or identify their owners.
Asset loading/deserialization, Editor reload, EAMM initialization and these runtime
paths remain candidates until a Profiler capture or exported Editor timing log
separates them. Neither the 166.55 ms hydration nor 3.13 ms cooking-slice maximum
explains or assigns the 37 s interval. The subsequent `uncached-01` report, recorded
17:38:04 UTC, also completed: 78,472.75 ms enter-to-ready, 46,062.09 ms gate time,
zero baked plans and 1,687 prepared runtime plans. It shares the entry-command
limitation above and is not a valid timing baseline for claiming an improvement.
Repeat both runs through the same explicit menu protocol and settings; cold-process
and Development Player measurements remain separate gates.

## Freshness after the Blender normals/bevel reexport

The subsequent export includes bevel geometry on loose stones that previously lacked
it. `OuterStoneRingImporter` assigns each loose stone's imported `MeshFilter.sharedMesh`
to its `MeshCollider.sharedMesh`. A geometry change in that export therefore can
change the fracture source, not only its rendered normals. Complete reimport/rebind
before invoking `StartupCacheBaker.BakeCurrentScene()` again. The exact planet revision
can be reused if its own signature is unchanged; the baker will validate it.

The convex runtime signature hashes vertex coordinates in array order. A normals-only
change with identical source reference and vertex array can safely reuse the partition
plans, whose solver reads positions only. FBX normal splitting/reordering can change
that array even if the apparent surface is unchanged. Added bevel geometry changes
the positions/count; changed local file IDs change the source identity. The baker key
includes GUID/local ID, vertex signature, transform determinant and policy/code
dependencies, so rebaking selects a new revision when those inputs differ.

A loaded cache does not prove coverage of the newly imported scene. Changed existing
sources produce rejected-plan warnings; a newly referenced source can instead be
absent while every old plan still validates. Old descendant plans can also remain
valid and consume cooking work even when their top-level source became stale.
`PrepareFracture` then prepares missing current-source plans synchronously during
structure initialization/decor Start, before gameplay readiness. The gate continues
to protect ground/physics availability, but that cold fallback can restore a loading
hitch. `PhysicsPrepared` and zero rejected plans alone are not proof of complete
current-source cache coverage or improved startup performance. Rebake after the final
source refresh and verify fresh-source preparation counters and the first-impact
preparation delta before accepting the optimization.

## CPU authored-cell picking workload to measure

`EarthArenaMeshPicking` now copies each authored source's vertices and triangle indices
once at structure initialization. This adds managed startup memory of approximately
`12 × vertex count + 4 × index count` bytes per cell, plus array/object overhead.
Each selection transforms triangle corners and computes point-to-triangle distances
for every available cell. Signed solid-angle containment adds square roots/atan2 only
when the query lies inside that cell's local bounds. No per-query mesh-array reads or
managed allocations are intended; the dedicated EditMode allocation test still needs
its own result and is not included in the startup evidence above.

This resolves dormant-collider/pivot ambiguity without depending on PhysX activation,
but it is a new CPU cost. One selection is linear in the available triangle count;
`ReleaseNearestPieces` repeats it for multiple requested cells, so a large disassembly
can approach quadratic cell work. The seven columns have 85 cells in total; their
production triangle counts and query timings remain unmeasured. Existing repair-cycle
timing starts after picking and therefore does not measure this cost. Include complete
pluck/impact/disassembly calls in the first-interaction profile, especially the larger
arena fracture sets. If those measurements expose a regression, add cached world-bounds
lower-bound rejection or a triangle acceleration structure while retaining exact
geometry and all 85 exact-index assertions; do not reduce geometry quality.

## Revision-3 cached sample and scatter coverage correction (2026-09-05)

The first saved revision-3 sample completed at
`BuildReports/EnvironmentAnimationRescue/cached-menu-20260905-073945-560-7ffa45/StartupSample.json`.
It used the exact planet cache and a 1,747-plan convex cache baked from 196 authoritative
arena/decor sources plus 12 debris/hero pool sources. This is a current Editor sample,
not a cold Development Player result and not a new production A/B claim.

| Metric | Revision-3 cached result |
| --- | ---: |
| Editor enter request to readiness | 13,057.19 ms |
| Readiness gate Awake to ready | 5,450.42 ms |
| Pre-gate interval (`enter - gate`) | 7,606.77 ms |
| Exact planet hydration | 58.36 ms |
| Baked-cache load | 15.48 ms |
| Background physics-cooking wall time | 3,638.57 ms |
| Largest cooking poll | 0.4324 ms |
| Baked plans / runtime-prepared misses | 1,747 / 45 |
| Cooked / scheduled collider meshes | 6,696 / 6,696 |
| Primary / secondary real impact calls | 9.4500 / 9.2013 ms |
| New plans and measured managed bytes in both impacts | 0 / 0 |
| Observed startup frames, maximum / second | 2; 10,811.36 / 1,972.49 ms |

The startup-only aggregate scopes located the synchronous scene work:

| Owner | Inclusive time | Narrow child time/count |
| --- | ---: | ---: |
| 15 arena structures | 442.77 ms | bevel 403.98 ms / 175; picking 3.25 ms / 175 |
| First baked wall shell | 598.51 ms | hard-shaded/beveled visuals 26.77 ms / 40 |
| Six prepared platforms | 15.44 ms | runtime mesh buffers 2.04 ms / 12 calls |
| 96-column wave pool | 23.23 ms | mesh library 4.65 ms; columns 17.05 ms |
| 72-piece debris pool | 98.51 ms | one Awake |
| 32-piece hero-fragment pool | 21.88 ms | one Awake |

The arena total is overwhelmingly its exact 175-piece bevel pass. Most wall time lies
outside the measured hard-shading/bevel child scope, in natural-fracture fitting,
matched collider construction, GameObject/component creation and bond joints. These
figures do not justify reducing pool capacities or moving preparation to first use.
They also show why a broad asynchronous pool rewrite is not the next correction: the
remaining accepted cache work is dominated by background cooking, while the same-scene
Editor transition still contributes a 7.61 s pre-gate interval that no scene `Awake`
scope can cover. The two very long observed startup deltas still mean the loading cover
does not get a smooth sequence of rendered frames when entering this heavy scene
directly.

The 45 misses were resolved to concrete runtime sources. `EarthPlanetRockScatter`
starts once planet/surface/gravity are ready, without waiting for the debris cache or
the readiness gate. It creates four physical rocks per frame. With saved seed
`20260904`, its first eight large-rock choices are collider indices
`[9, 8, 6, 11, 6, 6, 7, 6]`: five distinct meshes over the two observed loading
frames. A new source preparation requests two top plans and one descendant plan for
each of the three- and four-piece top children, nine plans total. Five sources times
nine plans exactly accounts for all 45 reported misses. The meshes are:

- `Rock6Collider.asset`
- `Rock7Collider.asset`
- `Rock8Collider.asset`
- `Rock9Collider.asset`
- `Rock11Collider.asset`

The full scatter collider library is `Rock0Collider.asset` through
`Rock11Collider.asset` under
`Assets/Elemental/Content/Generated/MaterialPass`. All twelve must be cached: the
other seven can be selected after the gate opens and would otherwise move cold
partition/bevel/cooking work into early gameplay.

The baker now includes the scatter component's deduplicated collider library alongside
debris and hero sources. The focused EditMode suite passed 6/6 at
2026-09-05 07:50:25 UTC. The resulting transaction committed
`EarthCoreSliceConvexFracture_4ce5e6516f26d8e0196f09b5313f4a6b.asset`
(247,018,340 bytes), whose meta GUID is
`36b4d260dafafc64192878bf72947003`; saved `EarthCoreSlice.unity` references that GUID.
Read-only scene enumeration confirms 12 existing debris/hero sources plus 12 distinct
scatter collider assets, for 24 persistent pool/scatter sources. The staging asset was
removed after commit.

A subsequent scoped animation PlayMode report,
`BuildReports/AnimationPunchContinuityPlay.xml` (2026-09-05 07:56:08 UTC), loaded the
new saved cache and logged 1,939 baked plans, zero misses and 7,368/7,368 cooked meshes.
Its first readiness log was 2,271.66 ms with 1,408.24 ms background cooking; a later
scene lifetime logged 1,210.77 ms with 1,129.46 ms background cooking, followed by a
complete scatter pass (24/24 large, 160/160 medium and 128/128 clusters). This proves
the new asset is accepted and has no miss at those readiness boundaries. It is evidence
from a focused animation test inside an existing Editor process, so its readiness times
are not a replacement for the dedicated production startup sample and do not prove the
post-readiness miss counter stayed unchanged through the entire scatter pass.

### Dedicated cached acceptance after scatter coverage

`BuildReports/EnvironmentAnimationRescue/cached-menu-20260905-080142-723-cb2a2d/StartupSample.json`
completed through the clean explicit cached menu with the final counters enabled:

| Metric | Final cached result |
| --- | ---: |
| Editor enter request to readiness | 12,749.85 ms |
| Readiness gate Awake to ready | 5,192.24 ms |
| Pre-gate interval (`enter - gate`) | 7,557.61 ms |
| Exact planet hydration / baked-cache load | 58.64 / 17.63 ms |
| Background physics-cooking wall time | 3,247.48 ms |
| Largest cooking poll | 0.3129 ms |
| Baked plans | 1,939 |
| Prepared plans at ready / after sample | 0 / 0 |
| Baked-plan misses at ready / after sample | 0 / 0 |
| Cooked / scheduled collider meshes | 7,368 / 7,368 |
| Primary / secondary real impact calls | 9.6627 / 10.7354 ms |
| New plans and measured managed bytes in both impacts | 0 / 0 |
| Observed impact maximum / p95 | 23.4598 / 16.0727 ms |
| Observed startup frames, maximum / second | 2; 10,668.65 / 1,798.72 ms |

The exact base was present with 232 runtime chunks and zero pending render/collider
work. Both real calls were accepted on `OuterArch_01_INTACT`; the first released four
pieces and the second shattered one into the expected three physical children. The
unchanged zero preparation/miss counters after the 45-frame post-ready window prove
that staged scatter did not reintroduce cold partition work after controls resumed.
This closes the cache-coverage and first-interaction acceptance criteria for the saved
1,939-plan asset.

Current synchronous scopes were arena initialization 345.27 ms (311.32 ms in 175
bevels), wall Awake 602.72 ms, six-platform pool Awake 15.62 ms, 96-column wave pool
Awake 29.00 ms, debris pool Awake 85.68 ms and hero-fragment pool Awake 19.92 ms.
The cache correction therefore removed the measured cold misses without shifting the
hitch into the two tested impacts or reducing any pool/geometry setting.

This result accepts cache correctness, not total startup performance. Direct Editor
entry into the heavy scene still took 12.75 s and exposed only two observed loading
frames, including a 10.67 s first interval. About 7.56 s occurred before the readiness
gate's `Awake`, where scene code cannot draw or time its loading cover; the 5.19 s gate
interval was led by 3.25 s of background physics cooking plus the measured synchronous
scene setup. The comparable current-code uncached menu run is recorded below, but a cold
Development Player capture remains required before claiming a shipping startup
speedup. Further work on the visible hard stall should target direct-scene
deserialization/activation and staged wall/arena preparation behind an already rendered
bootstrap cover, based on a Player Timeline capture. Do not undo the complete cache or
defer its work to first interaction.

### Paired uncached diagnostic and scene-local bevel normalization

`BuildReports/EnvironmentAnimationRescue/uncached-menu-20260905-083115-129-d6d28f/StartupSample.json`
completed through the explicit uncached menu in the same Editor process as the final
cached run. It reported 43,744.16 ms from enter request to readiness, 35,174.97 ms
inside the gate and an 8,569.19 ms pre-gate interval. Arena initialization alone was
20,419.35 ms and 1,689 fracture plans were prepared by readiness and remained 1,689
after the sample. The two real impacts took 9.07/9.70 ms with zero measured managed
allocation and zero further preparation. Cache references were restored, the saved
scene bytes were unchanged and the scene returned clean. Against the paired cached
12,749.85 ms result, this is strong same-process Editor evidence that the cache removes
the canonical cold fracture work. It remains an Editor diagnostic rather than a cold
Player or cold-disk performance claim.

A read-only YAML inventory then identified a separate pre-`Awake` cost. The
43,189,164-byte `EarthCoreSlice.unity` contained 105 inline `Mesh` objects totalling
37,249,783 bytes. Eighty-five objects named `* Beveled Render` occupied 37,121,000
bytes. They were generated when the outer-ring importer called
`EarthArenaStructure.Configure` in Edit Mode, assigned to the dormant fracture-piece
filters, and saved into the scene. Runtime `Awake` did not reuse them: it deterministically
rebuilt and reassigned every bevel from the persistent fracture asset. The inline copy
therefore paid scene parsing/deserialization and memory cost without changing runtime
geometry or first-interaction physics.

The first normalization transaction completed at 2026-09-05 08:45:18 UTC. It required
a clean active scene, preflighted all 85 replacement bindings and refused any bevel
shared by an unrelated filter, collider or skinned renderer. It wrote a timestamped
43 MB scene backup before replacing the dormant filters with their persistent fracture
asset meshes. The saved scene is now 6,072,112 bytes, a 37,117,052-byte (85.94%)
reduction. `EarthArenaStructure` now creates the identical presentation bevel only in
Play Mode, preventing an importer or later authoring pass from serializing the duplicate
again.

This size reduction is provisional startup evidence until the focused persistent-mesh
EditMode test, the full outer-ring PlayMode suite and a fresh cached startup sample
finish. Required acceptance is unchanged: 85 exact piece mappings, materials, imported
normals, fracture/repair/picking and physical collision behavior must remain green.
No timing improvement is claimed from file size alone. A Bootstrap-first additive
loading cover and fresh Development Player harness are staged separately and are not
yet part of the production scene order.

The first post-normalization cached sample,
`BuildReports/EnvironmentAnimationRescue/cached-menu-20260905-085934-789-edd8b0/StartupSample.json`,
completed at 2026-09-05 08:59:45 UTC. Enter-to-ready fell from 12,749.85 to
11,427.65 ms; pre-gate time fell from 7,557.61 to 6,585.86 ms, and gate time was
4,841.79 ms. The largest observed startup interval fell from 10,668.65 to 9,455.54 ms.
Background cooking remained the dominant gate task at 3,034.67 ms. All 1,939 plans
were accepted with zero misses/preparations both at readiness and after the sample;
7,368/7,368 collider meshes cooked, and the real impacts remained 9.5054/8.7377 ms
with zero measured allocation or preparation. This same-process Editor comparison is
consistent with removing duplicate deserialization, while the remaining 9.46 s hard
interval still requires the staged Bootstrap cover and a fresh Development Player
capture. It is not a cold-Player claim.
