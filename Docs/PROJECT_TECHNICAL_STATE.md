# El-Emental project technical state

Updated: 2026-08-31

Snapshot branch: `codex/foundation-stability-rescue`

Tested implementation commit: `8c2e6245a0e4fe1e169b63998e918ce800477b36`

Evidence condition: tested 2026-08-31 from the exact implementation content committed above;
later documentation-only commits do not invalidate the code/test snapshot.

Companion tracker: [`Docs/PROJECT_EXECUTION_TRACKER.md`](PROJECT_EXECUTION_TRACKER.md)

## Purpose and truth protocol

This is the canonical short technical entry point for gameplay work. It explains what the
project is, where authority lives, how the layers connect, and which technical claims are
actually supported. It does not replace the detailed architecture, milestone, ADR, or raw
evidence files.

Use these labels when updating this file:

- **Fact** — visible in source, serialized data, an accepted ADR, or a named report.
- **Measured** — includes the exact report, conditions, and important caveats.
- **Decision** — accepted design/architecture intent; it may still lack implementation evidence.
- **Unknown** — must not be silently promoted to complete.

Freshness rule: after a material public-contract, package, scene-generator, performance, or
authority change, update only the affected sections and set the header to the tested branch and
commit. A report predating that commit is historical evidence, not validation of the new commit.
The live gate and risk state belongs in the [execution tracker](PROJECT_EXECUTION_TRACKER.md).

## Canonical source order

When sources disagree, prefer the narrowest current evidence in this order:

1. Current source/serialized assets at the tested implementation commit.
2. Fresh test/build/profiler report produced from that same commit and worktree.
3. Accepted ADR for architectural intent.
4. Current milestone for product scope and acceptance criteria.
5. Older milestone, README, or `.solo-studio` narrative for history only.

Start with this file and the tracker, then follow links instead of rereading the repository:

- [`Docs/architecture.md`](architecture.md) — stable layering, clocks, terrain authority, errors.
- [`Docs/milestones/M11-earth-mvp-0.1-rumble-duel.md`](milestones/M11-earth-mvp-0.1-rumble-duel.md)
  — current slice and detailed history.
- [`Docs/adr/ADR-0001-engine-stack.md`](adr/ADR-0001-engine-stack.md) — engine baseline.
- [`Docs/adr/0014-fracture-and-reassembly-v2.md`](adr/0014-fracture-and-reassembly-v2.md) — structural truth.
- [`Docs/adr/0021-semantic-earth-intents-and-shared-surfaces.md`](adr/0021-semantic-earth-intents-and-shared-surfaces.md)
  — input and surfaces.
- [`Docs/adr/0026-earth-core-v4-matter-grammar-and-choreography.md`](adr/0026-earth-core-v4-matter-grammar-and-choreography.md)
  — target matter/technique chain.
- [`Docs/adr/0029-visible-humanoid-ragdoll-and-deferred-platform-fracture.md`](adr/0029-visible-humanoid-ragdoll-and-deferred-platform-fracture.md)
  and [`Docs/adr/0030-mvp-01-earth-input-impact-and-matter-follow-up.md`](adr/0030-mvp-01-earth-input-impact-and-matter-follow-up.md)
  — current MVP runtime handoffs.
- [`Docs/adr/0031-broken-crown-arena-and-meteor-floor.md`](adr/0031-broken-crown-arena-and-meteor-floor.md)
  — arena authoring and damage contract.
- [`Docs/adr/0032-performance-aware-atmosphere-and-focus.md`](adr/0032-performance-aware-atmosphere-and-focus.md)
  — rendering policy.
- [`Docs/adr/0033-animation-contact-and-arena-rendering-rehabilitation.md`](adr/0033-animation-contact-and-arena-rendering-rehabilitation.md)
  — proposed animation/contact acceptance gate.

## Product and golden path

**Fact:** El-Emental is a Unity physics-action prototype about manipulating matter on small,
destructible planets. The non-negotiable Earth promise is readable, heavy, spatial magic:
shape or pull stone, move it under local spherical gravity, and make physical impact,
fracture, repair, animation, camera, and feedback agree about the result. The fuller product
charter is in [`.solo-studio/PROJECT_CHARTER.md`](../.solo-studio/PROJECT_CHARTER.md).

**Current golden-path scene:**
`Assets/Elemental/Content/Scenes/EarthCoreSlice.unity`.

**Rebuild authority:** `Elemental.Authoring.Editor.M3EarthCoreSetup.Configure()` in
`Assets/Elemental/Authoring/Editor/M3EarthCoreSetup.cs`, exposed as
`Elemental/Setup/Create M3 Earth Core Slice`. The scene is generated; change profiles,
source assets, importer/integrator code, or the setup code, then rebuild. Do not treat a
hand-edited generated scene as durable source.

**Current representative loop:**

1. Launch the 55.1 m-radius Earth world and move a Humanoid on local spherical gravity.
2. Draw/raise Earth structures or acquire physical matter through the routed mouse grammar.
3. Move, launch, fracture, disassemble, or repair provenance-bearing matter.
4. Fight the deterministic rival in Broken Crown; impacts produce localized response,
   recoverable knockdown, or bounded ragdoll/KO depending on the accepted hit contract.
5. Recover/respawn and replay without growing the prewarmed pools or losing terrain/matter truth.

The exact `55.1` profile value is serialized in
`Assets/Elemental/Content/Profiles/PlanetWorldProfile.asset`. M11 still contains older
radius-36 performance/gate prose; treat those passages as historical until the milestone is
reconciled. MVP 0.1 intentionally excludes HP bars, score, rounds, victory UI, progression,
NavMesh, behavior trees, and a second enemy class.

## Engine and package baseline

**Fact at the snapshot commit:**

- Unity `6000.5.7f1` (`ProjectSettings/ProjectVersion.txt`).
- URP `17.5.0`, Forward renderer; built-in PhysX plus custom local gravity.
- Input System `1.20.0`, Cinemachine `3.1.7`, Animation Rigging `1.4.1`.
- Burst `1.8.30`, Collections `6.5.0`, Mathematics `1.4.0`.
- Unity Test Framework `1.7.0`.
- MiniBokeh is a Git dependency pinned to commit
  `faa491907b7a580ef2ddfebfbef4590d1d3c6628`; the project-local dual-subject DOF path is
  the current NativeHigh owner and MiniBokeh is disabled in the latest profiler audit.
- VFX Graph `17.5.0`, AI Assistant `2.18.0-pre.2`, and AI Inference `2.6.1` are present in
  `Packages/manifest.json`. Their presence is not evidence that they own canonical gameplay.

Native Windows/macOS are the engine-stack priority. WebGL2 is a reduced `WebLab` capability
profile. A production online transport is not selected.

## Layer map and dependency direction

```text
device input
  -> normalized frame / semantic route
  -> command or bounded runtime intent
  -> pure simulation policy + canonical state
  -> Unity runtime adapters / PhysX / scene lifecycle
  -> typed events, snapshots, read-only state
  -> camera / animation / VFX / audio / UI / diagnostics

authored profiles + imported/baked assets
  -> validators and scene generator
  -> copied/baked runtime data (never mutable authoring state as gameplay truth)
```

| Layer | Owns | Important entry points | Dependency rule |
|---|---|---|---|
| `Elemental.Core` | Stable IDs, deterministic helpers, simulation tick primitives | `Assets/Elemental/Core/` | Assembly has `noEngineReferences: true`; no Unity object lifecycle. |
| `Elemental.Simulation` | Gravity, voxel/SDF state, magic/combat policies, matter/provenance, support, fracture/reassembly, networking contracts | `GravityWorld`, `VoxelPlanetState`, `EarthActionRouter`, `EarthMatterRegistry`, `EarthMvpBotPlanner`, `EarthDuelRespawnSolver` | Source scan at this commit finds no UnityEngine dependency, but the asmdef does **not** set `noEngineReferences: true`; discipline/tests currently enforce the boundary rather than assembly metadata. |
| `Elemental.Input` | Input System device boundary, viewport-normalized gesture sampling, semantic routing | `EarthInputAdapter`, `EarthActionRouterBehaviour`, gesture pipeline under `Assets/Elemental/Input/Gestures/` | Only `EarthInputAdapter` may read physical actions/devices. Consumers receive normalized data or resolved intent. |
| `Elemental.Runtime` | Scene/session lifecycle, PhysX handles, pools, world queries, command execution, fighter/terrain adapters | `MagicExecutor`, `VoxelPlanetBehaviour`, `PlanetMotor`, `EarthSurfaceQueryService`, `EarthArenaStructure`, `EarthMvpDuelController` | Converts pure contracts to Unity operations; must not let Rigidbody, collider, renderer, or GameObject state become canonical truth. |
| `Elemental.Presentation` | Camera, Humanoid/IK/secondary motion, URP, VFX, audio, UI, capture/telemetry | `EarthCameraDirector`, `EarthFootContactController`, `EarthCinematicDepthOfFieldController`, `CelestialSystemBehaviour`, `EarthPerformanceTelemetry` | Reads state/events. Animation/VFX completion may not decide damage, KO, terrain, matter, or repair. |
| `Elemental.Authoring` | Profiles, ScriptableObjects, validators, importers, bakers, editor menus, generated scenes | `M3EarthCoreSetup`, `BrokenCrownArenaImporter`, `EarthFractureBaker`, `ElementalProjectValidator` | Authoring data is validated, then copied/baked into bounded runtime state. |
| Tests/tooling | Pure unit tests, scene/runtime tests, build and evidence runners | `Assets/Elemental/Tests/`, `Scripts/Test-Unity.ps1`, `Mvp01FocusedTestLauncher`, `Mvp01EvidenceRunner` | Test duration is regression evidence, not a frame-time measurement. Reports must identify commit and conditions to validate current code. |

The assembly references currently enforce the broad direction
`Core <- Simulation <- Runtime`, with Input, Presentation, and Authoring depending on the
runtime/domain layers as needed. Presentation also references Input for camera/interaction
context; this is acceptable only while presentation remains non-authoritative.

## State ownership and data flow

### Input to Earth action

`EarthInputAdapter` reads Unity Input System actions and emits normalized viewport/device
state. `EarthActionRouterBehaviour` buffers the short dual-button decision window and feeds the
pure `EarthActionRouter`; the resolved owner/phase decides whether primary magic, force,
vector field, gravity, armor, resonance, surf, or movement consumes the input. Gesture code
resolves stable surface/target handles before runtime execution. `MagicExecutor` and dedicated
bounded controllers adapt accepted intent to the world. Replays store resolved commands, not
raw pixels.

The non-shipping Rumble lookdev shortcuts also route through optional actions on
`EarthInputAdapter`; presentation components no longer read `Keyboard.current` directly. The
physical-device boundary source scan is green in the 2026-08-31 full EditMode run.

Primary risk: input, camera ray projection, dense arena colliders, and delayed chord replay meet
at this seam. This is why physical-device PlayMode tests are acceptance gates rather than an
optional unit-test supplement.

### Terrain and matter

`VoxelPlanetState` owns the analytic base SDF plus ordered `SdfEdit` history.
`VoxelPlanetBehaviour` owns Unity-side chunk/render/collider queues; meshes and colliders are
caches. A terrain extraction remains transactional until `EditCommitted`; only then may the
reserved fragment become visible/held. `EarthMatterRegistry` owns stable matter identity and
provenance across terrain, fragment, structure, armor, and return transitions.
`EarthMatterMassPolicy` plus `EarthMatterMassRuntime` convert authored/collider scale to shared
gameplay mass for the runtime sources that are wired to it.

### Structures, fracture, and repair

`EarthFractureAsset` and `EarthArenaFractureCatalog` contain validated authoring topology.
Pure bond damage, island, ordering, and pose solvers own canonical structural decisions.
`EarthWall`, `EarthPlatform`, and `EarthArenaStructure` own bounded Unity representations and
proxy switching. Stable structure/piece/bond IDs survive pooling. The Broken Crown floor is
ordinary-damage immune and may swap to its 36-piece representation only for typed meteor
impact; walls, columns, gate, and loose rocks use their narrower contracts.

Platform casting prewarms six roots and up to 48 shells per root. The solid walkable collider
is immediate; fracture topology/preparation is deferred one cell per rendered frame, and early
impact is retained until `FractureReady`.

### Characters, support, impact, and duel

`PlanetMotor` owns canonical movement/grounding. Presentation contact IK must follow the motor
and stable support handles; it may not move gameplay truth. At the tested implementation commit,
`CharacterSupportRuntimeAdapter` classifies bounded non-alloc SphereCast/Raycast candidates for
the pure `CharacterSupportAuthority`, and `PlanetMotor` consumes the selected support with a
small retention bias. Arena and voxel surface providers expose explicit support classification;
released pieces and dynamic debris are rejected. The targeted runtime debris/support regression
passes in `BuildReports/FoundationWorkingTreePlay-20260831.xml`.

The Linebreaker runtime FBX now retains the original 48-bone Humanoid hierarchy and adds only
`Secondary_HelmetAnchor` and `Secondary_HairLock`. The authored weighted source distributes the
plume across the three tail bones plus the hair lock and weights both two-bone belt chains. The
generated shipping scene was rebuilt through `M3EarthCoreSetup.Configure()` and the targeted
scene test confirms that player and rival configure helmet/hair, tail, and both belts before
scene unload.

`EarthMvpBotPlanner` owns deterministic rival phases. `EarthMvpBotController` adapts them to the
motor and pooled projectile. Impact solvers decide local reaction, recoverable knockdown, or KO.
`HumanoidRagdollRig` owns the visible 11-body physics handoff, while
`EarthMvpDuelController` owns KO timing, fade, reset, and respawn. Animator and PhysX must never
own the same bones simultaneously.

### Presentation and lighting

Typed events fan out through presentation adapters to camera, dust, debris, scars, audio, and
animation. `EarthWorldResponseEvent` identifies one accepted gameplay outcome; presentation
must not reapply it. `EarthCameraDirector` owns semantic composition requests, not targeting or
damage. `CelestialSystemBehaviour` uses `CelestialLightingClockPolicy`: gameplay defaults to a
locked authored key/ambient state, with animated ephemeris reserved for explicit QA/lookdev.
URP atmosphere, DOF, SSAO, shadows, and motes are degradable presentation paths.

### Persistence and replay

`VoxelSaveCodec` is a version-2 binary codec for voxel base parameters and ordered edits and can
read version 1. `MagicReplayRecorder` is an in-memory, tick-ordered command list. Tests exercise
both, but no runtime code calls `VoxelSaveCodec.Write/Read`; atomic disk save, backup/recovery,
whole-session schema, cloud conflict, and migration fixtures beyond voxel v1 are therefore
**not shipping-complete**.

### Networking

`Elemental.Simulation.Networking` defines transport-independent commands, authority decisions,
terrain replication, snapshots, relevance, correction, and a deterministic
`SimulatedTransport<T>`. ADR 0003 accepts this for the M8 spike only. There is no selected or
wired shipping socket/relay transport, and the M11 golden path is local.

## Current strengths

- Explicit typed IDs, commands, events, provenance, and bounded pure solvers give most important
  state a named owner and a unit-test seam.
- Terrain and structure representation separate canonical state from meshes, colliders, VFX,
  and pooled views.
- The project has wide automated coverage: 129 C# test files and 590 `[Test]`/`[UnityTest]`
  annotations at this snapshot, plus focused physical-input, scene, animation, and profiler gates.
- Hot paths use fixed buffers/non-alloc physics queries and named profiler markers; capability
  profiles make many hard capacities explicit.
- Generated-scene, Blender, import, fracture, animation, material, and project validators turn
  repeated solo-production work into reproducible tooling.
- Experimental rendering and online work have explicit fallback/removal seams rather than owning
  gameplay truth.

## Weaknesses and technical debt

- The tested implementation has no complete green acceptance run. A fresh full EditMode run passes
  `586/587`; its sole failure is the zero-warning build-evidence gate because the existing Native
  Windows report records 186 warnings. The two new foundation PlayMode regressions pass `2/2`,
  but the complete focused/broad PlayMode suites remain pending. See the tracker for exact scope.
- `M11-earth-mvp-0.1-rumble-duel.md` mixes accepted claims, radius-36 history, radius-55.1 state,
  and a previously partial `139/142` + `10/17` snapshot. Status cannot be inferred from its title.
- `M3EarthCoreSetup.cs` (~3,506 lines), `MagicExecutor.cs` (~2,151 lines), and the generated
  `EarthCoreSlice.unity` (~175,029 lines) are large integration surfaces. They accelerate one-click
  reconstruction but amplify merge risk, hidden coupling, and broad regression blast radius.
- The Simulation source is currently Unity-free, but its asmdef does not enforce
  `noEngineReferences`; one accidental Unity reference could erode the domain boundary.
- `BuildReports` contains tracked baselines plus overwritten/untracked working evidence. A
  filename containing `Latest` or `Accepted` is not proof unless its timestamp/commit and result
  match the claim.
- Some newest foundation policies are not equally integrated. Shared mass is used by arena
  pieces/decor, celestial clock policy is wired, and classified support now feeds `PlanetMotor`;
  full focused PlayMode and profiler evidence for their combined scene remains incomplete.
- Shipping persistence, production networking, controller/accessibility coverage, external-player
  comprehension, and release operations remain incomplete or unproven.

## Technology trade-offs and limits

| Choice | Benefit | Limit / required fallback |
|---|---|---|
| Unity 6 + URP + PhysX | Mature editor, authoring, Humanoid, rendering, and rigidbody integration | PhysX outcomes are tolerance-deterministic, not bitwise cross-platform replay; preserve commands/provenance and reconcile instead of serializing scene objects. |
| Analytic SDF + ordered edits | Compact authoritative terrain and replayable changes | Meshing/collision are asynchronous caches; startup/edit queues can hitch if budgets or stale-result rejection regress. |
| Authored 3D prefracture | Stable topology, art direction, provenance, predictable limits | Requires Blender/import validation and many precreated shells; active Rigidbody/piece budgets remain hard limits. |
| Fixed arrays and prewarmed pools | Low-GC, bounded behavior | Capacity exhaustion must reject loudly or degrade by profile; it cannot silently drop canonical matter/impact. |
| Animator + procedural IK + visible ragdoll | Readable authored motion with physical KO | Update-clock and ownership handoffs are fragile; motor, Animator, IK, and PhysX need explicit interruption/reset tests. |
| One-pass screen-space atmosphere and semantic DOF | Predictable cost and readable capability fallbacks | Not physical volumetrics; target-device GPU evidence and dual-subject/camera-motion validation remain necessary. |
| Git MiniBokeh and prerelease/AI packages | Fast access to specialized tooling/experiments | Pin, license, platform, build-size, and maintenance risk must be reviewed; none may become an unwrapped gameplay dependency. |
| Transport-independent network spike | Tests authority payloads without early package lock-in | Not a shipping online implementation; disconnect, relay, NAT, security, service failure, and real multi-machine performance are unknown. |
| Voxel codec + in-memory replay | Demonstrates versioned terrain and ordered command contracts | Does not yet provide atomic full-game saves, backup/recovery, durable replay files, or cloud conflict policy. |

## Performance and test evidence

The live pass/fail table is in the [execution tracker](PROJECT_EXECUTION_TRACKER.md). The strongest
performance artifact currently present is `BuildReports/Mvp01Profiler.json`, captured
2026-08-31 05:06 UTC in a 1920x1080 D3D11 standalone run on an RTX 4070. Across 720 samples it
reports CPU/total p95 `8.335 ms`, zero measured steady-state GC, `AcquireSolid` peak `2.2064 ms`,
fracture preparation peak `0.4800 ms`, and a passing render audit. GPU timing was unavailable and
explicitly waived; this is not a GPU-budget proof. It also predates the snapshot commit.

The later editor diagnostic `BuildReports/Mvp01ProfilerEditorDiagnosticLatest.json` reports total
p95 `19.1631 ms`, GPU p95 `10.0344 ms`, and fails CPU, foot-contact, pipeline/camera, and overall
gates. It is non-authoritative editor evidence but cannot be cited as a pass.

`BuildReports/NativeWindows.json` reports a successful Windows build but also 186 warnings. That
fails the repository's zero-warning acceptance rule. No current-HEAD performance, warning-free
build, or complete PlayMode pass is claimed here.

Fresh evidence produced through the connected Unity Test Runner from the tested implementation is:

- `BuildReports/FoundationWorkingTreeEdit-20260831.xml`: full EditMode `586/587`; only
  `FinalBuildEvidenceAndCapabilityMatrixAreGreen` fails because the older Native Windows report
  contains 186 warnings.
- `BuildReports/FoundationWorkingTreePlay-20260831.xml`: targeted foundation PlayMode `2/2`,
  covering the rebuilt helmet/tail/belt configuration and rejection of closer dynamic debris as
  character support.

## Important entry points

| Need | Entry point |
|---|---|
| Open representative scene | `Assets/Elemental/Content/Scenes/EarthCoreSlice.unity` |
| Rebuild generated Earth slice | Menu `Elemental/Setup/Create M3 Earth Core Slice`; `M3EarthCoreSetup.Configure()` |
| Full local EditMode/PlayMode | `Scripts/Test-Unity.ps1` with `UNITY_EDITOR_PATH` or `-UnityPath` |
| Focused MVP gates | `Assets/Elemental/Tests/EditMode/Mvp01FocusedTestLauncher.cs` menus |
| Accepted evidence/captures | `Assets/Elemental/Authoring/Editor/Mvp01EvidenceRunner.cs` and `Mvp01SceneCapture.cs` |
| Windows/macOS/Web builds | `Assets/Elemental/Authoring/Editor/ElementalBuildPipeline.cs` |
| Project validation | Menu `Elemental/Diagnostics/Validate Project`; `ElementalProjectValidator.cs` |
| Performance budgets | [`Docs/performance-budgets.md`](performance-budgets.md) |
| Third-party inventory | [`Docs/THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md), `Packages/manifest.json` |

## Known unknowns

- Which focused and repository-wide gates pass after rebuilding the scene at snapshot HEAD.
- Whether the new classified support policy will be wired into runtime support selection, and
  which existing owner it will replace or constrain.
- Current-head GPU p95/frame pacing on the reference PC and on minimum target hardware.
- Whether the 55.1 m world and current Broken Crown placement are accepted across every physical
  input, KO/respawn, fracture/repair, surf, camera, and visual scenario.
- Player evidence: no fresh external playtest proves comprehension, feel, dominant strategies,
  or that the repeatable duel is enjoyable without developer explanation.
- Whole-game persistence/recovery, production online transport, accessibility/controller matrix,
  supported hardware floor, store/business model, and release window.
- Whether all present third-party/AI packages and generated/imported assets pass final licensing,
  provenance, build-size, and platform review.
