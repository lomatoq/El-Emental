# Airborne mantle hand reach

Copy the three files under `after` over their matching `Assets` paths, refresh,
then run:

1. `Elemental/QA/Airborne Mantle Admission Edit Tests`
2. `Elemental/QA/Airborne Moving Platform Mantle Play Test`
3. `Elemental/QA/Run Automatic Mantle Motor PlayMode Tests`

The production trace acquired the wall at 1.275 m while still airborne. The old
path held the body at that point until late Raise, so Humanoid IK owned the hands
but could only leave the right hand 0.9245 m from the physical lip. The revised
airborne Reach collision-sweeps the capsule toward the wall and stops at capsule
radius + 0.08 m. Lift and transfer remain separately swept and the moving support
continues to own all path anchors in local space. Grounded mantle timing and path
use the original overload and 0.12 Reach boundary.

Acceptance remains `closestHand < 0.35 m`; no test threshold changed.
