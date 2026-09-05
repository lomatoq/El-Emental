# SONIC Humanoid reference-pose retarget patch

Status: staged outside `Assets`; production preview has not been rerun.

## Evidence and diagnosis

The failed production preview at
`BuildReports/SonicPrototype/ProductionActorPreview/20260905-113302-303/PreviewReport.json`
measured a `0.698 m` head-to-feet height against a `1.505 m` valid baseline.
The pinned parity report shows the first walk output is close to the mechanical
G1 zero pose, while later frames contain robot-specific hip/knee excursions.

The retarget callback currently replaces every selected Humanoid bone with an
absolute rotation based on `Avatar.humanDescription.skeleton` T-pose metadata.
That discards the valid Animator pose evaluated immediately before `OnAnimatorIK`.
The staged adapter captures that evaluated local reference once, before its first
SONIC write, then applies every source parent-frame delta to that stable reference.
This keeps the opt-in planner as the base-pose owner without accumulating deltas
from its own prior output. The calibrated `DeltaBasis` remains authoritative, so
per-avatar axis calibration is preserved.

Unity documents `Animator.SetBoneLocalRotation` as accepting a bone local rotation
during the IK pass; it is not a normalized muscle parameter. Unity separately
defines `SkeletonBone.rotation` as the imported T-pose local rotation. These API
contracts support using `SetBoneLocalRotation` here, but they do not make imported
T-pose metadata equivalent to the actual controller pose being replaced:

- https://docs.unity3d.com/ja/current/ScriptReference/Animator.SetBoneLocalRotation.html
- https://docs.unity3d.com/cn/6000.0/ScriptReference/SkeletonBone-rotation.html
- https://docs.unity3d.com/cn/6000.0/ScriptReference/SkeletonBone.html

## Files

Copy the staged `after/Assets/Experimental/SonicPrototype` files over their exact
`Assets` paths. The change adds pure `SonicHumanoidRetargetMath` tests for neutral
reference preservation, noncommuting source rest/hinge order and basis-axis
conjugation.

Rerun in this order:

1. The experimental EditMode assembly; expect the three new math tests plus the
   existing SONIC tests to pass.
2. `Elemental/Experimental/SONIC/5 Preview SONIC On Production Actor` in Play Mode.
3. Require both walk and boxing captures, recent nonzero retarget applications,
   and the unchanged `0.80–1.25` anatomy ratio. Do not accept inference success by
   itself.

The patch is a concrete candidate for the measured collapse, not visual proof.
If the anatomy gate still fails, the next evidence needed is per-binding A/B with
the captured reference enabled and individual Hips/Chest/leg writes disabled;
the gate should not be relaxed.
