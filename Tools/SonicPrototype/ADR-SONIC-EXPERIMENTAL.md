# Experimental ADR: SONIC as an isolated base-pose source

Status: active feasibility spike. Structural Unity import passed; Unity runtime,
retarget-quality and production-adoption gates remain open.

## Scope and decision

Evaluate the pinned SONIC V2 ONNX as a hidden Unitree G1 pose generator for idle,
walk, run, turning and boxing. Keep all prototype code and the model in the
`Assets/Experimental/SonicPrototype` assembly during the spike. Production
assemblies, scenes, profiles, package references and animation authority remain
unchanged until the full acceptance matrix passes on both characters.

SONIC generates reference motion. `PlanetMotor` continues to own gameplay root
position, local-up orientation, velocity, collision and mantle admission.
`EarthAnimationGraph` remains the single presentation composition point.
`EarthFootContactController` continues to own the final feet, knees and pelvis.
Authored Earth magic, hit, recovery, mantle and ragdoll lanes retain their current
priority and lifecycle.

## Exact model-space contract

Each planner pose has 36 values:

1. world root position `x,y,z`;
2. world root quaternion `w,x,y,z`;
3. 29 joint angles in G1 MuJoCo order.

The 29-angle order used by NVIDIA's MuJoCo deployment is:

1. left hip pitch, roll, yaw; left knee; left ankle pitch, roll;
2. right hip pitch, roll, yaw; right knee; right ankle pitch, roll;
3. waist yaw, roll, pitch;
4. left shoulder pitch, roll, yaw; left elbow; left wrist roll, pitch, yaw;
5. right shoulder pitch, roll, yaw; right elbow; right wrist roll, pitch, yaw.

Forward kinematics must use the pinned NVIDIA G1 hierarchy, joint axes, rest
transforms and quaternion convention. Applying these 29 scalars directly as Unity
Euler angles is outside the contract.

SONIC uses X forward, Y left and Z up. In the temporary planner frame, a source
vector maps to Unity tangent space as `(-source.y, source.z, source.x)`. Rotations
must be changed through the complete basis transform. The output world-root
translation is used for stride phase and velocity diagnostics; it never moves the
gameplay transform.

## Hidden G1 to Humanoid seam

The prototype base-pose source publishes one immutable sample per evaluated frame:

```text
SonicBasePoseSample
  sequence: monotonically increasing planner sequence
  sourceTime: generated 30 Hz pose time
  valid: finite contract and calibrated mapping passed
  localRotations[HumanBodyBone]: calibrated Humanoid local rotations
  sourceRootDelta: diagnostic tangent displacement only
  sourceFacing: diagnostic tangent facing only
  mode: requested SONIC mode
  confidence: 0 when contract, age or calibration is invalid; otherwise 1
```

The consumer reads the latest complete sample through a double-buffered boundary.
The planner thread never touches `Transform`, `Animator`, `PlayableGraph`, Unity
physics or mutable gameplay state. The main animation graph resamples the 30 Hz
poses, crossfades eight generated frames at each accepted replan, and composes the
result as an optional base pose before final contact correction.

That double-buffered graph seam is the production target, not a claim about the
first isolated adapter. `EarthAnimationGraph` and its attach API are internal to
the production presentation assembly, so the staged adapter cannot integrate
there without changing production visibility. For the bounded preview it disables
the local EAMM bridge, schedules the worker from its own component, and applies
Humanoid base rotations during layer-0 `OnAnimatorIK` at execution order 500.
The order-1000 contact controller still performs the final foot pass. Any adoption
must replace this preview ownership with the graph seam and its lifecycle tests.

Context history comes from the uncorrected hidden G1 output. Foot/pelvis IK,
Humanoid retarget corrections and authored action offsets are excluded from the
next planner input. This prevents contact corrections from becoming delayed
planner feedback.

The source clears both published buffers and invalidates pending output on disable,
scene unload, character replacement, ragdoll entry and planner failure. Pause
freezes source-time consumption. Resume starts from a fresh four-pose context and
crossfade; it does not replay a stale future. Mantle, authored magic and recovery
set base-pose weight to zero and restore it only with a new sequence produced after
the protected lane exits.

The current preview implements this lifecycle in one isolated component using a
generation number: pending output from an earlier mode or protected lane can
finish, but cannot be accepted. Worker/input disposal happens only after readback
or after worker disposal on failure, so scheduled CPU jobs cannot retain disposed
input tensors. Inference exceptions stop preview ownership and restore the prior
EAMM bridge state.

Each character owns an independent planner context, mode request, random seed,
buffer and retarget calibration. The player and bot can therefore request boxing
or locomotion concurrently without sharing sequence state.

## Initial mode boundary

The spike may evaluate SONIC idle (0), walk (2), run (3), idle boxing (9), walk
boxing (10), left/right jabs (11/12), random punches (13) and left/right hooks
(15/16). Earth-specific casts, ledge mantle, land/recovery and damage responses
remain authored actions. A later expansion requires separate measured evidence.

## Gates before any production dependency

1. The exact pinned model imports and runs on Unity Inference Engine 2.6.1 with a
   recorded operator report, finite output and no semantic-conversion warning.
2. CPU and GPUCompute reports include import, model/worker startup, p50/p95 planner
   latency, allocations and peak memory. Scheduling is evaluated off the gameplay
   main-thread critical path at a maximum 10 Hz planner rate.
3. A hidden G1 debug rig reconstructs the ONNX output and matches an official
   reference sequence. The current pinned distribution lacks reference tensors,
   so this gate is open.
4. Calibrated mappings for X Bot and Linebreaker pass idle, walk-stop, 180-degree
   turn, direction reversal, run and repeated left/right boxing captures at
   controlled 30/60/120 Hz.
5. The comparison reports head angle, knee bend, foot gap/drift, one-frame bone
   angular velocity, motor-to-generated stride error and transition discontinuity.
6. Hump, pit, slope and spherical traversal pass through the existing final
   contact authority. Magic, mantle, hit, ragdoll, disable, pause and resume tests
   show no stale pose replay or authority overlap.
7. EAMM remains a selectable fallback until SONIC quality and memory are accepted
   for the target Player build.

The current ONNX Runtime evidence meets a local CPU feasibility sub-gate:
about 52.9 ms p50 / 56.2 ms p95 for walk and random punches, 1.89 s session creation
and roughly 1.55 GB peak process working set on the measured machine. Unity import
also passed structurally with exact input/output contracts. Its warnings use the
same values as Unity's defaults and do not alter LayerNormalization or
nearest-neighbour Resize semantics. Unity CPU execution returned 24 finite frames
for both cases at 79.4/82.3 ms walk p50/p95 and 75.9/79.3 ms punches p50/p95.
GPU performance, numerical equivalence and visible retarget quality are still
open.

The fixed walk and random-punch Unity-vs-ONNX Runtime comparison now passes with
matching 24-frame counts and maximum qpos absolute error around `2.2e-5` at the
recorded tolerances. CPU numerical equivalence is therefore closed for these two
requests; broader modes and visible retarget quality remain open.

The first adapter maps source joint-local deltas, rather than G1 world rotations,
onto captured Humanoid local rest rotations. This lets the target hierarchy keep
its own shoulder and hip layout while preserving waist, three-axis hip/shoulder
and compound ankle/wrist motion. The baker derives each initial `deltaBasis` from
the mapped G1 parent-rest frame and the target parent's imported T-pose world
rotation; it remains an explicit calibration parameter. A profile is valid only for the avatar identity from which
it was captured. The hips receive source-root tilt with tangent twist removed;
`PlanetMotor` therefore keeps visible facing authority while SONIC waist joints
still provide torso twist.

Profile rest rotations come from the imported Humanoid Avatar skeleton definition,
not from a live or Editor controller frame. The bounded production-actor reviewer
menu creates the profile and adapter with `HideFlags.DontSave` during Play Mode,
captures walk and boxing after fresh planner sequences, restores EAMM ownership,
and destroys the temporary objects without saving the scene or prefab.

## Rollback

Delete `Assets/Experimental/SonicPrototype`. No production assembly, scene,
profile, input route or animator reference needs changing. The local ONNX under
`Tools/SonicPrototype/Models` can be removed independently.
