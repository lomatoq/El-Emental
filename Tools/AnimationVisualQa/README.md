# Final animation visual QA capture contract

`AnimationVisualQaCaptureSession.cs` is an Editor-side artifact recorder, not a
gameplay driver. Stage it in an Editor assembly and call `Begin()` only in the
ready, unpaused production Earth Play scene. A scenario runner then uses the
same deterministic `IPlanetMotorInputSource`, temporary uneven-track mesh,
production automatic mantle ledge and real technique/event entry points already
exercised by the September animation PlayMode tests. Do not force Animator state,
write bones, disable EAMM/IK, change animation speed or substitute a QA camera.
Wait at least two rendered frames after `Begin()` so probes contain a final-pose
sample before the first request.

At each state, call `Capture(label, scenario)` and wait until `IsReadyForNext`.
Required label prefixes are:

- `idle`, `walk`, `stop`, `turn`
- `uneven-pit`, `uneven-slope`
- `mantle-reach`, `mantle-raise`, `mantle-transfer`, `mantle-settle`
- `magic-start`, `magic-sample`, `magic-recovery`

The uneven fixture must have a temporary visible MeshRenderer as well as the real
MeshCollider, using the existing arena material without modifying it. Otherwise
screenshots cannot prove sole tangency to the pit or slope. Place the inactive
actor beside the lane when needed so the live production rig can keep both actors
inside the frame; record this temporary pose and restore body pose and velocities.

Every PNG contains the real Game view from the live production Main Camera and
camera rig. Each manifest row samples both production actors, even where only one
actor performs the named action. The other actor must remain visible so folded
heads, delayed IK and bot/player differences cannot hide outside the frame.

The scenario runner owns a strict `try/finally`: restore both motors' original
input sources; actor rigidbody poses and velocities; bot/duel enabled states;
camera-rig state; any hidden combat suppressions; global clock settings; and
destroy every temporary ledge/track/input object. Only then call `Finish()`. The
recorder itself restores the pre-existing pose-probe labels and the three known
runtime debug UI behaviours. It never moves the camera, changes a lens, edits a
profile, saves a scene or writes into `Assets`.

The final human review checks consecutive images as motion, not isolated hero
poses. Confirm head and neck remain upright; stride starts and stops without a
crooked frozen leg; turn-in-place shows an authored turn with at least one stable
foot; soles lie tangent to the measured pit/slope and the swing foot is released;
mantle hands approach the ledge while foot IK releases and then resumes; and magic
shows changing hand/body samples rather than a frozen pose. A complete manifest
and passing telemetry are necessary evidence, but do not replace that review.

The older `EarthAnimationVisualAuditRuntimeTests` remains useful telemetry for
idle/start/stride/stop/turn/jump/dodge/knockdown. Its screenshots are manual
`Camera.Render()` renders into a private 1280x720 texture, it lacks readiness and
full failure restoration, and its latest 2026-08-31 manifest failed the pivot
continuity gate. Do not present those images as current actual-Game acceptance.
