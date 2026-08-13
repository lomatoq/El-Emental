# M9 Web Magic Lab

Status: complete

## Deliverables

- NativeHigh, NativeLow and WebLab capability assets centralize active chunks, mesh/collider work, fields, fluids, particles, ragdolls and memory budgets.
- `AdaptiveBudgetScheduler` reduces presentation first, distant simulation second, and rejects new distant work instead of silently changing active canonical rules.
- `WebLab.unity` is the Earth-focused WebGL2 lab with visible profile, budget, startup, managed-memory and degradation telemetry.
- `ElementalSuiteWindow` provides an editable UI Toolkit authoring suite: Ability Workbench, Material Lab, Planet Lab, budget estimator, validator, bug bundle and build controls.
- `ElementalBuildPipeline` produces JSON evidence for Windows, macOS and WebGL2.
- Native build scene ordering is explicit and regression-tested: `EarthCoreSlice.unity` is always the executable entry scene while technical and lab scenes remain available in the player data.

## Build and browser evidence — 2026-08-12

- Windows x64 development build: 165,882,014 bytes, 80.44 s, 0 warnings/errors; headless player exited 0 after 120 ticks with 0 error lines.
- macOS development app: 290,339,930 bytes, 123.16 s, 0 warnings/errors.
- Final WebGL2 development build: 109,906,845 bytes, 168.35 s incremental, 0 warnings/errors.
- Local HTTP browser QA reached the interactive 960×600 Earth lab; HUD displayed `WebLab · WebGL2 baseline`, `chunks 64`, `fields 24`, `fluid 16`, `VFX 1600`, visible degradation reason and runtime telemetry.
- Browser console confirmed a WebGL 2.0 / OpenGL ES 3.0 context, GLES 3, PhysX single-threaded mode, cached data download, and no warning/error entries before or after keyboard/pointer input.
- Performance matrix runs 216,000 scheduler ticks (60 simulated minutes) per profile: all three pass with 0 B managed allocation, 360 presentation degradations and no canonical rule change.
- Full final suites: EditMode 73/73 and PlayMode 23/23.

WebGPU remains optional and experimental as required by the blueprint.
