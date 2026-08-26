# Project Memory — El-Emental

Updated: 2026-08-26

Current gate: Earth Core V3, self-armor camera/readability pass

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

## Current architecture truth

- Gameplay authority: Core/Simulation data and Runtime physics adapters.
- Input: `EarthInputAdapter` is the only device action reader; pointer is normalized before camera intent.
- Presentation: state-based director, local-up camera rig, Humanoid presentation and shader-driven scaled space.
- Critical evidence: `EarthAnimationAssetValidator`, camera EditMode tests, wall/sky/locomotion PlayMode foundations.

## Next executable slice

Outcome: visually inspect the compiled self-armor pass in the live Game View while
keeping the full V3 Earth grammar and the platform → walk → pillar loop protected.

Definition of done:

- compact/dome/orbit armor preserves a useful gameplay composition through motion;
- plate silhouettes read as different stones without exposing or repainting the hero;
- future changes keep the 238 EditMode / 92 PlayMode baseline green;
- no expansion is accepted if it reintroduces camera collapse, rider velocity
  cancellation or stale collision-ignore pairs.

Non-goal: no new ability is added inside this camera/readability patch.
