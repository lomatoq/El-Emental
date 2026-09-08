# Restrained frontend motion continuity

September 8, 2026. Source changes in the shared working tree; runtime evidence is pending the root-owned Unity test run.

The existing stone UI keeps its saved layout, artwork, type, colors and motion presets. No dependency or replacement UI has been introduced.

`FrontendMenuView` now reverses panel entrance/exit from its currently rendered alpha and offset. Previously an interrupted entrance restarted the exit from fully opaque, and reopening restarted from the authored hidden endpoint. The same reference easing and durations remain: entrance 0.28 s, exit 0.16 s. Partial visibility supplied by the countdown flow now remains authoritative instead of being replaced by a track's zero/one endpoint. Showing an already active page no longer resets its content alpha to zero.

`FrontendButton` retargets scale from the rendered size whenever either the target or its easing track changes. Hover uses the configured hover duration; press and release keep their configured durations. A held mouse press now survives selection changes as well as pointer exit. Disabling interactability clears a stale press so re-enabling does not leave the button compressed. Reduced Motion immediately removes an in-progress scale effect. Disable clears cached hover blend; keyboard submission respects the same authored press duration as the visible press.

The centered `Press Visual` child remains the only scaled transform. The authored button rectangle, pivot, nonuniform scale and pointer hit surface remain stationary. All clocks remain unscaled, including during pause. Reduced Motion removes panel translation and button scaling and retains the preset's bounded fade duration.

## Verification entrypoints

- `Elemental.Tests.PlayMode.FrontendMotionContinuityTests`: two isolated runtime tests, both at timeScale 0. The panel fixture loads the production animation preset file, checks zero-age reversal continuity, final/partial alpha, immediate interaction gating and Reduced Motion. The button fixture checks held press across deselect/exit, interactability cancellation, Reduced Motion and the authored transform.
- Existing regression: `Elemental.Tests.PlayMode.FeelFollowupUiPlayTests.ButtonHoldsCenteredPressWithoutChangingAuthoredAnchorOrScale`.
- Visual review uses the existing production frontend scene: open/close pause during its first 0.1 s, return from Settings, hold a button while dragging outside, and repeat with Reduced Motion. Existing AlphaFrontend production captures can record the result. Isolated fixture tests are not full-screen visual or performance acceptance.

No fresh test pass, profiler result or screenshot is claimed in this source-only handoff. Root alone owns the live Unity editor.

Fresh root verification: isolated2/2 plus existing held-press1/1 passed in MotionFogWindPlay5/5 at2026-09-08T00:23:41Z. Layout/theme/art unchanged; timeScale0 interruption and Reduced Motion checks pass. These tests prove continuity/input contracts, not a separate UI render-cost measurement.
