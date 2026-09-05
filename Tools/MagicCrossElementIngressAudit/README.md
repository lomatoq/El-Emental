# Cross-element command ingress audit

Staged outside `Assets` so it cannot trigger a Unity domain reload during the
running animation suites.

## Confirmed defect

`WindLab.unity` serializes `selectedElement: Air` and `ElementLab.unity`
serializes `selectedElement: Water`, but `MagicInputController._selectedAbility`
is runtime-only and initializes to Earth `LineWall`. Before this patch the first
stroke after a saved-scene load carries ability `1` to the Air or thermal/water
executor and is rejected. Pressing an ability/element key masks the defect.

## Integration

1. Replace `Assets/Elemental/Input/Gestures/MagicGesturePolicy.cs` with the staged
   `MagicGesturePolicy.cs`.
2. Apply `MagicInputController.selection.patch` to the current controller.
3. Copy `CrossElementMagicIngressTests.cs` to
   `Assets/Elemental/Tests/PlayMode/`.

The focused Play test calls the real `TryCommitScreenPath` boundary and requires
one accepted executor command plus one `MagicCommandExecuted` edge for a
representative Earth, Air, Fire and Water ability. It never calls
`RequestSemanticPresentation`.

A second Play test loads the two saved cross-element lab scenes and verifies the
first selected ability immediately after `Awake`; this is the seam the previous
executor-only lab tests did not exercise.

## Vocabulary scope

Eleven `EarthHumanoidPoseSlot` values are shared animation poses, not the total
number of abilities. The present command-semantic table contains sixteen
abilities: six Earth, four Air, two Fire and four Water. Multiple abilities reuse
the same pose. Typed Earth gameplay techniques such as Quick Stone punch, armor,
repair and Ground Wave additionally enter that same pose vocabulary through their
own accepted-world events.
