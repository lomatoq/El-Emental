# Character motion readability, September 6

Working tree on main `1235579`. This audit preserves the user's authored
locomotion children, controller settings and imported clips.

## Evidence before tuning

The shipping controller already contains `Turn In Place`, driven through
`EarthAnimationDriver` and explicitly protected from EAMM replacement. Its
left-turn source and mirrored right child use the complete 1.033-second FBX
source (31 frames at 30 fps), not a truncated clip. The motor uses A/D tank
steering. Mouse camera orbit does not rotate the motor and is not a missing
turn-animation transition.

The prior acceptance checked turn-state ownership and artificially stressed
head/hip anchors. It did not prove ordinary gameplay articulation or that
rendered vertices actually followed accessory bones.

The new production test drives normal motor input with all final pose passes:
the initial SeptemberCharacterReadability run passed, measuring player knee
articulation 26.138 degrees, tail-joint peak 6.781 degrees and belt-joint peak
18.397 degrees. The other actor measured 29.195 / 4.162 / 14.427 degrees.
These are bone measurements, not claims of visible silhouette displacement.

## Readability work and next acceptance

New-component secondary defaults lower spring frequency from 5.6 to 3.4 Hz,
increase acceleration response from .72 to 1.25, turn inertia from .045 to .08,
and velocity drag from .35 to .75. Angle bounds, collision envelopes, rigid
helmet hair, ragdoll suspension and teleport reset remain intact. Existing scene
values were promoted to both shipping actors through the coordinated Editor and saved.

`ProductionBriefTurnTapArticulatesLegs` tests an 80 ms steering tap rather than
only a sustained turn. `ProductionTurnsMoveLegsAndOrdinaryGaitMovesAccessories`
now also compares baked skinned vertices before and after the secondary pose,
requires separate belt/plume displacement and captures the actual game camera.
Fresh `ReadabilityRepairPlay` is **2/2 PASS**, 08:31 UTC. Both sustained and
80 ms turns visibly articulate the final leg pose. With the tuning saved, player
tail vertices move **2.381 cm** and belt vertices **4.746 cm** versus the neutral
secondary pose; the rival measures **2.186 cm / 8.777 cm**. These are actual baked
skinned vertices, not only bone rotations. The gameplay camera capture
`BuildReports/CharacterReadability/Planet Character-gait.png` was inspected.
There is no reproduced missing turn-state or skin-weight defect, so no extra
turn latch, controller rewrite or invented IK layer was installed. The change
makes the existing accessory response slower and more readable. A single still
capture does not by itself prove temporal visual quality.
