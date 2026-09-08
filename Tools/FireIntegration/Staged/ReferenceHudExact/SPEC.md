# Exact reference HUD — measured composition

Source: user image `codex-clipboard-e25c8458-3633-44d3-97cf-0a666a9ceff6.png`, 1493 × 840. This is the replacement HUD reference, not the older radial wheel. Coordinates below are top-left logical reference pixels. All values multiply by 1080/840 for the existing 1920 × 1080 UI panel; PanelSettings, world camera, lighting and gameplay do not change. X center groups anchor to screen center, edge groups to their respective edges. At different aspect ratios these anchors preserve the authored margins rather than stretching artwork.

## Layout and decomposition

| Layer | Reference bounds / position | Normalized reference anchor | Treatment |
|---|---|---|---|
| Wordmark | x34 y16 w176 h45 | top-left .0228,.0190 | Existing transparent serif wordmark artwork; no opaque plate |
| Scoreboard | x554 y7 w395 h83 | center .5033, top .0083 | Absolute three columns 95/206/94; no flex redistribution |
| Timer plate | x649 y7 w206 h76 | .4347,.0083 | Dark slate alpha .76, thin gold outline, clipped lower 19px corners, bottom gold diamond |
| EARTH DUEL | timer-local x0 y11 w206 h16 | centered | Cinzel 10, tracking1.2, warm gold |
| Live time | timer-local x0 y27 w206 h39 | centered | Cinzel bold36, tracking1.4, cream |
| Red / Blue scores | team-local x0 y2 w94 h36 | centers x601 /902 | Live text, Cinzel bold33; caption Cinzel9 tracking2 at y37 |
| Team rules | x554/855 y66 w95 h12 | top center group | Thin red / blue rule, no filled team box |
| Pause | x1431 y14 w47 h48 | top-right, offset15,14 | Existing real button; octagonal slate/gold decoration; stable rectangular hit target |
| HP / MP groups | x8 /1435 y272 w50 h300 | left/right and y .32381 | Existing curved gauge rendering; mirrored on right; gold outline and cap ornaments |
| Gauge channel | group-local x0 y0 w50 h222 | group-local | Existing real fill/trail/flash. Visible channel occupies about36px width |
| Vital icon | group-local x16 y230 w18 h19 | centered | Existing + / energy symbol, cream19 |
| Vital value | group-local x0 y255 w50 h27 | centered | Existing number, readable bold22 |
| HP / MP label | group-local x0 y282 w50 h17 | centered | Cinzel9 tracking1.5 gold |
| Element row | x539 y694 w430 h116 | center +7.5px, bottom30 | Four independent slots; no wheel or circular halo |
| Fire diamond | center593,740, nominal60 ×60 | .3972,.8810 | Orange edge and native alpha glyph25 |
| Earth diamond | center695,740, active72 ×72 | .4655,.8810 | Cream/green-gold edge, narrow three-step edge glow, glyph31 |
| Water diamond | center804,740, nominal60 ×60 | .5385,.8810 | Blue edge and glyph25 |
| Air diamond | center910,740, nominal60 ×60 | .6095,.8810 | Muted cream edge and glyph25 |
| Element labels | row-local y84 h18, centered on slots | reference y778 | Cinzel10.5 tracking1.8; Fire/Earth/Water/Air, separate semantic colors |
| Selected marker | selected center, reference y695..700 | above selected slot | 8 ×5 gold triangle; follows real selected element |
| Active underline | reference y801, selected center ±35 | row-local y107 | Gold70px line and 8px diamond over thin430px passive divider |
| Bottom local scrim | centered w600 h170, bottom0 | bottom-center | Smooth interpolated vertex alpha0 at top/ends, max .58 at bottom-center. No whole-screen dimming |
| Globe container | x1264 y558 w218 h218 | bottom-right group offset11,37 | Existing live EarthHologramGlobe, player direction and arena marker |
| Globe ornament | center1373,667 radius97/92 | .9196,.7940 | Transparent double gold ring; 4 cardinal diamonds; no painted terrain |
| Globe cardinals | center ±75 on axes | globe-local | Cinzel8; true sphere/player projection remains visible |
| LOCAL ORBIT | x1264 y783 w218 h18 | bottom-right | Cinzel10 tracking2 gold; old secondary legend hidden |

The native user screenshot has a terrain-looking sphere interior. This implementation intentionally retains the real scene-derived globe and navigation markers rather than introducing fictional map data. A terrain projection requires an independent actual-world data source; it is not faked with the reference bitmap.

## Input and safe areas

- Pause is the only interactive Combat decoration: 47 ×48 reference-pixel rectangle, at least44 on both axes. Root existing pause callback remains unchanged.
- Element diamonds, captions, all ornaments and scrim use PickingMode.Ignore. They reflect canonical selection; no fake local ability-selection callback is introduced. Reserved visual envelopes are72 ×72 at the selected slot and60 ×60 elsewhere; even selection changes leave gaps of at least30 reference pixels.
- Logo uses34/16 top-left padding, pause15/14 top-right, vitality8 edge padding, bottom row30 and globe37 bottom padding. These are margins from the existing UI viewport, not invented device-notch safe-area data.
- Existing result button interaction, authority-controlled restart availability, hover/focus artwork and stable hit rectangles are merged from the menu agent. End-of-round presentation hides every Combat root child, including the new scrim/row, through the existing visibility owner.
- Selected glow is static and confined to the diamond edge. Reduced Motion therefore keeps the same clear active state without scaling/pulsing. No Update allocations are introduced: Tick compares enum and only writes when real selection changes.

## Source assets

Wordmark and element glyphs use shared Stone skin sprite fields. `ReferenceSpriteFidelity` supplies actual alpha-cropped Fire/Earth/Water/Air artwork. Simple timer, pause, diamond, divider and globe ornaments are code-native vector paths; raster generation would reduce fidelity/scalability for these geometric shapes. No screenshot is shipped as a background.

## Required actual acceptance

1. Import ReferenceSpriteFidelity shared skin/art first, then this lane. Run Install Stone Artwork followed by Elemental/UI/Install Exact Reference HUD.
2. Run StoneHudExactLayoutTests plus the existing real HUD/round result fixture. Validate score/timer/value updates rather than fixed screenshots only.
3. Capture actual Combat at1493×840 and1920×1080, plus current1681×785 viewport. Inspect wordmark/scores, diamond edge/glyph proportions, HP/MP value clipping, globe readability and edge anchoring. Source compile is not visual acceptance.
4. Pause/resume and end the round: check Combat row/scrim disappear; real pause/restart/menu callbacks retain their behavior. Move player around the planet and confirm globe heading changes.
5. In the actual document query reference-exact-element-row/reference-token-* worldBounds. Check inactive and each selected element at rest, and reduced-motion state. No alpha should extend toward adjacent slots beyond the narrow edge glow.

Known deliberate limits: minimap contains genuine navigation/grid but not terrain extraction; labels use installed Cinzel/LiberationSans assets and need real UI raster comparison. No final screenshot parity claim is made before the Unity capture.
