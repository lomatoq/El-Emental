# ADR 0023: Vector flick and circular Earth grammar

Status: accepted (2026-08-14)

## Context

One RMB charge/release could not communicate both precise movement and a decisive
projectile launch. MMB also overloaded gravity hold, automatic fracture and
automatic repair, so the same press could perform the opposite of the player's
intent. Earth bending needs more verbs without adding a spell hotbar.

## Decision

- RMB hold is a bounded continuous vector field. It preserves momentum on release
  and uses lower precision speed caps than a projectile.
- A short RMB tap emits a compact camera-ray pulse. A viewport-normalized RMB
  swipe followed by release emits a projectile flick in the swipe direction.
  Mass, CCD and the existing per-target maximum speed remain authoritative.
- Rocks, wall pieces, whole walls and active wave pillars implement the same
  `IEarthPhysicalTarget` contract. A grabbed wave pillar detaches from its pooled
  emergence animation, receives radial gravity and uses angular damping.
- Holding MMB without a circular gesture remains gravity grip. A clockwise circle
  selects `Repair`; a counter-clockwise circle selects `Disassemble`.
- Signed accumulated angle selects direction. The normalized phase from 28 to 300
  degrees selects how much of the deterministic repair order or bond set is acted
  upon. Releasing early preserves a partial structure.
- During controlled disassembly, only pieces no longer structurally supported are
  admitted to the gravity cluster. This prevents ownership acquisition from
  breaking every remaining bond in one frame.
- HUD text, fill and ring colour/rotation expose the recognized direction and
  phase. Presentation remains downstream of canonical bond and Rigidbody state.

This supersedes the RMB release-only input choice in ADR 0006 while preserving its
mass-aware physics, and refines the gravity-well interaction introduced in ADR 0011.

## Consequences

Precise movement and combat launch share one motor skill without a mode key.
Circular MMB gestures create a reusable directional/phase grammar for future Earth
techniques. The simulation solvers remain device-independent and testable from
normalized samples; runtime input owns only pointer sampling and target context.

## Evidence

- EditMode covers hold/tap/flick classification and signed circular phase.
- PlayMode covers physical wall movement without caster recoil, pillar detachment
  and launch, partial repair growth, and deterministic partial/full disassembly.
- The production Humanoid golden path samples baked visible leg vertices while the
  real spherical motor walks, rather than trusting Animator parameters alone.
- Full regression: EditMode 215/215 and PlayMode 76/76.
- The production wall now bakes 24 full-depth, chipped Voronoi cells across five
  polygon families. Their volume range deliberately mixes a few hero chunks with
  smaller seam debris instead of repeating wide quadrilateral slabs.
- A platform drawn under the actor registers its rider on the first physics step.
  Its bounded emergence velocity is applied to the whole active-ragdoll puppet,
  so the actor rises with the top surface and can immediately charge a pillar jump
  from that moving support.
