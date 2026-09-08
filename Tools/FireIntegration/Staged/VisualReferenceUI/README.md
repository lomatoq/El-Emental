# VisualReferenceUI refinement

Four source files staged against actual current baseline. Import only after checking before-sha256.json freshness; refinement.patch supplied. No actual Assets changes or Unity calls.

- Optional exact supplied wordmark and divider; original theme fonts remain on dynamic text and fallback title. Header is already outside page objects and Show only toggles pages: no proven source header-disappearance bug, so no speculative reparenting fix.
- Existing bot/host/join/settings/quit/resume/back/copy/ready/connect actions now have explicit optional role sprites. Icon children are non-raycast, created before Configure transfers them into Press Visual. Original callbacks, page enum, outer470x70 hit rectangle and 72px caption inset retained.
- Button sliced border multiplier136/70 scales original border dimensions uniformly to the existing height. This addresses disproportionate endcaps without changing hitrect; visual acceptance still requires capture.
- Settings use supplied icons/diamond handle, percentage labels for volumes, actual sensitivity multiplier with invariant2decimals, check artwork. Existing sliders/preferences save and refresh callbacks retained; no new preferences. Slider outer hit rect unchanged.
- Installer fills new fields from already imported assets and keeps original idempotent save guard. Rerun installer after import; no texture additions required.
- Existing production Play test augmented to check header active across pages, correct numeric labels, diamond handle, role icon under animated graphic and original button size. Existing actual bot flow/HUD identity/input-state checks retained. No tests executed here.

Offline Unity Roslyn Presentation, Authoring.Editor, Play tests all0 (compile/report.json). Run Elemental/UI/Install Stone Artwork, then existing Elemental/QA/Stone Skin Play and installer idempotence menu after import. Inspect refreshed Main/Settings/Pressed/Pause captures: readable long captions, icons inside bevel, no top header clipping, values no overlap. No result screen/policy or modes, HUD manual layout, fonts, networking or rocks changed.
