# Sustained magic contact-entry patch

This review bundle stays outside Unity `Assets`. Unity has not imported or run it.

## Measured defect

`AnimationHeldAimPlay.xml` repeatedly records `GravityGrip / Sustain`, sequence `0`,
resident state `2123799777`, no Animator transition and `handIk=0`. Despite already
being in Sustain, the buffer starts at normalized time zero and traverses the
pre-contact section of slot 6 (`Standing 1H Cast Spell 01`, 2.2667 seconds) at the
generic maximum of 2 normalized units/second, about 4.53x source speed. The final
right forearm/hand rotates 171-176 degrees at normalized times 0.215-0.270.

This rules out native hand IK, Animation Rigging and an A/B state boundary for the
recorded fault. It is a phase-entry error: a gameplay-owned persistent field has no
authoritative one-shot event, yet its already-active Sustain phase is played from
the beginning as if it were anticipation.

## Change

`HumanoidCharacterPresentation` starts a persistent held body, gravity well or
vector field at the clip's contact marker only when no authoritative presentation
owns the cast. Event-owned casts retain anticipation. Explicit committed releases
keep the existing contact-aligned behavior. The clock continues into Sustain, so
the held pose is animated rather than frozen.

The EditMode regression proves the admission decision, confirms the first sample
does not enter the measured pre-contact interval, and confirms follow-through.
The existing production PlayMode test
`ShippingHeldBodyAimActivatesAfterContactAndReleasesWithoutAFlip` is the final-pose
acceptance gate and must be rerun after integration.

## Files

- `Assets/Elemental/Presentation/Animation/HumanoidCharacterPresentation.cs`
- `Assets/Elemental/Tests/EditMode/SeptemberAnimationRescueTests.cs`

The staged Simulation, Presentation and EditMode assemblies compile with Unity's
Roslyn response files without diagnostics. Generated DLL/PDB artifacts are not
kept in this bundle.
