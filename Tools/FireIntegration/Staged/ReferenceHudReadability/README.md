# Reference HUD readability — narrow actual capture follow-up

Baseline: actual `BuildReports/CombinedVisual/Combat-exact-hud.png` at1681×785. Four existing source files only; placement/anchors and original result controls remain unchanged.

- Element labels12 instead10.5 logical pixels; LOCAL ORBIT11 instead10; top captions slightly larger/brighter. Cinzel regular400 was visibly too fine; matching-color narrow text outlines strengthen strokes while retaining the installed serif face. Timer/score outline .55px, other text .22/.28px. Real raster capture is required to confirm it is not too heavy.
- Active diamond interior alpha .16 instead .72; inactive .26. Wider edge-only6px falloff and2.2px core rim. No circular halo, scale pulse or changed selection authority.
- Corrected scrim mesh triangle orientation to the UI Toolkit quad orientation, and installer increases localized bottom fade to .80. Prior screenshot showed no meaningful footer dimming; the winding issue is the concrete suspected cause and must be verified in actual capture.
- Existing EarthHologramGlobe adds optional BackingOpacity default .35 (original appearance unchanged elsewhere). Exact HUD sets .84 for dark contrast behind its own real grid/player/arena navigation. No painted terrain or fake map.

Import four files, then run Elemental/UI/Install Exact Reference HUD again to update existing exact profile opacity. Four real Roslyn assemblies PASS. Actual screenshot remains pending, not a visual pass claim. Check selected rim, text stroke weight, visible smooth bottom fade and readable live globe.
