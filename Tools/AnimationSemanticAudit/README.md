# Animation semantic audit (staged)

These tests intentionally remain outside `Assets` until the current Unity run is idle.

Copy:

- `SeptemberAnimationSemanticAssetAuditTests.cs` to `Assets/Elemental/Tests/EditMode/`;
- `SeptemberAnimationSemanticRuntimeTests.cs` to `Assets/Elemental/Tests/PlayMode/`;
- `AnimationSemanticAuditTestLauncher.cs` to `Assets/Elemental/Tests/EditMode/`.

Then run the Unity menus:

- `Elemental/QA/Animation Semantic Asset Audit`;
- `Elemental/QA/Animation Semantic Runtime Audit`.

The EditMode gate verifies the actual saved controller, mask, direct-blend parameters,
clip paths, turn tree, and the actual saved timing profile. The PlayMode gate proves
each accepted technique becomes the intended one-hot semantic input and observes head
validity. It also measures both knee angles and their largest one-frame step through a
real walk-stop transition.

Expected reports:

- `BuildReports/AnimationSemanticAssetAuditEdit.{xml,json}`;
- `BuildReports/AnimationSemanticRuntimeAuditPlay.{xml,json}`.
