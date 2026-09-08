# Result screen polish stage

Stage only; no Unity/editor or actual Assets were mutated by this lane.

## Runtime changes
- Result background now combines a low uniform outcome tint and smooth localized darkening behind the center content. The live arena remains visible at the scene edges.
- Colored four-element logo, LOCAL ALPHA label and existing serif wordmark restored; title enlarged to 142 logical px. Score still comes from the actual local/opponent scores. Real callbacks and restart authority gating are untouched.
- Removed broad radial halo image. Fine broken ring/diamond framing, bright title edge glow and 18 slowly drifting outcome-colored sparks provide restrained movement. Reduced Motion freezes sparks and keeps existing short fade-only result entrance.
- Important concrete repair: result button wrapper previously ignored the requested earth/danger plate and always replaced it with generic cream menu selection art. Result primary now actually uses the existing supplied green earth / ember danger sprite, independent of hover. Passive uses the supplied dark reference plate. Both have border-following glow; hover/focus increases visual scale to 1.04 and boldens text; press to .984 with stable hit rectangles.
- Restart text is REMATCH / RETRY, or WAITING FOR HOST. Existing callbacks are preserved. Back arrow, earth icon and forward glyph are separate live decorative children.

## Layout (1920 x 1080 logical canvas)
All central items align at canvas x=50%. Logo top36 size64; alpha top105 width300; wordmark top135 width350 height90; emblem top275 width185 height155; title top414 width1100 height170; divider top560 width650 height30; live score top586 width500; primary top715 width590 height96; secondary top835 width550 height84. Button captions center on their hit rectangle; icon at left28 and arrow right28. Defeat motto sits right55 bottom60. Local darkening centers at (50%,57%); its lateral falloff preserves scene edges.

## Assets / scope
Uses existing imported transparent artwork and the existing font; no additional raster generation or installer/schema changes. New code-native UI ornaments fit the existing Painter2D system. This is a runtime polish implementation, not a claim of pixel identity: the exact reference's detailed cracked defeat emblem and art-specific fire trails are not newly painted in this stage.

## Validation
Offline Unity Roslyn overlay compilation: see compile/report.json. Presentation has zero compiler diagnostics; downstream existing obsolete API warnings are unrelated to this lane. Unity Play screenshots / pointer / keyboard / reduced-motion verification remain root integration work. Import after overlay before-hash validation; run existing StoneSkin Play capture suite including Victory, Defeat, Draw, restart and back-to-menu. Check 16:9 and current wide Game view.
