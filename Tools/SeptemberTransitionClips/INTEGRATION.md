# Incremental Mixamo transition handoff

Staged outside Assets. Do not copy into Assets or run authoring while another agent
owns the Unity Editor. No scene/controller/import mutation has run from this helper.

Downloaded user-authorized source files in `BuildReports`:

| File | Bytes | Mixamo source |
| --- | ---: | --- |
| X Bot@Start Walking.fbx | 731808 | Start Walking |
| X Bot@Crouch To Stand.fbx | 585968 | Crouch To Stand |
| X Bot@Jumping.fbx | 566384 | Male Jumping Down From2ftHighPlatformWithOneFoot |

SHA256 of the downloaded files, checked locally:

* Start Walking: `E52DD00FF5EEA2E22B28B7172F5E6313C2CCA770BE54A986A38204ADB766E261`
* Crouch To Stand: `2FC3AD5D9E47A6D0B53930F9A14B5CF410786495707B0BB0137E3240E835CA69`
* Jumping: `7A7C6698A9CF3ED577515BBB0762CBD56424AF6A904E008DD0834BAFC1596485`

Copy the three staged C# files to their corresponding Assets paths, compile, then
run `Elemental/Animation/Install Three Short Transitions`. The helper imports only
those three FBXs into `Assets/ThirdParty/Mixamo`, copies the existing X Bot Avatar,
uses the existing extracted-root contract, detects source motion onset, and keeps
0.32/0.38/0.36 s of normal-speed source motion respectively. It samples the eight
existing contact curves; it does not infer physical support from the clip.

The exact measured trim is written to `BuildReports/ShortTransitions/import-report.txt`.
Onset detection is an authoring heuristic; review the actual pose sequence before
acceptance. It does not retime a multi-second action into a fraction of a second.

Only three uniquely named base states are added/updated. No existing state, exit,
locomotion child, threshold, timescale or mirroring is replaced. Existing motion
graphs are serialized before installation and compared afterwards. There are no
Animator exit transitions: runtime owns completion and physical cancellation.

## Required runtime hunk in HumanoidCharacterPresentation (root owns this file)

1. Add fields `EarthShortTransitionState _shortTransitionState` and
   `EarthShortTransitionSample _shortTransitionSample`; reset both to `default`
   in `ResetTransientAnimationState()` and before the protected mantle early return.
   Expose `public EarthShortTransition ShortTransition => _shortTransitionSample.Kind`
   for actual runtime QA.

2. After existing `rescue`/`candidate` have been computed, before
   `desiredGroundedState`, call the pure policy once per Update. Use actual
   `motor.HasStableSupport`, not the 110 ms presentation grounding grace.

```csharp
Vector3 bottom = motor.Capsule.transform.TransformPoint(motor.Capsule.center) -
    motor.LocalUp * (motor.Capsule.height * Mathf.Abs(motor.Capsule.transform.lossyScale.y) * .5f);
Vector3 candidatePoint = new Vector3(candidate.Point.x, candidate.Point.y, candidate.Point.z);
var shortInput = new EarthShortTransitionInput {
    Grounded = motor.HasStableSupport,
    Crouched = surfing || pillarCharge,
    ProtectedOwner = protectedAnimationOwner || directionalDodge || authoredKnockdownRecovery ||
        _wasCasting || _castWeight > .02f || HandConstraintWeight > .02f ||
        Time.time < _impactUntil || rescue.LandingStyle == EarthLandingStyle.Hard || LandingRollAllowed,
    DeliberateJump = _deliberateJump || pillarMobility != null && pillarMobility.IsLaunchPending,
    TangentSpeed = tangentVelocity.magnitude,
    ForwardSpeed = Vector3.Dot(tangentVelocity, facing),
    VerticalSpeed = verticalSpeed,
    HasLandingCandidate = candidate.IsValid,
    FloorDistance = candidate.IsValid ? Mathf.Max(0f, Vector3.Dot(bottom - candidatePoint, motor.LocalUp)) : 0f
};
_shortTransitionSample = EarthShortTransitionPolicy.Step(ref _shortTransitionState,
    in shortInput, Time.deltaTime);
```

`directionalDodge` and `authoredKnockdownRecovery` are currently declared just before
`desiredGroundedState`; place this sample there. For first-frame casts, also include
the current pose-controller cast/ownership request rather than only last-frame
`_wasCasting`. The existing protected-ragdoll and mantle paths retain priority.

3. In `ResolveGroundedStateHash`, after its grounded-phase guard, return the
   hash for an active StartWalk or CrouchExit slot. Keep the existing turn/locomotion
   resolution as its fallback. This prevents ordinary lane checks from repeatedly
   crossfading back to Locomotion during the bounded bridge.

4. In `DriveRescueTransition`, immediately after the existing phase switch,
   override the target for an active slot:

```csharp
if (_shortTransitionSample.Kind != EarthShortTransition.None) {
    stateHash = Animator.StringToHash("Base Layer." +
        EarthShortTransitionPolicy.StateName(_shortTransitionSample.Kind));
    bool drop = _shortTransitionSample.Kind == EarthShortTransition.StepDown;
    destinationState = drop ? EarthMotionStateId.Fall : EarthMotionStateId.Locomotion;
    destinationCategory = drop ? EarthMotionCategory.Airborne : EarthMotionCategory.AuthoredAction;
}
```

Cache these three hashes as static fields rather than concatenating strings in the
steady-state loop. The existing director handles the fade. Using AuthoredAction for
the two grounded entry targets starts them at zero; labeling them Locomotion would
incorrectly start the short clip at the old gait-cycle time. They do not become
uninterruptible: the pure policy already cancels immediately on higher-priority
ownership, movement reversal, jump or actual landing. Ensure the existing
`canInterrupt` gate accepts an eligible short-slot transition and its released frame.

5. Include `_shortTransitionSample.Changed` in the existing condition that calls
   `DriveRescueTransition`. This is mandatory for returning to the normal loop at
   timeout even when the rescue phase did not change. On StepDown exit, force the
   normal rescue transition even if both old and new semantic states are Fall:
   the existing forceRestart condition already compares their different hashes.

6. Do not give these slots their own foot/hips transform writer. Existing sampled
   clip contact metadata remains guidance; physical support, shared contact solver,
   ordinary flight, ragdoll and mantle remain authoritative. StepDown exits on the
   first true physical contact; a predicted floor alone never plants a foot.

## Policy scope and gates

StartWalk only follows at least 180 ms of settled idle and forward movement. It
does not steal backward/strafe locomotion or replay while continuously moving.
CrouchExit is a grounded cancellation/release bridge; it cannot replace a real
pillar launch. StepDown needs previously observed physical support, a valid
8–85 cm lower landing candidate in the first 180 ms, and mild descent. It works
for backward movement too, while large drops, deliberate jumps and landing rolls
retain their existing airborne clips. All three have bounded durations and
immediate higher-priority cancellation.

`Elemental.Tests.EditMode.EarthShortTransitionPolicyTests` has 12 cases.
Root must additionally verify saved controller states and production trigger /
timeout / cancel transitions with actual pose captures. Source provenance and
authoring completion alone do not establish visual quality.
