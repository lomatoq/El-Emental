# Fire continuity research and implementation decision

Reviewed 2026-09-10. Unity 6000.5.7f1, EarthCoreSlice, working tree based on b646f675. This is an implementation decision and experiment record, not visual acceptance.

## Problem observed in our captures

The last Sphere capture contains separate orange parcels and edge-on atlas streaks. Adding particles alone has not produced a continuous spherical silhouette. The late expanding ring also loses coverage as its circumference grows. White source temperature, orange outer flame, and cooling smoke need to remain distinguishable against both stone and sky.

## Primary sources and what they change

- [Green and Horvath, NVIDIA/Pixar, Flame On, GTC 2012](https://developer.download.nvidia.com/GTC/PDF/GTC2012/PresentationPDF/S0102-GTC2012-Flame-Simulation-Games.pdf): the presentation explicitly identifies the Horvath/Geiger Harry Potter technique as inspiration. Its practical lessons concern transported density and temperature, advection quality, turbulent detail that travels with the flow, HDR emission, heat distortion, and cooling embers carried by the same velocity. Its historical GPU timings are not a budget for our game. Decision: coherent transport and temperature hierarchy are requirements; random per-frame displacement is inadequate.
- [Sørb, Making fire feel alive: Ignitement, Unity, April 2026](https://unity.com/blog/real-time-fluid-simulation-fire-vfx-ignitement-breakdown): a solo developer combines 2D velocity, density, temperature and reaction fields, obstacle interaction, vorticity, smoke conversion and shared lighting. Rendering uses a parallax approximation. Vertical behavior and domain shifts have explicit limitations. Decision: adopt the shared-data principle; defer this specific planar grid because our camera, planet gravity and protective sphere require free 3D views. The article does not establish our performance or compatibility.
- [Aka, Simple Fire Shader Breakdown, Real Time VFX](https://realtimevfx.com/t/simple-fire-shader-breakdown/11213): consistent warping across masks, independently phased particle noise and lifetime control of both opacity and emission help avoid synchronized cards and abrupt birth. Decision: retain atlas detail as a secondary layer, with smooth birth and actual render-camera billboarding. This technique alone cannot provide the sphere volume.
- [James L Watt, Hogwarts Legacy production breakdown](https://www.gamevfxartist.com/blog/2022/11/20/hogwarts-legacy): shared master materials, authored vector fields and Houdini flipbooks are part of the artist's documented production workflow. Decision: share flame appearance across hand, foot, projectile and column emitters; differentiate their motion and temperature rather than create incompatible materials. This source does not document the film's cave fire.

## Chosen implementation boundary

| Approach | Decision | Reason |
| --- | --- | --- |
| More independent billboards/capsules | Reject as the primary sphere representation | Does not guarantee continuity; exposes cards and synchronized silhouettes. |
| Full moving 3D pressure grid | Defer pending a measured comparison | Adds simulation memory, obstacle voxelization, pressure solve and integration complexity; no evidence yet that it meets this scene's budget. |
| Procedural volumetric sphere plus transported flame/smoke details | Implement and visually test | A single density volume carries the silhouette, while existing bounded collision-aware flows provide surface breakup. |
| Existing collision-aware parcels for directed spells | Retain and refine | They already connect visual movement to real geometry. Smooth birth, coherent guidance and cooling detail must improve their temporal appearance. |

The sphere candidate uses a bounded raymarched thick shell with transported multi-scale noise, HDR emission/extinction, opaque-depth clipping and camera-inside handling. It is a procedural volume, **not a Navier–Stokes simulation**. Density continuity and apparent fluid motion must be assessed separately. Existing CPU collision and damage authority remains authoritative; a shader is not proof of protection or damage.

## Representative acceptance experiment

Use the saved arena and actual character. Capture equal-time sequences from outside, inside, and a camera moving after LateUpdate. Include opaque geometry crossing the sphere, bright sky, dark rock, a close column, foot contact, projectile cooling and the late released ring. Keep the rival alive and away from the isolated visual test so round-end teardown cannot invalidate the capture.

Reject visible edge-on streaks, three-ring final silhouettes, repeated isolated beads, sudden flame birth, smoke detached from the cooling flow, or volume visible through opaque geometry. Confirm the small near-white source remains separate from the orange body; increasing environmental lamp intensity is not a substitute. Record native test output, capture paths and CPU/GPU limitations before marking accepted. A still image and a successful compile are insufficient evidence of fluid motion.

## Current evidence

Native089 resolved the earlier device-frame timing failures; Native09358/58 Edit and0945/5 Play pass. Native0958/8 Play produced the first combined current-art corpus. The white/yellow helical sphere matches the selected baseline more closely and has broad cooling smoke. Actual capture still exposed a detached bolt tail, tiny column plume, invisible source trace and late ring beads; these were not accepted simply because their runtime tests passed. Corresponding corrections and stricter capture assertions are imported; repeated visual validation is pending an idle Editor. The atlas opacity envelope correction is staged. See PROJECT_TECHNICAL_STATE and the review gallery for exact current scope. No standalone GPU/GC acceptance is claimed.

## Reproduced editor cyan placeholder

The actual paused dust card at dusk became a solid cyan quad while its material shader reference stayed unchanged. Unity documents that asynchronous variant compilation displays a cyan placeholder. Use the per-shader `#pragma editor_sync_compilation` on the affected dust, cooling smoke and rock color pass; preserve premultiplied blending/tint. First-variant Editor stall is the tradeoff, not a player-build rendering change. See [Unity manual](https://docs.unity3d.com/6000.0/Documentation/Manual/AsynchronousShaderCompilation-enable-or-disable.html). Native098 image evidence is G05/DustSky-20260910-115720/sky-dust-0.520.png; corrected-shader repeat remains pending.
