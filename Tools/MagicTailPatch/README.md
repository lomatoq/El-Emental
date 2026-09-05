# Magic release-tail staged patch

> Superseded after the user's physical-release repro by
> `Tools/MagicPhaseEntryPatch`. The first version was already integrated; do not
> apply the later same-punch staging from this directory independently.

This directory is outside Unity `Assets`. It is a source-review bundle; Unity
has not imported or executed it.

## Concrete defects addressed

1. Stomp Stone deliberately emits Pillar (rise), Pull Stone (hover), then Quick
   Punch (launch). The pose arbiter required Pull Stone to render its contact
   before admitting the already-committed launch. With the calibrated slow Pull
   clip this delayed the punch and left presentation active beyond 1.5 seconds.
   Quick Punch, fragment launch and body release are now explicit committed
   action boundaries: they clear the one pending anticipation and immediately
   enter the inactive A/B magic buffer. Gameplay timing is unchanged.
2. When a slow clip reached rendered contact after the fixed semantic clock had
   already become Idle, the pose controller ended without ever exposing a
   Recovery phase. It now retains Recovery until the active clip's authored
   `RecoverEnd` marker has actually rendered.
3. The 30 Hz physical-input test asserted `router.Current` one Update after its
   commit edge. The durable `EarthDualMouseAbilityController.IsStompStoneActive`
   session is now the acceptance signal, while `Current` remains a valid
   same-frame alternative. This still fails if the physical chord does not start
   gameplay.

## Staged files

- `Assets/Elemental/Presentation/Animation/EarthCharacterPoseController.cs`
- `Assets/Elemental/Presentation/Animation/HumanoidCharacterPresentation.cs`
- `Assets/Elemental/Tests/EditMode/SeptemberAnimationRescueTests.cs`
- `Assets/Elemental/Tests/PlayMode/SeptemberAnimationSemanticRuntimeTests.cs`

The Humanoid file was rebased after the root-owned synchronous ragdoll handoff
change. Its diff is limited to the four recovery-marker hunks; still review the
diff if production changes again before integration.

## Verification

Using the current Unity 6000.5.7f1 Bee response files, staged Presentation,
EditMode tests and PlayMode tests all Roslyn-compiled with zero diagnostics.
No Unity test or visual capture has run. After integration run:

- `Elemental/QA/Animation Semantic Magic Edit Audit`
- `Elemental/QA/Animation Magic Frame Rate Runtime Audit`
- `Elemental/QA/Animation Punch Continuity Runtime Audit`
- `Elemental/QA/Animation All 11 Visual QA`

The expected PlayMode report is `BuildReports/AnimationMagicFrameRatePlay.json`.
The visual manifest is under
`BuildReports/EnvironmentAnimationRescue/AllMagicVisualQA/<timestamp>/`.
