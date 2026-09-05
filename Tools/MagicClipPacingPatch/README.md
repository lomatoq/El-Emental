# Measured per-slot magic clip pacing

Staged outside `Assets`; Unity has not imported or run this patch.

The source audit in `BuildReports/SeptemberAnimation/MagicSourceClipContinuity.json`
shows that one global `2 normalized units/second` clock plays the shipped clips at
roughly 1.7x to 8.6x their authored speed. This patch gives each stable semantic
slot a real-time rate based on its clip duration and worst native 30 Hz bone step.
It keeps the predicted authored 30 Hz step below the existing 48 degree final-pose
envelope without a rendered-frame throttle.

| Slot | Clip seconds | Clock / s | Source speed |
| --- | ---: | ---: | ---: |
| RaiseWall | 3.533 | 0.27 | 0.95x |
| RaisePlatform | 3.267 | 0.60 | 1.96x |
| PullStone | 2.167 | 0.65 | 1.41x |
| HeavyThrow | 3.167 | 0.62 | 1.96x |
| VectorPush | 1.500 | 0.85 | 1.28x |
| GravityRepair | 2.267 | 0.65 | 1.47x |
| WaveResonance | 4.300 | 0.24 | 1.03x |
| Pillar | 0.833 | 1.70 | 1.42x |
| ArmorAssemble | 2.167 | 0.65 | 1.41x |
| ArmorBarrage | 1.300 | 0.72 | 0.94x |
| GenericCast / QuickPunch | 0.867 | 1.50 | 1.30x |

Committed gameplay events still use the already implemented contact-aligned entry:
the projectile/impact does not wait for a long wind-up. The slower rates govern
visible anticipation and follow-through. First-use persistent VectorPush and
GravityRepair still begin with anticipation; focused tests require them to render
contact within one second. QuickPunch starts at contact on commit and completes
follow-through within half a second.

Files to integrate as hunks against the current branch:

- `Assets/Elemental/Simulation/Characters/EarthMagicClipClock.cs`
- `Assets/Elemental/Tests/EditMode/SeptemberAnimationRescueTests.cs`
- `Assets/Elemental/Tests/EditMode/AnimationSemanticAuditTestLauncher.cs`

Run `Elemental/QA/Animation Semantic Magic Edit Audit`, then the existing held-aim,
physical dual-mouse FPS matrix, and All Magic Visual QA. The latter three are the
required runtime/visual calibration; this staged patch has only passed Roslyn
Simulation and EditMode compilation.
