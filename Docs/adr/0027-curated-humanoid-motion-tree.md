# ADR-0027: Curated Humanoid motion tree and support continuity

Status: accepted and implemented.

## Context

The previous controller mixed a one-dimensional locomotion tree with a small fallback
spell bank. Casting accents slowed the entire Animator, surf had no authored base pose,
and an LMB pluck could finalize an emerging platform before fracturing it. Together
these paths produced frozen legs, dragged foot constraints and a visible bind/T-pose
after moving-support loss.

## Decision

- Gameplay publishes semantic `EarthTechniqueId`; a pure resolver maps it to eleven
  stable presentation slots. FBX filenames remain editor-only data.
- Base locomotion is a `Turn × Speed` Freeform Cartesian 2D BlendTree. Surf owns a
  standing-to-crouch transition and looping crouch state. Falling and hard landing are
  explicit states.
- All Mixamo clips are Humanoid, in-place, with root motion baked into pose and
  reuse the canonical `X Bot.fbx` Avatar through `Copy From Other Avatar`. Per-file
  automatic T-pose inference is forbidden because it changes hip/knee retargeting.
  `PlanetMotor` remains authoritative.
- The temporary neutral idle is the upright first-frame segment of
  `Standing Idle To Crouch`; `Injured Idle` is damage-only.
- Magic uses a normalized Direct BlendTree with one-hot semantic weights. Its
  state time is presentation-scrubbed by `EarthMotionTime`, so sustained casts hold
  an authored middle pose without freezing the locomotion layer.
- Casting never modifies global or per-Animator playback speed. Pose hold is metadata
  for presentation accents, not permission to stop locomotion.
- Foot locks release on locomotion input even while MMB remains active. Upper-body
  aim never writes `Animator.bodyRotation`, which would rotate the pelvis and legs.
  Surf keeps its support-relative crouched lock.
- Player pluck is rejected until platform emergence completes; external impact damage
  remains independent.

## Consequences

Moving platforms and gravity sessions no longer stop the gait clock. A motion can be
reassigned by editing one curated mapping without touching gameplay or physics. The
controller contains unused imported clips as documented reserves rather than silently
inventing gameplay states for them.

## Rollback

The previous KayKit fallback clips remain imported and can fill any missing slot. The
semantic resolver and in-place motor contract remain valid if the visible Humanoid is
replaced later.
