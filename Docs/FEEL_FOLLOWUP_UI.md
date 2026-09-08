# Per-life results and held menu buttons

Saved user HUD/theme values were backed up before editing under BuildReports/FeelFollowup/UserUiBackup. All old serialized fields were compared byte-for-byte after normalization: unchanged; only life-result fields were added.

Each accepted death changes the match score. EarthLifeResultState consumes local/opponent death counts, waits 0.12 seconds to combine near-simultaneous deaths, and displays YOU WON ROUND, YOU LOST ROUND or DRAW for 2.4 seconds. Physical knockdown without death does not affect scores and never produces a result. Results reset on a new match and follow the online local actor perspective. Hidden online HUD still consumes results while the world runs, avoiding stale combined results after returning from pause.

Settings: Elemental > UI > Select In-Game HUD Theme > Per-life round result for duration/window/font; Elemental > UI > Edit HUD Layout > Life Result for anchor/position/size/scale/rotation. Existing health/mana/pause/globe values stay untouched.

FrontendButton now scales a centered Press Visual child containing the existing image and labels. The authored RectTransform, anchor, pivot, nonuniform scale and clickable area remain stationary. Pointer exit does not release a held press; pointer up, focus loss or disable releases it. Theme press/release times and pressedScale remain user-owned.

Validation: FeelFollowupUiEdit 5/5 at 2026-09-06 21:44:08 UTC; FeelFollowupUiPlay 2/2 at 21:52:29 UTC. Play checked real production match deaths/repeated results and held button visual geometry; captured Life-Won/Lost/Draw under BuildReports/FeelFollowup. Draw and Won captures visually inspected. This is not a multiplayer test; SDK/Relay proof is recorded separately.
