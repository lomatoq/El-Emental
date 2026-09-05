# Paused choreography additive-owner guard (staged)

`AllMagicVisualQaDriver` freezes `Time.timeScale` while Unity writes each PNG.
Production config sets the Animator to `AnimatorUpdateMode.Normal` and the EAMM
PlayableGraph to `DirectorUpdateMode.GameTime`, so neither backend rewrites the base
pose during those render frames. `EarthChoreographyDirector` currently advances with
`unscaledDeltaTime` and multiplies the full accumulated chest/head/shoulder offset
again in every `LateUpdate`. Five screenshot-write frames can therefore manufacture
the large head angles seen in selected All11 captures.

The candidate returns from the existing choreography owner when scaled delta time is
zero. It retains the already evaluated local pose and `_appliedVisualPose`, then
resumes normal one-application-per-animation-evaluation behavior when GameTime moves.
It adds no writer and does not change mantle, IK or live unpaused timing.

The same ownership defect exists in `HumanoidOrganicIdle`: its weights stop moving at
zero scaled delta, but it still multiplies the retained idle/surf pose into frozen
bones. `HumanoidProceduralBodyResponse` replaces zero delta with `0.0001`, integrates,
and also writes chest/head. Their candidates return before state integration and bone
writes when scaled GameTime is held. State and pose resume from the same frozen sample.

The staged production PlayMode test isolates the director from the two other known
additive chest/head passes, creates a real active RaiseWall request, freezes scaled
GameTime, then requires upper chest, head and both shoulders to remain within 0.05
degrees for 12 rendered end-of-frame samples while `CurrentRequest` stays active.
The pre-patch source should fail through repeated quaternion multiplication; the
candidate should pass. Invoke
`Elemental.Tests.EditMode.PausedChoreographyTestLauncher.Run()` after integration.
Evidence paths are `BuildReports/PausedChoreographyPlay.json/xml`.

A second test leaves every production pose owner enabled and checks local position and
rotation of the full Humanoid torso, head, arm, leg, hand and foot chain for the same
12 rendered pause frames. This is the acceptance gate for screenshot validity; the
isolated director test alone is only causal evidence.

This folder is staged only. No `Assets` file or Unity state was changed and no pass is
claimed before the focused production test runs.
