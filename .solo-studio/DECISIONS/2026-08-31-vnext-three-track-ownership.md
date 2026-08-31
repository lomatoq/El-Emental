# ADR — VNext three-track ownership and integration gates

Date: 2026-08-31  
Status: accepted  
Decision owner: Director / integration branch

## Context

The VNext rescue changes animation transitions, physical recovery/game feel, and duel rendering at the same time. These systems meet at runtime but must not acquire duplicate writers or be edited concurrently in shared scene and setup assets.

## Decision

All work starts from immutable snapshot `d2174eded114dd022e4a9c442abadda7a0e44555` and lands through `codex/vnext-integration` in three gated waves.

| Track | Branch | Worktree | Owns |
| --- | --- | --- | --- |
| R | `codex/vnext-rendering` | `ElEmentalVNext/rendering` | duel shadows, stable shading, render diagnostics |
| A | `codex/vnext-animation` | `ElEmentalVNext/animation` | playable graph, inertialization, animation transition policy |
| P | `codex/vnext-physical` | `ElEmentalVNext/physical` | physical animation, hit response, recovery selection and alignment |

Workers may add tests inside their owned subsystem. They may not edit shipping scenes, prefabs, M3 setup, packages, project settings, or another track's controller. The Director alone merges, resolves contracts, wires shared assets, runs Unity, and records gate evidence.

## Feature seams

- `UseDuelShadowMap` defaults off until rendering Gate 1 passes.
- animation playable-graph/inertialization flags default off until animation Gate 1 passes.
- `UsePoseMatchedRecovery` defaults off until physical Gate 1 passes.
- Every new path retains a legacy fallback until the corresponding gate is green.
- Visible animation bones have one semantic owner per phase; physical takeover and recovery handoff are explicit states, never simultaneous writers.

## Cross-review ring

At the end of every wave: R reviews A, A reviews P, and P reviews R. A track is not integrated without an explicit verdict and resolved blocking findings.

## Baseline evidence

- EditMode: 280/280 passed (`BuildReports/Mvp01FocusedEdit.json`, 2026-08-31 UTC).
- Focused PlayMode: 15/19 passed; four known failures in shipping court, visible-knockout state, accepted evidence, and shallow-graze arming.
- Character animation audit: failed because turn-in-place contained 11 frames without a planted foot.
- 30/60/120 contact matrix: failed because cross-FPS outcome delta exceeded 10%.
- Rendering A/B capture set was regenerated under `BuildReports/RenderingAB`.
- Project validator passed: 10 scenes, 14 abilities, 3 profiles; Console had no project errors or warnings after validation.

## Gates

Gate 1 requires the real inertialization job, duel-shadow foundation, pose-matched recovery, default-off feature flags, tests, cross-review, and no duplicate state/bone owners. Gate 2 requires subsystem vertical integration and measured stability. Gate 3 requires production wiring and content-complete validation. Final acceptance requires all features enabled, the full test matrix, a 20-minute soak, 500 hit events, 100 recoveries, profiling evidence, and clean Console.

## Source-control constraint

The snapshot contains the exact local production changes to `ArtSource/Characters/Linebreaker/LinebreakerRigged_weighted.blend` (Git LFS object `f93587...`) and `Assets/Elemental/Content/Materials/GroundPreviewPebble.mat`. Local work may continue, but publishing those files to `https://github.com/lomatoq/El-Emental.git` requires the user's explicit authorization before retrying the rejected push.

## Revisit trigger

Reopen this decision if a feature needs a shared writer, a worker must change a forbidden shared asset, a gate cannot be measured in isolation, or the immutable snapshot must change.
