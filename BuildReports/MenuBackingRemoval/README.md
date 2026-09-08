# Immediate main-menu backing removal

User explicitly requested removing the old backing immediately, independently of HUD/V2 work.

Actual FrontendMenuView no longer creates Left veil. The stone curtain is attached directly to Menu contents with full-height aspect-preserving sizing. No legacy rectangle fallback remains.

Verified in live EarthCoreSlice Play: FrontendMenuView exists; all descendant transforms including inactive contain zero Left veil objects. Main.png is the actual screen capture after the change, visually reviewed. Unity console returned zero errors. This verifies backing removal only; typography, broader UI reference matching and environment acceptance remain in progress.

Before Play, only the owned EE_RockPreview_V2 child beneath DistantBackdrop was removed and the scene saved; existing backdrop remained.
