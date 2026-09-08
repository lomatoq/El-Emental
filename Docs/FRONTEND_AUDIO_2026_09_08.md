# Frontend music and sidebar audio — 2026-09-08

Working tree based on main `2f0fbe86`. The three user-supplied MP3s are imported unchanged into `Assets/Elemental/Content/Audio/Frontend` (music streams; short panel cue decompresses on load).

## Contract and tuning

`FrontendAudio.asset` is bound through the existing `ElementalUITheme`. Main/Host/Join play Main Menu; Starting/Combat play Sky Temple Gate. Pause keeps the game track at 65% gain, and Settings retains its originating track. Returning to Main crossfades the two tracks. Disabling the frontend requests a fade out. Master volume and existing UI volume preferences still apply.

Each track has two reusable 2D voices. Audio DSP time schedules the next beginning before the current ending; equal-power sine/cosine envelopes overlap for 3 seconds without restarting the same live voice. State changes retarget current gain with a 1.5 second smooth fade. Audio runs while game time is zero. No per-frame source creation or managed arrays.

Defaults: menu 0.42, game 0.46, panel 0.24 multiplied by saved UI volume; pause music multiplier 0.65. These and all fade times are editable in the profile. Panel movement is a single short cue with 25ms attack and 150ms release. Duplicate same-frame movement requests coalesce; rapid reversals retire the old cue over 60ms before replay. Countdown completion does not replay a second exit sound after the sidebar has already left.

Runtime adapter: `FrontendMusicDirector`; pure envelope: `FrontendAudioEnvelope`; profiler marker: `Elemental.FrontendAudio.Tick`. Authoring installer: Elemental > Audio > Install Supplied Frontend Tracks. Installer preserves existing profile tuning and user layout assets.

Unity primary references: [PlayScheduled](https://docs.unity3d.com/ScriptReference/AudioSource.PlayScheduled.html) and [DSP clock](https://docs.unity3d.com/ScriptReference/AudioSettings-dspTime.html). Scheduled independent voices avoid tying loop boundaries to scaled game time. Focused short-clip runtime tests exercise actual repeated seams; full-length artistic listening across every original track seam is separate from these checks.

## Validation

Focused audio Edit **6/6** passed at `2026-09-08T12:53:54.2594101Z`; actual-source Play **2/2** at `2026-09-08T12:56:28.0296393Z`, 5.5892s. Twelve scheduled clip starts and two simultaneous overlapping voices were observed with scaled time frozen; complete context changes and quiet panel playback passed. Audio marker mean0.00531ms / peak1.73030ms over1198 samples in the isolated Editor fixture (initial scheduling included). Reports: BuildReports/FrontendAudioEdit.json, FrontendAudioPlay.json and FrontendAudio/runtime.txt. Final combined scene integration is recorded below. No new standalone build or online pair claimed.

Final scene integration verifies the supplied clip bindings, actual Main playback while physics is frozen, a single sidebar-departure sound through countdown completion, and distinct Pause/Resume movement cues. Latest isolated/audio+camera Play3/3 passed13:16:24 UTC with an explicit listener in the isolated fixture. Final audio sample:12 scheduled starts,2 overlapping voices, mean0.00534ms/peak1.74740ms across1212 samples. Initial scheduling included; steady-state game-wide audio cost not claimed. Native Editor graphics-ring-buffer warnings occurred when launching Edit tests; final C# compilation succeeded and console error count was zero.
