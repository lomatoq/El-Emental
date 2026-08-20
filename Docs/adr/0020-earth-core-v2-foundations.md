# ADR 0020: Earth Core V2 foundation ownership

Status: accepted
Date: 2026-08-14

## Context

The V2 audit found four coupled presentation failures: wall emergence animated a Rigidbody root into the planet, daytime retained the star skybox, the exploration camera hid near-ground techniques, and Git LFS animation data could silently degrade into invalid presentation assets. Adding new techniques before fixing these ownership boundaries would multiply regressions.

## Decision

- `EarthWall` keeps its physics root at the final chord pose throughout emergence. A child named `VisualEmergenceRoot` owns lift, scale and tremor.
- The intact collider is disabled during emergence, receives a foundation-aware inset and is validated with non-allocating overlap plus `Physics.ComputePenetration`. The completed wall remains anchored/kinematic until a magic manipulation explicitly activates dynamic physics.
- `EarthSkyController` is the only owner of `RenderSettings.skybox`. `CelestialSystemBehaviour` keeps ephemeris/light/scaled-space authority and supplies its snapshot to the sky controller.
- Day sky is a guaranteed zenith/horizon gradient; star visibility reaches zero in daylight. Dusk and night are shader/profile states of the same sky owner.
- `EarthCameraDirector` and `PlanetCameraRig` remain the only camera owners. A pure `EarthCameraPointerIntentSolver` adds soft dead-zone cursor composition; the rig adds bounded focus speed and spring reset for teleports/extreme local-up changes.
- `EarthAnimationAssetValidator` fails before play when FBX files are missing/LFS pointers, clips have invalid durations, the Humanoid Avatar is invalid, or the controller/profile wiring is incomplete.

## Consequences

Wall emergence cannot inject contact momentum by transform-driving a Rigidbody. A standing wall becomes dynamic in the same magic interaction that applies its first force. Sky ownership is explicit and testable. Camera input remains normalized through `EarthInputAdapter`; no second device reader or camera controller is introduced. Animation failures become actionable build/editor errors rather than a visually static character.

## Evidence

- EditMode: 195/195 passed.
- PlayMode: 66/66 passed, including physics-root drift, collider penetration, daylight fallback, camera composition and runtime Humanoid locomotion tests.
- Windows Development/Release: succeeded with zero warnings/errors.
- QA: `BuildReports/EarthV2/PostPhase1/` records day, wall, locomotion/cast, dawn, night and earth-material scenarios.

## Rollback

- Wall: move the intact renderer back to the root and restore the previous emergence method; this also restores the known snap and is therefore only a bisect path.
- Sky: remove `EarthSkyController` and let the celestial behaviour own the star skybox again.
- Camera: reset `EarthCameraProfile.asset` and omit pointer-intent offsets; the local-gravity rig remains intact.
- Animation gate: remove the editor validator/test assembly reference; imported assets and controller remain unchanged.
