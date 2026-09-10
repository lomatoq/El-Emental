
Validation closure for charge behavior: Native05819/19 EditMode PASS (ring-only45Hz swept integration; other emitters90Hz). Native0591/1 PASS for held charge but late visual sample expired while encoding PNG; its zero counters are not performance evidence. Native0601/1 PASS with captureDeltaTime=1/60 restored after fixture. Actual late frame:24sectors,869parcels,1099queries,0budgetstops,36blocked contacts,7.8805ms CPU step/upload; not a like-for-like frame-time/GPU benchmark against Native056. Fixed-step screenshot retains irregular visible continuity at obstacles; full visual ring-density acceptance remains open. Full-charge screenshot: BuildReports/HardPolish/G05/FireAbilities/20260909-211541-ring-held-full-charge.png. Editor restored saved scene, no active run. All changes uncommitted.

## September 9 — held Fire and shared Earth charge feedback (uncommitted)

Imported held Shift+Space ring (release on chord release, no automatic timeout), dual mouse click fireball / swipe ground line, sequential overlapping upward ground pulses, cooling-tail Fire parcels, attached sustained ignition, foot surface heat and shared Earth Pillar Charge animation. FireRing is a separate read-only camera charge channel and uses the existing FOV/chromatic/vignette envelope without another camera writer. Flight uses existing Fall ownership and bounded 18-degree presentation lean.

Evidence: six staged assemblies compile; 27 standalone pure checks pass. Native054:31/31 EditMode PASS. Native055:0/2 exposed wrong Fire controller lookup in humanoid pose and an input-disabled camera fixture; both corrected. Native056:5/5 PlayMode PASS on saved fighter: exact Earth Pillar Charge state, held ring and FOV/chromatic/vignette response, Fall/18-degree flight lean and reset, sustained visible ignition/cooling, both ground-line press orders. Native057:1/1 PlayMode PASS through actual mouse input for both press orders, one fireball and no second-release duplicate. Late expanding ring capture still shows uneven continuity around scene obstacles; do not treat these behavioral passes as complete visual ring-density acceptance. Before import the previous Editor terminated with unrecoverable D3D11 device reset (Editor.log); restarted once after importing 25 files. This failure occurred before these changes were imported; cause is not established. Full plan and GPU/GC acceptance remain open. Baseline HEAD b646f675, dirty working tree retained. Backup: ../FireHeld-before-import.

# Hard polish execution — September 9, 2026

Base: `main` at `41b13d6a` (clean working tree before this work). The supplied
Belarusian handoff/audit/plan/contracts/acceptance documents are reference inputs,
based on older `cb7538ad`. Their hypotheses and proposed values are not measured
results or proof that a feature is already integrated. Current owners and recent
main changes take precedence over stale implementation assumptions.

## Latest verification — 2026-09-09, work in progress

Current code is uncommitted on `b646f675`. This section supersedes older
intermediate counts below; failed and interrupted runs remain evidence, not passes.

- `Final-contracts-edit.xml`: 164 passed, 0 failed, 2 skipped. The two managed-GC
  checks are unsupported: escaping positive-control allocations return zero on
  this runtime. Zero-byte claims from that counter are invalid.
- `Toe-projection-edit.xml`: 23/23 passed. Toe-floor prediction now uses the same
  weighted rotation as submitted foot IK. Actual repeated surface validation is
  still pending; the previous run failed locked-toe over-lift and a separate
  swing-height heuristic that included pit depth.
- `Final-native-visual-owner-play.xml`: 9 passed, 2 failed, 1 skipped. Passed:
  actual Fire forms/captures, three shader families in both Forward paths,
  eight production temporal pairs, both G13 channels/cues and bounded flood,
  all eleven visible techniques and moving ten-second Fire hold.
- The two failures above (HUD hint size after pause and heavy fixture ingress)
  subsequently passed in `Final-repeat-heavy-fire-play.xml` (6 passed, 1 failed,
  1 unsupported GC skip). Heavy captures use the real saved Linebreaker avatar,
  original material and released-body presentation event at normal speed.
- Production temporal testing uses the active EarthCinematicDepthOfField feature;
  legacy MiniBokeh is intentionally disabled. Eight ten-second motion scenarios
  have matched-pose frame pairs and renderer-ID interiors. Pair mean RGB deltas
  range 0.000688–0.000808; this is not a guarantee for every moving frame or edge.
- Startup bake succeeded with 1939 recursive plans shared by both saved pools.
  Runtime admitted all 1939 with zero misses and 7368/7368 cooked meshes; no stale
  cache warning. Existing mesh GUIDs and prior transform geography are retained.
- Gold twenty simultaneous dual-life cycles and stage six day/night outcome
  cycles passed in `Native-production-acceptance-play.xml`. Menu native 1080
  layout passed in `Fire-temporal-menu-owner-play.xml`.
- Fire visual quality remains OPEN: current native stream still reads as a bright
  ribbon. Column flame improvements and functional passes do not close this gate.
- Standalone native-GC diagnostic build is in progress. No standalone performance
  or zero-allocation result is claimed yet. Final A/B must use one final binary.

## Continued full-plan execution (base `b646f675`)

The user requested completion of the remaining plan. Work is in progress; the
earlier retained scope and evidence below describe the previous commit only.

Current-source inspection identifies the active saved wind-dust profile as
EarthClusterGroundDustProfile (384 particles before this work), distinct from the
unused 192-particle EarthSurfaceWindDustProfile. The new active candidate uses 112.
EarthCinematicDepthOfFieldController.ResolveFrame already applies the requested
0.5 bokeh-radius multiplier; it is preserved. Main/settings retain live-clock behavior.

The first added production regression evaluates the actual animation graph twice
and compares the resulting body position. It passes on the base implementation
(`b646f675/Pelvis-baseline.xml`, 1/1). This does not establish a missing cached
pelvis correction; a new correction will not be justified by that hypothesis.
The planted-contact pelvis timing experiment and the complete surface corpus are
being evaluated separately, without relaxing contact acceptance limits.

G01–G13 source candidates and the saved scene installation are integrated; combined
acceptance is still in progress. This is not a completion claim. Current evidence:

- September 9 08:52 UTC scene installation succeeded: twelve floating meshes keep
  their GUIDs, all 117 prior transform entries have zero position/rotation/scale
  change, twelve new entries were admitted, and no new component-bounds conflict
  was detected. See Integration/SceneInstall.json. Rendered acceptance is pending.
- Final-integration-foundations-edit.xml: 52/52 passed, including saved mesh shape
  containment, in-place retention/timing, native initialization, and pure timelines.
- Integrated-fire-respawn-stage-play.xml: 9 passed, 9 failed. The subsequent
  lifecycle run was interrupted while waiting for an uncreated bot pose controller;
  it has no completed XML and supplies no passing-test claim.
- Bot Earth telegraphs remain their normal animation owner. The new Fire bridge
  prepares dormant pose drivers and leases their ownership only through Fire and
  recovery. Runtime verification is in progress. Results controls now remain
  disabled until their reveal completes, preserving host-only Retry authority.
- User visual review rejects the current fire screenshots and generic mannequin
  animation comparisons. Fire appearance and G04 comparisons require the actual
  saved stone humanoid. Functional Fire tests do not satisfy that visual gate.

- Geometry/contact/camera Edit: 114/114. Targeted G01 bake: 32 existing assets,
  GUIDs preserved. Render orbit: 1080 static diagnostic frames; temporal flicker
  and production SSAO/MiniBokeh remain separate acceptance work.
- Camera continuity: four aspect/motion combinations passed; real Escape before
  readiness and during departure passed. Lighting sun/moon/point tests passed
  for all three material families in Forward and Forward+ before light-layer QA.
- Bake/channel/Fire math Edit: 39/39. Production 10-second channel hold passed.
- Fire authority/environment Play: seven cases passed, including 30/60/120 rates,
  compound colliders, cover, reset and capability. Further reentrant Stop tests pending.
- Respawn/Fire/backdrop Edit: 26/29; three respawn timeline test clock rounding
  failures corrected. Subsequent stage/dust/storm/response Edit: 29/29, including
  all six respawn timeline cases. No production respawn acceptance yet.
- Latest foot run: walk-stop passed, surface corpus failed bot/30 drift at 16.38 mm.
  The floor branch now uses the existing normal-IK submission weight conversion;
  complete surface corpus must pass again. No tolerance relaxed.
- Combined source compiles after correcting test-only MouseButton ambiguity and
  installed URP API usage. Earlier compile failures remain archived in
  Logs/HardPolish-* rather than counted as test runs.
- Integration inspection found two scene duel components (one disabled, unbound
  online placeholder) and two Earth audio directors. Installers/tests resolve the
  actual frontend-bound match and local executor-owned audio, not enumeration order.
- G09 first rebuild rejected a saved MainIsland root; rollback restored mesh
  assets without saving the scene. The revised in-place API retains existing
  transforms/LODs; its first candidate introduced eight conservative
  component-bounds conflicts. The rejected report is archived in Integration;
  shape compatibility is now corrected and installation passed above. Bounds overlap is not a proven triangle
  intersection, and original geography has not been nudged to hide the issue.
- G07 component installation exposed native MaterialPropertyBlock allocation in
  a nested field initializer; moved it to explicit initialization. Seven native
  initialization Edit regressions passed in the latest 52-test run.

Evidence resides under BuildReports/HardPolish/b646f675 and per-slice HardPolish
folders. All current-source changes remain uncommitted during integration.

## Previous committed scope

**Retained scope at `b646f675`:** teleport frame guard, diagnostic counter and regression
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

## 2026-09-09 current-editor integration (supersedes earlier staged-only status)

The saved EarthCoreSlice now contains restored original Island_6..11 LOD meshes, separate supplement meshes for the 15 new island roots, and seven validated additive floating rock companions (three West, four East). Unity Preview rejected105 occupied proposals, accepted all seven, and InstallCurrentScene saved successfully; independent inspection confirms seven roots and a clean scene. Existing island transforms were preserved.

Integrated G07/G08 canonical respawn/terrain wave/result arm-chain corrections, bounded domino admission, locomotion run-start/turn-release changes, collision-transported fire demonstration, and stone matte response. These are implementation changes, not completed visual acceptance. Unity reports no compilation errors; both RumbleRockLit and FireFlowParcel report supported with no shader errors. Direct execution inside Unity passed saved20render+12physics mesh geometry assertions under identity and anisotropic transforms. The active7368render fracture meshes have no zero or opposing normals; three corner normals have dot below.99 (minimum.989413). Removed radial side-normal substitution from six stone materials and the default shader setting so lighting uses the actual geometry normals.

Still open: native actual-Linebreaker captures and PlayMode acceptance of newly integrated changes; stone rest/pair collision repair and Ctrl+Space guard integration; updated2-way fracture cache; repeated foot corpus and final standalone performance/GC. Earlier passing runs do not certify this new revision.

## Review ended: integrated follow-ups, 2026-09-09

User explicitly finished personal Unity review. Imported the additive transported capsule follow-up (existing atlas detail, sparks, stream lighting and heat), user-confirmed old stationary column look, dusk/dust correction, stone counter/rest/pair fixes and stronger installed-island checks. Preserved current scene/user changes through baseline checks and backup. Fixed counter installer to follow FrontendFlowController.MatchController rather than assuming the scene contains one duel (online placeholder is also present). Counter binding saved on actual local Linebreaker.

Actual current-editor after-review-edit-001: 35 passed, 0 failed/skipped, 11.42s. Includes saved geometry/materials, installed seven-island composition, dusk continuity, fire solver and stone counter/rest response policies. PhysX/native tests started in after-review-physics-001. Appearance and real collision acceptance remain pending; two-way fracture cache still needs rebake before final performance.

## Native follow-up results, 2026-09-09 14:54 local

Review pause ended by explicit user reply. Current saved fracture cache was rebaked with two-way partitions (475,094,864-byte asset EarthCoreSliceConvexFracture_813b1e7967c2dc27b81e5ceeb4dee4bb); bindings saved. Earlier rebake-pending statement is superseded.

Native after-review-physics-003: six stone tests passed, two fire tests failed. Saved stone pile sleep/support wake, opposing pair collision, armor temporary ignore expiry and both production-column domino tests pass. The fire shallow-contact correction does not yet resolve PhysX zero-point initial-overlap contacts; exact regression expects wall z=3 but receives zero, and the full transport test diagnoses one invalid contact. This is open, not accepted.

Native after-review-native-001: terrain wave, proxy alignment and stone surface captures passed. Result pose first-outcome arms/hat assertions passed before its UI-exit fixture failed; sunset fixture failed ambiguous actor selection. Actor selection now follows frontend match authority; result fixture now sends pointer events with a bounded exit wait. Native-002 is rerunning sunset only: its other selector accidentally used the partial source filename rather than MatchStageProductionTests.ResultArmsRemainOutsideTorsoAndWholeHatIsFramed, which must run separately. No pose acceptance is inferred from that unmatched selector.

## Native character/counter/respawn follow-up, 2026-09-09 15:02 local

Native-002 sunset capture passed (51 frames, actual original Linebreaker material), but visual inspection reveals a near-horizon readability trough. Face-region display luminance drops .244 -> .101 -> .021 at frames20/25/30 while the sky remains bright. Imported a bounded existing grading recovery timing correction; new A/B capture pending. Whole-screen successive-frame dust differencing may include animated fire/temporal changes and is not isolated dust acceptance.

Counter-pose-001: three passed, one failed. Actual Ctrl+Space routing/movement/jump suppression, canonical counter partitions, and all three result arm/hat poses with actual UI return passed. Third-size counter capture failed because preceding movement carried the actor behind the gateway; imported independent fresh-scene size tests with physical ingress checks and trajectory telemetry. No production collision or guard bypass introduced. Native rerun ongoing.

Movement-respawn-001: both GoldRespawnProductionTests passed, including40completed cues over20simultaneous cycles, zero reservation failures, unchanged scales/materials and five rendered post-active frames per cycle. Actual locomotion run/start passed its assertions, but fourteen stationary turn foot-release jumps fail at60/120Hz. Full270PNG/telemetry evidence retained; agent is repairing the final-chain feedback, not relaxing the threshold.

The current-editor build entry point accepts an explicit worktree identity/output directory and refuses dirty scenes or existing executable evidence. Final standalone performance/GC remains pending; editor timings do not close it.

## User-approved fire direction and latest native findings, 2026-09-09 15:25 local

User explicitly likes the current fire and requested only slightly less internal glow. Preserved capsule form/motion/additive body/sparks and reduced the core contribution to75% via _CoreGlowScale; no broad visual redesign. Current native005 fire capture passed and its corner camera now sees the interaction rather than the wall's back face. Runtime materials use the shader default with no overriding setter. Prior72PNGs retained under HardPolish-fire-inner-glow-staging/EvidenceBefore outside the repo. Native004 strict shallow-contact/full PhysX/capture tests all passed after ClosestPoint surface recovery; no invalid contact or query saturation in the three collision scenarios.

Current twilight grading+ambient bridge retains full day/night endpoints. Actual frame30 face patch .0211 before follow-ups -> .0316 grading -> .0613 ambient; helmet .0574 -> .0845 -> .1654. Native007 diagnostic confirms no broad face SSAO blackout, black albedo, or inconsistent normals. Remaining facial twilight readability is an art-balance limitation, not a proven shader failure; no further blind grading multiplier introduced.

Native005 small counter passed; medium/large ingress preparation still needed correction. Native007 medium passed; large countered primary1800kg exactly once but two150kg debris contacts followed. A real secondary-fracture immunity propagation bug was found and fixed independently: counter descendant eligibility remains false through ordinary physical partitioning while inherited velocity remains unchanged; deliberate magic release restores eligibility. Native008 is validating this fix.

Native006 locomotion release transition removes all but one prior discontinuity. Remaining right120Hz frame187 is a59.9mm vertical floor/toe projection change. Added diagnostic toe/rotation/floor values; native008 reruns unchanged movement acceptance. Earlier failures and captures are preserved outside the repo. No full locomotion acceptance or final standalone performance/GC is claimed yet.

## 2026-09-09 latest review: fire cooling, mesh holes, single-rock pin, animation research

The user's latest review supersedes the earlier minor-inner-glow acceptance: hand fire still clips white, needs distinct orange/yellow, cooling smoke and smaller varied wall tongues; column fire needs a localized hot center and orange outer/top. Five hash-verified fire cooling candidate files are integrated. Native run `fire-cooling-locomotion-010` is in progress. Its new head-on frame still clips broadly at wall overlap; this is not visual acceptance. A lower per-parcel energy shoulder and a localized column-temperature candidate are staged outside Assets, pending this run completing.

New cache render audit found 189579 invalid normals among 13518 render meshes, with sampled collapsed/repeated-vertex triangles; collider samples have valid normals. This is a render geometry defect, not a normals-only tweak. Repair is being prepared from unchanged collider hulls with robust planes and a valid closed-hull fallback.

The user again reports a stone trapping the enemy without death. Existing sustained-load and pile checks do not certify the single-stone case; a production single-stone contact/load/recovery/health regression is being prepared. Root has not claimed the incident reproduced or fixed.

Animation primary-source comparison is at `../HardPolish-animation-research/ANIMATION_SOLVER_RESEARCH.md`. Two-bone IK already exists; target history, foot-contact phase and bone/goal mapping are demonstrated failure sources. Current post-solve mapping fix awaits native010's unchanged position/angular assertions. The short offset decay does not preserve outgoing velocity and must not be called full inertialization.

Editor was independently observed idle before imports and native010; no user Play session was stopped.

## Native verification update: movement013 and crush014

`locomotion-toe-013` PASS on saved Linebreaker, both60/120Hz with unchanged position/angular/calibration limits. The remaining010 release jump was demonstrated to be a bad pivot capture toe clearance (69.7mm correction), now measured before new lock capture. This certifies the tested transitions, not every animation clip or broader visual quality.

`crush-unloading-014` BOTH PASS: actual single90kg stone kills the pinned bot at frame414 (~8.3s measured fixed steps), exactly one score; removing actual loose-rock pile stops damage. Before fix012 remained27.55HP at frame899. Existing force threshold unchanged; .30s qualification memory bridges short solver unloading, adds no qualifying time or damage below current force threshold, resets for clear geometry/nonphysical state.

Latest user sky screenshot: source inspection identifies atmosphere composition ordering: near SRPDefaultUnlit fire is drawn before ValleyAtmosphereV2 blends the entire source using opaque background depth, so sky fog attenuates near flame as distant sky. Dedicated post-atmosphere flame/smoke route with true foreground depth rejection is in preparation. Mere brightness increase is insufficient.

## Review hold after native015

Native `fire-contact-015` FOUR PASS: exact shallow/growing sphere, tangent/separating/inward contact, perpendicular corner and actual PhysX3scenario. Global per-frame query exhaustion improvement still needs actual stream capture after this fix.

After that run completed/restored, editor was observed externally in Play (not compiling/updating). `policies-edit-016` was rejected before starting; no result/acceptance claimed. User clarification is pending; no stop, sky import or cache repair is performed during this review.

Ready unimported: `../HardPolish-fire-sky-depth-staging/manifest.json` four files; post-atmosphere route, exact-clear-depth guards, actual sky/foreground diagnostic. Current live fire still has pre-atmosphere route until this stage is imported. Native depth-bypass ratios certify clipping behavior; actual before/after visual capture is separately required to certify sky readability. Current live energy cap .30, localized column temperature, cooling smoke and contact skin fix are already imported.

Cache backup475094864bytes+meta verified at `../HardPolish-cache-render-before-repair`; no cache mesh mutation yet. Render reconstruction/editor audit stage in progress.

## Latest user corrections: denser volume, towers, all dust routes

Hand parcel density first increased20% at user request. User still found it too transparent; pure additive radiance cannot attenuate the background. A colored hot-medium extinction term now shares the existing alpha smoke draw (no third draw), while hot emission remains additive with .30 per-parcel cap. It is a hybrid opacity/emission model; native visual acceptance is pending021. Same shared shader applies to towers.

Stationary tower transport now live:32particles each,24births/s,~1m/s upward flow,3m path; maximum224 particles across7towers, existing nearest4lights. Native Edit02025PASS includes15/60/120stationary flow and originalhandsolver tests, exact native failing hull regression and wall/bevel rules. Native019 tower test failed in an irrelevant shared hand-session readiness gate before visual checks; corrected tower-only actual combat fixture now running021.

Dust audit126 callsites/16savedcomponents found dead gravity feedback binding and missing ordinary hold emission. Live AirborneShed uses actual held/captured lower surfaces, max3bodies/.16s, existing hub/pools/newatlas40/60mix and downward chips; known EarthMagicFeedback owner link repaired. Native019 isolated actual capture/newdust/fallingchips/cancel test PASS. SavedLinebreaker five-rock visualcapture staged/imported, pending run.

Sky017/019 demonstrate visible fire over sky after routefix, but quantitative fixtures failed due moving background pixels, not accepted as all-pass. Stable-sky mask now excludes measured temporal drift and retains original .9visibility/.9–1.1energy on >500stablefirepixels. Foreground wall test still required. Run021 pending.

Cache repair: first version failed after0 because source collider triangles have duplicate edges/Tjunctions even though PhysX cooks their vertex cloud. Convex-envelope fallback solves first147vertex73triangle mesh (74closedfaces), but full native attempt failed after343 at north_west cell0/cell3. No asset save occurred; diskSHA remains974D5D8D226D6960C203B8DF5E7E8B82406F9F98FBFC66E25DABAA6A067E422D, partial in-memory edits discarded by forced synchronous reimport. All13518 originalcolliders exported to G01/all-original-collider-hulls.bin for wholecorpus audit. New cache is NOT repaired yet; do not claim holes fixed. Wall independentclamp replacement is live; complete cache topology work continues.

## Native cache completion and current fire opacity follow-up

Supersedes the pending cache notes above: full native repair saved and synchronously reloaded all 13,518 render meshes. Final audit: 620,296 triangles; zero invalid normals, opposing corners, degenerate triangles, or open/non-manifold meshes. 13,461 unsafe bevels use closed convex render envelopes; 57 retain validated bevels. Physical collider geometry signatures unchanged. Reports: G01/contained-render-repair-final.txt and contained-render-audit-reloaded.txt. Saved cache SHA256 1595176D6656903ADB039C7B789A8BC150221D565003084E4A344279B1E586D8. Imported active FBX props and separate generated arena cells remain a distinct geometry investigation; this is not a claim that every scene hole is fixed.

Native021 tower transport PASS (source seating, light limit, pause and restore). Sky pixel fixture still failed from camera dithering; new fixture disables/restores dithering only during diagnostic capture and keeps acceptance thresholds. Native022 saved Linebreaker actual five-rock gather/hold/move/release capture PASS, 40 images. New atlas dust was too confined visually; its existing six births now spread farther below the pile with larger, longer-lived puffs, unchanged pools/counts. Native024 captures pending.

Latest user still found fire transparent: shared colored medium now uses optical coefficient12 (previous7), maximum body alpha.90 (previous.72). Additive emission remains bounded at.30, avoiding extra white bloom. Shared change applies to hand and stationary tower fire; native024 verifies sky, tower and held dust.

Native024: tower and actual saved Linebreaker held-dust capture PASS. Sky first-view visibility31800/31818 and near-identical energy, but freeze count captured before pending LateUpdate failed132vs139. Fixture now waits through a complete zero-delta frame before storing baseline; same original equality and all visual gates retained.
Native025: BOTH PASS, three sky views and foreground occluder plus actual prepared arena cell render integrity. Sky visible/expected40208/40234,16444/16452,11914/11938, energy ratios~1; foreground normal0 vs bypass40732. Tests restored production scene. Shared denser fire visually inspected in actual1920x1080 hand and tower captures. This proves visibility/depth behavior, not final user art acceptance. Separate175 prepared arena cell repair now live and native closed-face/normal validation passed.

Dust024 visual review limitation: falling chips clearly visible, but new atlas puffs are not clearly distinguishable beyond the held pile silhouette in hold005/move003. Counts increased, but live pile body count differed from022, preventing controlled pixel A/B. Release also contains real impact dust. Routing tests pass; do not claim final held-dust visual acceptance or solve it by another blind density increase.

## User accepted fire color/opacity; refine tail and only surrounding half-arches

User clarified simplified decor refers ONLY to half-arches around the arena. No island/LOD/other decor production changes made. Diagnostic026 compared3views automatic/high LOD (PASS); this is not the requested target after clarification.

Native02711 solver tests PASS. Native0286PASS: shallow contact3, PhysX head-on/oblique/corner, actual Linebreaker fire72-frame capture, all7 half-arch actual mesh captures. Tail refinement physical radius contracts after .48 life/path, middle preserved, initial emission+20% before shoulder; stationary unchanged. However visual corner008 still shows red ellipsoids: hot extinction skipped authored tongue mask, filling additive gaps. A shader follow-up is required, not accepted as final shape.

All7 intact half-arches still render full original OuterStoneRing FBX,5212–11240triangles each, intactRenderer enabled. The regression is the fracture exposure route: robust fragment repair discards authored smooth exterior normals and substitutes collider hulls. Native source audit found88 OuterRing baked Render assets all exact-closed with zero invalid unit normals. These authored meshes can preserve existing shape rather than rebuilding it. Data: G01/outer-arch-baked-audit.txt and outer-arch-baked-pieces-with-normals.bin. Targeted preservation and post-damage/restore native test pending.

Native029 both fire capture and three-view sky/foreground PASS. The cooled/contact tail now uses separated winding density and shares its authored silhouette between hot medium and additive emission. Current corner008 shows thin tails exposing the contacted wall instead of filled red ellipsoid outlines; fresh/middle fire remains dense, first16% emission gets20% pre-shoulder boost. Actual216 simulation rows: query peak621/768, zero budget stops, editor mean1.305ms peak3.161ms (not standalone/GPU acceptance). Stationary tower styling unchanged.

OuterRing authored-render policy installed and saved only on7 owners (15 new serialized booleans,7true8false; verified scene diff against immediate backup contains only these fields). Initial save was marked dirty again by deferred Undo records; run030 never started. Flushed own Undo records, saved and native031 TWO PASS: all175 prepared cells closed;85 OuterRing exact authored references/normals,90 other cells retain helper; all7 arches expose remaining attached cells after release, preserve smooth normals, and restore. Report G01/outer-arch-authored-damage-restore.txt. Native032 focused visual capture pending. No LOD, FBX, material or whole-cache changes made for this user correction.

Native033 focused unobstructed half-arch visual capture PASS, restored clean production scene. Images at G01/HalfArch-20260909-171259 cover all7 intact/after-damage/restored states plus no-post diagnostic; OuterArch02 before/after reviewed, curved silhouette/chamfers remain. Earlier032 camera was obscured by inner arena columns, so it was not used as final visual evidence. No remaining work in this focused fire-tail/half-arch correction; larger plan performance and earlier separately recorded dust/geometry limitations remain open.

## Fire ability expansion and immediate canonical respawn size

New user scope: fading soot, cumulative stone burning and push, progressive foot lift, rapid-click hand/foot bolts, Shift+Space ring, dual-mouse ground swipe. Contract ADR0037. Verified staged import preserves previous sources outside Assets in FireExpansion-before-import. The saved scar owner had every reference empty: scoped installer repairs it and activates Fire-only marks, avoiding unrelated historical Earth rectangles. Existing configured Earth scar owners retain their behavior.

034:18/18 native Edit policy/respawn checks passed. 035:2passed/6failed; real keyboard/mouse routing passed. Four direct fixtures selected Fire before frontend capability readiness; two burn fixtures left acquired matter in Forming. Shared stale pool rejection text did not prove a Fire transaction. Corrected fixtures use existing readiness and StopBendControl; no ownership safeguard is bypassed.

036:2passed/3failed. Actual ceiling sweep and hand/foot release, ring, supported ground line and cancellation passed. Actual lift reached14.7759m and8m/s after2.8s but selected Apex: lift-only animation input was inside the existing .3 apex band, now corrected. Respawn fixture wrongly expected authored scale1 instead of1.2, now measures canonical scale and first gold proxy factor separately. Soot fixture referenced a nonexistent material path, now targets GraphicsV5 RumbleSandstone.

Desktop URP Automatic decals use DBuffer. RumbleRockLit (also character mode) and DistantStone now receive albedo-only decals. Prior default-cube soot test proved lifetime/attachment but its black cube image was visually inconclusive; production-material before/after pixel measurement is required. 036 sparse foot/ring parcels prompted an ability-only injection refinement: inherited projectile velocity, charged downward foot jets and denser ring sectors. Accepted stream/tower defaults remain unchanged. Final native rerun and visual inspection pending at this entry.

Final Fire acceptance:037 Edit38/38 PASS. 038 native7PASS/1FAIL (soot still invisible). 039 native5/5PASS after the final corrections; combined distinct passing native scenarios9: real keyboard/mouse routing, progressive actual lift/Falling/release, real ceiling, actual hand/foot launch with ring/ground/cancel, respawn lifecycle, canonical3-size burning, actual stream contact, visible moving/fading soot, independent gold-proxy geometry.

Visual review caught a real gold-proxy geometry bug that transform-factor tests missed: default BakeMesh retained scaling, then copied hierarchy applied it again. Static gold bake now explicitly uses BakeMesh(mesh,true); victory skinned proxy path unchanged. Independent actual LinebreakerBody CPU bone-weight/bindpose oracle compares5095 world vertices: max error0.000015259m; expected height2.343948m, proxy2.343964m. 039 firstgold/firstactor PNGs confirm matching apparent size at the same camera. Timeline presentation scale stays1 from first visible frame, authored actor1.2 preserved.

Soot's actual URP template ignores tint fields and reads Base_Map RGB directly; cached texture now contains dark RGB with irregular soft alpha. Thin projector pivot explicitly zero centers its .16m volume across the contact, instead of the package's default .5m pivot missing the surface. RumbleSandstone measured luminance .3079255 unmarked -> .0749985 marked. Attachment/fade/7s expiration PASS; captures FireWorldScorch-20260909-183659 reviewed. Fire-only ownership remains enabled for the previously empty repaired pool.

Final foot-only injection increases near-nozzle density within the same32-parcel pool; ground flames reject disabled/destroyed/reused support generations. 039 actual ascent14.6914m at2.812s,8m/s,Falling. Effect step/upload editor mean.3228ms,peak1.5052ms across153frames, zero budget stops. Earlier038 ring capture1.66ms/432queries/214parcels with no budget stop. These are scoped editor CPU diagnostics, not whole-frame/GPU/standalone/GC acceptance. Ring and foot jets remain stylized parcel approximations; accepted main stream/tower defaults unchanged. Broader full-plan native-GC/performance and previously recorded unrelated limitations remain open. No commit/push performed.

## Fire/arena/radial follow-up, September 9 evening

User required stronger flame force/fracture, denser coherent charged abilities, hot torches, soot/char/smoke, night dust, armor departure chips, enemy size parity, retained arena detail and radial running. The later persistent blue flash report was confirmed in measured ambient values: at phase .505, sky RGB (.27,.345,.465), equator (.18,.23,.31), from a temporary DayAmbientSky*1.5 bridge. Removing that bridge keeps authored ambient interpolation; grading recovery remains. Native natural-clock test uses one initial phase assignment then real progression through sunset, checks ambient endpoint bounds and unchanged actual material shaders.

Sources imported with SHA256 checks from isolated staging while Unity was idle. No manual Play interrupted. User subsequently explicitly confirmed automatic testing. Backup: ../FireFollowup-before-import and ../FirePolish-before-import. No commits/pushes.

Native040:24/24 Edit (exposure/generation safety, thermal lifecycle, flow and knee transport). Native041:0/9 due real Unity MaterialPropertyBlock field-initializer exception, fixed by creating the native object in Configure. Native042:8/9; armor fixture incorrectly treated wheel-selected Phase01 as elapsed assembly time. Added real wheel input; no runtime gate weakened. Actual enemy world-vertex heights2.355710m/2.384718m (ratio1.012314), both root scales≈1.2.

Native043:2/5.175cell source topology/detail check passed, dusk captures passed. Arena first-damage fixture incorrectly tried plucking the explicitly protected base floor. Existing locomotion fixture was contaminated by bot ingress reactivation; radial600frames were grounded with source frame angle0 but its last-normal run-only lock sampling was insufficient to test planted turns. Corrected ingress ownership and added explicit stop/turn contact phase. No gate weakened.

Visual review rejected cold red torch blobs and sparse foot beads, despite passing pure/compile checks. Hot torch birth restored at fixed32slots; feet now stagger first swept timesteps, develop body faster and defer tail erosion at unchanged96slots. Native044:16/16 revised solver checks. Native045:7/9; armor3shots/chips, smolder lifecycle, coherent foot lift, large moving/fading soot, bounded torches, normal run/stop/reverse and natural-clock sunset passed. Radial failed after the prior lane was exited sideways and the actor died: fixture missed canonical respawn render-root interpolation flush before resetting motor aim. Added that production ordering and continuous support checks. Arena floor immunity fixture still failed and was corrected to assert immunity, then exercise the seven breakable owners.

Accepted visual evidence so far: G05/FireLift-20260909-193638/ascent.png has continuous paired short jets from soles,139scoped editor CPU samples mean1.024472ms/peak2.3338ms, zero query budget stops. Ring 20260909-192419-ring-dense-release.png:404parcels/1199queries/4.4825ms in that editor frame. Smolder 20260909-193626-smolder-hot-char-smoke.png has irregular small ember veins; old round glow masks were rejected. Native046 verifies smoke on/off pixel contribution after using the actual impact atlas at normal lifetime playback, arena damaged detail and corrected radial traversal. Full standalone GPU/GC budgets remain unproven.


Native046-FinalVisualGates:3/3PASS,82.4147629s; restore completed. Smoke renderer on/off pixel comparison174132 affected pixels, actual protected-floor immunity and breakable-owner detail preservation pass. Four-normal run/stop/turn960frames all grounded, source frame angle0 degrees; lock counts59/54/63/56. Gate intact/damaged and final foot/smolder images reviewed. Root reviewed0/90/180/270 samples:90 is veiled and180 completely hidden by the existing underside atmosphere. This is NOT accepted radial visual evidence despite passing numeric mechanics. A proposed out-of-seal diagnostic stage was not imported; requested a production near-geometry correction instead, preserving distant underside closure. User clarified persistent blue occurs in normal gameplay. Ambient overshoot correction alone does not establish that report fully resolved.

After046, Unity was found playing with bridge running=false/restorePending=false. No manual session stopped and no Assets imported; clarification requested before more native runs. Read-only shader audit: RumbleRockLit zero messages; LightDustMote reports potentially uninitialized DustVolumeMainShadow warning (provenance not yet determined). Full standalone GPU/GC remains open.

Persistent blue foreground candidate reviewed and frozen in ../HardPolish-near-geometry-fog-staging (4 files, SHA256 baselines verified). Geometry-only12m clear foreground, smooth12–24m transition; original sky and all distant treatment>=24m retained. Simulation/Edit/Play C# compile passes; standalone pure checks pass3heights,2401continuity samples,4far distances. NOT imported during unexplained manual Play. Pending: native ValleyFogClosureRuntimeTests.ProductionFogFullyClosesFarSourceAndPreservesNearContrast (equator/south6m contrast plus80m seal), then original four-normal actor capture. The out-of-seal radius+160 diagnostic is deliberately not integrated. Dust warning cleanup is separately staged in ../HardPolish-dust-shadow-return-staging, native compilation pending. Next native run number047.


## Shorter living Fire and reliable ground gestures, September9 late follow-up

User completed review and requested shorter/denser/varied flame parcels across abilities, low ground collision spread, less black char with faster complete fading, reliable staggered dual-button ground gesture, and large-rock response. Imported21 SHA256-verified sources after leaving completed manual review; backups FireLiving-before-import. Fire parcel capacities unchanged, ring support probes bounded16hits/sector and counted. Foot lifetime .17–.20s, bolt .18s, torch .28s, short ring/groundtail .16–.20s. Direct fire push36m/s² capped24000N before energy; thermal exposure timing retained. Intact arena had been dropped before structural damage due absent/invalid loose target; now separate stable structure/generation exposure routes into existing structural gate. Nozzle overlap now retains actual nearest contact instead of returning without a target. Char shader minimum albedo factor .32–.46 (was .07–.14), ember slightly stronger; thermal hold1.2s followed by .25char/s fade, all clear within5.2s; soot3.5s smoothstep fade.

Ground gestures preserve general school/UI/focus suppression but have separate stream-only rejection latch, dedicated real support targeting and tangent-plane fallback, last-valid endpoint, and visible rejection status. Native regression includes both press orders after interrupted stream. Near-geometry fog candidate from preceding report also imported (12m clear/12–24m blend), as was single initialized dust-shadow return. Native047 targeted Edit underway; production048 planned. No standalone/GPU/GC acceptance claimed.

Native04745/45PASS. Native0487/9PASS: attached structural heat, smolder, foot lift, ring charge, actual staggered mouse chords, original960-frame radial test and actual URP foreground/far-seal contrast all pass. Visual90/180captures now show clear actual actor/feet at original test radius, distant fog retained. Native048 large-nozzle fixture unintentionally enclosed/crushed caster before heat accumulation; isolate only that rock/owner collision pair while retaining real Fire queries/force. Ability fixture required all loose supports to survive new destructive fire; now validates live support synchronously at cue birth and cancellation, not survival after damage.

048 images rejected: ring had only63live parcels due immediate ground overlap (12emitters225queries0budgetstops). Additional safe.48m injection, slower downflow and per-birth sphere-swept tangential spread preserve low contact while sustaining density; same48slots. Native050 validates nonconvex floor and ability visual regression. Char/soot combination still looked black in048; final char darkening weight reduced to.45, soot texture opacity.38 with warmer dark brown RGB. Native0492/2PASS: corrected actual large-nozzle force/split and lighter smolder clearing completely. Final hot-char201347 image visually shows retained brown material under ember veins. Updated shader gives maximum darkening multiplier .694–.757 before soot (rather than full-black overlay).

Native0505/6PASS. Actual projectiles/ground-node birth/cancellation and ring anticipation pass; three pre-existing corner/shallow collision regressions pass. New nonconvex floor density>28 passes, but its combined invalid/blocked zero check reports56, diagnostic follow-up pending. Ring201627 visual now407parcels,898queries,3.8664ms scoped editor step/upload,0budgetstops; dense continuous broad ring accepted, ground201617 forms substantial distributed flame along real destroyed surfaces. Final shader audit: RumbleRockLit/LightDustMote/FireFlowParcel0messages; AtmosphereFullscreen retains potentially uninitialized FragAtmosphere warning, no error. Scene restored EditMode and clean after050.

Native051 isolated nonconvex floor reproduced real issue: Invalid0,Blocked56,UnresolvedMesh56; no deep/sentinel cases. Exact spherecenter(-.07990486,100.9063,-.02863461),radius.4563153 near floorY100.5. Failed PhysX penetration resolution during radius growth now has bounded actual-triangle ray recovery (max6 queries per sweep); only measured front-facing hits within radius accepted. Unresolved/inside cases still block, normals are never guessed. Exact-center plus subsequent perpendicular-corner regression added; Native052 underway.

Native0525/6: triangle recovery reduced blocked56->2 and recovered252; remaining2 sit on exact4mm broadphase boundary, outside physical radius. Added1mm ray-length arithmetic tolerance, accepting measured outside-skin hit with depth0 so existing physical-radius admission correctly ignores it; no geometry expanded or invented. Native053 complete class pending. User reviewed ring again and still rejects gaps: next ring-only candidate doubles sectors within existing24timed seats and scales per-birth arc coverage with radius; earlier root visual approval is superseded by user correction.

Native0535/5PASS final shallow/nonconvex/corner class; no blocked/invalid parcels in isolated density case. Final ring-only correction imported after idle check: up to24 existing timed sectors,35% sector birth overlap tracks radius, active ground nodes reserved; older windup ring deliberately reused on release. Brightness/lifetime unchanged. Presentation and native fixture compile. Added overhead early/late(.4s expansion) ring captures and sector/probe/drop telemetry. Before Native054 could begin, user started Play after import; bridge correctly rejected launch. Native054 does not exist. Asked whether to leave manual run or finish capture; no manual session interrupted.
