# ADR 0019: State-aware local-gravity camera direction

Status: accepted
Date: 2026-08-13

## Context

The Earth slice already had a stable local-up camera, but one fixed composition could not keep the caster, a distant aim point, held mass and a drawn structure readable at the same time. The former default also left the character too small. Replacing the rig would risk breaking spherical gravity, heading ownership and the free cursor used by Earth gestures.

## Decision

- `PlanetCameraRig` remains the only component that places and rotates the camera in the local gravity frame. `EarthCameraDirector` supplies presentation targets and never mutates gameplay state.
- Explore, Aim, BendLight, BendHeavy, DrawStructure, HoldMass, Airborne, Impact and Recovery use authored state profiles with entry/exit hysteresis.
- Focus is a bounded weighted blend of upper torso, aim point, held mass and active construct midpoint. Explore leads the character while DrawStructure moves the shoulder away from the stroke.
- Default exploration distance is 5.9 m with a 2.15 m height and 64 degree FOV. State transitions retain enough planet curvature without reducing the caster to an icon.
- Occlusion uses a non-allocating sphere cast, immediate inward response, delayed slower release and owned/held collider filtering. Q swaps shoulder through the canonical input adapter.
- Impact response combines deterministic high, medium and low frequency bands. Shake, lag and FOV motion are independently scaled; reduced motion suppresses FOV changes and strongly reduces shake and lag.
- `Elemental.Earth.Camera.Direct` profiles the director hot path. Camera settings live in `EarthCameraProfile` and remain editable through Elemental Suite.

## Consequences

The camera reads the active Earth action without becoming simulation authority. Pure state, focus, shoulder and occlusion solvers are replayable and testable without a scene. The authored EarthCore scene owns its director explicitly; no runtime migration component is added.

## V2 amendment (2026-08-14)

The exploration composition is now an elevated three-quarter view: 7.4 m distance, 3.85 m height and 60 degree FOV. `EarthCameraPointerIntentSolver` maps normalized pointer position through a soft central dead zone into bounded horizontal, vertical and ground-distance bias. `PlanetCameraRig` clamps focus velocity and resets springs after teleports or extreme local-up discontinuities. The director/rig ownership decision is unchanged; see ADR 0020 for the cross-system foundation pass.

## Rollback

Disabling `EarthCameraDirector` restores the existing `PlanetCameraRig` framing. The local-gravity placement, occlusion cast and gameplay input contracts remain intact.
