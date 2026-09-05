# Unobstructed surf-pillar QA

The failed runtime and visual captures were not evidence that the launch axis was
wrong. `02-PillarBreak.png` shows the player colliding with the blue production bot
directly below/forward. The tests disabled `EarthMvpBotController`, but its dynamic
capsule, rigidbody and ragdoll remained active. The collision deflected the rider
`-1.17m` opposite a correctly tilted pillar and broke one board stone early.

This test-only overlay deactivates the bot GameObject in the additive scene, giving
the shipping movement mechanic an unobstructed lane without changing production
physics. The scene is unloaded after the test. The runtime test also writes one
`[SurfPillarDiag]` row per fixed tick with position/velocity projections, initial
surf velocity, input command, support id, directed-motion lease and ragdoll mode.
The existing positive-forward, rise, velocity, tilt, event and stone gates remain.

Files to copy over `Assets/Elemental`:

- `Tests/PlayMode/EarthSurfRuntimeTests.cs`
- `Tests/PlayMode/SurfPillarJumpVisualQaTests.cs`
