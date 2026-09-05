# Release-aligned magic phase entry

Staged outside `Assets`; no Unity operation was invoked.

The physical projectile/throw remains gameplay-authoritative. When its release
event arrives after a visible held/load action, presentation now starts the
inactive A/B buffer at the new clip's authored contact marker and offsets the
semantic phase clock to Strike. The crossfade carries the outgoing preparation
into contact, then the clip continues through sustain and recovery. It does not
replay wind-up after the projectile has left.

Repeated releases of the same punch before visible contact do not keep rewinding
the active buffer. They coalesce to one latest follow-up. That follow-up starts
from zero after contact so the arm retracts and extends again rather than parking
at a static contact pose.

Files:

- `Assets/Elemental/Presentation/Animation/EarthCharacterPoseController.cs`
- `Assets/Elemental/Presentation/Animation/HumanoidCharacterPresentation.cs`
- `Assets/Elemental/Simulation/Characters/EarthMagicClipClock.cs`
- `Assets/Elemental/Tests/EditMode/SeptemberAnimationRescueTests.cs`
- `Assets/Elemental/Tests/PlayMode/SeptemberAnimationSemanticRuntimeTests.cs`

The EditMode suite includes the dynamic Pull contact wait that replaces the stale
40-frame assumption, release-at-contact clock/follow-through coverage and rapid
same-punch coalescing. PlayMode adds a rapid contact→latest retract/extend test;
the shipping dual-mouse matrix now requires physical launch-to-rendered-contact
latency below 140 ms.

Unity 6000.5.7f1 Bee response files: staged Simulation, Presentation, EditMode
tests and PlayMode tests Roslyn compile with zero diagnostics. Runtime/visual
acceptance is pending the root-owned Unity run.
