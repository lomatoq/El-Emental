# Gameplay commit to authored-contact admission

Staged outside `Assets`; Unity has not imported or run this patch.

Preparation still starts at clip zero while input/hold is active. Once gameplay has
actually accepted a command or emitted a concrete world event, presentation is
promoted to its authored contact marker. This prevents the new source-paced wall,
platform and long magic clips from visually landing one or two seconds after the
world already changed.

Confirmed boundaries in the patch:

- successful `MagicCommandExecuted` for all elements;
- wall created;
- earth fragment spawned or body grabbed;
- fragment/body launched or released (already contact-aligned);
- push impulse applied;
- pillar launch applied;
- armor radial volley released;
- dual-mouse quick launch (already contact-aligned).

A same-tick world event and accepted-command event are common because executor
callbacks run before `MagicCommandExecuted`. The stronger contact admission now
promotes the existing request. `AuthoritativePresentationGeneration` gives the
Humanoid A/B buffer and clip clock a distinct render token even when network tick
and semantic slot are unchanged; event telemetry continues to use the real tick.
Without that token, the contact flag changed but the old anticipation clock did
not restart.

Tests:

- same-tick anticipation to commit promotion and render generation;
- exact shipping private event ingress for representative wall, pull and repair;
- production PlayableGraph/A-B rendering for all eleven slots, requiring actual
  rendered-contact evidence within 0.25 simulated seconds and 16 render frames.

Integrate as narrow hunks because the root branch has newer public QA seams:

- `EarthCharacterPoseController.cs`: generation, same-tick promotion, committed
  flags on the event handlers;
- `HumanoidCharacterPresentation.cs`: use generation for buffer/clock identity,
  keep `LastAuthoritativeTick` for `NotifyRenderedMagicSample`;
- add `MagicCommittedContactRuntimeTests.cs`;
- merge two EditMode tests and launcher entries.

Roslyn Simulation, Presentation, EditMode and PlayMode compilation: zero diagnostics.
Run `Elemental/QA/Animation Semantic Magic Edit Audit`, then
`Elemental/QA/Animation Semantic Magic Runtime Audit`.
