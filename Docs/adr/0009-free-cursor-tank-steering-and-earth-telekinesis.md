# ADR-0009: Free-cursor tank steering and existing-stone telekinesis

Status: accepted  
Date: 2026-08-12

## Context

Playtesting rejected locked-pointer third-person orbit because the same mouse must draw wall footprints and directly select Earth objects. The mobility pillar also visually passed through the player when presentation rose independently from the Rigidbody. Existing loose stones needed the same continuous physical control as extracted fragments.

## Decision

- The gameplay pointer remains absolute and unlocked. LMB screen paths therefore map directly to surface drawing and object selection.
- In the Earth slice, `A/D` rotate the character heading around local planet up without strafing. `W/S` drive forward/back along that heading. The pure `PlanetTankSteeringSolver` bounds and normalizes the turn.
- `PlanetCameraRig` remains presentation-only and follows the character's tangent heading in `LateUpdate`.
- LMB prioritizes a bounded sphere cast for an existing dynamic Earth body under the pointer. Accepted bodies have an Earth/gravity/impact runtime marker and remain non-kinematic.
- `EarthTelekinesisController` applies the shared mass-aware PD force solver in `FixedUpdate`, emits typed grab/release events through `MagicWorldEvents`, and never edits planet voxels.
- Extracted fragments and existing stones share the `MagicExecutor.HeldBody` facade, target update, charge and release path.
- During an Earth lift, `EarthPillarMobility` continuously matches the player's local-up velocity to the same eased pillar height used by `EarthPillarFeedback`. The final upward launch is applied after the body has ridden the visible top.

## Consequences

The camera no longer has independent mouse orbit in the Earth slice. This is intentional: mouse precision belongs to magic gestures, while character heading provides explicit camera authority. Existing rocks can be grabbed without creating a voxel edit, and the mobility pillar has one shared motion curve for physical and visual state.
