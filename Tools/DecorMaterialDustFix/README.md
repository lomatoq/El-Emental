# Outer decor material and fracture-feedback fix

## Confirmed saved-content state

- `OuterArch_01_INTACT` through `OuterArch_07_INTACT`, all 85 dormant `FR_outer_arch_*`
  render cells, and all eight authored loose rocks use the exact arena material pair:
  `RumbleArenaSandstone` (`91fc0cc0e76ed8348bf172edefa7fd44`) followed by
  `RumbleSandstoneFractureInterior` (`e318bcc672b00554f9504b2493470036`).
- The current `OuterStoneRing.fbx` also has exterior then interior slots on every intact
  column. No Blender model or scene regeneration is required.
- The different material GUID on `COL_FR_outer_arch_*` belongs only to inactive collision
  proxies (`m_IsActive: 0`); it is never a visible column renderer.

The deeper source-mesh audit found why one column can nevertheless look as if it has a
different material. `OuterArch_06_INTACT` exposes 4.3884 square metres of authored fracture
interior, roughly 9.4% of its exterior area and the largest ratio among the seven. The current
interior material is stale: it is lighter than the exterior and still has `_SideShadowFade=1`
while the current arena material and deterministic integrator use `0`. Its exposed cut therefore
uses both a different value and a different realtime-shadow receiver path. The optional
`material-palette/after` patch brings that shared interior asset back to the exact current
integrator output. It changes no FBX assignment or geometry.

## Confirmed feedback regression

`EarthArenaFractureDustPresenter` used to emit the tuned 120-260-particle shaped fracture
cloud for arena structures. When shared material feedback was added, its subscription was
disabled for every production arena and outer-column structure. The replacement shared cue
emits 80 broad-dust particles and 18 mesh chips per released cell, which retained the chip
route but made the authored broad fracture cloud much less visible.

The staged patch restores the dedicated fracture-cloud subscription. The shared material
feedback remains active, so mesh chips and physical follow-up fracture debris are unchanged.
Both dust systems use the same `EarthEffectsTuningProfile.Materials.FractureDust` lit material,
so the day/night lighting correction remains in force.

## Focused validation

Run:

- `Elemental/QA/Run Outer Stone Ring EditMode Tests`
- `Elemental/QA/Run Outer Stone Ring PlayMode Tests`
- `Elemental/QA/Run Arena Fracture Shading EditMode Tests`

The strengthened saved-content assertion requires the exact ordered arena material pair.
The production PlayMode assertion fractures a real outer column and verifies the dedicated
cloud, shared broad dust, and shared mesh-chip particle counts all increase, and that both
dust renderers use the configured lit fracture material.
