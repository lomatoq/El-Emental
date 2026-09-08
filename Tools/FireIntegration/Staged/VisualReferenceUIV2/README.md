# VisualReferenceUI V2 — first reviewable build, pending Unity visual acceptance

No actual Assets edits or Unity calls. Before/after + before-sha256.json + refinement.patch. Four offline Unity Roslyn assemblies pass: Presentation, Authoring.Editor, EditMode, PlayMode. Imported Cinzel hash verified against root. Original theme/font/layout assets remain unchanged: installer adds a separate StoneReferenceProfile, Cinzel Reference SDF with persisted atlas/material + fallback, and StoneReferenceHudLayout. Profile defaults opt in; saved original HUD remains accessible by disabling profile and rebuilding/re-entering view. Menu profile switching requires rebuilding the menu/Play restart.

## Gap matrix

| Target | Current baseline observed | V2 implementation | Remaining visual gate |
|---|---|---|---|
| 1 gameplay | tiny cramped timer, missing wordmark/wheel; tiny caption, plain globe; original authored gauges | optional600x122timer layout/280x117plate, Cinzel readable roles, wordmark, dynamic selected-element ornaments, frame/compass around real globe, HP/MP captions, larger vital values,62pxpause | inspect Combat at16:9; world/backdrop handled by geometry/fog agents; real globe is retained rather than painted minimap |
| 2 settings | rectangular veil under curtain, compact labels, no active-element strip | fullheight proportionate curtain direct root; no Left veil; larger header; Cinzel labels, readable exact numeric values, actual selected-element ribbon/status, diamond handle/check/icons; existing preferences retained | verify footer/back/slider spacing; targets' decorative rock texture is not regenerated |
| 3 mode/menu | narrow buttons and tiny old font |560x98existing actions,52pxrole icons, optional serif, hover halo/shine/focus/press; source-style large column | Practice/combined Multiplayer page are not invented; real bot/host/join/settings/quit remain available. Exact labels differ deliberately where no backend mode exists |
| 4 defeat | small ordinary round-complete box | real IsRoundOver -> local-perspective defeat, red scrim/emblem/title, guarded retry round and real EndMatch callback, configured defeat fade | capture actual lost round; per-life respawn feedback never triggers fullscreenresult; background sunset/rocks are not UIassets |
| 5 victory | small ordinary result, no score hierarchy | centeredbrand/emblem/largeVictory/actualscore/twoactions, independent halo; same authorityguard | capture won round and Draw; no fake3-1score, no forged rematchpolicy; cannot claim exact textured letters/foreground rocks |

## Archive motion mapping — endpoint revision

All20 JSON presets copied byte-for-byte. Profile reads duration/ease/start/end/loop, numeric channels, and authored error x-key sequence (in-memory union rename only; original JSON untouched). Unscaled UI time. 18 UI-owned presets have actual callers; 2 world/camera presets are not claimed as UI implementation.

| Preset | Actual binding and authored endpoints |
|---|---|
| panel_enter | menu + curtain x-28→0, alpha0→1, .28 outCubic |
| panel_exit | menu + curtain x0→-16, alpha1→0, .16 inQuad |
| content_enter | page visual y8→0, alpha0→1, .2 outCubic |
| button_hover | Press Visual scale1→1.014, independent halo0→.42, .13 outQuad |
| button_press | Press Visual targetscale.984, .07 outQuad; interrupted transitions begin from current scale to avoid a discontinuity |
| button_release | Press Visual targetscale1, .12 outCubic; hover may subsequently select hover target |
| focus_enter | dedicated nonraycast keyboard focus ornament0→1, .1 linear |
| element_select | actual MagicInput.SelectedElement token scale.92→1/glow0→.7, .22 outCubic |
| selected_breath | selected token/result halo opacity.28↔.42,3.6 sine, looping |
| success | actual local victory: result alpha0→1; decorative child y12→0, scale.96→1,.48outCubic; action hitboxes remain stationary |
| defeat | actual local defeat/draw fallback: alpha0→1, decorative y5→0,.34outCubic |
| toast_in | actual status alpha0→1/y8→0,.16outCubic |
| toast_out | actual status clearing alpha1→0,.16inQuad |
| meter_value | real health/mana presentation current→target,.14outQuad; authoritative values untouched |
| meter_damage_tail | real health trail previous→target,.36outQuad; retarget from current visual when interrupted |
| error_feedback | actual error status x sequence[0,-3,3,-2,0],.16sine; no alternate rotation |
| shine_pass | clipped independent glint traverses phase0→1,.6linear on hover/focus |
| connect_pulse | actual SetConnecting status opacity.35↔.75,1.8sine |
| menu_camera_blend | NOT rebound: existing CinematicMenuCamera owns actual pose/countdown/transition, separate lane; metadata retained only |
| island_float | NOT rebound: world geometry/fog/environment owner, not a UI object; metadata retained only |

ReducedMotion caps fades at.08s; no translation/scale/shake/shine/looping. Focus visibility remains functional. Paused menu uses unscaled time; underlying round's pause policy stays in FrontendFlowController. Endpoint contract assertions include both panel distances, success scale/y, defeat neutral scale, error midpoint, glows, pulse bounds and semantic meter interpolation. Runtime/capture validation still pending.

## Import and acceptance

1. Check freshness hashes. Import after/ (includes JSON); Cinzel source/OFL already imported byroot.
2. Run Elemental/UI/Install Stone Artwork. Creates its own font/profile/layout assets; preflights font+JSON before texture mutation.
3. Run Elemental/QA/Stone Reference Profile Edit; existing Stone Skin Edit and Installer Idempotence.
4. Run Elemental/QA/Stone Skin Play. Productionfixture now captures Main/Settings/Host/Join/Pressed/Combat/Pause/Victory/Defeat/Draw; real match clock fast-forward only with actual runtime endtransition. Checks no veil,20presets, original layout preserved, button states/stationaryhitrect, ReducedMotion, true HUD node/globe identity, actual score/outcome and restart forbidden state. Original HudLayoutPlayTests explicitly chooses original layout skin-null on its cloned theme to continue testing original authoring workflow; productionfixture tests the optional profile.
5. Inspect captures against all5targets and iterate before visual acceptance. No runtime/performance/visual pass claimed yet.
