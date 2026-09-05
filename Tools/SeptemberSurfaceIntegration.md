# Focused actual-surface animation verification

Do not import during another Unity operation. Copy `Tools/SeptemberAnimationSurfaceRuntimeTests.cs`
to `Assets/Elemental/Tests/PlayMode/SeptemberAnimationSurfaceRuntimeTests.cs`, and change
the existing SeptemberAnimationRescueRuntimeTests declaration from `public sealed class`
to `public sealed partial class`. No other existing test changes are required.

Run only:
`Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.FinalHumanoidFeetTraverseRealPitHumpAndSlopeAtControlledThirtySixtyOneTwentySteps`
through the existing focused launcher mechanism. Its existing UnitySetUp waits for
scene readiness and disables bots after readiness restored the controls.

Scope: both real production actors, actual MeshCollider terrain with a shallow
spherical-cap base, 11 cm hump, 13 cm asymmetric pit and 13 cm smooth rise. Fixture
setup repositions one actor above other scene geometry; measured travel uses its
ordinary motor, EAMM, authored clips and final IK. No frozen body, IK override,
forced gait/phase or synthetic contact-solver inputs. Final planted gap/drift gates
reuse EarthAnimationContactAcceptance; contact frames must be current and swing
must release IK and lift above measured resting ankle clearance.

Time.captureDeltaTime controls animation deltas at 1/30, 1/60 and 1/120 seconds;
physics remains 1/60. Requested FPS is explicitly not achieved FPS. The report
`BuildReports/SeptemberAnimation/ActualSurfaceControlledSteps.json` records every
actual Time.deltaTime and wall-clock frame interval, plus final foot errors and
coverage. Divide frames by wallDeltaSum for observed throughput; do not call a
slow 120-step capture actual 120 FPS. This is not a performance certification.

Existing matrix inventory: EarthAnimationContactTelemetryRuntimeTests has a
30/60/120 production final-pose run on scene/flat support, but its slope/step/seam
and moving-support branches invoke the pair solver with synthetic inputs. Its bot
translation is frozen, and startup still disables bots before scene Ready. Running
that unchanged test alone cannot certify real irregular-surface traversal.

The new test remains uncompiled/unrun while staged. It deliberately keeps strict
existing planted-foot gates; investigate any failure instead of relaxing them.
