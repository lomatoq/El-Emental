# ADR 0033: Per-foot contact ownership and arena rendering rehabilitation

Status: proposed (2026-08-30; implementation under live verification)

## Context

The Broken Crown gameplay frame exposed three regressions that passing locomotion
tests did not measure:

- ordinary walking applied one global contact IK weight to both feet. The airborne
  swing foot was therefore pulled toward the floor while the authored clip tried to
  lift it;
- both feet could enter the clearance window on the same frame. A late tie-break
  released one anchor, but that foot had no re-arm state and could capture again on
  the following frame;
- the arena exterior asset still serialized radial side-normal synthesis at `0.55`
  and a broad vertical shadow fade at `0.86`, despite the imported architecture
  already carrying authored normals. Native scene camera AA was serialized Off;
- all loose/cosmetic props were lifted to the floor collider's global AABB maximum,
  including props outside the actual floor footprint. Those exterior rocks were
  visibly suspended above the spherical world.

The earlier ADR 0028 rollback (`locomotion footIk=0`) remains valid evidence that
unbounded contact IK is worse than authored locomotion. This decision supersedes
only that locomotion contact policy with an instrumented, per-foot state machine;
it does not change movement, grounding, support or damage authority.

## Decision

### Animation contact ownership

- `PlanetMotor` remains canonical for grounded/support state. Animator root motion
  remains disabled.
- `EarthFootStanceGate` is a pure per-foot state machine. A locomotion foot captures
  only while descending into a narrow contact window, only after a visible swing
  re-arm, and only when the other locomotion foot does not own an anchor.
- Cast/surf may deliberately own two feet. Their state is marked separately and is
  released before the gait is allowed to recapture.
- Each foot owns its own applied IK and knee-hint weight. The swing foot fades to
  zero with sole clearance; it never inherits the stance foot's weight.
- Pelvis correction reads only feet with meaningful applied contact weight and is
  limited to `0.045 m` during locomotion. Support-relative anchors retain the
  existing surface ID/generation contract.
- The runtime path stays allocation-free and performs the existing bounded two
  non-alloc foot probes per presented Humanoid.

### Telemetry and acceptance

The production locomotion PlayMode scenario writes both a timestamped raw CSV and
`BuildReports/AnimationArenaTelemetryLatest.{csv,json}`. Each rendered sample
contains:

- measured Speed/GaitRate and global plus left/right applied IK weights;
- left/right lock state and anchor error;
- support ID/generation and stable-support bit;
- both foot positions in character space;
- both knee directions and pelvis correction.

The summary counts lock transitions, simultaneous-lock frames, support transitions
and temporal discontinuities, and records maximum foot step/speed/acceleration,
knee-angle step, pelvis step and applied-IK step. The arena golden path must show
zero simultaneous locomotion-lock frames. Quantitative jerk thresholds remain test
constants and must be calibrated from the first clean live trace, never from a
hand-picked animation frame.

### Arena seating

- The visible cratered floor uses its imported readable mesh as a static, non-convex
  `MeshCollider`; a bounding box is not an acceptable visual support proxy.
- Floor seating occurs only when a downward ray at the prop's tangent position hits
  that floor. A hit seats the collider `0.02 m` into the visible surface. No hit
  preserves the already-established sphere seat.
- A PlayMode gate measures every `Arena_Rock_*` and `Arena_Rubble_*` against the
  actual floor hit or planet radius and rejects a visible gap above `0.035 m`.

### Shading, shadows and aliasing

- Broken Crown architecture uses authored mesh normals (`SideShadingSmoothness=0`)
  and real main-light shadows (`SideShadowFade=0`) in Scene, Game and standalone
  rendering. The binary stable-side receiver remains a diagnostic fallback only;
  measured camera-pan evidence showed no temporal improvement while it removed
  broad architectural form. The arena setup still serializes the exact planet
  center into `_ReceiverPlanetCenter`, so diagnostic classification is deterministic.
  Ambient fill is restrained to `0.76`, facet contrast starts at `0.16`, and macro
  strength stays below `0.025`; intact and fracture-interior materials share this
  broad form response while cut-face palette carries fracture readability.
- Native High retains a 4096 atlas/four cascades and uses URP High 7x7 soft-shadow
  filtering. The directional light uses bounded depth/normal bias (`0.12/0.36`)
  to remove the saw-tooth self-shadow fringe without detaching contact shadows.
- Native scene cameras serialize SMAA High, shadow rendering, depth, NaN stop and
  dithering. Runtime guards remain idempotent recovery, not the first AA owner.
- Contact SSAO uses high samples, high bilateral blur and restrained intensity so
  it grounds intersections without spreading noisy dark bands over broad stone.
  The accepted NativeHigh baseline is intensity `0.68`, radius `0.055 m`, direct
  contribution `0.06`, full-resolution depth normals and high bilateral blur.
- Arena sandstone has a quieter, less saturated exterior palette. Fresh fractures
  retain the warmer accent; characters keep authored UV detail and their own color
  grouping.

## Rehabilitation plan beyond this rescue

1. **Contact corpus** — replay flat floor, 15/30-degree slopes, a step, convex ridge,
   rotating/translating support, sphere seam and support ID/generation swap. Preserve
   raw traces and normal-speed video for each.
2. **Locomotion synthesis** — add stride/turn warping from measured tangent velocity,
   bounded slope lean and toe/heel roll. Do not add another foot owner or drive
   canonical movement from animation.
3. **Transition quality** — add inertialized start/stop/turn, predictive landing and
   explicit cast/surf hand-off. Validate interrupts, KO and re-entry from arbitrary
   gait phase.
4. **Character/environment presentation** — validate skinning normals, motion
   vectors and shadow caster deformation before considering TAA. Keep SMAA as the
   deterministic fallback. Establish quiet exterior, hero fracture and character
   material roles rather than adding surface noise.
5. **Scale policy** — full procedural contacts only near the camera. Distant rivals
   use authored clips plus support-relative root alignment. Profile CPU/GPU and
   preserve the current 60 Hz frame budget before expanding the actor count.

## Verification gate

This ADR becomes accepted only when all of the following are fresh in the same
worktree:

- focused EditMode is green with no compiler/shader warnings;
- production locomotion PlayMode is green and writes a parseable telemetry trace;
- Broken Crown PlayMode proves all props seated and the exact floor collider active;
- a rebuilt 1920x1080 gameplay capture visibly removes the vertical stripe fringe,
  detached rocks and jagged silhouettes;
- the representative profiler remains inside the existing M11 budget.

## Rollback

The entire change is presentation/authoring-side. If the contact state machine fails
a terrain corpus case, set ordinary locomotion contact weight back to zero as in ADR
0028 while retaining telemetry, cast/surf support locks, exact arena seating and the
render-quality corrections. Gameplay state and save data require no migration.
