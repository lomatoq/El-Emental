# Project Memory — El-Emental

2026-09-04 outer column integration: the artist-edited OuterStoneRing working
blend is authoritative. Seven columns remain separate from Broken Crown in both
asset paths and scene hierarchy; 85 repairable cells plus eight authored loose
stones share arena materials/runtime. Preserve native FBX Y-up, frame-local rest
poses, 2.5 m final spherical-ground clearance and buried caps. Repair restores the
current damaged silhouettes. Focused 2/2 Edit + 2/2 Play passed; details in
`Docs/OUTER_STONE_RING.md`. Do not regenerate the artist's Blender meshes.

Updated: 2026-08-30

Current gate: M11 animation contact and Broken Crown rendering rehabilitation

2026-09-02 animation handoff: the dirty working tree contains the feature-flagged EAMM production pass (single animation graph, cached per-bone inertialization, stable semantic catalog, 2D locomotion, front/back recovery, bounded slope and magic reach). Existing arena/material/scene edits were preserved and the generated scene was not rebuilt. Runtime/visual/30-60-120/profile acceptance is deliberately pending because the implementation request excluded PlayMode/test runs.

Baseline commit: `7bdce0dac855007ec14e1f7114a6af37e563614a`

## Product truth

- Promise: expressive earth magic must feel heavy, readable and physically causal.
- Current signature: wall raise → physical impact/fracture → grip/release of persistent pieces.
- Visual thesis: warm stylized stone, blue atmospheric daylight, elevated technique-aware camera.
- Current cut line: do not add another spell until the existing V3 grammar is
  readable through the gameplay camera and remains green in the Editor gate.

## Current golden path

- Launch: `Assets/Elemental/Content/Scenes/EarthPolishLab.unity` in Unity Editor.
- Scenario: explore, assemble compact armor, open it into dome/orbit, fire a plate,
  then draw/fracture/repair a structure.
- Expected: camera never collapses into armor; head and chest remain readable;
  stones are visibly varied and physical; the Humanoid retains its authored materials.
- Reset: restart scene/player.

## Latest evidence

- Animation/arena rescue is implemented but not yet accepted. Unity's own Roslyn
  response files compile the current Simulation, Presentation, Authoring.Editor,
  EditMode and PlayMode sources with zero diagnostics. The production locomotion
  test now writes per-render-frame foot/knee/pelvis/support telemetry. Live Unity
  test reports and rebuilt captures are still required; see ADR 0033.

- M11 lookdev focused EditMode gate: `139/139` passed on Unity 6000.5.7f1,
  including five deterministic clarity-tier/hysteresis tests. Runtime MCP
  verification confirms exactly one directional light, legacy fog disabled,
  clarity controller and bounded light motes present, both custom shaders
  supported with zero shader compiler messages. Evidence:
  `BuildReports/Mvp01FocusedEdit.json` and
  `BuildReports/Mvp01CombatLatest.png`.
- The latest long editor performance session on the shared checkout records CPU
  p95 `9.30694 ms`, GPU p95 `3.50618 ms`, main-thread p95 `7.05697 ms` and
  render-thread p95 `1.82327 ms` across 10,950 samples. This is a whole-frame
  shared-scene sample, not an isolated atmosphere-only cost measurement.
  Evidence: `BuildReports/Mvp01PerformanceLatest.json`.
- MVP 0.1 input rescue: `109/109` focused EditMode and `12/12` focused
  PlayMode passed on Unity 6000.5.7f1. The PlayMode gate now drives near and
  distant wall strokes through the real 80 ms dual-button input buffer, proves
  staged terrain extraction produces and launches its reserved rock, and keeps
  RMB telekinesis from selecting the caster. Reports:
  `BuildReports/Mvp01FocusedEdit.json` and `BuildReports/Mvp01FocusedPlay.json`.
- Latest accepted 720-frame session: total-frame p95 `11.3045 ms`, CPU p95
  `11.3798 ms`, GPU p95 `3.1386 ms`, solid-platform acquisition peak `2.6504 ms`
  and fracture slice peak `0.5188 ms`. `BuildReports/Mvp01RescueCurrent.json`
  reports success; the Unity Console is clean.

- Baseline: 191/191 EditMode and 64/64 PlayMode passed on Unity 6000.5.7f1.
- Baseline Release: successful, 106,003,631 bytes, 0 warnings/errors.
- Captures: `BuildReports/EarthV2/Baseline/` records day, wall, legacy wave, platform and locomotion/cast.
- Observed baseline failures: black daylight background, low over-back camera, slab-like wall dominance, gapped pillar wave, weak locomotion readability.
- Phase 1 final: 195/195 EditMode and 66/66 PlayMode pass; Development and Release build with 0 warnings/errors.
- Phase 1 visual: blue day, graded dawn, readable night and elevated ground framing captured in `BuildReports/EarthV2/PostPhase1/`.
- Performance caveat: standalone `FrameTimingManager` yielded no samples; cold-start voxel queue peak 65.22–70.44 ms is not steady-state frame time.
- Platform/pillar rescue: 216/216 EditMode and 76/76 PlayMode pass. Windows Release is 106,776,381 bytes with 0 warnings/errors.
- Standalone D3D11 golden path: immediate platform capture, 1.343 m lift, 0.698 m commanded walk, pillar-jump return with 0.072 m descent clearance, retired pillar and zero active chips. Evidence: `BuildReports/PlatformPillarRescueRendered/`.
- Current self-armor pass: 238/238 EditMode and 92/92 PlayMode on Unity
  6000.5.7f1. Evidence: `BuildReports/CameraArmor-20260819-Edit.xml` and
  `BuildReports/CameraArmor-20260819-Play.xml`.

## Accepted decisions

- Follow the V2 package order: Phase 0 evidence and Phase 1 foundations before new techniques.
- Keep the existing `EarthCameraDirector` + `PlanetCameraRig` split; add pure pointer intent and reset/motion limits.
- Stable wall physics root with a child `VisualEmergenceRoot`; no transform-driven Rigidbody emergence.
- One skybox owner (`EarthSkyController`) driven by the existing celestial snapshot.
- Controlled-magic colliders never participate in Cinemachine obstacle shortening.
  A render-only head/chest sightline guard may hide occluding armor renderers, but
  never disables their physics or target handles.
- Compact armor is a physical external shell. It does not replace or recolour the
  animated character, and its plate family must contain real convex shape variation.
- Lighting/readability uses one persistent sun, one depth-aware RenderGraph
  atmosphere blit with a two-sample sky-only cloud cue and a single bounded
  light-mote renderer. Depth of field is semantic: Gaussian in native Explore,
  off during unsafe motion/Web, and Bokeh only for stable deliberate NativeHigh
  states. See ADR 0032.

## Current architecture truth

- Gameplay authority: Core/Simulation data and Runtime physics adapters.
- Input: `EarthInputAdapter` is the only device action reader; pointer is normalized before camera intent.
- Presentation: state-based director, local-up camera rig, Humanoid presentation and shader-driven scaled space.
- Critical evidence: `EarthAnimationAssetValidator`, camera EditMode tests, wall/sky/locomotion PlayMode foundations.

## Next executable slice

Outcome: rebuild the generated EarthCoreSlice and prove the animation/contact,
arena seating and rendering rescue in the live Game View while keeping the full V3
Earth grammar and platform → walk → pillar loop protected.

Definition of done:

- raw `AnimationArenaTelemetryLatest.csv/json` proves alternating single-foot
  locomotion anchors, bounded applied-IK steps and no simultaneous gait locks;
- every Broken Crown loose/cosmetic prop is within the measured floor/sphere seating
  tolerance and the player spawn uses the local crater-floor hit;
- the rebuilt camera serializes SMAA High; arena material, High soft-shadow filter
  and clean contact-AO profile pass their asset contract;
- fresh 1920×1080 capture visibly removes vertical stripe fringe, floating rocks and
  jagged silhouettes without flattening real cast shadows;
- focused EditMode, production locomotion, Broken Crown PlayMode and the representative
  profiler are fresh and green.

Non-goal: no new ability, second animation owner, runtime neural animation, motion
blur or TAA adoption is added inside this rescue. TAA remains a later motion-vector
validation, with SMAA as the deterministic fallback.
