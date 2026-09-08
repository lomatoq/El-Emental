# Graphics source environment integration — inspected existing owners

The user subsequently supplied the local StoneUI ZIP. Its actual unchanged sources are now available under `Tools/FireIntegration/Reference/StoneUI/EL_Emental_StoneUI`; the adapted patch is in this directory's `after/`. No further browser retrieval was attempted. This map records the inspected existing checkout seam.

- `Assets/Elemental/Content/Scenes/EarthCoreSlice.unity` serializes `VoxelPlanetBehaviour.radius: 55.1` at line 16760. Both inspected game cameras use a 5000 m far clip plane. Do not rescale/regenerate the arena to fit source defaults.
- `Assets/Elemental/Runtime/Physics/PointPlanetGravitySource.cs` exposes Radius and transform position. The environment should receive its world anchor explicitly and derive radial placement from that owner, without Camera.main-following or an implicit fixed origin.
- `Assets/Elemental/Presentation/Rendering/CelestialSystemBehaviour.cs` owns the existing shared `_ElementalPlanetCenterRadius`, `_ElementalSunDirection`, `_ElementalNight01`, `_ElementalSolarAltitude`, `_ElementalTwilight01` and atmosphere globals. Do not introduce a second daylight or sun authority.
- `Assets/Elemental/Presentation/Rendering/EarthSkyController.cs` owns the runtime skybox, local-up horizon and sky palette. It intentionally separates camera horizon from the authoritative lighting anchor.
- `Assets/Elemental/Presentation/Rendering/AtmosphereFullscreenFeature.cs` runs the depth-aware atmosphere before post-processing. `M3EarthCoreSetup.cs:1718` explicitly sets `RenderSettings.fog = false` because this is the sole fog authority. Source environment shader must write opaque depth and omit its own fog/URP MixFog pass. Do not modify the global atmosphere or authored vignette to accommodate new geometry.
- `Assets/Elemental/Content/Profiles/AtmosphereProfile.asset` currently uses aerialPerspectiveStrength 1.28, aerialPerspectiveDistance 22 m, maximumAerialOpacity 0.5 and heightFalloff 1.05. These are existing owned values; distant geometry will receive this pass automatically from depth.
- Existing color/material code uses normal URP linear rendering. Source colors should enter once through Unity material color handling. Inspect recovered shader and C# for manual `.linear`, GammaToLinearSpace/SRGBToLinear, or LinearToSRGB conversions before integrating; avoid applying two conversions.

Intended new ownership is presentation-only: seeded irregular distant valley/mountain masses and floating islands with fixed world anchors and low-frequency bounded oscillation. No Collider, Rigidbody, network component, terrain authority, navmesh or per-frame geometry rebuild. Use supplied source meshes/generator only after their actual code is recovered and inspected. Avoid camera-following billboards and evenly spaced/evenly sized ring placement.

No existing Assets or Unity state was modified while preparing this map.
