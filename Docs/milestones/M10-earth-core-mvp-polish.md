# M10 Earth Core MVP Polish

## Self-armor camera and plate variety follow-up (2026-08-19)

- Cinemachine obstacle avoidance excludes the controlled-magic layer, so compact
  armor, dome, orbit and released plates cannot push the camera through the hero.
- A pure sightline solver and render-only pre-cull adapter hide only armor renderers
  that cover the protected head/chest corridors. Colliders, mass, interception and
  target handles remain active.
- Compact armor no longer replaces the Humanoid's authored materials. The visible
  hero stays identifiable underneath the physical shell.
- The repeated regular tile was replaced by deterministic convex geological plates
  with varied side counts, hulls, chips, crown offsets, aspect ratios and bounded
  per-renderer stone variation. Matching convex colliders retain valid outward normals.
- Fresh isolated Unity evidence: `238/238` EditMode in `2.204 s` and `92/92`
  PlayMode in `157.525 s` (`BuildReports/CameraArmor-20260819-Edit.xml` and
  `BuildReports/CameraArmor-20260819-Play.xml`). Focused camera and armor gates also
  pass independently.

## V3 editor-first extension (2026-08-15)

- `EarthActionRouter` now owns the complete overlapping Earth input grammar, including the deterministic Shift+Space wave/resonance chord and Shift+W surf.
- Production walls bake to 40 true 3D convex cells and 126 bonds. Platforms prepare 28–48 true 3D cells and release local damage while foundation cells remain supported.
- Unified target capabilities cover structures, pieces, rocks, pillars, armor and resonance projectiles; fracture activation is captured by the live MMB session in the same physics tick.
- Surface drawing locks one face frame. Stable side faces create perpendicular attached walls; vertical faces reject platforms.
- Added resonance volley, a baked 64-segment bone-aware Humanoid stone shell, and a kinematic impact-capable earth plough.
- `EarthPolishLab` is the V3 golden-path scene. Validation stays in Unity Editor/Test Runner; no Windows build is required for this iteration.
- Architecture rationale and rollback are recorded in [ADR 0025](../adr/0025-earth-core-v3-volumetric-grammar.md).

### Final V3 evidence

- Full EditMode after armor packing/camera follow-up: `237/237` passed in `2.320 s` (`TestResults/FullEditArmorFix.xml`).
- Full PlayMode after armor packing/camera follow-up: `91/91` passed in `157.995 s` (`TestResults/FullPlayArmorFix.xml`).
- Focused runtime gates cover the continuous 64-piece external armor shell, outward normals, inter-plate packing, head coverage, armor-safe camera collision, aimed single-plate and remaining-shell volleys without caster recoil, production-player surf carry, two-click quick stone, resonance dome fire, and attached/intersecting structures.
- `EarthPolishLab` enters Play Mode with zero runtime exceptions in `Editor.log`. Automated runtime geometry gates verify the visible hero inside the armor gaps, a filled upper-hemisphere resonance gradient, camera exclusion for controlled formations, and `Shift+W` movement on the enlarged physical plough. The local DX11 editor Game View capture remains white and is therefore not claimed as visual acceptance evidence.
- Windows Development/Release builds were intentionally not produced; V3 acceptance is Editor-only by decision.

Status: complete
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
- V3 enables the lab in Editor Build Settings so PlayMode tests can load the golden-path scene. Shipping build scripts must explicitly exclude it; player builds are not a V3 acceptance gate.
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

## Task 5 — contextual input grammar

Status: complete

- `EarthInputAdapter` is now the only runtime Input System boundary. The canonical grammar is LMB `BendPrimary`, RMB `BendForce`, MMB `BendField`, Shift `BendModifier`, Space `JumpOrStomp`, wheel `BendParameter`, and Escape `Cancel`.
- `PlanetInputReader` and `MagicInputController` no longer query physical devices. Ability digits are isolated behind editor/development forcing and Earth has no shipping hotbar dependency.
- Earth strokes are viewport-normalized, duplicate-filtered, lightly smoothed and resampled to 32 points. The feature vector includes path/direct length, straightness, direction, curvature, signed area, closure, speed, duration, aspect, self-intersections and a quantized digest.
- The template recognizer returns best/second-best, confidence and ambiguity. Source/session/button context gates template recognition first; invalid, obstructed, over-mass, low-confidence and ambiguous input reject without simulation mutation.
- Resolved replay input contains source ID/generation, quantized geometry, charge, wheel parameter, modifiers, ticks, seed and digest; raw pointer motion is not authoritative.
- `EarthPreviewPresenter` owns line geometry. The HUD exposes the active wheel channel and reticle states for source, ambiguity, invalid and valid commit.
- Profiling scopes cover `Elemental.Earth.Gesture.Sample`, `Elemental.Earth.Gesture.Recognize`, and `Elemental.Earth.Intent.Resolve`.
- Architecture decision: `Docs/adr/0016-contextual-earth-input-grammar.md`.

### Evidence

- EditMode: 170/170 passed, including 720p/1080p/1440p/ultrawide equivalence, fixed sample count, wall/arc/closed topology, N-best ambiguity, context priority, quantization and a 256-iteration `0 B` recognition loop.
- PlayMode: 64/64 passed in 125.592 s. Invalid input preserves the authoritative command count; the existing 200-case camera/curvature corpus keeps exact preview/commit geometry hashes.
- Windows Development: 171,058,210 bytes in 57.092 s, 0 warnings/errors (`BuildReports/NativeWindows.json`).

## Task 6 — six Earth techniques

Status: complete

- Grip, Wall, Platform, Pillar, Ground Wave and Repair now share a pure command, a closed rejection vocabulary and the same Intent → Anticipation → Release → Impact → Settle lifecycle.
- A single authored presentation profile carries per-technique timing, pose effort/bracing, camera impulse/look-ahead and dust/chip/rumble channels without becoming physics authority.
- RMB plus an LMB terrain sweep casts the bounded moving-crest ground wave through the normalized contextual recognizer. Physical targets still win source selection and remain grippable.
- The wheel is contextual: wall height / Shift thickness, platform height / Shift tilt, held-object distance and ground-wave sector width. Wall thickness is carried into the authoritative command and committed collider geometry.
- Existing physical systems remain authoritative for mass-aware Grip/Throw, cause-driven Wall/Platform fracture, moving support, grounded Pillar, bounded wave columns and provenance-aware Repair.
- Architecture decision: `Docs/adr/0017-six-earth-technique-contract.md`.

### Evidence

- EditMode: 174/174 passed in 0.818 s, including all six context routes, explicit rejection reasons, parameter round-trips and lifecycle boundaries.
- PlayMode: 64/64 passed in 126.040 s, preserving the complete fracture, MMB, moving-platform, pillar/wave and recovery suite.
- Windows Development: 171,075,122 bytes in 62.964 s, 0 warnings/errors (`BuildReports/NativeWindows.json`).

## Task 7 — character feel

Status: complete

- Casts now use simulation-facing Acquire → Root → Load → Strike → Sustain → Recover phases with startup, active, recovery and contact timing in ticks.
- Immutable `EarthPoseIntent` data carries family, phase, local direction, target, effort, brace, stance width, pelvis compression and torso twist. Mass, acceleration, charge and support feed a bounded pure solver.
- The KayKit Humanoid receives local-up foot probes, stance widening, planted-foot lock windows, surface-normal foot rotation, pelvis compensation and torso follow-through. Foot locks release when airborne.
- Wall, fragment, held-body and pillar typed events align the presentation Strike with the authoritative event tick. Stomp dust is presentation-only and echoes that tick.
- `PlanetMotorFeelProfile` adds separate acceleration/deceleration, turn response, coyote time, jump buffer, slope/traction, ground snap and cast/brace speed limits. Telemetry exposes these windows without giving animation authority over gameplay.
- `EarthMagicPoseDriver` remains only the primitive fallback; the Humanoid path installs `EarthCharacterPoseController`.
- Architecture decision: `Docs/adr/0018-earth-character-pose-authority.md`.

### Evidence

- EditMode: 180/180 passed in 1.165 s, including tick-phase boundaries, mass-scaled effort, airborne brace rejection, foot-lock stability, coyote/buffer overlap and bounded pelvis compensation.
- PlayMode: 64/64 passed in 125.851 s. The EarthCore scene contains a valid in-place Humanoid plus its embodied local-up pose controller; moving-platform, pillar and 100-cycle ragdoll recovery coverage remains green.
- Windows Development: 171,098,682 bytes in 57.851 s, 0 warnings/errors (`BuildReports/NativeWindows.json`).

## Task 8 - camera director

Status: complete

- `PlanetCameraRig` remains local-gravity placement authority while `EarthCameraDirector` selects Explore, Aim, BendLight, BendHeavy, DrawStructure, HoldMass, Airborne, Impact and Recovery presentation profiles.
- Focus is a clamped weighted blend of player chest, aim, held mass and construct midpoint. Drawing shifts the shoulder away from the structure; Q performs an explicit shoulder swap through `EarthInputAdapter`.
- The authored exploration frame is 5.9 m away, 2.15 m high and 64 degrees, with a strong forward lead. Heavy states reduce positional lag and increase rotational stability.
- Spherecast occlusion now pulls inward quickly, waits through clear-space hysteresis and releases slowly. Character and held-body colliders are ignored.
- Camera impact uses deterministic high/medium/low bands with bounded roll. Profile sliders control shake, lag and FOV motion; reduced motion suppresses FOV animation and strongly attenuates shake/lag.
- The EarthCore scene serializes its director and editable profile. No runtime migration component is created.
- Architecture decision: `Docs/adr/0019-state-aware-local-gravity-camera.md`.

### Evidence

- EditMode: 185/185 passed in 1.471 s, including state priority, clamped weighted focus, pull-in/release hysteresis, shoulder stability and reduced-motion behavior.
- PlayMode: 64/64 passed in 125.879 s. Close framing, forward lead, explicit profile wiring and the complete Earth physical suite remain green.
- Windows Development: 171,119,134 bytes in 58.631 s, 0 warnings/errors (`BuildReports/NativeWindows.json`).

## Task 9 - hardening

Status: complete

- Consecutive `EarthImpactEvent` presentation work is accumulated without allocation and flushed once per frame. Dust and chips have explicit frame caps, while the strongest kinetic energy and an impulse-weighted contact frame preserve the readable physical cause.
- All twelve profiler boundaries required by the execution brief are present and guarded by an EditMode audit: fracture damage/islands, repair select/order/solve/weld, proxy switch, gesture sample/recognize, intent resolve, camera direct and feedback route.
- Existing deterministic replay, 100-cycle repair/ragdoll, 200-case preview/commit corpus, pool stress and capability degradation suites remain green. Authored scene wiring is enforced so the camera does not depend on migration code.
- NativeHigh, NativeLow and WebLab each ran 216,000 budget ticks with `0 B` managed allocation, 360 presentation degradations, no canonical rule change and a passing result.
- Seven fresh standalone Release baselines cover dawn, ground wave, platform, gravity grip, meteor impact, Mage casting and reassembly. PNG integrity, minimum size and hashes are recorded in `Docs/qa/m10-final-evidence.md`.

### Final evidence

- Project validation: 10 scenes, 14 abilities and 3 capability profiles.
- EditMode: 191/191 passed in 1.225 s, including profiler, build-report, visual-baseline and steady-state allocation gates.
- PlayMode: 64/64 passed in 125.823 s, preserving fracture, reassembly, MMB, moving support, camera, wave, meteor and Humanoid recovery behavior.
- Windows Development: 171,123,394 bytes in 57.941 s, 0 warnings/errors.
- Windows Release: 106,003,631 bytes in 60.678 s, 0 warnings/errors.

The Earth Core MVP polish sequence is complete. Further art/content work can tune the existing profiles and replace presentation assets without changing the established simulation contracts.

## V2 Phase 0/1 foundations

Status: complete (2026-08-14)

- Recorded a standalone visual baseline at audited commit `7bdce0dac855007ec14e1f7114a6af37e563614a`; evidence and SHA-256 hashes live in `BuildReports/EarthV2/Baseline/README.md`.
- Added a fail-fast KayKit animation gate covering Git LFS payloads, clip duration, Humanoid mapping, Animator parameters/layers/motions/mask and presentation profile wiring. The gate found and fixed a missing upper-body `AvatarMask`.
- Refactored wall emergence to a fixed physics root plus animated `VisualEmergenceRoot`, delayed safe collider activation and explicit magic-time dynamic activation. Profiler scopes: `Elemental.Earth.Wall.Emergence` and `Elemental.Earth.Wall.ColliderValidation`.
- Added the single-owner profiled `EarthSkyController` with blue day gradient, dusk/night blend, sun glow and daylight star suppression.
- Raised Camera V2 into a 7.4 m / 3.85 m / 60 degree baseline, added pure normalized pointer intent, ground near/far mapping, focus speed limits and spring reset behavior.
- Proved real KayKit locomotion data, valid Humanoid/runtime controller state and root-motion-off behavior in PlayMode.

### Evidence

- EditMode: 195/195 passed in 1.300 s.
- PlayMode: 66/66 passed in 127.972 s.
- Windows Development: 171,138,506 bytes in 76.496 s, 0 warnings/errors.
- Windows Release: 106,015,551 bytes in 55.817 s, 0 warnings/errors; EXE SHA-256 `D05C5848F7CC2EB32FF6016F54762EE3C666A69B862BE297DF2390830C1EA8CF`.
- Standalone D3D11 post-pass frames in `BuildReports/EarthV2/PostPhase1/` confirm a blue day, a graded dawn, readable stars/planets at night, elevated ground framing and stable wall presentation.
- `FrameTimingManager` returned no samples in the automated Release capture; no CPU/GPU claim is made for this phase. The cold-start voxel queue peak was 65.22–70.44 ms with zero pending work and is not steady-state frame time.

Next dependency: Phase 2 intent resolver and shared Earth-surface abstraction. Quick cast, Wave V2 and Surf remain intentionally deferred.

## V2 Phase 2 semantic intent and shared surfaces

Status: complete (2026-08-14)

- Fixed the production character rescue regressions before expanding systems: grounded adhesion now accelerates inward, ordinary locomotion releases foot IK, the upper-body cast layer receives its authored weight, the locomotion blend tree uses `0/2/6 m/s` thresholds, and character colliders cannot become vector-field targets.
- Increased orientation authority to a critically damped `60/12/140` production frame and moved the camera's nearest ground focus from `3.5 m` to `4.4 m`, keeping the Mage responsive and the play space visible.
- Added the fixed-priority `EarthActionIntentResolver` and one normalized `EarthGestureFrame` capture at the existing single input boundary. Cancel, landing wave, surf, self-wave, target manipulation, quick intent, full bend, MMB and pillar ownership are deterministic.
- Added stable-generation Earth surface contracts plus explicitly injected planet, platform and wall-top providers. Pillar and landing cushion now select the nearest capable support and account for its velocity.
- Replaced the old 3 m platform ceiling with an 8 m soft / 22 m hard budget; cost increases and stability decreases with height and footprint aspect ratio.
- Repaired the authored `EarthGestureProfile` MonoScript binding by moving the ScriptableObject into its correctly named file. A fail-fast EditMode asset test prevents another silent build-time loss.
- Tuned wall vector-field leverage against standalone physics rather than only synthetic fixtures. A full hold moves the production wall `2.294 m`, while the caster remains untargeted and moved `0.421 m` from ordinary world physics.
- Architecture decision: `Docs/adr/0021-semantic-earth-intents-and-shared-surfaces.md`.

### Evidence

- EditMode: 206/206 passed in 1.521 s; focused semantic/surface suite 9/9 passed.
- PlayMode: 70/70 passed in 130.536 s; focused surface runtime suite 3/3 and character rescue suite 3/3 passed.
- Windows Development: 171,173,794 bytes in 45.380 s, 0 warnings/errors.
- Windows Release: 106,043,223 bytes in 55.289 s, 0 warnings/errors; EXE SHA-256 `D05C5848F7CC2EB32FF6016F54762EE3C666A69B862BE297DF2390830C1EA8CF`.
- Standalone D3D11: `BuildReports/EarthV2/Phase2/WallPush.png` and `MageWalk.png`; both exited 0. Wall-push telemetry is recorded in the adjacent log.
- Cold-start voxel queue peaks were `70.62-71.50 ms` with zero pending work at capture; these remain startup debt, not a steady-state frame percentile.

Next dependency: Phase 3 quick stomp-stone-punch. Wave V2 and Surf remain deferred to their ordered backlog phases.

## Camera and character rescue pass

Status: complete (2026-08-14)

- Replaced the unstable camera transform writer with Cinemachine 3.1.7 using a
  spherical local-up frame, independent pitch pivot, third-person obstacle solver,
  explicit camera priority and an immediate 60-degree startup composition.
- Replaced the hat-obscured Mage presentation with the KayKit Knight subset. Helmet,
  visor and cape meshes are disabled for a clean back silhouette; locomotion validation measures movement of
  the enabled rendered leg meshes, not only hidden skeleton state.
- Fixed production quick wall selection so an exact camera ray beats the broad
  target-assist volume. A click-release shove now locks and moves the visible wall
  instead of a nearby dummy or the caster.
- Retuned wide-wall vector leverage from `24x` to `38x`; the standalone visual-QA
  contract requires a charged production wall to clear 2 m while caster drift stays
  below 0.75 m.
- Raised the nonlinear charged pillar range to `12-25 m/s` velocity change, retaining
  the existing ease-out charge curve and stronger long-hold tail.
- Architecture decision: `Docs/adr/0022-cinemachine-spherical-camera-rescue.md`.

### Evidence

- EditMode: 212/212 passed.
- PlayMode: 73/73 passed, including visible gait, spherical camera composition,
  production camera-ray wall shove, pillar mobility, fracture and reassembly.

## Gesture and locomotion rescue

- Replaced the obscuring hooded/long-haired presentation with a removable-helmet
  KayKit Knight subset; helmet, visor and cape are disabled for a clean camera
  silhouette. Humanoid translation DoF is enabled on the shared source avatar.
- The locomotion golden path now bakes the visible skinned leg meshes every frame
  and proves gait deformation while a real `PlanetMotor` command moves the actor.
- RMB hold is the low-speed precision vector field, quick tap is a compact pulse,
  and a viewport-normalized swipe/release is the full-speed projectile launch.
- Active wave pillars implement `IEarthPhysicalTarget`, detach from the wave pool
  when grabbed and remain angularly damped under radial gravity.
- MMB circles add signed phase control: clockwise progressively reweaves a wall;
  counter-clockwise progressively releases deterministic bonds. Partial results
  remain physical when the button is released.
- Architecture decision: `Docs/adr/0023-vector-flick-and-circular-earth-grammar.md`.
- Replaced the last uniform wall cut with a 24-cell, 61-bond production bake.
  Deterministic chipped shared edges produce five collider-vertex families and a
  broad chunk-volume distribution, preserving full wall depth without thin strips.
- Platforms register an overlapping rider immediately, keep collision ignored only
  until the rider is above the rising top plane, and carry the complete physical
  puppet. Platform support remains valid during emergence, including charged pillar
  launch and inherited support velocity.
- Final gesture-rescue regression: EditMode 215/215; PlayMode 76/76. The standalone
  Release QA measured a 1.317 m platform lift with first-step rider capture and no
  fracture/ragdoll; the charged wall shove travelled 2.675 m with 0.297 m caster drift.
- Platform/pillar follow-up: support carry now preserves relative walking velocity,
  collision pairs restore after the safe emergence envelope, and an external launch
  cannot be reacquired as a rider. Jump pillars retreat before the return arc while
  every cosmetic chip continues ballistic shrink to deactivation.
- Final follow-up regression: EditMode 216/216; PlayMode 76/76. Windows Release built
  106,776,381 bytes with 0 warnings/errors. Standalone D3D11 measured a 1.343 m lift,
  0.698 m platform walk, successful return with 0.072 m descent clearance, retired
  pillar and 0 active chips.

## Web wave, quick cast and armor iteration

Status: complete (2026-08-15)

- Replaced production wave boxes with six instanced 3-8-sided geological mesh families
  driven by six radial/spiral seeded topologies. Physics remains a bounded 96-cell pool.
- Platforms now prefracture into 18-28 exact contour-clipped Voronoi meshes and repair
  through the same controller contract as walls. Collider handoff is piece-first.
- Added viewport-normalized CW repair / CCW disassembly, 0.42-second two-click quick
  stone, direct wall projectile-flick velocity and an oriented projectile sweep guard.
- Added `Shift+MMB` armor with wheel-controlled body/dome/orbit phases and confirmed
  radial overscroll release. Space is not consumed by armor or wave release.
- Architecture decision: `Docs/adr/0024-web-wave-quick-cast-and-armor-grammar.md`.
- V3 supersedes the earlier 2D platform cut with true volumetric wall/platform cells,
  routes the complete input grammar through `EarthActionRouter`, and closes this
  iteration with the `235/235` EditMode and `89/89` PlayMode gates recorded above.

## Curated Mixamo motion continuity pass

Status: complete (2026-08-20)

- Imported the provided X Bot motion library and forced every animation-only FBX to
  reuse the canonical `X Bot.fbx` Humanoid Avatar. Independent auto-T-pose inference,
  the direct cause of inconsistent hip/knee retargeting, is rejected by the asset gate.
- Replaced the temporary `Injured Idle` locomotion slot with an upright neutral
  subclip; the crouch transition remains a separate non-looping motion.
- Rebuilt locomotion as `Turn × Speed`, added explicit surf/fall/landing states, and
  removed the AnyState surf re-entry that restarted clips while they were playing.
- Rebuilt earth casting as an eleven-way normalized Direct BlendTree. One-hot weights
  crossfade only the requested motions; `EarthMotionTime` holds sustained techniques
  at an authored pose without stopping the base gait.
- Removed global Animator slowdown and pelvis `bodyRotation` writes. Stationary casts
  can brace, while locomotion input releases both foot locks immediately and leaves
  the MMB session active.
- Platform pluck cannot force-complete emergence, armor wheel states fly through a
  damped formation solver, and retired wave cells stay buried for two physics ticks
  before pool return.
- Mapping: `Docs/EARTH_HUMANOID_MOTION_MAP.md`; architecture:
  `Docs/adr/0027-curated-humanoid-motion-tree.md`.

### Evidence

- Isolated Unity Editor compile/import/controller authoring completed with exit code 0.
- Full EditMode: `340/340` in `38.729 s`.
- Full PlayMode: `99/99` in `168.776 s`.
- Windows builds were intentionally not produced for this Editor-only iteration.
