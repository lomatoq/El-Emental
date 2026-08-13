# ADR 0018: Earth character pose and cast timing authority

Status: accepted
Date: 2026-08-13

## Context

The fallback Earth pose rotated a root and two arms, while the Humanoid layer only aimed both hands at the current target. Neither communicated planted feet, pelvis compression, load transfer, mass-dependent effort or recovery. Gameplay mutations must still occur from commands and typed simulation events rather than animation callbacks.

## Decision

- Earth casts use Acquire, Root, Load, Strike, Sustain and Recover phases. `EarthCastTiming` expresses startup, active and recovery in ticks plus an explicit contact phase.
- `EarthPoseIntent` is immutable presentation data derived from technique, authoritative event tick, target, mass, acceleration, charge and support state. Heavy mass increases effort, bracing, stance width and pelvis compression through a bounded pure solver.
- Typed wall, fragment, body and pillar events align Strike to the already-authoritative mutation tick. Animation events may emit local dust or sound only.
- The Humanoid presentation uses local-up foot probes, foot lock windows, stance widening, foot-normal rotation, bounded pelvis compensation and torso twist. Locks release while airborne.
- `PlanetMotorFeelProfile` owns acceleration, deceleration, turn response, slope/traction, coyote time, jump buffer and cast/brace speed limits. `MagicInputController` supplies the brace state from canonical bending state; the presentation component does not decide locomotion authority.
- Normal locomotion remains Rigidbody-driven and in-place. Full ragdoll remains reserved for strong impacts and recovery.

## Consequences

Casting now reads through feet, pelvis, torso and hands while remaining stable around arbitrary planet normals. A presentation clip can be replaced without changing the mutation tick. Locomotion telemetry exposes grounding, local up, desired/current speed, support, brace and jump windows for tests and tuning.

## Rollback

The existing `HumanoidCharacterPresentation` hand IK and `EarthMagicPoseDriver` fallback remain functional if the embodied pose controller is disabled. The tick timing and motor profile are independent pure/runtime contracts and need no replay migration.
