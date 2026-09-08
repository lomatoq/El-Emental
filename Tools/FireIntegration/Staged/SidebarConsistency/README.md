# Sidebar consistency
Fresh stage from current actual FrontendMenuView (includes footer 96px segment). One file only. Root owns actual import/Unity.

All sidebar action buttons follow x=y/3: each 90px downward step adds 30px right. Main 0/90/180/270/360 y remains identical; Pause y74/164/254 beneath centered PAUSED heading; Host 156/246/336; Join 176/266; Settings Back y348. Same 78px height and 12px row gaps. Page base x32 for every reference sidebar; inputs/sliders keep internal layout/hitboxes. No default selection for Pause or Main; first Tab/arrow selects Resume/Play respectively.

Every reference sidebar label now uses Reference.menuFont including status, room code, entered code, slider values and hint. Existing legacy non-reference font behavior stays unchanged.

Optical glyph offsets measured from source alpha mass inside installed sprite rects (40px image): Fire(+.93,-2.98), Earth(+.11,-3.12), Water(+1.01,-3.83), Air(+.37,+1.43). Same corrections scaled for 68px card glyph. Card source is verified icon-free, glyph source isolated; no baked duplicate to hide. Root must inspect live hierarchy if older icon remains visible underneath.

Offline Elemental.Presentation compile exit0, no warnings/errors. Runtime visuals pending root; verify Pause/Main/Host/Join/Settings at 16:9 and narrow aspect, keyboard navigation, technical text coverage and duplicate presentation count.
