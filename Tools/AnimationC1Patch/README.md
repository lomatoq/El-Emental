# Animation C1 staged patch

This directory is intentionally outside Unity's `Assets` tree. It contains a
reviewable replacement for the final per-bone rotation inertialization and does
not change the active project until the files are copied by the Unity owner.

## Baseline

`Baseline/Assets/...` is the exact production source captured before this patch:

- `EarthInertializationJob.cs` SHA-256
  `1A0086EC0E5F6D5061888FF2718D2901C7DBDF1D3BCA2DCDAE9515A4F614F8A9`
- `EarthAnimationGraph.cs` SHA-256
  `621B604C42E7427FEE0E998AECA633796DF7D37D1718FD7C562B22D2EE18A1E9`

The same hashes still matched their production files after staging.

## Replacement behavior

- Stores the outgoing visible local rotation and angular velocity per bone.
- Captures a transition from the visible output, including while an earlier
  transition is still decaying.
- A zero-delta call captures the exact boundary. A rendered transition advances
  the previous visible pose by its measured angular velocity for that frame, so
  the finite difference of returned poses agrees with the reported velocity.
- Waits for two consecutive samples from the incoming source before deriving its
  target velocity. The discontinuity between states is never treated as velocity.
- Reuses JLPM's `InertializeJointTransition` and implicit-spring
  `InertializeJointUpdate`; decay remains time-based.
- Keeps planted foot/toe groups on the exact bypass path, including on the same
  frame as a semantic transition. Final foot/knee/pelvis ownership is unchanged.
- Uses one persistent `NativeArray` of blittable states and introduces no managed
  work in the animation loop.
- Measures sub-degree angular deltas with a shortest-path `atan2` quaternion log;
  the package `acos(w)` helper lost velocity precision at 120 Hz.
- Contact bypass clears the pending incoming-derivative state and treats a
  transition-frame final-contact snap as authority transfer with zero authored
  angular velocity.

## Files to integrate

- `Assets/Elemental/Presentation/MotionMatching/EarthRotationInertialization.cs`
- `Assets/Elemental/Presentation/MotionMatching/EarthInertializationJob.cs`
- `Assets/Elemental/Presentation/MotionMatching/EarthAnimationGraph.cs`
- `Assets/Elemental/Tests/EditMode/MotionMatching/EarthRotationInertializationTests.cs`
- `Assets/Elemental/Tests/EditMode/MotionMatching/AnimationC1TestLauncher.cs`

## Verification state

Roslyn compiled the complete staged `Elemental.Presentation` source and the
complete EditMode test assembly with zero diagnostics using Unity 6000.5.7f1's
current Bee response files. Unity tests have not been run because this patch was
prepared under the root-owned Unity execution window.

The focused pure test class covers stationary switching, moving switching,
interruption during an existing blend, 30/60/120 Hz time-step agreement and exact
planted-contact bypass. After integration, run that class first, followed by the
unchanged production walk-stop, actual irregular-surface, magic frame-rate and full
September animation regressions. A passing pure test is not visual acceptance;
the existing final-pose recorder must confirm bone velocity, head and contact
quality on both production actors.
