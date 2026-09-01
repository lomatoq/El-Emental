# Playtest report — VNext Gate 1 Foundations

Date: 2026-09-01  
Snapshot: `d2174eded114dd022e4a9c442abadda7a0e44555`  
Accepted integration: `6543585fcd9cf3ed02c23d1a4fd0e1e56e30f536`  
Decision: **PASS — Wave 2 may start**

## Branch heads

| Track | Branch | Head |
| --- | --- | --- |
| Director | `codex/vnext-integration` | `6543585fcd9cf3ed02c23d1a4fd0e1e56e30f536` |
| R | `codex/vnext-rendering` | `eef4e697598907b88de19d7e5e746ea82cac99ec` |
| A | `codex/vnext-animation` | `eea20b6b5a9ebfbcad06398e0c6a3495c22299bf` |
| P | `codex/vnext-physical` | `bfef253f149dd618870f6c0860c9f678ad0325d6` |

## Worker commit lists

R: `9bb9c3e`, `cf97fe1`, `8ee1540`, `be3e828`, `92bbd9f`, `eef4e69`.  
A: `dbcccc6`, `6250ef1`, `b900155`, `153714b`, `c2d438f`, `4c0a5da`, `31814a4`, `9697d61`, `11a3e3d`, `eeec466`, `da50f5f`, `eea20b6`.  
P: `6dad6ec`, `9b8c6fb`, `e87fa7f`, `a861f90`, `09ec267`, `32f0a13`, `b2cd4be`, `9f2363f`, `9d3930f`, `1eb1e3f`, `4cd1fb7`, `dcb41c9`, `dce8a66`, `22dead1`, `bfef253`.

The integration range from the snapshot contains the corresponding cherry-picks plus Director contracts, baseline, ADR, review records, and capture-harness corrections. `git log --reverse --oneline d2174eded..6543585f` is the canonical exact integration list.

## Changed files by owner

Exact manifests are the immutable Git deltas `d2174eded..codex/vnext-rendering`, `d2174eded..codex/vnext-animation`, and `d2174eded..codex/vnext-physical`.

| Owner | Files | Owned areas |
| --- | ---: | --- |
| R | 60 | `Content/GraphicsVNext/Rendering`, `Presentation/Rendering`, duel-shadow shaders/HLSL/profile, renderer feature registration, Gate 1 capture harness, rendering tests, and two VFX API-warning corrections |
| A | 29 | animation graph/profile/job/math/history/diagnostics, transition policy/director integration, motion authoring upgrade, animation tests |
| P | 28 | physical profile/coordinator/recovery database/match/alignment/continuity, ragdoll rig and physical controller integration, recovery tests |

Shared integration files (`EarthTransitionDirector.cs`, `HumanoidCharacterPresentation.cs`) were merged by the Director after cross-review. No worker edited a shipping scene, prefab, package manifest, or ProjectSettings file.

## Unity validation

| Evidence | Result |
| --- | --- |
| Unity import/script compilation | PASS, Unity 6000.5.7f1 |
| Focused EditMode `VNext-Gate1-EditMode-Final4.xml` | 75 passed, 0 failed |
| Exact PlayMode `VNext-Gate1-PlayMode-Final12.xml` | 12 passed, 0 failed |
| Isolated recovery + canonical graph | 2 passed, 0 failed |
| Animation hot-path profiler | 720/720 Update, job, rig and marker frames; 0 topology failures; 0 managed-allocation frames/bytes |
| Fresh project compiler warnings | 0 |
| Fresh curve-owned Animator warnings | 0 |

A deliberately broader PlayMode fixture run reported four failures. Three were known legacy ActiveRagdoll failures; because that fixture lacks `UnityTearDown`, failed enumerators left an additive scene and temporary objects alive, polluting the later canonical animation test. The exact test pair passed 2/2 and the clean Gate 1 set passed 12/12. This is recorded as test-infrastructure debt rather than hidden as a product pass.

## A/B capture

Manifest: `BuildReports/Gate1AB-Final1/manifest.json` — `complete=true`, `success=true`.

| Pair | Files | Runtime proof |
| --- | --- | --- |
| legacy vs inertialization | `animation-legacy.png`, `animation-inertialization.png` | graph/topology/inertia 16 frames, 37 transition requests |
| no shadows vs duel shadow map | `duel-no-shadows.png`, `duel-shadow-map.png` | disabled path 1 frame; enabled map 1 frame; 2 drawn casters; luminance 0..1 |
| old vs pose-matched recovery | `recovery-legacy.png`, `recovery-pose-matched.png` | pose/state/clearance/isolated/live-support all 1; continuity error `3.7252903e-8 m` |

All six 1920x1080 PNGs are nonempty and were visually inspected. Beauty frames show the arena and both fighters; the debug shadow frame is a meaningful binary scene/caster mask; recovery frames show distinct legacy and authored recovery poses. All transient state restored and the scene remained clean.

## Feature-flag defaults

- `EarthAnimationGraphProfile.asset`: Playables graph off, pose inertialization off.
- `DuelRenderingProfile.asset`: duel shadow map off.
- `EarthPhysicalAnimationProfile.asset`: pose-matched recovery off.

Legacy behavior therefore remains the serialized default at Gate 1. The capture harness activates each new path transiently and restores it.

## Cross-review

- R -> A: APPROVE.
- A -> P: APPROVE.
- P -> R: APPROVE.
- Unresolved review blockers: none.

## Unresolved defects and limits

- Wave 0's known gameplay/visual failures remain baseline work for later waves; Gate 1 did not reclassify them as fixed.
- `ActiveRagdollRuntimeTests` needs fixture-wide teardown or `try/finally` cleanup so a failed test cannot contaminate later PlayMode tests.
- Unity's SearchDatabase logs a startup `ArgumentOutOfRangeException` in capture-mode batch launches. The stack is editor-only, contains no project frame, and capture completion is successful; keep it as a narrowly identified engine/environment exception, not a blanket warning allowlist.
- Single-frame A/B evidence proves feature paths, not final motion quality, shading quality, 30/60/120 equivalence, full CPU/GPU budgets, or soak stability. Those remain Gate 2/Gate 3/final acceptance work.
- The snapshot's two production-local files remain unpublished because pushing them still requires explicit user authorization.

## Gate decision

Gate 1 satisfies the attached plan's Foundations conditions: common snapshot, ownership boundaries, default-off compilation, legacy fallbacks, real AnimationScriptPlayable/IAnimationJob, tested duel-shadow and recovery math, explicit owner handoffs, complete A/B evidence, archived profiler evidence, and an all-APPROVE review ring. Start Wave 2; do not enable High-profile defaults or report final product completion.
