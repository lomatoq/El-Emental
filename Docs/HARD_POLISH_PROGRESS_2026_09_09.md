# Hard polish execution — September 9, 2026

Base: `main` at `41b13d6a` (clean working tree before this work). The supplied
Belarusian handoff/audit/plan/contracts/acceptance documents are reference inputs,
based on older `cb7538ad`. Their hypotheses and proposed values are not measured
results or proof that a feature is already integrated. Current owners and recent
main changes take precedence over stale implementation assumptions.

**Final retained scope:** teleport frame guard, diagnostic counter and regression
fixtures. Final **55/55 Edit**, **2/2 rendered Play**. The broader surface gate
still has a recorded bot/120 failure and G03 as a whole is **not accepted**.

## G03: teleport contact frame ownership

`EarthFootContactController.OnAnimatorIK` marks the current frame before contact
evaluation. On a new motor teleport sequence, evaluation calls
`InvalidateBasePose`, which clears that marker along with necessary pose history.
The fix restores the completed-frame marker after evaluation. Other callbacks
still submit cached foot goals and knee hints without advancing contact state.
External invalidation and disable continue to clear the guard normally.

A cumulative, allocation-free diagnostic `ContactEvaluationCount` counts actual
evaluations, not cached goal submissions. It does not own or change simulation.
No animation graph, solver, motor or final-bone writer was added.

The existing IK submission curve and pelvis/contact timing remain unchanged.
An experimental stronger terminal curve was tested and reverted because it did
not close the surface acceptance failure. No acceptance tolerance was relaxed.

Changed implementation/test paths:

- `Assets/Elemental/Presentation/Animation/EarthFootContactController.cs`
- `Assets/Elemental/Tests/PlayMode/SeptemberAnimationRescueRuntimeTests.cs`
- `Assets/Elemental/Tests/EditMode/SeptemberAnimationRescueTestLauncher.cs`

Fresh Unity 6000.5.7f1 evidence in `BuildReports/HardPolish/41b13d6a/`:

Full surface traces are retained in `surface-telemetry.zip` (three original JSON
files); test XML, summaries and final walk-stop data remain directly readable.

- Final source Edit: **55/55**, `Contact-final-edit.xml`,
  2026-09-09T00:38:22Z, .2122 s. Includes
  contact acceptance, support authority and locomotion rhythm; the experimental
  curve and its two tests are absent.
- Final source rendered Play: **2/2**, `Contact-final-play.xml`,
  2026-09-09T00:36:18Z–00:37:09Z, 50.8068 s. Exact cases are teleport duplicate
  evaluation and walk-stop, both on production actors.
- Final CPU: `Contact-final-cpu.json`, 63 nonzero frame samples, mean
  **.143808 ms**, peak **.4346 ms** for the whole foot-contact marker. Editor,
  Windows 11, RTX 4070, D3D11. This is neither a standalone budget result nor a
  before/after delta. GPU and GC deltas are unmeasured.

- Baseline production Play: **0/1**, 2026-09-08T23:56:07Z, 25.2233 s.
  Expected completed frame 977, actual -1. This proves erased ownership; the
  native frame in this run did not itself demonstrate duplicate advancement.
- Fixed production Play: **1/1**, 2026-09-09T00:00:06Z, 25.1972 s.
  Covers both actual production actors, the native teleport frame, the following
  frame, and two explicit real playable-graph evaluations at landing weight .5.
  The graph evaluates at least twice while contact state advances exactly once.
  The landing weight is restored in `finally`.
- Shared foot-support Edit: **23/23**, 2026-09-09T00:00:58Z, .0384 s.
- Guard-only surface corpus: **0/1**, 2026-09-09T00:03:41Z, 70.7563 s.
  Five actor/rate cases completed; bot/120 stopped with a .019005 m planted gap.
  The probe agreed with the actual track, the planted target was stationary,
  and the contact weight was .9166666. Residual authored influence is a candidate
  explanation; final pre-IK base-foot telemetry was unavailable.
- Original-guard surface comparison: **1/1**, 2026-09-09T00:08:04Z, 64.5161 s.
  Thus the earlier failure is not proven pre-existing, nor does this establish
  that the guard alone caused it. Keep both raw reports; these runs did not lock
  the initial authored pose to an identical phase.
- Experimental contact-contract/support/rhythm Edit: **57/57**,
  2026-09-09T00:16:13Z, .1734 s. This includes two experimental tests that were
  reverted with the terminal curve and is not a final-source test count.
- Experimental rendered CLI Play: **2/3**, 2026-09-09T00:32:08Z, 92.4186 s.
  Teleport and walk-stop passed; bot/120 surface gap remained .017859 m with
  full submitted IK weight. This falsifies the stronger terminal curve as a
  sufficient fix. Its XML, surface trace and CPU sample are labelled
  `terminal-experiment`; the curve and its two tests were reverted.

Reproduction commands in the open editor:

```csharp
EditorApplication.ExecuteMenuItem("Elemental/QA/Hard Polish Foot Contact Edit Tests");
EditorApplication.ExecuteMenuItem("Elemental/QA/Hard Polish Foot Contact Play Tests");
EditorApplication.ExecuteMenuItem("Elemental/QA/Hard Polish Teleport Foot Guard Play Test");
EditorApplication.ExecuteMenuItem("Elemental/QA/Animation Foot Support Edit Tests");
EditorApplication.ExecuteMenuItem("Elemental/QA/September Actual Surface Foot Tests");
```

Run one suite at a time through the existing focused launcher, which restores
the scene. This uses existing test assemblies. The supplied optional
`tools/apply_foot_guard.py` was not among the five attached files and was not run;
the small change was applied manually after tracing the current source.

The new manual Edit launcher ignores calls while `-runTests` owns the process.
During recovery the connector repeatedly dispatched an earlier synchronous Edit
command, contaminating global Test Runner result callbacks. A file labelled Play
with 57 Edit test cases was rejected after inspecting its XML. Batch isolation
was also rejected: the installed framework explicitly does not support
`WaitForEndOfFrame` in batch mode, which these final-pose fixtures require.
Final Play validation therefore uses rendered, non-batch `-runTests` with an
explicit filter. These interrupted/mislabelled runs are not passing evidence.

Final command-line filters (Unity 6000.5.7f1 executable, project root above):

```text
Edit: -batchmode -nographics -runTests -testPlatform EditMode
  -testFilter "Elemental.Tests.EditMode.EarthAnimationContactAcceptanceTests;Elemental.Tests.EditMode.EarthFootSupportAuthorityIntegrationTests;Elemental.Tests.EditMode.LocomotionRhythmTests"
  -testResults "BuildReports/HardPolish/41b13d6a/Contact-final-edit.xml"
Play: -runTests -testPlatform PlayMode -force-d3d11
  -testFilter "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.TeleportAdvancesFootContactsOnlyOncePerRenderedFrame;Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.WalkStopKeepsKneesFiniteAndAvoidsAOneFrameLegSnap"
  -testResults "BuildReports/HardPolish/41b13d6a/Contact-final-play.xml"
```

The actual invocation used absolute `-projectPath`, `-testResults` and `-logFile`
paths and a hidden Start-Process launch. Keep the Game view active for the
rendered Play tests. Edit used `UNITY_MCP_DISABLE_BATCH=1` only in its process
environment. The launcher also exposes the broader surface test so its open
failure is reproducible rather than hidden by the smaller retained-fix filter.

## Acceptance boundaries

During final validation the old editor session reported D3D11 device removal
`0x887A0005` and shut down after a normal close request. It was restarted with
the same project/editor/API; final Edit evidence above comes from that fresh
session. This interruption is not counted as a completed or failed assertion run.
No cause for the device removal is claimed. Package-level Cinemachine warnings
were observed at startup; no package was edited to silence them.

The teleport regression is verified in Unity, not just by a model or source-text
assertion. This is not full Q04/G03 acceptance. No new CPU/GPU/GC delta,
standalone performance result, visual comparison, or complete
idle/turn/cast/surf/fracture/respawn matrix is claimed by this report. The existing
`Elemental.Character.FootContact` marker remains the profiling boundary.
`scene-context.png` was inspected and shows the production arena; it is not an
isolated final-chain comparison and does not close visual acceptance.

## Remaining plan

- G01: identify the problematic runtime renderers and mesh provenance, preserve
  baseline, then isolate topology/shadows/AO/DOF before generator changes.
- G02: runtime renderer/Volume inspection and day/night reference captures.
  Current source still shows main-only distant-stone lighting and a separate
  dust night-exposure multiplier; visual impact has not been newly measured.
- G03: reproduce and fix bot/120 planted gap with controlled initial animation
  phase, recording incoming/final pelvis and duplicate-callback data. Broader
  contact and final-chain visual acceptance remains open.
- G04–G05: action segments, school selection and local Fire lifecycle/combat.
- G06–G08: continuous menu departure, gold respawn and cosmetic results stage.
- G09–G13: backdrop, fog/DOF, thin dust, birds/storm and typed-event feedback.

None of those remaining items is represented as completed by this change.
