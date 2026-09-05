# Head armor fitting and seam stones — September 4, 2026

Dirty working tree on `d2174eded114dd022e4a9c442abadda7a0e44555`.

The shipping Linebreaker character has one skinned body mesh. Its 2,093 head-weighted
vertices extend far beyond the old head-to-neck-radius estimate: the skull/helmet
center is approximately .38 m above the head bone, whereas the old solver offset
the center only .08 m. This seated the head stones inside the visible model.

At armor assembly, `EarthArmorHeadSurface` bakes the visible skin once and selects
vertices weighted at least .5 to Head or its descendants. `BakeMesh(..., true)`
accounts for the imported scale; the original mesh need not be CPU-readable.
The pure `EarthArmorHeadShell.SurfacePoint` places each head plate outside its
support plane. Positions, normals and tangents are cached relative to Head.
There is no mesh sampling or allocation in the steady-state follow loop.

The original 96 body/head anchors retain their indices, dimensions and authored
profile values. Sixteen extra small seam stones fill crown and middle-ring gaps.
They occupy independent slots 96–111 in the normal armor pool, so expansion,
orbit, release, collision, recall and cleanup use the existing controller paths.
The base profile remains 96; capacity is 112. Generic/non-humanoid characters
still use their base budget. The semantic separate-head renderer path no longer
moves front head samples aside to create a face aperture.

Armor evaluates the final compact pose after procedural body response and IK
(execution order 2300). This removes the late-animation offset between the head
and its cached plate anchors. Expanded formations use their own surface tangent.

## Evidence

- `BuildReports/HeadArmor/Latest.json`: production fitted vertices, five exterior
  render-mesh ray checks, head-follow error, orbit and launch counts for all fillers,
  and measured `Elemental.Armor.CompactFollow` time.
- `BuildReports/HeadArmor/Front.png` and `Back.png`: actual scene close-ups.
- `BuildReports/WaveHeadArmorPlay.json`: focused production wave/head plus three
  existing body/volley/recall regressions.
- `BuildReports/WaveContactEdit.json`: pure wave timing, head fitting and seam layout.

The first broad exploratory run is preserved under `BuildReports/HeadArmor/FirstRun/`.
It exposed the now-corrected unreadable-mesh path. Three other existing tests failed
outside head fitting: the old no-defensive-collision assertion (the controller now
intentionally enables defense after gathering), synthetic collider-shell proximity,
and the platform sweep duplicate-impact assertion. They are not claimed fixed by
this work. The focused command does not certify the complete M11 acceptance suite.
