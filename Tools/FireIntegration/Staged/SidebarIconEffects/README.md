# Sidebar icon/effects pass
Fresh before/after, three UI source files. No Unity/live Assets writes.

Each sidebar token has ONE centered RectTransform (70x70, center x88+128*i/y35), ONE symbol, ONE sharp outline. Frame/soft rim/symbol all inherit the same selected 1.24 scale. Previous setup independently scaled the frame while leaving a larger unscaled sibling glow at a different radius and drew two frame rings; root alpha-zero workaround then removed the active frame. New SingleDiamond draws only one outline, retains it active, changes active tint to pale cream-green, and aligns soft rim radius exactly .47. No replacement/original symbol stack is created.

Passive frames retain element color. Rim pulse and tiny bright glints are localized to perimeter, not central wash. Selected token remains large and bright in Reduced Motion, motion is frozen.

Fire/Earth/Water use previously measured alpha-center offsets. Air is reduced 40->34px and raised .85px inside its common center (long tail optical correction), card Air 68->58px. Card moved y122->134, retaining 15 logical px before Main button top. Shared diagonal x=y/3, all fonts, keyboard behavior and no forced Bot/Resume selection are untouched.

Buttons: stronger perimeter highlight and small edge glints, mouse or keyboard focus gets the existing sweep (max alpha .28); Reduced Motion disables sweep and holds static edge glow. Existing source-driven page entrance effects preserved.

Validation: Elemental.Presentation offline compile exit0, no warnings/errors. Runtime root QA pending: 4 token holder children each must contain exactly one Element glyph and one Element status; inspect no ghost original in Main/Pause/Settings/Host/Join, active frame brighter than passive, Air tail comfortably inside frame, card gap and hover/keyboard effects. Native source card has been visually verified icon-free; no raster painting or opaque coverup performed.
