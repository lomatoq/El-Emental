# VNext Gate 1 cross-review

Date: 2026-09-01  
Snapshot: `d2174eded114dd022e4a9c442abadda7a0e44555`  
Gate status: **blocked pending corrective commits**

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
