# Airborne moving-platform mantle fix

## Cause

`PlanetMotor.StepAutoMantle` admitted mantle queries only while
`HasStableSupport` was true. A normal jump clears grounded and moving-support
retention, so the character could reach a ledge in the air while the motor was
forbidden from acquiring it. Presentation could still enter a climb-looking
state through other authored action ownership, but there was no motor path or
hand target to follow.

## Runtime change

- Keep the existing grounded mantle policy unchanged.
- Admit a short forward airborne catch window after takeoff.
- Require a reachable ledge, clear landing capsule and footprint, walkable top,
  and a bounded vertical speed relative to the top support.
- Read moving-support point velocity from `IMovingSurface.SupportFrame`, falling
  back to the attached rigidbody when the collider is not an `IMovingSurface`.
- Preserve all ledge/end/path anchors in the support collider's local frame, so
  translation and rotation during traversal update the physical path and hand
  target together.
- Publish `MantleStartedAirborne` as acceptance telemetry. Animation remains a
  consumer of the motor-owned mantle and cannot start this path by itself.

The catch rejects relative falls faster than 3.25 m/s and upward motion faster
than 6.5 m/s. These are initial safety bounds around the current 3.2 m/s jump;
the focused production test records the actual moving-support traversal before
these values are tuned from broader play sessions.

## Focused validation

- `Elemental/QA/Airborne Mantle Admission Edit Tests`
  writes `BuildReports/AirborneMantleEdit.json` and its XML report.
- `Elemental/QA/Airborne Moving Platform Mantle Play Test`
  writes `BuildReports/AirborneMantlePlay.json` and its XML report.

The PlayMode proof uses the production player, real jump input and motor, a
kinematic rising `IMovingSurface`, the saved mantle presentation, and final
humanoid hand bones. It fails if climb presentation appears without motor
ownership, if the airborne catch does not start, if the hands do not acquire
the moving lip, if any motor phase is skipped, or if the actor does not settle
on the same moving support generation.
