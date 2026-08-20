# ADR 0028: Predictive animation rescue and render-clock support contacts

Status: accepted (2026-08-20)

## Context

`PlanetMotor`, moving supports and the Animator intentionally run on different
clocks. The previous presentation waited for authoritative grounding before it
started a landing clip and recaptured surf foot contacts with a fresh raycast every
render frame. That produced late landing poses, dragged legs and pelvis jitter even
though canonical physics was valid.

## Decision

- `PlanetMotor` remains the sole movement/grounding authority and exposes read-only
  capsule, gravity and support data to presentation.
- `EarthAnimationContactPredictor` performs a bounded 2–8 step ballistic
  `CapsuleCastNonAlloc` forecast. It rejects self hits, unwalkable normals and free
  dynamic debris, and includes moving-support point velocity. It never changes a
  Rigidbody, grounding, damage or support ownership.
- `EarthAnimationStateResolver` owns presentation phases. `PreLanding` may begin
  60–180 ms before expected contact; actual contact confirms soft, moving or hard
  recovery. Candidate loss returns to Fall through a bounded grace window.
- Landing states use an authored contact time from `CharacterPresentationProfile`.
  Their fixed-time start offset is `contactTime - predictedTTC`; confirmed contact
  neither restarts the same state nor downgrades a moving/hard classification.
- Curated FBX root tracks are extracted rather than baked into Humanoid bones and
  are discarded by `applyRootMotion=false`. This keeps the rendered hips attached
  to the `PlanetMotor` capsule while preserving authored limb motion.
- Runtime phase changes use full-path hashes with `CrossFadeInFixedTime`; gameplay
  remains independent from clip duration.
- Speed is support-relative and filtered with separate acceleration/deceleration
  responses. Turn combines measured radial yaw with low-speed input fallback,
  hysteresis and a minimum 120 ms release.
- A moving-support foot contact is captured once per surface ID/generation. Surf
  resolves those anchors from the interpolated presentation deck pose. Raycast and
  recapture occur only after a genuine support change or lock loss.
- Pelvis correction has one critically damped owner with a configurable maximum
  correction speed. Stable radial knee hints remain the last leg constraint.

## Tuning and evidence

All Rescue V1 timing and filtering values live in
`CharacterPresentationProfile`. `EarthPolishLab` reports phase, landing candidate,
support identity, filtered parameters, foot error and pelvis correction. The
`animation-landing` visual court captures three impact classes at four moments each.

Final isolated Editor evidence: `352/352` EditMode in `36.563 s`, `101/101`
PlayMode in `168.795 s`, and all twelve landing frames plus the terminal capture in
`BuildReports/VisualQa/AnimationRescue-20260820-v28/`. Windows builds were
intentionally not produced for this Editor-only rescue.

## Consequences

Landing prediction can improve presentation without lying to gameplay. Surf foot
contacts no longer form a render/fixed feedback loop. Full phase metadata,
inertialization and obstacle-aware full-body constraints remain later VNext work and
are not smuggled into this rescue patch.
