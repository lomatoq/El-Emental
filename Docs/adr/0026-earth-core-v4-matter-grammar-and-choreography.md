# ADR-0026: Earth Core V4.1 matter grammar and choreography

Status: accepted, implementation gated.

## Context

V3 proved individual Earth abilities, volumetric fracture, moving platforms, armor,
resonance and surf. It did not yet make those systems one continuous material language.
The V4.1 audit also identified geometry publication and cross-system character motion as
prerequisites rather than polish tasks.

## Decision

The authoritative runtime chain becomes:

`raw input -> gesture tokenizer -> ranked intent -> technique/combo graph -> Earth Matter Kernel -> simulation/runtime -> choreography director`.

The rollout is ordered:

1. Freeze evidence and close geometry/motion/support foundations.
2. Introduce stable matter identity, provenance and an honest mass/volume ledger.
3. Prove atomic single-stone return before multi-piece return.
4. Normalize wheel/gesture input and resolve ranked intents with continuity.
5. Express techniques and follow-ups as data, not controller branches.
6. Drive full-body pose, camera, VFX and audio from the same technique phase.
7. Add combat and shipping evidence only after the above gates are green.

VFX Graph and GPU-indirect debris remain visual-only. Canonical mass, collisions,
damage, provenance and reintegration stay CPU-authoritative. Runtime geometry must pass
the shared integrity court before publication; mixed winding is rejected rather than
hidden with blind normal recalculation.

## Consequences

- A stone remains the same matter while moving between terrain, structure, rigid body,
  fragment, armor and visual representation tiers.
- Returning matter is a transaction: the physical body survives until the destination
  SDF change is confirmed, then the registry transition commits atomically.
- Input latency is addressed with speculative preview while ranked intent remains able
  to revise within its bounded decision window.
- Character locomotion, platform carry, pillar riding, surf and ragdoll recovery share
  one support-frame and motion-state contract.
- A gate can fail without invalidating earlier green evidence, but later gates cannot
  claim completion while an earlier dependency is red.

## Rollback

V3 ability controllers remain available behind their existing scene/profile wiring
until the corresponding V4 technique is proven. No production path is removed merely
because a new abstraction exists.
