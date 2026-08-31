# Earth Animation Graph A1 integration

## Contract and flags

- `EarthTransitionDirector` remains the only semantic transition decision owner. `EarthAnimationInertializationReason` is derived telemetry for the eight first-wave seams, not a parallel state policy.
- `UsePlayablesAnimationGraph` and `UsePoseInertialization` both default to `false` in code and in `Assets/Elemental/Content/Profiles/EarthAnimationGraphProfile.asset`.
- The checked-in profile is intentionally not assigned to a canonical prefab or scene in A1. Integration must assign it through `HumanoidCharacterPresentation.SetAnimationGraphProfile` or the serialized profile reference, then opt in flags separately.
- With the graph flag off, with a missing Animator/controller, after component disable, or after graph build/topology failure, the Animator and `RigBuilder` return to their legacy ownership path. Non-feature-disabled build failures emit an explicit warning and diagnostics reason.

## Active graph and ownership

The runtime graph is:

`AnimatorControllerPlayable -> AnimationScriptPlayable<EarthInertializationJob> -> AnimationPlayableOutput (sorting order 0) -> RigBuilder external outputs`

`EarthAnimationGraph` owns the PlayableGraph, controller playable, script playable, persistent NativeArrays, and external `RigBuilder` build for its enabled lifetime. It disables and clears the legacy `RigBuilder` graph before building, calls `RigBuilder.SyncLayers` while active, clears the appended rig runtime before graph destruction, disposes all NativeArrays, and restores/rebuilds the prior legacy `RigBuilder` state on shutdown. This ordering prevents two active rig graphs and stale external handles.

`PlanetMotor` retains gameplay/world-root translation. `EarthInertializationJob.ProcessRootMotion` does not write the animation stream root. Bound humanoid bones cache local position, local rotation, output linear velocity, output angular velocity, and critically damped offset state. The controller destination starts immediately; the job composes from the last rendered pose and velocity, including interrupted requests. Quaternion offsets take the shortest path and use a bounded analytic decay.

Active planted-foot chains, active hand-contact chains, and all full-ragdoll-owned bones are excluded from generic pose decay. The foot lock mask is read from `EarthFootContactController`; hand ownership is provided by presentation IK state; full-ragdoll ownership is read from `HumanoidRagdollRig`.

## A1 seam coverage

- run to stop
- direction reverse
- turn to settle
- cast to locomotion
- locomotion to flinch
- stagger to locomotion
- recovery to locomotion
- fall to landing

State-changing seams are still resolved by `EarthAnimationTransitionPolicy` and initiated by `EarthTransitionDirector`. Same-state locomotion parameter discontinuities request the same inertialization service through the director.

## Evidence and Unity validation required

EditMode tests cover an abrupt 90-degree destination, interrupted composition, shortest quaternion path, convergence, finite malformed input, strict 30/60/120 Hz equivalence, planted-foot/full-ragdoll exclusions, a zero-managed-allocation hot math loop, the existing transition-policy seam, and live graph topology containing `AnimationScriptPlayable`. PlayMode tests cover default-off behavior and explicit missing-controller fallback.

A1 static validation is limited to source inspection and `git diff --check`; the integration director owns Unity execution. Before enabling either flag in production, run all EditMode and PlayMode tests and capture:

- zero compile warnings/errors;
- graph/controller/script/topology diagnostics valid on the canonical humanoid;
- `RigLayersAppended` true when the production rig has layers;
- one graph and one rig evaluation path through enable/disable and runtime flag toggles;
- stable parameter/trigger behavior and no stale handles after disposal;
- profiler evidence of no managed allocations in the graph/job hot path;
- visual checks for all eight seams with foot and hand contacts active;
- no regression to the known turn-in-place 11-frame no-planted-foot result or the existing greater-than-10-percent 30/60/120 matrix delta. A1 does not relax those gates.

## Known ordering and Wave A2 prerequisites

Animation Rigging's external build appends outputs that consume previous animation outputs at its package sorting order. A1 places the controller/job base output at order 0 and relies on the installed Animation Rigging package's downstream `PreviousInputs` behavior. Validate this on the installed package and canonical rig, including any built-in `OnAnimatorIK` callbacks. Controller parameters are mirrored into `AnimatorControllerPlayable` in `EarthAnimationGraph.Update` at execution order 1000; triggers owned by this presentation are forwarded directly.

Wave A2 motion matching must reuse this graph owner and director request contract, provide a validated motion catalog and deterministic query seam, and preserve all ownership masks, lifecycle rules, diagnostics, and legacy fallback. No production motion matching, metadata baker, motion database, or A2/A3 asset wiring is included in A1.
