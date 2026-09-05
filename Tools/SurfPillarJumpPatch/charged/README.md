# Charged tilted surf-pillar long jump

This stage was copied from the current production sources after the first physical
Space ingress test passed. Copy only the files below to the matching `Assets/`
paths; do not copy the older `after/`, `visual/`, or `visual-robust/` snapshots.

## Shipping behavior

- Shift+forward enters the existing surf.
- Space press begins the existing `EarthPillarMobility` charge. It does not
  break the board or schedule a launch.
- Space hold keeps the board moving and exposes the authoritative
  `EarthPillarMobility.IsCharging/Charge01` values to HUD and presentation.
- Space release schedules one launch, breaks the 12-stone board, and clears the
  input owner. A stale held/released frame cannot schedule another launch.
- The support sample and pillar base still use the ground normal. The surf launch
  axis leans 18–28 degrees toward projected board velocity (falling back to the
  actor facing). The visible pillar, rider rise, final velocity, event direction,
  and board scatter all use that one axis.
- Losing the surf/support, entering full ragdoll, Cancel, or disabling the input
  router cancels the held charge without replay.
- Ordinary non-surf pillar charging still uses the vertical overload unchanged.

The existing pillar component owns the 1.45 s charge envelope and min/max height,
radius, rise time, and velocity. `EarthSurfProfile` adds only the surf-specific
minimum/maximum tilt and board-stone scatter speed. This avoids a second gameplay
charge clock.

## Focused verification

After copying and compiling:

1. `Elemental/QA/Run Surf Pillar Jump EditMode Tests`
2. `Elemental/QA/Run Surf Pillar Jump PlayMode Test`
3. `Elemental/QA/Capture Surf Pillar Jump Visual Proof`

The visual test uses a real paired Keyboard+Mouse control scheme, holds Space for
0.72 s, then releases it. It suspends every production camera owner and bot,
recomposes a synchronous URP side shot immediately before each capture, and
restores devices, behaviours, event subscriptions, and the additive scene in a
`finally` block. The report and three PNGs are written below
`BuildReports/EnvironmentAnimationRescue/SurfPillarJumpVisualQa/`.

Acceptance requires one pillar event, all 12 stones released, at least eight stone
centres framed during the break, an 18–28 degree pillar lean toward travel,
positive radial and forward rider displacement, upward release speed above
2.5 m/s, and both the launch origin and airborne rider inside the final frame.

The presentation consumer staged separately at
`Tools/SurfChargePosePatch/.../EarthCharacterPoseController.cs` can use
`EarthPillarMobility.IsCharging`; no animation-owned clock is required.
