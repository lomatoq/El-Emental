# Performance and capability evidence

Editor: Unity 6000.5.7f1 (017862109af0). Reference PC: Windows 11, AMD Ryzen 7 5700G, 16 logical processors, 65,404 MB RAM, NVIDIA GeForce RTX 4070 for interactive/browser runs.

## Targets

- Physics: 60 Hz; Field: 20 Hz; Thermal: 10 Hz.
- Gravity and documented pure simulation hot loops: 0 B managed allocation after warm-up.
- Mesh and collider work: separate bounded queues; stale results rejected by version.
- Active gameplay rules remain invariant under capability pressure.
- WebLab: WebGL2 baseline, no required compute or threaded jobs, 768 MB profile ceiling.

## Capability budgets

| Profile | Active chunks | Mesh/frame | Collider/frame | Fields | Fluids | VFX particles | Ragdolls | Memory MB |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| NativeHigh | 512 | 8 | 4 | 128 | 128 | 12,000 | 18 | 4,096 |
| NativeLow | 192 | 4 | 2 | 64 | 48 | 4,500 | 10 | 2,048 |
| WebLab | 64 | 2 | 1 | 24 | 16 | 1,600 | 6 | 768 |

### Earth structural budgets

| Profile | Hero pieces/structure | Full-detail structures | Active repair pieces | Reforming bonds/tick |
|---|---:|---:|---:|---:|
| NativeHigh | 64 | 2 | 64 | 128 |
| NativeLow | 36 | 1 | 36 | 72 |
| WebLab | 20 | 1 | 20 | 40 |

Pure graph storage is validated against a separate safety ceiling of 256 pieces and 1,024 bonds per structure. This is not permission to activate that many Rigidbodies: capability budgets still gate adapters and repair work. Damage and island batches reuse caller-owned arrays and are profiled as `Elemental.Earth.Fracture.Damage` and `Elemental.Earth.Fracture.Islands`.

## Final automated evidence — 2026-08-12

- EditMode: 73/73 passed in 0.332 s (`TestResults/EditMode-Final-v5.xml`).
- PlayMode: 23/23 passed in 32.919 s (`TestResults/PlayMode-Final-v3.xml`).
- Capability matrix: 216,000 ticks/profile, 0 B allocation for NativeHigh/NativeLow/WebLab, no canonical rule change (`BuildReports/PerformanceMatrix.json`).
- Gravity: 10,000 steady-state samples, 0 B.
- Voxel: 1,000 edits stay sparse; 1,000 post-warmup density queries, 0 B; Burst job topology/stale-result gate passes.
- Active ragdoll: 200 impacts/100 recoveries with pure hot-loop 0 B.
- Water: 10,000 transfers conserve mass; 64 regions stay within the bounded query budget.
- Air: 100 bodies query 64 overlaps under a hard 16-check cap with exposed debt.
- Windows build: 165,882,014 bytes, 80.44 s, 0 warnings/errors; player smoke exit 0.
- macOS build: 290,339,930 bytes, 123.16 s, 0 warnings/errors.
- Final WebGL2 build: 109,906,845 bytes, 168.35 s incremental, 0 warnings/errors.
- Browser QA: WebGL 2.0/GLES 3 context, 960×600 interactive canvas, runtime HUD, no console warnings/errors after startup and input.

Raw evidence is kept under `BuildReports` and `TestResults`. Interactive P50/P95/P99 frame captures on a shipping content workload remain a production-content profiling activity; they are not fabricated from the architecture labs.

## Earth action-polish evidence — 2026-08-13

- EditMode: 108/108 passed in 0.445 s (`TestResults/PolishWaveEdit.xml`).
- PlayMode: 52/52 passed in 67.169 s (`TestResults/PolishFinalPlay2.xml`).
- Windows Development build: 169,189,131 bytes, 40.83 s, 0 warnings/errors (`BuildReports/NativeWindows.json`).
- Standalone DX11 visual QA passed for wall, platform, moving crest, gravity grip and dynamic wall debris. The release wave capture reported a 20.17 ms one-time voxel render-queue peak and zero pending work; the wave itself performs no voxel edits.
- Bounded profiler scopes cover `Elemental.Magic.GravityWell` and `Elemental.Particles.RadialGravity`; both reuse fixed target/particle buffers after warm-up.

## Earth Core world/space deliverable — 2026-08-13

- EditMode: 113/113 passed in 0.610 s (`Logs/EarthCoreShipEditMode-results.xml`).
- PlayMode: 56/56 passed in 74.699 s (`Logs/EarthCoreShipPlayMode-results.xml`). This includes the 3-second MMB fracture hold, platform carry/jump, celestial day, deterministic meteor, Humanoid recovery and legacy profile migration.
- Windows Development: 170,711,334 bytes in 79.47 s, 0 warnings/errors (`BuildReports/NativeWindows.json`).
- Windows Release: 105,643,759 bytes in 57.76 s, 0 warnings/errors (`BuildReports/NativeWindowsRelease.json`).
- Windowed D3D11 standalone QA exited 0 for dawn, night/moon, 43-piece wall gravity grip, platform lift, physical meteor impact and Mage casting. Evidence is stored as `BuildReports/QA-*.png`; the gravity run reported 43 active fracture pieces, 43 source targets and 46/48 captured Earth targets.
- Cold-start QA reported 68.81–85.55 ms one-time voxel render-queue peaks and zero pending work at capture. These are startup queue peaks, not P95 frame time. Shipping-content CPU P95 and the 1.5 ms atmosphere/scaled-space GPU target still require an interactive profiler capture and are intentionally not inferred from screenshots.
- Unity Test Framework 1.7 emits one editor-shutdown persistent-allocation marker after both batch suites while also reporting no leaked weak pointers. The established 0 B claims above apply to measured managed steady-state loops, not Editor/package shutdown.

## Earth Core MVP polish Task 0 — 2026-08-13

- Before first aid: EditMode 113/113 in 0.516 s; PlayMode 56/56 in 74.065 s.
- After first aid: EditMode 113/113 in 0.483 s; PlayMode 58/58 in 74.064 s. New runtime gates cover cause-only wall fracture, persistent repairable structural pieces and a convex irregular debris fallback.
- `Scripts/Test-Unity.ps1` now waits for the Unity Windows process and propagates its real exit code; the prior direct GUI-process invocation could return an empty exit code while tests were still running.
- Capability matrix recaptured at `2026-08-13T12:49:56Z`: three profiles passed 216,000 ticks each, total managed allocation `0 B`, no canonical rule changes.
- Windows Development build: 170,713,574 bytes in 53.775 s, 0 warnings/errors.
- D3D11 standalone QA wall capture exited 0. Its one-time voxel render-queue peak was 71.18 ms with zero pending work. This is cold-start evidence only and is not presented as frame-time P95.

## Earth Core MVP polish Task 1 — 2026-08-13

- EditMode: 141/141 passed. The 28 new cases cover graph validation, stable identifiers, tension/shear/compression response, radial and area weighting, bounded output overflow, invalid impacts, world/foundation support, missing pieces, deterministic connected components and a 1,000-iteration zero-allocation hot-loop check.
- The pure solvers use only caller-owned arrays. Runtime profiling scopes are isolated in `EarthFractureBatchRunner`; no UnityEngine type enters the canonical contracts.
- `Elemental/Diagnostics/Earth Fracture Graph` visualizes bond health, stable component IDs and foundation-supported islands from the same profiled batch boundary.

## Earth Core MVP polish Task 2 — 2026-08-13

- EditMode: 146/146 passed in 0.812 s; PlayMode: 60/60 passed in 79.324 s.
- Production wall topology is baked at 43 pieces and 140 bonds. Its convex piece meshes remain below the 255-vertex PhysX ceiling; structural state is copied once into fixed runtime arrays.
- The production pool does no Voronoi generation or topology collection work at runtime. The procedural path is explicitly debug-only.
- Damage and support batches remain under `Elemental.Earth.Fracture.Damage` / `Elemental.Earth.Fracture.Islands`; the PlayMode no-decay scenario observes stable bond count for 3.2 s after pending contact callbacks are isolated.
- Windows Development: 170,811,033 bytes in 54.669 s, 0 warnings/errors.
- D3D11 standalone wall-collapse QA exited 0. The captured cold-start voxel queue peak was 69.72 ms with zero pending work; this is not a shipping frame-time percentile.

## Earth Core MVP polish Task 3 — 2026-08-13

- EditMode: 153/153 passed in 0.772 s; PlayMode: 63/63 passed in 125.573 s.
- Repair ordering and pose integration use caller-owned fixed arrays. A 100-cycle warm test reported `0 B` managed allocation and finite, repeatable pose output without accumulated drift.
- A focused run against the serialized production profile repaired all 43 pieces in 23.831 s. A missing-piece run completed as partial without invented mass in 22.199 s, and interruption safely restored dynamic motion in 0.840 s.
- Repair work is isolated under `Elemental.Earth.Repair.Align`; active piece and bond work remains bounded by the capability table above.
- Windows Development: 170,857,854 bytes in 68.406 s, 0 warnings/errors.
- D3D11 standalone reassembly QA exited 0 with all 43 pieces selected and one physically welded at capture. The remaining active piece reported a finite `0.015 m` pose error and no retry; startup voxel queue timing remains cold-start evidence, not a frame-time percentile.

## Earth Core MVP polish Task 4 — 2026-08-13

- EditMode: 158/158 passed in 0.749 s; PlayMode: 63/63 passed in 125.612 s.
- Production wall presentation remains 43 pieces/140 bonds. Render meshes carry two submeshes and RGBA face masks; collider meshes are separate and remain under 255 vertices. Platform fragments reuse four authored beveled convex variants.
- Native High uses full micro-normal distance detail and a 40-slot pooled decal ring. Native Low disables the micro-normal sample through `_EARTH_DETAIL_LOW`, shortens detail distance, caps decals at 20, dust at 28 and chips at 8.
- URP stays Forward pending an equivalent-scene Forward+ A/B capture. GPU Resident Drawer remains excluded for dynamic pieces; no unmeasured renderer switch is claimed as an optimisation.
- Windows Development: 171,019,694 bytes in 57.634 s, 0 warnings/errors.
- Standalone D3D11 post-fracture capture: 45 frames, CPU average/max 4.26/7.00 ms and GPU average/max 0.59/0.99 ms. This short capture verifies bounded material/decal cost but is not substituted for a long P95/P99 shipping-content trace.
- The same run reported a 78.50 ms cold-start voxel render-queue peak and zero pending work at capture. It remains separately labelled startup debt.

## Earth Core MVP polish Task 5 — 2026-08-13

- The normalized Earth gesture path is bounded to 192 collected samples and 16–64 resampled points (32 by default). Recognition evaluates only the context-relevant templates.
- `Elemental.Earth.Gesture.Sample`, `Elemental.Earth.Gesture.Recognize`, and `Elemental.Earth.Intent.Resolve` isolate sampling, classification and intent work.
- A 256-iteration warm recognition loop reports `0 B` managed allocation. Replay commands keep quantized resolved geometry and do not retain raw pointer streams.
- EditMode: 170/170 passed; PlayMode: 64/64 passed in 125.592 s. The 200-case preview/commit corpus remains hash-identical.
- Windows Development: 171,058,210 bytes in 57.092 s, 0 warnings/errors.

## Earth Core MVP polish Task 6 — 2026-08-13

- The six-technique resolver and command codec are pure value operations with fixed-size data and no scene queries.
- Ground Wave reuses the existing maximum 96-column pool and never edits voxel SDF while the crest travels.
- Technique presentation is data-only; pose, camera and feedback values cannot alter damage, impulses, fracture or reassembly.
- EditMode: 174/174 passed in 0.818 s; PlayMode: 64/64 passed in 126.040 s.
- Windows Development: 171,075,122 bytes in 62.964 s, 0 warnings/errors.

## Earth Core MVP polish Task 7 — 2026-08-13

- Pose intent, phase timing, effort, foot planting, pelvis compensation and jump forgiveness use fixed value data and bounded non-allocating queries.
- Humanoid foot IK uses two fixed eight-hit buffers; no per-frame overlap or collection allocation was added.
- EditMode: 180/180 passed in 1.165 s; PlayMode: 64/64 passed in 125.851 s.
- Windows Development: 171,098,682 bytes in 57.851 s, 0 warnings/errors.
