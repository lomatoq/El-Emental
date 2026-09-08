# Sidebar procedural reveal

Presentation change on uncommitted main, September 8. No menu ScriptableObject values or button hierarchy were rewritten. The original `x = y / 3` staircase remains.

The curtain/column slides 170 reference pixels with the existing out-cubic track. Header artwork fades and shifts in, followed by page controls after a 140 ms lead; control rows stagger 45 ms over a 260 ms reveal. Pause uses a 220 ms panel and 70 ms content lead. Switching pages while the panel is open reveals the new rows immediately. Related caption/value/slider nodes use visual row position, avoiding a long flat list of unrelated delays. Reduced Motion removes translation and stagger and completes a short 70 ms fade.

`SidebarRevealSettings` on `FrontendMenuView` exposes the timing and travel values. `SidebarRevealNode` composes only a temporary horizontal presentation offset, removes that exact offset before the next layout pass, and never alters authored scale or creates a Buttons wrapper. Page entrance no longer writes a hard-coded page root coordinate. Interrupted panel retargets use the animation's own rendered delta, preventing a ScriptableObject offset from being mistaken for the next animation baseline.

Contract tests: `Elemental.Tests.EditMode.SidebarRevealContractTests` (three), including repeated reveal/live offset edits, settled staircase coordinates and Reduced Motion. Existing `Elemental.Tests.PlayMode.FrontendMotionContinuityTests` covers interrupted panel reversal and held-button feedback. Validation is delegated to the main agent, which exclusively controls Unity; this report does not claim tests have run yet.

Coordinated final validation passed: all 3 SidebarReveal contracts in DenseAtmosphereEdit 21/21, plus existing continuity and production layout checks in DenseAtmospherePlay 11/11. CanvasGroup creation uses explicit Unity-null checking.
