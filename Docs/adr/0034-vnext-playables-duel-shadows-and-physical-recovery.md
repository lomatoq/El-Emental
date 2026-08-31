# ADR 0034: VNext Playables, duel shadows and physical recovery ownership

Status: proposed (2026-08-31; three-gate implementation in progress)

## Context

The current M11 slice has separate but coupled failures in transition continuity, planted-foot presentation, physical recovery, and stable geometric form. Fixing them as independent scene patches risks duplicate bone writers, a second ragdoll, camera-following shadow instability, and evidence that cannot be tied to one commit.

## Decision

- `PlanetMotor` remains movement and grounding authority. Animator root motion stays disabled.
- `EarthTransitionDirector` remains the only semantic animation-transition decision owner.
- The optional animation path is one active graph: `AnimatorControllerPlayable` → `AnimationScriptPlayable(EarthInertializationJob)` → the existing rig/IK owners. It may decay pose/velocity offsets but may not decay gameplay root translation, planted-foot world targets, active hand contacts, or full-ragdoll-owned bones.
- `EarthFootContactController` remains the final visible foot-contact owner; VNext does not add a second foot solver.
- `HumanoidRagdollRig`/`ActiveRagdollPuppet` remain the only full-ragdoll stack. Pose matching selects recovery clip, entry phase, live-pelvis alignment and marker handoff; it does not return to a pre-impact root.
- `CharacterPhysicalMode` and accepted impact semantics remain gameplay authority. Physical-presentation coordination cannot apply damage or manufacture another impact.
- The optional duel-shadow path uses one bounded orthographic light-space map over the duel region, not camera cascades. Coverage is quantized, hysteretic and texel-snapped. Caster registration uses stable group/generation state so intact/fractured handoff is atomic and pooled stale casters stay inactive.
- Existing project-local dual-subject DOF remains the only DOF owner. VNext may extend its sharp envelope but must not enable stock URP DOF or MiniBokeh as a simultaneous owner.
- Public feature gates default off until their subsystem gate is green. The legacy Animator, no-realtime-shadow, and existing recovery paths remain explicit rollback seams.

## Delivery boundary

Three isolated branches start from snapshot `d2174eded114dd022e4a9c442abadda7a0e44555`: rendering, animation, and physical animation. Workers do not edit the generated shipping scene, canonical prefabs, packages, project settings, or `M3EarthCoreSetup.cs`. Only the integration branch performs shared serialized wiring and scene rebuild.

Every wave requires the cross-review ring R→A, A→P, P→R. Gate 1 accepts foundations with flags off; Gate 2 accepts production subsystem behavior; Gate 3 accepts complete wiring. Final status requires same-commit tests, A/B captures, profiler evidence, 30/60/120 equivalence, soak results and a clean Console.

## Consequences

The architecture gains explicit replacement seams without changing simulation authority or save data. Until same-commit evidence is green, the new systems are implemented/proposed rather than accepted. The additional profiles, fixed-capacity buffers and diagnostics cost memory and integration work, but bound hot paths and make rollback measurable.

## Verification

- Real script-playable topology and interruption/convergence tests.
- Stable shadow math, caster generation and visible debug-receiver proof.
- Front/back/left/right pose match, live-pelvis alignment, clearance and repeated-recovery tests.
- No duplicate bone/impact/shadow/DOF owner with every feature independently toggled.
- Exact gate evidence recorded in `Docs/PROJECT_EXECUTION_TRACKER.md` only after Unity validation at the integration commit.

## Rollback

Disable the corresponding feature gate and use its existing legacy path. Do not roll back by enabling camera cascades, creating another ragdoll/transition system, or bypassing `PlanetMotor` support authority.
