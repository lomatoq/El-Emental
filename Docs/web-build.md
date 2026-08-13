# WebLab build and QA

Baseline: WebGL2. WebGPU remains experimental and is not required for canonical simulation.

## Build

Unity menu: `Elemental → Build → Build WebLab WebGL2`.

Batch mode:

`Unity.exe -batchmode -quit -projectPath <repo> -executeMethod Elemental.Authoring.Editor.ElementalBuildPipeline.BuildWebLab`

Output: `Builds/WebLab`. Serve it via HTTP, for example from that directory with `python -m http.server 8765 --bind 127.0.0.1`; do not open `index.html` directly.

## Verified baseline — 2026-08-12

- Exact editor: 6000.5.7f1 (017862109af0) with matching WebGL Build Support.
- Final build: 109,906,845 bytes, 168.35 seconds incremental, 0 warnings/errors.
- In-app Chromium QA: title `Unity Web Player | El-Emental`, interactive 960×600 canvas, Earth lab and player visible.
- Console: WebGL 2.0 context, OpenGL ES 3.0/GLES 3, PhysX single-threaded mode, no warning/error entries after startup and keyboard/pointer input.
- HUD: WebLab/WebGL2, centralized budgets, visible degradation result, startup time and managed-memory telemetry.
- Profile: compute, Air and threaded jobs disabled; active Earth rules remain identical to native profiles.

Raw build evidence: `BuildReports/WebLab.json`; performance evidence: `BuildReports/PerformanceMatrix.json`.
