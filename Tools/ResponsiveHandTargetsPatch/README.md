# Responsive sustained-hand targets

## Confirmed regression

`HumanoidCharacterPresentation.UpdateHandTargets` copies `GravityWellFocus`,
`VectorFieldPoint` or `HeldBody.worldCenterOfMass` straight into a symmetric pair
of wrist targets every rendered frame. `EarthMagicReachSolver` limits vertical
aim and reach, but it does not limit rearward aim and has no temporal state.

This produces three visible faults in a sustained hold:

1. switching or moving the controlled body teleports both wrist targets;
2. a focus that crosses behind the torso asks the arms to solve behind the body;
3. after the live focus disappears, the fading IK weight chases an unrelated
   up-forward fallback point instead of releasing from the last controlled pose.

The current contact barrier is correct. One-shot clips keep full arm ownership,
and sustained IK receives weight only after the authored contact sample was
rendered. The integration hunk leaves that policy unchanged.

## Patch contract

`EarthResponsiveHandTargetSolver` stores aim/reach/spread in character-local
space. Root translation and rotation therefore carry the held pose immediately;
only changes of intent are filtered. Live goals are restricted to a front cone
of ±70 degrees yaw and -18/+38 degrees pitch. Aim changes at no more than 300
degrees/second, reach at 1.20 m/s and hand spread at 0.60 m/s. A render hitch is
limited to 50 ms of target advancement. Invalid inputs produce a finite sample.
The existing choreography owner may consume the same filtered local aim for at
most ±4.5 degrees of extra chest yaw after contact. It does not add a head or neck
writer and does not rotate the gameplay root.

When the sustained source is released, the last local goal is held while the
existing rig-weight release fades. `ResetMagicIK` clears the filter atomically.
The solver keeps the existing symmetric hand pair because the target path has no
authoritative per-hand role. `BendingPoseRequest.LeftDominant` describes body
choreography from the target side; treating it as hand ownership would invent a
new input contract.

## Integration and acceptance

Copy the staged solver and test files below `Assets` into the project, then apply
`HumanoidCharacterPresentation.integration.patch`. The patch only adds one state,
filters the existing reach sample and resets that state with the current IK reset.

Roslyn compile command:

```powershell
dotnet build .\Tools\ResponsiveHandTargetsPatch\RoslynCompile.csproj --nologo --verbosity:minimal
```

The staged source and seven EditMode tests compile with zero warnings and zero
errors. They cover the front cone, explicit angular/reach speed bounds, a fixed
world focus during a body turn, 30/120 Hz elapsed-time equivalence, release hold and
invalid input, plus the post-contact torso-yaw bound.

Runtime acceptance still requires the production held-input case:

- acquire and render contact before any constraint weight becomes nonzero;
- drag a held body from left to right, then rotate and move the character;
- switch between held-body/gravity/vector sources if the gameplay route permits;
- release while moving and confirm the wrists fade from the last pose;
- record per-frame target angular step, wrist/bone angular step, reach and rig
  weight at 30/60/120 Hz;
- reject any rearward target, target angular step over `300 * dt + tolerance`,
  non-finite sample, or one-frame arm-chain flip.

`ResponsiveHandTargetRuntimeTests.GravityGripCarryKeepsHandsBodyRelativeWhileWalkingAndTurning`
is staged for that gate. It creates three real physical targets, acquires them
through the production area-grip API, waits for rendered contact, walks and turns
through `PlanetMotor`, moves the live field, and checks the two-hand frame,
front-cone/reach bounds, response latency and the existing chest owner's bounded
contribution. It has not been run because Unity remains under the root task's
exclusive test lane.

This patch improves persistent aim behavior only. It does not add procedural arm
motion to one-shot casts, change authored clip timing, change mantle ownership or
claim distinct left/right hand behavior.
