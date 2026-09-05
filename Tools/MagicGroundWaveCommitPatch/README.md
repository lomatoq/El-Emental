# Ground-wave physical commit presentation ingress

Staged outside `Assets`; Unity has not imported or run this patch.

`MagicInputController.CommitGroundWave` calls `EarthPillarWaveAbility.TryCast`
directly. Unlike walls, pushes, pillars and projectiles, a successful wave launch
published no event consumed by `EarthCharacterPoseController`. The input preview
could animate, but once physical columns launched the pose request disappeared or
continued its slow pre-contact clock.

This patch adds one typed `EarthPillarWaveAbility.CastCommitted` event emitted only
after `wavePool.Launch` returns at least one physical column. The pose controller
subscribes once, resolves the existing `WebWave` semantic slot, and admits that
world change as an immediate authored-contact boundary. Both `ReleaseCharge` and
`TryCast` share the event and each launch emits exactly once.

The focused PlayMode test calls the shipping `TryCast` path against the production
pool, requires real columns, verifies the pose selected WebWave with contact entry,
and requires rendered-contact evidence within 0.25 simulated seconds / 16 rendered
frames.

Integrate narrow hunks:

- `Assets/Elemental/Runtime/Characters/EarthPillarWaveAbility.cs`
- `Assets/Elemental/Presentation/Animation/EarthCharacterPoseController.cs`
- add `Assets/Elemental/Tests/PlayMode/MagicGroundWaveCommitRuntimeTests.cs`
- merge the dedicated launcher menu entry.

Roslyn Runtime, Presentation and PlayMode compilation completed with zero errors
(existing Runtime obsolete-API warnings remain). Run:
`Elemental/QA/Animation Ground Wave Commit Runtime Audit`.
