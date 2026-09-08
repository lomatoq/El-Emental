# Bot start: complete sidebar departure and camera cover

Uncommitted main, base 1235579. User rejected the previous partial sideways movement combined with alpha disappearance.

The previous exit endpoint was only 65% of the 170 px entrance displacement. FrontendFlowController also changed its fade target continuously while starting the countdown and camera immediately.

Bot start now uses a dedicated departure from the rendered position. The menu remains opaque while the panel and oversized curtain travel beyond the viewport over 0.54 seconds. A dark blue-black full-screen cover rises from 0.44 to 0.58 seconds. Countdown camera framing begins at the cover peak. The cover clears by 0.82 seconds, then the full local countdown starts and the existing camera dolly continues into gameplay. Controls are disabled from the click. Reduced Motion uses a short 0.18-second fade transition without travel. Returning to Main clears the cover and restores ordinary menu animation. No user layout or material settings were changed.

The network countdown still reads its external authority; the introductory visual phase does not add time to the server countdown.

Validation: BotStartTransitionPlay runs FrontendMotionContinuityTests and MenuLayoutProductionTests. The production fixture checks opacity during departure, offscreen movement, no premature countdown, and captures all three phases in BuildReports/MenuLayouts/Start-*.png. Final results are recorded in the project state and execution tracker.

Artist control references: ground wind material is Content/GraphicsV5/Materials/WallcoeurGroundDust.mat; density/motion use Content/Profiles/EarthClusterGroundDustProfile.asset. Contact SSAO settings are on Assets/Settings/ElEmentalRenderer.asset, Elemental Contact SSAO. Current SSAO is achromatic. RumbleRockLit materials expose Soft Shadow Rock for shadow tint and Contact Occlusion Strength for AO response; use RumbleSandstone and RumbleArenaSandstone for current stone/arena art rather than the older EarthLooseStone material.

Final PlayMode result: 3/3 passed, 2026-09-08T10:55:46.7301500Z, 59.6992 seconds. All three production phase captures inspected. Existing layout profiler output: samples=64; meanMs=0.327396875; peakMs=5.4404; scope=layout binding only, editor, not total UI rendering
