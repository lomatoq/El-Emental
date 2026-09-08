# Frontend audio onset synchronization

Panel movement previously queued playback for the next Update and, during a reversal, waited for the previous voice's 60 ms retirement before starting the next clip. The new path starts AudioSource.Play in PlayPanelMove itself; a second owned voice retires the previous movement concurrently. Same-frame duplicate layout notifications coalesce. Quiet panel gain and attack/release envelopes remain authoritative.

Button hover and press already dispatch from PointerEnter/PointerDown/Submit; they do not wait for animation completion. Menu button action callbacks now enter a scoped audio context so a flow Confirm does not play again after the button press. Direct non-button Confirm cues remain available. The scope resets through finally, including failed callbacks. Existing clips, music, volumes, layout, and button motion are preserved.

Unity verification and clip-onset measurement are coordinated by root. Results must be recorded after the focused runtime run; this document does not claim an unperformed test or listening pass.

Root measured the imported panel clip: 1.149388 s duration, first sample above abs .001 at 0.0444671 s. New profile field panelStartOffsetSeconds defaults to .044; runtime sets timeSamples before Play and bases release on remaining duration. The envelope starts at a half-DSP-buffer gain immediately so first sound is not blocked on a render-frame Update. Existing Hover.wav and Press.wav remain assigned; PCM inspection found approximately 39.68 ms and 13.61 ms of sub-.001 onset respectively.

Focused additions: FrontendAudioRuntimeTests.PanelReversalStartsWhilePreviousVoiceRetires and PointerAudioStartsImmediatelyAndActionDoesNotAddLateConfirm, plus same-frame/nonzero first-buffer assertions in PanelUsesQuietSinglePlaybackAndFadesOutWhilePaused. Root owns execution.

Hover/press onset repair: Configure prepares one cached runtime clip per assigned interaction source, scanning at most the first 80 ms for abs sample > .001, retaining 2 ms preroll and applying a tiny 2 ms attack. PlayOneShot and overlapping cue behavior remain unchanged; original assets/data are untouched. Prepared clips are reused across Configure and repeated actions, kept alive while one-shots can still play, and released on owner destruction. Added FrontendAudioRuntimeTests.InteractionOnsetIsPreparedOnceWithoutChangingSourceAndReleasedOnDestroy: synthetic stereo cue with 40 ms silence, immediate dispatch, audible onset within 4 ms, exact source data preservation, cache reuse and cleanup.

## Verification on September 8

FrontendAudioRuntimeTests passed all5 cases in BuildReports/LandingRowAudioPlay.json at14:11:12Z. This verifies same-call playback, concurrent reversal tail, pointer cue timing without late confirm, runtime onset trim/source preservation/cache cleanup, and music DSP crossfades at frozen scaled time. The two failures in that combined8-case report were floor-slam fracture admission, independently repaired and verified2/2 in LandingFinalPlay. No new human listening pass is claimed.

All5 audio cases passed again in EarthInteractionFinalPlay at15:14:20Z. Latest measured audio marker mean0.00453ms, peak0.04890ms; runtime report15:13:08Z.
