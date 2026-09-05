# C1 spatial angular-velocity frame patch

Staged outside Unity `Assets`; Unity has not imported or run it.

`EarthRotationInertialization.MeasureAngularVelocity` uses
`next * inverse(previous)`, a left/spatial derivative expressed in the bone's
parent coordinate system. The previous implementation captured a right offset
`inverse(target) * source` and added its angular velocity directly to that spatial
target velocity. Those vectors are in different rotating frames. Single-axis
tests cannot expose the error.

The patch represents the visible pose as `output = offset * target`, where offset
is also spatial. It captures
`omegaOffset = omegaSource - rotate(offset, omegaTarget)` and reconstructs
`omegaOutput = omegaOffset + rotate(offset, omegaTarget)` after spring decay.
Pose, velocity and spring offset therefore use one convention through initial,
moving and interrupted transitions.

The new non-commuting three-axis test finite-differences the actual returned
quaternion product and compares that derivative with the reported velocity. It
also proves the fixture cannot pass through the old unrotated vector addition.

Run the existing menu after copying the two files into their matching `Assets`
paths:

`Elemental/QA/Animation C1 Inertialization Edit Audit`

Expected report: `BuildReports/AnimationC1InertializationEdit.json`.
