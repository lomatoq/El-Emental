# VNext Gate 1 cross-review

Date: 2026-09-01  
Snapshot: `d2174eded114dd022e4a9c442abadda7a0e44555`  
Gate status: **APPROVE — source review and Unity integration evidence complete**
Accepted integration: `6543585fcd9cf3ed02c23d1a4fd0e1e56e30f536`

## Reviewed commits

| Track | Commit | Reviewer | Verdict |
| --- | --- | --- | --- |
| R1 duel-shadow foundation | `9bb9c3eb37a2a6d82b70e355d918d09505df0019` | P | APPROVE WITH REQUIRED FIXES |
| A1 Playables inertialization | `dbcccc6aaed5bcac22dc2518c89c88ad62e9e8ce` | R | APPROVE WITH REQUIRED FIXES |
| P1 pose-matched recovery | `6dad6ecfaf650c8b9cee99bd162999f219cd15d2` | A | REJECT |

## Blocking findings

### R1

- Serialized signed identity cannot represent canonical high-bit `uint` IDs and cannot be rebound when pooled fragments acquire a new ID/generation.
- `DuelShadowCaster` registers serialized values on enable but has no idempotent bind/rebind adapter.
- Component/pool/fracture lifecycle has only pure registry tests, not PlayMode evidence.

Required correction: canonical unsigned identity, unregister-before-rebind, presentation-side owner seam, and PlayMode lifecycle/generation tests.

### A1

- Topology validation proves only controller → script playable → base output; the test uses an empty `RigBuilder` and does not prove downstream rig outputs or cleanup.
- Runtime feature toggling creates/destroys the graph without transferring controller state/time and may visibly jump.
- Full enabled-path profiling/capture diagnostics are not yet surfaced or measured.

Required correction: non-empty rig-layer topology/restore test, safe OFF→ON→OFF state handoff, capture-visible diagnostics; profiling remains a Director Gate 1 action.

### P1

- A second stateful physical-mode machine duplicates canonical `CharacterPhysicalMode` authority.
- A new impact during get-up/recovery can be dropped, including with the feature off.
- Invalid markers/state hashes can become authoritative before `Animator.HasState` validation.
- Recovery support uses an arbitrary ray and latches validity instead of following stable `PlanetMotor` support.
- Entry-phase persistence and 30/60/120 marker equivalence lack runtime/pure evidence.

Required correction: canonical-mode-driven ownership adapter, interruptible recovery, pre-handoff metadata/state validation with exact fallback, stable support revalidation, and the missing tests.

## Merge rule

No Wave 1 implementation commit is cherry-picked into `codex/vnext-integration` until each owner lands a separate corrective commit and the original reviewer returns APPROVE or APPROVE WITH REQUIRED FIXES containing no unresolved Gate 1 blocker. Unity validation starts only after that re-review.

## Corrective commits and final verdicts

| Track | Corrective commits | Original reviewer | Final verdict |
| --- | --- | --- | --- |
| R1 duel-shadow foundation | `cf97fe17d1c7795630d50644539b9aeed016931a` | P | APPROVE |
| A1 Playables inertialization | `6250ef1d4ca7ccadf01c9c66c8adca7986780c1d`, `b900155c5f734527b41e2a337b60a26449b8df1c` | R | APPROVE |
| P1 pose-matched recovery | `9b8c6fba4c1ac69a611e5cf1de54637115707b6a`, `e87fa7f6cd1cf68f635dfb878c55ed75cb2d5254` | A | APPROVE |

### Closed R1 blockers

- Identity and generation are canonical `uint`, including high-bit identities.
- Pooled casters use explicit idempotent bind/rebind with unregister-before-register and cannot revive a stale acquisition after disable.
- Six lifecycle PlayMode tests cover high-bit identity, pool disable/re-enable, idempotent reacquisition, stale generation rejection, renderer eligibility, and atomic representation handoff.

### Closed A1 blockers

- The canonical PlayMode test explicitly installs and configures the graph/profile on the selected EarthCoreSlice Animator, so its non-empty RigBuilder topology assertions are reachable without production scene wiring.
- Two OFF -> ON -> OFF cycles verify state/time, parameters, layer weights, pose continuity, stale-handle destruction, and restoration of a fresh legacy rig graph.
- A 720-frame active-graph window requires Update, AnimationScriptPlayable job, RigBuilder synchronization, profiler-marker, topology-capture, and zero managed-allocation evidence.

### Closed P1 blockers

- `CharacterPhysicalMode` remains the sole physical-mode authority; the coordinator is an ownership adapter rather than a second state machine.
- Recovery can be interrupted by a new accepted hit with the feature both off and on.
- Invalid markers and missing Animator states fall back before ownership mutation.
- The selected recovery state and materially nonzero entry phase (`0.55`) pass through the existing transition director as the sole Animator writer and persist into the next frame.
- Recovery support is freshly sampled by a bounded non-alloc probe while movement is disabled; runtime coverage verifies support loss revokes ownership and reacquisition restores it.
- Marker thresholds have 30/60/120 FPS and skipped-threshold coverage.

## Final corrective review ring

| Track | Final corrective commit | Integrated as | Reviewer | Verdict |
| --- | --- | --- | --- | --- |
| R1 capture/recovery evidence | `eef4e697598907b88de19d7e5e746ea82cac99ec` | `6543585` | P | APPROVE |
| A1 curve-owned handoff | `eea20b6b5a9ebfbcad06398e0c6a3495c22299bf` | `9786635` | R | APPROVE |
| P1 deterministic recovery fixture | `bfef253f149dd618870f6c0860c9f678ad0325d6` | `185d6bb` | A | APPROVE |

No final reviewer reported an unresolved blocker.

## Director-owned Gate 1 evidence

- Unity 6000.5.7f1 imported and compiled the integrated branch.
- Focused EditMode: `VNext-Gate1-EditMode-Final4.xml`, 75/75 passed.
- Exact focused PlayMode: `VNext-Gate1-PlayMode-Final12.xml`, 12/12 passed.
- Isolation audit: recovery plus canonical animation graph passed 2/2. A prior broad fixture run exposed pre-existing `ActiveRagdollRuntimeTests` cleanup pollution, not an A1 runtime failure.
- The canonical animation graph test recorded 720 active frames with 720 Update/job/rig/marker samples, zero topology failures, and zero managed-allocation frames/bytes. The archived interpretation is `.solo-studio/PERFORMANCE_CAPTURES/2026-09-01-vnext-gate1-animation-720.md`.
- `BuildReports/Gate1AB-Final1/manifest.json` is complete and `success=true`; all six 1920x1080 captures are nonempty. Pose-matched recovery has live support and a pelvis continuity error of `3.7252903e-8 m` against a `0.002 m` limit.
- Restoration is complete: runtime override, bounds provider, caster registry, animation owner, physical owner, scene cleanliness, registry count `0 -> 0`, and zero transient components.
- Fresh EditMode, exact PlayMode, and capture logs contain no project compiler warning and no curve-owned Animator warning. The capture process still logs a UnityEditor.Search startup `ArgumentOutOfRangeException`; its stack contains only Unity editor search code and the capture subsequently succeeds.
- All three production feature flags remain serialized off. Gate 1 proves safe foundations and fallbacks; it does not enable Wave 2 or final-profile behavior by default.

## Final verdict

**APPROVE.** Gate 1 Foundations is closed at `6543585`; Wave 2 may start. Final product acceptance, 30/60/120 evidence, full soak, and High-profile enablement remain later gates.
