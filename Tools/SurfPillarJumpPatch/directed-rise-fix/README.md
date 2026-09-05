# Directed surf-pillar rise fix

This patch is based on the current integrated charged surf-pillar sources.

It fixes the production PlayMode failure where the launch event and visible pillar
were tilted forward but the actor travelled `-0.91885m` along that direction.
`PlanetMotor.ApplyMovement` continued applying ordinary air-control deceleration
during the authored pillar rise. `EarthPillarMobility` controls velocity only along
the pillar axis, so that deceleration accumulated as a backwards component
perpendicular to the axis.

The patch adds a bounded directed-motion lease to `PlanetMotor`. Only a directed
external launch uses it, and only for the exact authored rise tick count. During
that lease the motor omits locomotion acceleration while gravity, collisions,
orientation, ragdoll propagation and the pillar's own physics motion keep running.
The existing `BeginExternalLaunch(int)` behavior is unchanged for ordinary jumps,
vertical pillars, landing cushion and QA callers.

`ProductionSurfPillarJumpRaisesOnePillarBreaksBoardAndLaunchesHero` keeps its
positive forward-travel assertion and additionally verifies that the lease is live
at release and expires when the rise completes.

Files to copy over `Assets/Elemental`:

- `Runtime/Characters/PlanetMotor.cs`
- `Runtime/Characters/EarthPillarMobility.cs`
- `Tests/PlayMode/EarthSurfRuntimeTests.cs`
- `Tests/PlayMode/SurfPillarOwnerRegressionTests.cs`

After integration, run:

- `Elemental/QA/Run Surf Pillar Jump PlayMode Test`
- `Elemental/QA/Capture Surf Pillar Jump Visual Proof`
