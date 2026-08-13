# ADR-0007: Continuous physical bending sessions

Status: accepted  
Date: 2026-08-12

## Context

The first Earth slice exposed three selected abilities and moved a held fragment by writing its transform. A throw then replaced that motion with a fixed camera-forward impulse. This made mass, mouse trajectory and the transition from holding to release largely cosmetic. The new bending specification requires one shared physical grammar rather than separate spell buttons.

## Decision

- `BendSessionState` is the pure lifecycle authority: `Idle → Acquiring → Forming → Holding → Charging → Committing → Recovery` with explicit cancellation and sustaining states.
- `Amount01` selects material quantity. `Charge01` stores release/control energy; changing one never changes the other implicitly.
- Holding the initial LMB acquisition nearly still grows `Amount01` continuously from 0.18 to 1 over 1.15 seconds. The selected underground volume and HUD `FORM` meter grow before commit; an early horizontal sweep instead transitions into the wall-footprint state.
- A controlled Earth fragment remains a non-kinematic Rigidbody. `BendForceSolver` computes a mass-scaled spring/damper force in the physics clock, compensates sampled local gravity and clamps total control force. Heavy masses therefore fall behind when the clamp is reached.
- `BendTarget` is a world-space point. Explicit mouse motion or wheel input moves it along the camera ray and produces a smoothed target velocity; a stationary cursor freezes it in world space.
- An explicit world BendTarget clears the legacy child-transform anchor. Moving or turning the caster therefore never drags a controlled rock without matching pointer input.
- Lost release events and controller disable both terminate physical control, preventing a rock from remaining attached to a stale bending session.
- Release velocity is current physical velocity plus charged aim velocity plus transferred gesture velocity. Release does not teleport a body and does not discard its momentum.
- RMB charges an active bend session. The older targeted push remains a compatibility action only when no bend session is active.
- Shift records `BendOriginMode.Self`; it does not select or cast a shield.
- Earth extraction uses one amount-derived radius for canonical SDF subtraction, visible fragment scale, effective mass and the spawn event. The fragment begins partially inside that selected surface volume and is physically drawn out.
- Runtime diagnostics are presentation-only and read the session/rigidbody snapshot. They never author gameplay state.

## Consequences

The basic Earth manipulation loop is now physically continuous and testable without VFX. Existing Line Wall and the legacy direct test APIs remain operational during migration. Self-origin Earth fields, terrain-backed wall source reservation, Focus expenditure and the other element solvers remain separate follow-up gates and must not be claimed complete. Earth Space mobility is decided separately by ADR-0008.
