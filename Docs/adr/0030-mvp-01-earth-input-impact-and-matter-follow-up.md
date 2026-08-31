# ADR 0030: MVP 0.1 earth input, localized impacts and persistent matter

Status: accepted (2026-08-26)

## Context

The first rescue removed the main platform hitch and made visible KO physical, but
the playable path still had authority gaps. A dual-button chord replayed from the
delayed pointer position, extraction transactions lost their runtime commit
subscription after scene load, fall damage ignored the landing cushion, and every
stone impact could only choose between a root shove and a full KO. Authored arena
rocks also looked physical while remaining inert decoration.

## Decision

- `EarthActionRouterBehaviour` buffers the original press pointer and replays that
  exact sample if an 80 ms dual-mouse chord does not complete. Ordinary wall and
  platform strokes therefore retain their full first segment.
- `EarthDualMouseAbilitySolver` owns deterministic tap/hold timing and crest
  layout. A tap rises for 0.28 s, visibly hovers for 0.25 s, then uses the boxer
  punch presentation and launches along the camera crosshair. A held upward stroke
  accepts bounded lateral drift and lays overlapping pillars nearest-to-farthest.
- The typed punch projectile is prewarmed. Cast does not add components or grow
  the pool. Animator parameters are written by the late runtime action owner before
  Animator evaluation, without introducing a presentation-to-runtime dependency.
- `MagicExecutor` idempotently restores its `VoxelPlanet.EditCommitted`
  subscription in `Awake`. A reserved fragment remains hidden until the complete
  terrain transaction commits, then becomes the held rock. Failed edits expose
  neither a hole nor a fragment.
- `EarthLandingCushion` publishes a bounded safe-landing window. The shared outcome
  bridge consumes fall severity during that window, so a high-speed landing on the
  authored cushion does not request ragdoll or death.
- Surf and pillar-wave hits still force the shared visible ragdoll, but
  `EarthRagdollLaunchLimiter` caps vertical launch to the velocity required for an
  approximately 3.8 m rise. The handoff applies the impulse exactly once.
- Every stone source, including loose fragments, bot projectiles, punch stones and
  armor pieces, first applies a localized impulse to the nearest visible Humanoid
  bone. Three hits within 0.72 s and 0.72 m escalate to the shared full-ragdoll/KO
  path. A single stone cannot convert an otherwise local hit into an immediate KO.
- Controlled armor pieces calculate their bounded formation velocity. Collision
  with a non-owner fighter above 1.25 m/s uses the same armor-projectile localized
  impact route; released barrage pieces keep that route as dynamic bodies.
- Authored amphitheatre and planet dressing stones use
  `EarthDestructibleDecorRock`: they start anchored, detach under a valid impact or
  grab, and shatter into pooled physical debris. Wave, surf and swept fragment
  routes all address the same target contract.

## Consequences

The complete interaction path now has one owner per decision: input resolves the
gesture, the transaction resolves terrain visibility, the impact target resolves
local-versus-global body response, and the duel controller alone resolves KO.
Visible authored stone is no longer an exception to the physical world. The
cluster rule deliberately favors readable local reactions over one-hit deaths;
several concentrated stones still produce the requested global ragdoll.

Focused evidence after the change is `65/65` EditMode and `8/8` PlayMode with a
clean Console. The 720-frame gameplay capture records total-frame p95 `10.8053 ms`,
CPU p95 `10.9746 ms`, GPU p95 `1.87904 ms`, `AcquireSolid` peak `2.3774 ms` and
fracture-preparation peak `0.4750 ms`.

## Rollback

The tap/hold action owner, landing suppression, localized cluster state and decor
damage adapter are independent runtime seams. Any one may be disabled without
changing the canonical terrain, matter, ragdoll or duel contracts. Restoring
delayed-pointer replay, non-transactional extraction visibility or one-hit stone
KO is not an accepted rollback.

## Finite surf follow-up — 2026-08-31

- The surf plough is a fixed fifteen-cell semantic graph: left/right foot cores and
  their bridge remain occupied support, while twelve nose/rail/tail cells are
  prebuilt detachable views. Damage callbacks only change a bounded bit mask and
  release existing views; they never create or cook a mesh/collider.
- Time, travel and coplanar support transfers cause no durability loss. Qualifying
  support discontinuities release one to three non-support cells. A severe large
  wall/nose crash ends the session, while a character or small dynamic body is
  ploughed without being misclassified as a board-killing wall.
- One physical overlap produces one damage event. Contact latching, a 0.75 second
  cross-target cooldown and a 0.30 second separation gate prevent repeated damage
  from one bot or wall contact.
- Surf impacts on an `EarthWall` use a lower-band-only bond query capped at 32% of
  wall height. The legacy automatic whole-wall decay is paused for this route.
- Three-scale dust bursts and actual released cells communicate damage. A separate
  55 ms cosmetic cut-chip cadence uses the existing 28-object pool and does not
  feed durability.
- Fresh focused evidence: `BuildReports/SurfFinitePlay.json`, UTC
  `2026-08-30T23:29:27.1008027Z`, `3/3` passed in `3.6499857 s`.
