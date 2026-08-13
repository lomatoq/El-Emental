# ADR-0012: Shared planet profile and scaled-space sky

Status: accepted  
Date: 2026-08-13

## Context

The gameplay planet is intentionally small, but radius `24` was duplicated across gravity, collision, spawning and decoration. The old sky used 42 nearby sphere renderers, so stars and planets visibly translated with the player. Moving the physical world through astronomical coordinates would make PhysX and voxel precision worse.

## Decision

- `PlanetWorldProfile` is the single authoring source for radius, surface gravity, SDF seed/noise, cell size, chunk resolution and render/collider budgets. Radius is applied only through the editor-side `Apply / Rebuild World` operation and is rejected after runtime state exists.
- Native High accepts 12–80 m, Native Low 12–48 m and WebLab 12–24 m. Initial voxel work is selected by conservative chunk-AABB/surface-shell intersection instead of filling the planet's complete bounding cube.
- The physical planet remains near the origin. `CelestialEphemerisSolver` produces a deterministic snapshot while `CelestialSystemBehaviour` moves direction-based sun, moon and distant-planet impostors around the camera at scaled-space distance. This removes near parallax without introducing large-coordinate physics.
- One seeded procedural skybox replaces per-star GameObjects. The visible sun and directional light share the same solver direction. Moon phase derives from the sun/moon dot product.
- URP atmosphere is split into a depth-aware `AtmosphereFullscreenFeature` before post-processing and a transparent limb shell for the outside edge. HDRP Physical Sky is deliberately not introduced.
- `MeteorShowerBehaviour` uses one bounded distant particle system and four pooled CCD physical meteors. Terrain edits are limited to two per second and happen only on impact.
- `EarthTriplanar` projects movable Earth in a normalized object frame measured in world metres. Voxel chunks share their planet-root transform and therefore one planet-local projection frame.

## Consequences

Runtime gameplay remains numerically stable and the sky does not follow lateral player motion. Radius changes require a deliberate rebuild, which keeps SDF state, collision proxies and spawn anchors coherent. Atmosphere and meteors are presentation/runtime adapters and do not become simulation authority. Full-screen atmosphere can be disabled by removing its renderer feature while the limb shell remains a low-cost fallback.

The procedural skybox explicitly writes the hardware far-plane depth so it remains valid under D3D11 reversed-Z. Scaled celestial bodies use dedicated unlit/phase materials; their illumination is independent of nearby scene-light culling while the moon terminator still follows the shared sun vector.
