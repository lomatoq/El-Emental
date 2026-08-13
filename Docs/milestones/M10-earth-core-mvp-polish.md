# M10 Earth Core MVP Polish

Status: in progress  
Source brief: `El-Emental_Codex_Execution_Brief_BE.md` (2026-08-13)

## Task 0 — baseline and immediate visual rescue

Status: complete

### Runtime changes

- The default `EarthWallProfile` uses `automaticCrackDelaySeconds = 0`; zero is an explicit disabled state and an undamaged wall remains intact.
- Repairable structural pieces use `shrinkDetachedStructuralPieces = false`. Timed shrink remains available only as an explicit legacy cleanup policy; gameplay/cosmetic debris keep their separate dynamic shrink lifecycle.
- `EarthRockDebrisPool` creates convex irregular mesh bodies directly instead of first creating a sphere primitive. The production scene distributes the four deterministic beveled/chunky mesh variants across the pool.
- The standard framing moved from distance/height `10.5 / 6.8 m` to `6.35 / 2.35 m`; focus height is `1.05 m`, look-ahead is `3.2 m`, and shoulder offset is `0.82 m`.
- The planet/base earth palette is muted from `(0.58, 0.34, 0.18)` to `(0.42, 0.285, 0.19)`, earth emission is reduced, dust is less saturated, and bright sparks are reserved as a narrow accent.
- Release builds hide the IMGUI bend-debug launcher and all of its world-space diagnostic rays. Development builds keep the collapsed launcher available.
- Existing-earth acquisition ignores every collider under the caster Rigidbody hierarchy, so a closer camera cannot select the active-ragdoll chest instead of the aimed stone.

### Reproducible lab

- `Elemental/Setup/Create Earth Polish Lab` rebuilds the M3 scene and creates `Assets/Elemental/Content/Scenes/EarthPolishLab.unity`.
- The lab is present in Editor Build Settings but disabled, so it cannot become a shipping entry point accidentally.
- The Windows test wrapper now waits for Unity's GUI-subsystem process and propagates its real exit code even when the project path contains spaces.

### Evidence

- Baseline before changes: EditMode `113/113` in `0.516 s`; PlayMode `56/56` in `74.065 s`.
- After Task 0: EditMode `113/113` in `0.483 s`; PlayMode `58/58` in `74.064 s`.
- Capability matrix: NativeHigh/NativeLow/WebLab all pass 216,000 ticks with `0 B` managed allocation and no canonical rule change (`BuildReports/PerformanceMatrix.json`).
- Windows Development build: `170,713,574` bytes in `53.775 s`, `0` warnings/errors (`BuildReports/NativeWindows.json`).
- D3D11 standalone wall capture exited `0`; voxel startup queue peak was `71.18 ms`, with `0` pending work at capture (`BuildReports/EarthPolishLab-after.png`).

The `71.18 ms` value is a cold-start queue peak, not shipping frame-time P95. Interactive CPU/GPU profiling remains required in the hardening task.

## Task 1 — pure fracture graph

Status: complete

- Added stable structure/piece/bond IDs, baked definition contracts and explicit canonical phases in `Elemental.Simulation.Structures` without UnityEngine dependencies.
- Added bounded graph validation with hard safety ceilings of 256 pieces and 1,024 bonds. Runtime/capability limits remain lower.
- `EarthBondDamageSolver` decomposes structure-local impulses into tension, shear and compression, then applies smooth radial, contact-area and material weighting in stable array order.
- `EarthIslandSolver` writes deterministic connected components into caller-owned buffers and marks support from world bonds or foundation pieces. Missing pieces and broken/reforming bonds cannot bridge islands.
- Runtime batch boundaries expose the profiler markers `Elemental.Earth.Fracture.Damage` and `Elemental.Earth.Fracture.Islands`.
- `Elemental/Diagnostics/Earth Fracture Graph` is an interactive debug visualization for damage and support topology.
- EditMode: 141/141 passed, including 28 new fracture-graph tests and a 1,000-iteration zero-allocation hot-loop check. No new compiler, shader or package warning was emitted.
- Architecture decision: `Docs/adr/0014-fracture-and-reassembly-v2.md`.

## Task 2 — baked fracture asset and runtime adapters

Status: complete

- `EarthFractureAsset` schema 2 stores the intact proxy, 43 stable structural pieces, 140 baked bonds, rest poses, mass/volume, convex collider meshes and exterior/interior/magic face metadata.
- `EarthFractureBaker` produces `Assets/Elemental/Content/Fracture/EarthWallFracture.asset` deterministically. Production M3 setup requires this validated asset; runtime Voronoi creation remains available only when an isolated pool explicitly permits the debug fallback.
- `EarthFractureValidator` rejects schema/capacity errors, missing proxy or piece meshes, duplicate/invalid graph IDs, convex-collider complexity over 255 vertices, non-manifold colliders, missing/invalid face metadata, hierarchy cycles/level mismatches, rest seams outside both piece bounds, missing support and disconnected intact graphs.
- `EarthStructureRuntime`, `EarthPieceRuntime`, `EarthBondRuntime` and `EarthStructureProxySwitcher` copy baked values into owned fixed arrays and adapt canonical state to PhysX/proxy visibility. The ScriptableObject is never mutated at runtime.
- Explicit impacts route through the profiled directional damage/island solvers. The baked path has no timed bond decay; untargeted structural pieces stay persistent.
- Pool reuse restores parent, position, rotation, scale, Rigidbody velocity, collision state, bond state and intact proxy exactly.
- Preview tools are available at `Elemental/Fracture/Preview Baked Earth Structure` and `Elemental/Diagnostics/Earth Fracture Graph`.

### Evidence

- EditMode: 146/146 passed in 0.812 s, including asset schema, manifold, face metadata, disconnected graph, hierarchy and rest-seam validation.
- PlayMode: 60/60 passed in 79.324 s. New scenarios cover a production baked wall with no timer decay and exact 43-piece pool reset.
- Windows Development: 170,811,033 bytes in 54.669 s, 0 warnings/errors (`BuildReports/NativeWindows.json`).
- D3D11 standalone `wall-collapse` QA exited 0 and produced `BuildReports/Task2-BakedWall-v2.png`; cold-start voxel queue peak was 69.72 ms with zero pending work.

## Task 3 — provenance-aware physical reassembly

Status: complete

- MMB on a fractured baked structure starts one `EarthReassemblyController` session. Selection is limited to the source structure and captured pieces keep their stable structure/piece provenance.
- The pure ordering solver chooses a foundation/largest-island anchor and expands deterministically through repaired-neighbour depth, height, contact area, volume and stable ID. Missing pieces stay missing and therefore produce an explicit partial result.
- Every selected piece first moves into a deterministic two-dimensional staging cloud, then seats sequentially at its exact baked rest pose. Translation uses mass-aware bounded PD; rotation uses a bounded shortest-arc controller while collisions remain active.
- Only bonded neighbours and static terrain needed by an embedded authored seat receive targeted collision suppression. Repair-internal seating contacts cannot feed damage debt back into the structure.
- A positional settle gate reforms and then repairs bonds. Jam detection expands the staging cloud and retries deterministically rather than teleporting pieces.
- A full repair disables all piece adapters, restores every baked bond and returns to the intact proxy. Interruption returns unwelded pieces to dynamic physics; a missing piece leaves a stable partial structure without inventing mass.
- Typed capture, stage, align, weld, bond-reformed, completed, partial and interrupted events expose the repair lifecycle without deriving canonical state from presentation objects.

### Evidence

- EditMode: 153/153 passed in 0.772 s. The repair suite includes deterministic ordering, missing-component handling, bounded finite PD/jam behavior, 100 repeated solve cycles with no drift/NaN and `0 B` managed allocation after warm-up.
- PlayMode: 63/63 passed in 125.573 s. A focused run against the serialized production profile repaired all 43 pieces in 23.831 s; missing-piece partial repair completed in 22.199 s; interruption safely returned pieces to dynamics in 0.840 s.
- Windows Development: 170,857,854 bytes in 68.406 s, 0 warnings/errors (`BuildReports/NativeWindows.json`).
- The standalone D3D11 `reassembly` scenario exited 0 with all 43 pieces selected and one piece physically welded before taking `BuildReports/Task3-Reassembly-final.png`.

## Task 4 — material and fracture presentation

Status: complete

- Fracture schema 3 bakes separate compact collider meshes and duplicated render vertices with exterior/interior/cavity masks. The production wall keeps 43 pieces and 140 bonds; every render piece has two material slots while every convex collider stays within the 255-vertex ceiling.
- `Elemental/SG Earth Master` supplies object-local scale-correct projection for moving Earth and one shared planet-local projection frame for all voxel chunks. The voxel and loose-stone materials are separate, so moving blocks do not swim and chunk boundaries do not reset texture coordinates.
- `EarthMaterialProfile` owns the warm exterior, cooler fresh break, dust/mineral hierarchy, metre-scale macro/mid/micro response and the `_EARTH_DETAIL_LOW` variant. Native High and Native Low profile assets define explicit quality ceilings.
- `EarthImpactEvent` now drives bounded dust, irregular mesh chips, exceptional-energy amber accents, camera response and a 40-slot persistent URP decal ring. Visual response never feeds back into bond damage or Rigidbody truth.
- Platform fracture pieces use the four authored beveled Earth-block variants and matching convex `MeshCollider`s. Production wall/platform/debris creation no longer relies on Unity cube/sphere render primitives.
- URP remains Forward. Forward+ and GPU Resident Drawer are documented opt-in A/B decisions; dynamic structural pieces are not resident-drawer candidates.
- Material/vertex/shader policy: `Docs/rendering/earth-master-material.md`; architecture decision: `Docs/adr/0015-earth-material-and-fracture-feedback.md`.

### Evidence

- EditMode: 158/158 passed in 0.749 s; PlayMode: 63/63 passed in 125.612 s.
- Windows Development: 171,019,694 bytes in 57.634 s, 0 warnings/errors (`BuildReports/NativeWindows.json`).
- D3D11 standalone `earth-material` QA exited 0 with 43 baked pieces, two distinct surface materials and one active persistent scar (`BuildReports/Task4-EarthMaterial.png`).
- A 45-frame post-fracture capture on the RTX 4070 measured CPU average/max `4.26/7.00 ms` and GPU average/max `0.59/0.99 ms`. The independent cold-start voxel queue peak was `78.50 ms` with zero pending work and is not presented as steady-state frame time.

## Next concern

Task 5 adds bounded ambient world simulation without changing Earth structural truth.
