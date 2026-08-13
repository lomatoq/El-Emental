# Master blueprint compliance matrix

Source of truth: `Elemental Planets — Core Design & Technical Architecture v0.1`, 52 pages, dated 2026-08-11. Retained source: `C:\Users\nirrt\Downloads\Elemental_Planets_Core_Design_Architecture_v0.1_BE.docx`.

## Milestone status

| Milestone | State | Direct evidence |
|---|---|---|
| M0 Bootstrap | Proven | Unity 6000.5.7f1/URP, pinned packages, asmdefs, tests, Windows and macOS builds, headless Windows player smoke |
| M1 Gravity Toy | Proven | Isolated local gravity, 32 bodies, force motor/camera, circumnavigation, 0 B sample loop, high-speed CCD regression |
| M2 Voxel Core | Proven | Canonical SDF/edit log/save, sparse chunks, Burst scheduled mesher, reusable buffers, stale rejection, collider debt, 1,000-edit gate |
| M3 Earth Core Slice | Proven | Gesture corpus/confusion matrix, footprint/extraction preview contracts, screen-input wall/pull/throw, speed-clamped flick, pooled wall/fragment stress, replay/save, presentation-invariance hash |
| M4 Active Ragdoll | Proven | Five-state physical controller, configurable-joint puppet, balance/recovery, impact routing and stress fixtures |
| M5 Air + FieldWorld | Proven | Sparse scheduler, four Air abilities, capped aero runtime, WindLab, 100-body overlap budget |
| M6 Heat + Water | Proven | Thermal/Water worlds, enthalpy/hysteresis, eight operators, six abilities, state reactions, conservation and ElementLab |
| M7 Missions | Proven | Contracts, six crises, seeded director, civilians, destructible Volcano Village, three winning strategy profiles |
| M8 Online Spike | Proven | Transport ADR, authority, snapshots, terrain replication, prediction/correction, replay divergence, 2–4 client latency/loss gate |
| M9 Web Magic Lab | Proven | Three capability profiles, adaptive budgets, Creator Suite, native/macOS/WebGL builds, browser QA and telemetry |

## Acceptance suite coverage

| ID | Requirement | State/evidence |
|---|---|---|
| T01–T04 | Gravity and motor convergence/circuit/jump | Proven, including explicit high-speed tunneling and 0 B gravity regressions |
| T05 | Gesture confusion matrix | Proven with a labelled 12-sample diagonal corpus |
| T06 | Preview/commit spatial agreement | Proven for the five-point ground footprint and committed wall endpoints; soft-asymptotic hold controls intentionally withheld bounded height, then the pooled wall emerges with mass tremor/delayed collision and later collapses through a deterministic 13-cell Voronoi partition |
| T07–T08 | Chunk boundary and save/reload hash | Proven |
| T09–T10 | Stale mesh job and collider debt/player safety | Proven with Burst `IJob`, version rejection and bounded runtime queues/debt |
| T11–T13 | Earth replay, mass/momentum, pool stability | Proven with 3,600-tick replay, screen-input Pull→held-anchor→Flick, 100-acquire pool and impulse-momentum fixture |
| T14–T15 | Active ragdoll recovery/explosion safety | Proven |
| T16–T17 | Air trajectory and overlap budget | Proven |
| T18–T21 | Water/thermal/reaction invariants | Proven |
| T22–T23 | Mission solvability/performance escalation | Proven |
| T24 | Save migration | Proven: explicit v1 → v2 migration report and state-preserving fixture |
| T25 | First divergence replay report | Proven: ordered subsystem hashes with first tick/subsystem/expected/actual report |
| T26–T27 | Native profiles and degradation | Proven: NativeHigh/NativeLow assets plus 216,000-tick/profile 0 B matrix |
| T28 | Web startup/memory/play | Proven: real WebGL2 build and browser run with HUD/console evidence |
| T29 | Presentation cannot mutate simulation | Proven by assembly direction and VFX-on/off canonical hash fixture |
| T30 | 30–60 minute memory plateau | Proven for canonical schedulers: 216,000 ticks/profile, 0 B steady-state allocation |
| T31 | Black-hole safety spike | Deferred by blueprint; optional experiment, not a milestone gate |

## Cross-cutting tools and contracts

- `ElementalSuiteWindow`: Ability Workbench, Material Lab, Planet Lab, capability budget estimator, diagnostics and builds in UI Toolkit.
- `AbilityRecipeJsonCodec`: schema-first JSON import/export with bounded validation and no mutation on rejection.
- `ElementalProjectValidator`: engine, ten scenes, unique/valid ability IDs and all three profiles.
- `ElementalBugBundle`: one-click environment, package, compliance, build, test and log archive.
- Mandatory telemetry is present per lab: queue/debt/field/thermal/mission/network/capability HUDs and profiler markers.
- Canonical save schema and replay checkpoint/divergence contracts are explicit.

## Honest production boundaries

The blueprint’s experimental/future content remains intentionally outside this proof: a shipping socket transport, WebGPU, black-hole gameplay, final animation assets, and a final naming/silhouette bible. The architecture has explicit seams for them; none is silently claimed as implemented. Hardware-specific final-content P50/P95/P99 and multi-hour soak certification must be repeated on the eventual content-complete build.
