# September 4 animation rehabilitation

Implementation in the dirty `d2174ed` workspace. This document records contracts,
not a claim of visual or whole-project acceptance. Unity compilation, focused
reports and final captures are recorded by the integrating task after execution.

## Armor, ordinary jump and centered aim — verified September 5, 13:16 UTC

This result supersedes the earlier armor-pose acceptance after the user's report
that equipped armor bent the character backwards with the head and hands raised.
The cause was persistent presentation ownership: active armor continuously selected
the `ArmorAssemble` semantic clip, whose upper-body mask includes body, head and
arms. Equipped armor now returns to ordinary locomotion after its finite action
window. A separate continuous encumbrance lane gives compact-to-expanded armor
about 83% to 75% of ordinary move speed. It does not enter cast brace, replace the
locomotion graph, or block automatic mantle; a simultaneous real cast still applies
its existing action brace once through the motor.

A short Space press was provisionally owned by `Pillar` before the 0.18-second hold
threshold, so presentation displayed `PillarJump` even when gameplay resolved the
input as an ordinary jump. Sustained pillar presentation now requires
`EarthPillarMobility.IsCharging`; confirmed ordinary takeoff clears an old magic
overlay once and does not suppress a later airborne cast. Exactly centered aim also
used the right-dominant branch because zero lateral input selected a side. The late
chest/head yaw and roll are now attenuated near the center line while pitch,
shoulders, authored arms, hand IK and real lateral aim remain intact.

Fresh focused reports on Unity 6000.5.7f1 are
`BuildReports/ArmorJumpAimAnimationEdit.json` **5/5 passed** at 13:05:54,
`BuildReports/ArmorJumpAimAnimationPlay.json` **1/1 passed** at 13:06:45, and
`BuildReports/ArmorJumpAimVisualProofPlay.json` **1/1 passed** at 13:16:48. The
visual proof uses the production `EarthCoreSlice`, readiness gate and shipping
Shift+MMB/short-Space input. Four full-body frames cover centered baseline,
centered armor, armor profile and short-jump profile. During armor the magic layer
and cast brace were both zero, encumbrance was 0.58, head height above the feet was
1.502 m versus the 1.538 m baseline, chest tilt was 2.59 degrees versus 2.51, and
the hands remained 0.601/0.625 m below the head. Centered chest-yaw change from the
baseline was 0.037 degrees.

The jump proof sampled every rendered frame for the first 0.22 seconds after real
support loss (19 samples), after waiting 0.8 seconds for released armor pieces to
clear the silhouette. It observed no `PillarJump`, zero maximum magic-layer weight,
0.207 m maximum hand-height asymmetry and at least 0.257 m from either hand to the
head. Root reviewed the resulting armor and airborne profile frames as readable,
ordinary hands-down silhouettes. These gates verify the reported armor/jump fault
and centered ordinary pose; they do not certify every magic clip's aim aesthetics.
The opt-in SONIC prototype remains experimental and is not accepted by this result.

## September 5 current repair, 11:30 UTC

Semantic logic 21/21, actual production magic 8/8, and real dual-mouse at 30/60/120 Hz
3/3 pass. Corrected spatial quaternion-offset angular velocities remove the prior
held forearm flip (C1 6/6); sustained targets follow body-relative aim, with bounded
chest response. Physical commits start at authored contact, while pre-commit load
keeps anticipation. Same-tick promotion has a distinct render generation so A/B
clocks recognize it without changing the gameplay tick. Ground-wave commits now
publish their previously missing event (actual pool/pose Play 1/1).

Per-source clip pacing prevents 4–8x playback of long flourishes. Rendered-contact
watchdog follows the actual source marker/rate and freezes with scaled animation;
it remains finite for invalid clips. This closes the newly exposed RaiseWall
contact timeout rather than just lengthening the test.

The latest matrix is `BuildReports/EnvironmentAnimationRescue/AllMagicVisualQA/20260905T112302289Z/AllMagicVisualManifest.json`,
36 valid frames covering eleven shared pose slots and a repeated punch. Head pitch
is -21.46 to +28.00 degrees, neck length >=0.1324 m. Root inspected platform, armor,
and repeated punch contact images. Final whole-body movement/lifecycle recheck is
still in progress; results below are historical when contradicted here.

## Reopened after actual input failures — September 5

The user's rapid-stone reproduction supersedes the old semantic-event pass below.
Two shipping routes were wrong: Quick Stone emitted an ordinary fragment launch,
which selected HeavyThrow/WheelbarrowDump; Dual StompStone sent rise and hover
requests but no punch request at projectile release. A 64-entry presentation queue
also replayed obsolete gestures for seconds after the input ended. The correction
uses a typed quick-punch launch style and latest pending intent, with rendered
contact required before handing over an active gesture.

Repeated casts need separate outgoing/incoming poses. The saved controller migration
`Elemental/Character/Configure Independent Magic Buffers` creates states `Earth Cast`
and `Earth Cast B`, each with its own normalized time and eleven direct weights.
Resetting the incoming clock must leave the outgoing clock unchanged. Holding a
single shared clock at contact is explicitly rejected: it would make repeated
same-slot punches static. The ordinary authoring path retains this migration.
Runtime input and final-bone acceptance of this change are still pending.

Fresh independent gates: `SeptemberSurfacePlay.json` **1/1**, 07:26 UTC, both real
actors on an actual pit/hump/slope collider at controlled 30/60/120 Hz steps;
maximum planted gap 1.80 mm player, 9.69 mm bot, maximum drift 1.13 mm.
`EarthFootSupportAuthorityEdit.json` **23/23**, 07:33 UTC.
`SeptemberMantleAnimationPlay.json` **2/2**, 07:35 UTC, after preventing the late
choreography pass from rotating chest/head/shoulders over the protected mantle pose.
These are scoped gates; they do not certify rapid magic or aesthetic quality.

## Historical focused gates — 2026-09-04 17:17:01 UTC

`BuildReports/SeptemberAnimationPlay.json` reports Unity 6000.5.7f1, **5/5 passed**,
0 failed/skipped, 76.2203702 seconds. This final-owner build passed authored/EAMM/
contact/additive head A/B, idle/turn/forward/backward/stop, all 11 accepted magic
presentations and EAMM recovery, and both production mantle scenarios (normal rig
and native hand fallback).

The idle/turn/stop trace contains 318 actor samples. Maximum reported locked-left
anchor error is 0.0180775635 m; reported right anchor error is zero. Minimum
head-to-hip height is 0.49211639 m. Calibrated head pitch spans -11.958536° to
-1.545204° (maximum absolute pitch 11.958536°). Zero anchor error for an unlocked
foot is a telemetry convention, not proof of exact sole contact. These numbers
describe this focused run, not the unexecuted 30/60/120 Hz terrain matrix.

`BuildReports/AutoMantleMotorPlay.json`, 2026-09-04 17:28:47.0900886 UTC, also
reports **15/15 passed**, 42.3505997 seconds: seven mantle checks plus eight existing
motor regressions. Mantle coverage includes forward intent, high ledge/blocked
headroom rejection, support destruction/release/footprint loss and new overlap
aborts, equatorial traversal, and real footprint rays at 30/55 degrees.
Broad final-skeleton slope/pit/edge/moving-support coverage and normal-speed visual
review remain open; do not mark the complete character work finished.

## Integration correction after first Play report

The first runtime report passed all 11 accepted magic presentations but disproved
the proposed callback-to-job IK handoff: graph evaluations advanced while explicit
weighted solves stayed zero. The downstream job ran before `OnAnimatorIK` supplied
its goals. The current implementation therefore uses `OnAnimatorIK` as the final
Humanoid contact owner after graph processing and removes the extra `SolveIK` path.
The terminal job is now observation only; `weightedContactPasses` counts submitted
contact passes, not Unity's internal solver executions. Existing head and foot-error
acceptance thresholds were preserved; the corrected five-test Play run passed above.

An original keyframed Humanoid mantle prototype has now been generated from the
upright Idle base; it is not Mixamo. Its sampled report records stable head-to-hip
height and actual arm/leg motion, but does not establish visual acceptance in scene.
Mantle entry resets stale impact-layer influence, ongoing impact decay continues,
and the missing-rig hand fallback uses base-layer IK while magic is weighted out.
Feet remain released until actual support is available; Settle timing alone never
marks the character grounded. Footprint rays now cover the height envelope derived
from capsule radius and allowed slope instead of a fixed 15 cm downhill budget.

Passed motor tests cover physical footprint rays at 30/55 degrees and a full
automatic equatorial climb. The passed scene mantle tests use forward motor input
and a broad ledge and check takeover, EAMM yielding, flight foot release, phase/clip
progression, wrist proximity, and return to real support/contacts.

## Findings and ownership

The EAMM Idle recipe used Crouch Idle. The ordinary controller's neutral child
used a single walk frame. EAMM full-body weight replaced the authored turn state.
Contact metadata and organic idle still read the standalone Animator while the
driver wrote an AnimatorControllerPlayable. Bot casting was written in LateUpdate
and cleared by the shared presentation on the next Update. Magic sampled six
fixed normalized timestamps and the playable backend discarded requested damping.

`PlanetMotor` continues to own movement, grounding and automatic mantle. Animation
never enables root motion or supplies gameplay damage/command timing.

The graph composes authored controller/landing blend and optional EAMM with
inertialization. Unity then invokes `OnAnimatorIK`, which sets final feet/pelvis/
knee goals directly for ordinary Humanoid IK. The terminal
`EarthAnimationEvaluationJob` observes evaluations only. No explicit extra
`HumanStream.SolveIK` remains. The independent arm rig continues to own arms when
built; native hands are the missing-rig fallback. `EarthAnimationDriver` exposes
graph evaluation and weighted contact submission counts.

Contact anchors, pelvis spring and knee filtering advance once per rendered
contact frame, even when the landing mixer evaluates both controller inputs.
Repeated input callbacks only republish cached goals. Foot probes sample the
current controller goals or validated EAMM candidate positions, instead of the
previous rendered IK result. Surface ID/generation, dynamic-debris rejection and
per-foot swing release remain authoritative. Capture is a configurable 0.10 s
starting value rather than the old 0.40 s concealment of pose conflicts.

`EarthAnimationDriver` is the shared state/parameter boundary. The Animator backend
retains native damping; the playable backend uses an explicit exponential response
for the damped overload. Already-filtered gait/speed and the continuous cast clock
use the immediate overload. Native Animator damping is not claimed numerically
equivalent to the new playable filter.

## Explicit content migration

Run in Edit Mode through the Unity integration owner:

1. `Elemental.Authoring.Editor.EarthAnimationRescueSetup.Repair()` imports the
   existing X Bot Idle, replaces only neutral locomotion/turn children and Idle
   recipes, and rebakes the EAMM database. It creates the magic motion profile.
2. `EarthAnimationRescueSetup.BindMagicProfileToLoadedScene()` assigns that profile
   to current Humanoid presentations and marks the scene dirty. Save separately.
3. `EarthAnimationRescueSetup.ConfigureMantleClip(assetPath)` binds a reviewed
   imported Humanoid climb clip to `Base Layer.Mantle`.

Idle is deliberately absent from the older `CuratedPaths` reload check. Adding it
there caused the existing InitializeOnLoad upgrader to reimport and rebuild all
controller lanes without the explicit migration; the September path does not do
that. Re-running the repair preserves an existing authored Mantle state.

## Eleven magic slots

`EarthMagicMotionProfile.asset` contains one entry per existing semantic slot.
Each entry exposes ordered normalized markers and interpolation durations for
Acquire, Root, Load, Strike, Sustain and Recover, plus transient/sustained arm
constraint influence. The defaults are start values requiring clip-by-clip visual
review, not measured contact annotations. Defaults are markers
0.10 / 0.22 / 0.38 / 0.52 / 0.68 / 0.98 and durations
0.10 / 0.12 / 0.16 / 0.10 / 0.18 / 0.22 seconds.

`EarthMagicClipClock` interpolates between phase markers continuously, advances
monotonically during a cast, and resets on a new accepted sequence or technique.
Sustain holds its reviewed target pose after entry; it does not loop an arbitrary
attack segment backwards. Recovery continues while the action layer fades.
Authoritative gameplay still occurs at its existing event tick, independently
from how long the visual takes to reach a marker. Defaults for arm influence are
0.16 during actions and 0.48 for sustained aim, multiplied by action layer weight.

## Automatic mantle presentation

The motor exposes IsMantling, MantleProgress, MantlePhase, MantleLedgePoint and
MantleSequence. Presentation selects a protected motor-timed Mantle state, suppresses
EAMM and the magic layer, anchors hands during reach/raise, releases them through
transfer, then returns feet to contact policy after real destination support. Final root displacement
and interruption remain in the motor. No jump-button trigger is introduced.

The Mixamo download was blocked by the browser. The delivered asset is instead
`Earth Authored Mantle Prototype.anim`, generated explicitly by
`EarthMantleClipAuthoring.CreateAndBind()` from the real upright Idle HumanPose,
with fitted muscle keys for reach, leg clearance, transfer and recovery. It is
tagged `AuthoredMantlePrototype`. Its sampled clip and two in-scene integration
tests pass; final aesthetic review is still separate. The repair command retains
an Idle fallback only when no authored Mantle exists and never overwrites this clip.

## Verification surface

`SeptemberAnimationRescueTestLauncher.RunEdit()` covers marker progression for all
11 slots at 30/60/120 Hz, phase continuity, damping, hand weight separation and the
saved Idle bindings. `RunPlay()` covers production idle/turn/stop, all 11 accepted
technique presentation events and authored/EAMM/contact/additive head A/B stages.
The accepted-event fixture does not replace input-routing or visual verification.

The optional `EarthAnimationPoseProbe` writes no bones and captures final head
height, calibrated head pitch, neck length, contact frame/normal/error/weight,
magic time, graph evaluations and weighted contact submissions. JSON traces are written under
`BuildReports/SeptemberAnimation/`, separately for each test. Existing
`EarthAnimationContactTelemetryRuntimeTests` adds the broader 30/60/120 matrix.
Standing-foot error thresholds in the new smoke tests are intentionally broad
(0.18 m); they cannot certify the desired centimetre-level sole contact. Final
acceptance needs actual pit/slope/edge/spherical/moving-support traces and normal
speed captures, plus user-facing review of each magic clip and the climb.

Existing markers remain `Elemental.Character.Presentation`,
`Elemental.Character.FootContact` and `Elemental.Character.Transition`.
No profiler measurement is claimed before the integrating task runs them.

## Primary references

- [Unity CrossFadeInFixedTime](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Animator.CrossFadeInFixedTime.html): transition duration and target offset use seconds. The A/B migration keeps independent time parameters for the two states rather than rewinding their common input while blending.
- [Unity Two Bone IK](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.4/manual/constraints/TwoBoneIKConstraint.html): position, rotation and elbow-hint influences are distinct. The diagnostic clone zeros the complete action/sustained arm influence while preserving production profile assets to isolate the authored clip from target IK.
- [Unity AnimationHumanStream.SolveIK](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Animations.AnimationHumanStream.SolveIK.html): the explicit native solve uses the stream's current goals and weights.
- [GetGoalPositionFromPose](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Animations.AnimationHumanStream.GetGoalPositionFromPose.html): distinguishes pose-derived goals from written targets.
- [AnimationScriptPlayable input order](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Animations.AnimationScriptPlayable.SetProcessInputs.html): inputs precede the downstream animation job.
- [RigBuilder](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.4/api/UnityEngine.Animations.Rigging.RigBuilder.html): existing rig postprocessing and external graph support.
- [Animator.SetFloat](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Animator.SetFloat.html) and [AnimatorControllerPlayable.SetFloat](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Animations.AnimatorControllerPlayable.SetFloat.html): different available overloads.
