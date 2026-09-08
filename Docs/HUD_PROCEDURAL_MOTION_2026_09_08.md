# HUD procedural motion — 2026-09-08

Presentation-only changes; simulation timing, score authority and existing authored layout assets remain unchanged.

- Pause now shows its centered pressed scale before invoking the configured pause callback. The feedback survives at least one frame, then an 85 ms unscaled interval (35 ms with reduced motion). Repeated activation while pending does not dispatch twice. Pointer release/capture loss cancels the held visual.
- Returning uses the existing dark `Stone/Art/Buttons/button_normal.png` sprite rather than the passive round-loss plate. Text/font and user-authored `Returning` layout remain in the same node. A reversible 240 ms reveal / 180 ms exit uses alpha and 0.92–1 scale; the last countdown text remains during its exit.
- Per-life win/loss/draw messages use a smooth alpha and small scale reveal and exit. Existing simultaneous-death aggregation and lifetime remain authoritative.
- Final victory/defeat/draw retain the reference profile's existing visual motion. Action-button visuals appear after 160 ms with a 65 ms stagger. Actual round restart fades the old result for 180 ms; exiting actions are disabled. An interrupted entrance fades from its current opacity.
- `UiVisibilityMotion` is the reversible data envelope. `ToolkitOpacityScaleMotion` restores only its own previous scale before composing authored state, and never modifies position, percentage translation, rotation or size. There is no new layout wrapper. All frame motion uses existing `Elemental.DuelHud.Tick` instrumentation.

Focused validation fixtures (execution owned by the main agent): `HudMotionContractTests` (3 EditMode cases) and `HudMotionPlayTests` (1 PlayMode case). The latter verifies callback order, single dispatch at timeScale zero and authored pause transform preservation. Existing `MenuLayoutProductionTests` and `FeelFollowupUiPlayTests.ActualLifeLossesDisplayWinLoseAndMutualDrawBeforeRespawn` cover the real scene and result styling. No test result is claimed in this file until the coordinated Unity run completes.

Coordinated final validation passed: DenseAtmosphereEdit 21/21 and DenseAtmospherePlay 11/11 (2026-09-08 10:37:15Z), including the new HUD fixtures and production layout regressions.
