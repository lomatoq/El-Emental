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

## Next concern

Task 1 adds pure, Unity-free fracture graph contracts, directional bond damage, deterministic island solving, tests and ADR-EARTH-01. It must not depend on the Task 2 runtime adapter.
