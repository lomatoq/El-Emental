# ADR 0022: Cinemachine spherical-world gameplay camera
Status: accepted (2026-08-14)

## Context

The hand-authored camera mixed local-gravity orientation, pointer pitch, collision
avoidance and presentation damping in one transform writer.  Character colliders
could collapse the boom into the avatar, additive scenes could leave competing
camera authority, and the camera sometimes fed unstable framing back into aiming.

## Decision

- Use official `com.unity.cinemachine@3.1.7` as the camera solver on Unity 6000.5.7f1.
- Keep a separate world-up frame aligned to `PlanetMotor.LocalUp` and a child aim
  pivot that owns pitch. `CinemachineThirdPersonFollow` tracks the aim pivot.
- Give the gameplay virtual camera explicit priority 100 and initialize its lens
  from the current `EarthCameraStateProfile`, avoiding first-frame zoom transients.
- Tag the complete character, presentation and puppet hierarchy as `Player`; the
  collision solver ignores that tag but continues to avoid world geometry.
- Preserve `PlanetCameraRig` as a telemetry/impulse compatibility layer in external
  driver mode. It no longer writes the camera transform while Cinemachine is live.
- Aim and wall selection continue to use the rendered gameplay camera. Exact ray
  hits win; the forgiving sphere assist runs only if the exact ray found no target.

## Consequences

The horizon follows spherical local up without inheriting aim pitch, collision
avoidance is maintained by a supported package, and camera ownership is stable in
both the standalone game and additive PlayMode tests. The compatibility layer
keeps existing feedback and accessibility profiles usable. Rolling back requires
disabling `EarthCinemachineCameraController` and returning transform authority to
`PlanetCameraRig`.

## Evidence

- Visible-gait golden path checks enabled leg renderer bounds, character framing,
  local-up horizon, downward pitch and bounded per-frame camera motion.
- Production screen-ray test raises a real wall, locks it through
  `MagicInputController`, releases a zero-charge quick shove and verifies wall
  displacement without selecting nearby helper bodies.
- Final EditMode: 212/212. Final PlayMode: 73/73.
