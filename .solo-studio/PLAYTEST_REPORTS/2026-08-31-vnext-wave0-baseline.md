# Playtest Report — VNext Wave 0 baseline

Date: 2026-08-31  
Build/commit: `d2174eded114dd022e4a9c442abadda7a0e44555`  
Facilitator: Integration Director  
Current gate: Wave 0 → Gate 1

## Decision question

Which failures are pre-existing at the immutable VNext snapshot, and what evidence must Gate 1 improve without regressing legacy behavior?

## Scenario and instrumentation

- Unity 6000.5.7f1, `Assets/Elemental/Content/Scenes/EarthCoreSlice.unity`.
- Focused EditMode and PlayMode launchers.
- Character animation visual audit.
- Animation contact 30/60/120 matrix.
- Rendering A/B matrix and project validator.

## Metrics

| Evidence | Result | Fresh timestamp |
| --- | --- | --- |
| `BuildReports/Mvp01FocusedEdit.json` | 280 passed, 0 failed | 2026-08-31T20:54:32Z |
| `BuildReports/Mvp01FocusedPlay.json` | 15 passed, 4 failed | 2026-08-31T20:56:32Z |
| `BuildReports/CharacterAnimationVisualAuditPlay.json` | 0 passed, 1 failed | 2026-08-31T20:57:52Z |
| `BuildReports/AnimationContactMatrixPlay.json` | 0 passed, 1 failed | 2026-08-31T20:58:57Z |
| `BuildReports/RenderingAB/*` | capture set regenerated | 2026-08-31 |

## Fatal/blocking findings

- Turn-in-place recorded 11 frames without a planted foot; acceptance is zero.
- Contact outcome differs by more than 10% across 30/60/120 FPS.
- Shipping duel court check found no qualifying active court.
- Shared visible-knockout test received `FlightIkOff` instead of `AuthoredContact`.
- Accepted MVP evidence did not complete successfully.
- A shallow near-surface projectile graze slept instead of remaining armed.

## Verified non-failures

- All focused EditMode tests passed before Wave 1.
- Scene was saved, Editor idle, and project validator passed for 10 scenes, 14 abilities, and 3 profiles.
- Console was clean of project errors and warnings after validator execution.
- Existing custom cinematic DOF remains the single intended owner; MiniBokeh is inactive.

## Decision

Keep the snapshot as the comparison baseline. Gate 1 must compile with every new flag off, preserve the exact legacy fallback, add real graph/shadow/recovery foundations, and not silently reclassify these six pre-existing failures as new regressions. Any additional failure is Gate 1 blocking.

## Smallest next test

- Integrate R1/A1/P1 after cross-review.
- Run the same four JSON-producing suites and A/B captures from the integration worktree.
- Compare test identities, counts, timestamps, and visible behavior against this report.
