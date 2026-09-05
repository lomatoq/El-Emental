# All-magic clip metadata staged patch

`EarthAnimationGraph` owns the live `AnimatorControllerPlayable` whenever EAMM
is active. Querying `Animator.GetCurrentAnimatorClipInfo` therefore returns no
resident clips even while the magic layer is visibly weighted. This patch adds
the matching graph/driver query and makes `AllMagicVisualQaDriver` use it.

Files are staged outside `Assets`; no Unity operation was run:

- `Assets/Elemental/Presentation/MotionMatching/EarthAnimationGraph.cs`
- `Assets/Elemental/Presentation/Animation/EarthAnimationDriver.cs`
- `Assets/Elemental/Authoring/Editor/AllMagicVisualQaDriver.cs`

Roslyn compiled staged Presentation and Authoring.Editor with zero errors using
Unity 6000.5.7f1's current Bee response files. Existing obsolete API warnings in
unrelated editor helpers remain.
