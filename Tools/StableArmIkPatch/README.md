# Stable arm IK patch

This staged patch replaces the two package `TwoBoneIKConstraint` jobs used for
magic arms with `EarthStableTwoBoneIkConstraint`. Existing serialized package
constraint references remain on `EarthAnimationRigBridge` for migration and are
held at zero weight.

The package solver interpolates the wrist target by the constraint weight and
then performs a complete solve. A nearly straight arm can therefore select the
opposite bend plane even at a weight such as 0.124. The new job:

1. derives a body-relative pole without reading the previously solved elbow;
2. clamps the requested wrist to 92% of measured two-bone reach;
3. analytically solves one full, stable elbow configuration;
4. blends the resulting root, mid and tip rotations by the rig weight.

No `LateUpdate` transform writer is added. `SetMagicWeight`, `ResetMagicIk`,
`PrepareForEvaluation`, `IsBuilt` and `Weight` keep their public API. The rig is
still reset for mantle, leaving the existing Humanoid ledge-contact fallback in
control.

Copy all files below `Assets` over the matching project paths, refresh once, run
the staged EditMode tests, then rerun the held-input continuity PlayMode test that
previously reported LeftArm 137°, LeftForearm 124° and LeftHand 167° between
frames 150/151. The patch should not be accepted from pure geometry tests alone.
