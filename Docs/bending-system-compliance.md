# Continuous bending specification compliance

Source contract: `C:/Users/nirrt/Downloads/elemental_bending_system_spec_ru.md`  
Audit date: 2026-08-12

This document is deliberately a live gap audit. A row is `proven` only when the playable scene and automated evidence exercise it.

| Contract | State | Current evidence / gap |
|---|---|---|
| Shared `BendSession` phases | proven for Phase 0 | Pure state tests cover acquire, form, hold, charge memory, commit and recovery. Sustaining exists but has no Earth gameplay consumer yet. |
| Amount independent from Charge | proven | Unit contract plus amount-derived extraction radius; a 1.15-second still LMB hold grows the live selected volume and HUD `FORM` meter, while Charge changes force/release only. |
| LMB terrain acquire → hold → release | proven for extracted and existing Earth mass | Runtime scene tests cover SDF extraction plus direct acquisition of an existing marked Rigidbody without a voxel edit. Both use continuous dynamic control and release. |
| RMB Power inside active session | implemented, needs device-level acceptance | Active sessions route RMB to Charge; standalone push runs only with no active session. |
| BendTarget from camera ray and wheel distance | implemented, needs device-level acceptance | Runtime target API and live input path use the ray and bounded distance. |
| Free-cursor control on spherical gravity | proven in solver/runtime | Absolute pointer remains available for magic. `A/D` apply pure local-up tank turning, `W/S` drive heading-relative motion, and camera follows character heading in `LateUpdate`. |
| PD/spring control in `FixedUpdate` | proven | Fragment stays dynamic, uses force only, compensates local gravity, exposes clamp telemetry; heavy-mass lag has a PlayMode test. |
| Preserve physical + gesture release velocity | proven | Pure solver and runtime release tests. |
| Extract directly from existing flat terrain | proven at contract/runtime level | One radius drives subtract edit, mass and fragment. Fragment starts 0.18 radius below the source surface and exits through the edit while only the stale planet collider cache is temporarily ignored. |
| Shift is `OriginMode.Self` | partially proven | State/input route exists and no shield is cast. Self-centered ring/shockwave behaviour is not implemented. |
| Normalized gesture thresholds | implemented for active verbs | Tap/hold/flick/up/down/horizontal use viewport-normalized deltas. Signed circular accumulation drives clockwise repair and counter-clockwise disassembly with a 0-1 phase; expand/contract remains reserved for a future technique. |
| Earth wall from horizontal gesture | partial / legacy adapter | A viewport-normalized horizontal LMB drag enters the wall-footprint state without an elapsed-time gate; HUD teaches start-on-ground, drag and release. The wall is still an art-directed pooled slab and lacks the new terrain-source reservation contract. |
| Earth pillar from vertical gesture | missing | No physically growing pillar or object-lift acceptance yet. |
| Space short jump / held Earth pillar launch | proven for Earth mobility | Space hold/release uses a pure charge solver and one event-driven reusable pillar. Runtime physics follows the same eased rise as presentation so the pillar carries the player before the final local-up launch. `LIFT` displays charge. |
| Focus, stance and source quality formula | missing | Debug values are placeholders (`Focus=1`, `Stance=1`, source quality `1`). No expenditure/recovery model. |
| Damage, impulse, stagger, ragdoll separation | partial | Physical impact and active-ragdoll adapters exist, but continuous bending has not yet received the full split response contract. |
| Mandatory runtime debug panel and force vectors | proven for Phase 0 | `BEND DEBUG` shows phase, origin, amount, mass, charge, focus, target/actual motion, error, force/clamp and predicted release; runtime debug lines show target/error/force. Mobility/element resources remain zero until their solvers exist. |
| VFX-off gameplay parity | inherited test only | Existing presentation parity covers canonical terrain; it must be rerun after the full bending migration. |
| All four elements share the grammar | missing | Earth Phase 0 is the only migrated element. Fire, Water and Air still use their milestone executors. |
| Networking/prediction contracts | missing | Transport-independent command architecture exists, but BendSession snapshots/authority are not implemented. |

## Current gate

Phase 0 Earth mass control is accepted only after the full EditMode and PlayMode suites, native build and playable visual check pass. This is not acceptance of the complete supplied specification.
