# Original duel HUD restored

2026-09-06, working tree on main `1235579`. The user rejected the premium visual
redesign and requested the previous appearance with slightly larger text.

The exact pre-redesign `EarthDuelHud.cs`, `EarthDuelHud.uss` and
`EarthDuelHudGeometry.cs` were recovered from the HUD worker's logged file read
at 01:07:08 UTC, before its premium rewrite. The recovered source is archived in
`BuildReports/HudOriginalRestore/RecoveredBaseline`. The source being replaced is
preserved separately in `BuildReports/HudOriginalRestore/BeforeRestore`.
This is a source restoration, not a reconstruction guessed from screenshots.

The original captured look is preserved in `BuildReports/HudPremium/Before/hud.png`
and `gameplay-hud.png`, also available in `BuildReports/DuelHud`. The baseline
image was inspected: slim bowed gauges with + / star symbols, unboxed side values,
separate red/blue score underlines, the round-bottom clock and unboxed globe.

Restored the original visual tree and exact gauge/globe drawing. The geometry
file matches the recovered source SHA-256:
`BC557D7F7C0F0A3F5440AACA4283CA5A846F176799AA719E068E2DE75380F2F6`.
The stylesheet increases its explicit font-size values,
increased by 12% and rounded to 0.1 reference pixels: vitals 12 -> 13.4, timer
34 -> 38.1, captions 10 -> 11.2, scores 46 -> 51.5. The larger side text exposed
a one-pixel overlap with navigation in the production layout check. Navigation
is lowered four reference pixels (bottom 26 -> 22); other positions, sizes,
colors, opacity, line widths, padding and corner radii are the original values.

Health, mana, countdown, scoring, readiness, navigation and restart subscriptions
remain current. Re-enable numeric-cache reset and empty-respawn hiding are kept.
There was no imported font to remove and no scene/prefab/authoring rebuild is
required; the existing UXML already references this stylesheet.

The existing production HUD test retains its health/mana/score/restart checks
and four resolution captures. Its visual expectations now enforce the requested
original slim layout and small type increase, saving fresh images under
`BuildReports/HudOriginalRestore`. Unity execution and fresh visual acceptance
are pending the coordinating task; scoped whitespace checks pass.
