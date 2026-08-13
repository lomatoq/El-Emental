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
