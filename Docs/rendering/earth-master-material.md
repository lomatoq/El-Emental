# Earth master material

`Elemental/SG Earth Master` is the shared URP 17.5 Earth surface contract. It is deliberately a hand-authored shader asset rather than canonical gameplay state: profile changes alter only presentation.

## Projection frames

- Moving walls, loose blocks, pillars and fracture pieces use a scale-correct object frame. Translation and rotation therefore carry the texture with the body while texel size remains measured in world metres.
- Voxel chunks share the owning planet's `worldToLocalMatrix`. `VoxelPlanetBehaviour` updates one runtime material clone, so adjacent chunks use identical coordinates and a transformed planet cannot make the texture swim.
- The voxel surface and loose-stone materials are separate assets. A shared material must never mix the planet-frame and object-frame modes.

## Baked vertex contract (schema 3)

| Channel | Meaning |
|---|---|
| R | weathered exterior classification |
| G | fresh fracture/interior classification |
| B | optional magic activation mask |
| A | cavity/depth response |

Every render piece has exterior submesh `0` and interior submesh `1`. Collider meshes keep compact shared topology and never depend on render-vertex duplication. The baker and validator reject missing red/green masks.

## Surface hierarchy and variants

The shader combines broad seeded variation, mid-frequency triplanar albedo/mask sampling, cavity/dust/mineral response and close-range normal detail. Micro normal detail fades from 9 to 24 metres in Native High. `_EARTH_DETAIL_LOW` removes that sample; `EarthMaterialProfile-NativeLow` also shortens detail distance. Fresh interiors are cooler, darker and rougher than the warm weathered exterior. Magic emission is bounded to a small accent.

Native High uses `EarthMaterialProfile` plus a 40-decal feedback pool. Native Low uses the paired `EarthMaterialProfile-NativeLow` and `EarthFeedbackProfile-NativeLow` assets with the micro-detail variant, 20 decals and lower chip/dust ceilings.

## URP renderer policy

The production renderer remains Forward. Task 4 does not switch to Forward+: its hero scene uses one directional key, tightly bounded transient lights and a small number of visible structures, so the additional tiled-light path has no demonstrated win yet. A Forward/Forward+ A/B capture is required before that setting changes. GPU Resident Drawer also remains disabled for dynamic fracture pieces; it is reserved for compatible static/repeated decor after an actual capture demonstrates benefit.

Surface scars use the URP decal renderer feature and a fixed ring pool. Scars are persistent until their slot is reused; no decal GameObject is allocated by an impact. Dust, irregular mesh chips, camera response and the rare bright mote are all driven by `EarthImpactEvent` through `EarthFeedbackProfile` and do not feed back into physical truth.
