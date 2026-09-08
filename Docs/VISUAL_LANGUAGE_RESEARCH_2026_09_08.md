# Earth / fire visual language follow-up

Working-tree research and implementation, 2026-09-08. Fresh focused checks and limitations are recorded in [the implementation report](VISUAL_PHYSICS_FOLLOWUP_2026_09_08.md); research alone is not visual acceptance.

## Source decisions

- [Cyanilux, Fire/Flame Shader Breakdown](https://www.cyanilux.com/tutorials/fire-shader-breakdown/): use a coherent camera-facing silhouette warped by low-frequency noise. Transparent overdraw grows with overlapping cards; do not solve a weak silhouette by increasing particle count. Adopt for column bodies, preserve separate tongues and sparse embers.
- [Federico Bellucci, procedural fire](https://blog.febucci.com/2019/05/fire-shader/): gradient shape plus advected noise provides cheap controllable motion. Adopt the separation between silhouette, flow and color bands; do not copy a flat sprite as the complete effect.
- [Sørb / Unity, Ignitement breakdown, April 2026](https://unity.com/blog/real-time-fluid-simulation-fire-vfx-ignitement-breakdown): a shared velocity field makes fire and embers react coherently; pseudo-volume shading can improve depth. Retain our existing CPU field/contact redirection. Defer a new GPU fluid framework: it would duplicate gameplay authority and needs its own performance comparison.
- [NVIDIA GPU Gems 3, light scattering](https://developer.nvidia.com/gpugems/gpugems3/part-ii-light-and-shadows/chapter-13-volumetric-light-scattering-post-process): visible shafts depend on occlusion, not just brighter noise. Our follow-up integrates directional-shadow visibility along the camera air segment using the existing shadow atlas. This is a bounded near-arena effect, not a global volumetric renderer.
- [Unity production-ready Shader Graph samples](https://unity.com/blog/engine-platform/new-shader-graph-production-ready-shaders-in-unity-6): rock detail needs coherent projection and material layering. Preserve existing shared fracture/triplanar coordinates. Adding independent per-piece noise would expose seams and fight the low-poly forms.
- [Roystan, Toon Shader](https://roystan.net/articles/toon-shader/): separate directional form lighting, ambient fill and restrained lit-side highlights. Apply the principle to current URP shading rather than copying Built-in pipeline code. Existing arena settings (facet contrast .12, shadow floor .66, ambient .76) flatten form; the baseline correction is .30/.48/.64, with low macro detail preserved.
- [Unity SSAO configuration](https://docs.unity3d.com/6000.0/Manual/urp/ssao-renderer-feature-reference.html): use bounded contact scale and downsampling rather than maximum intensity. Existing SSAO was already active at radius .065m/full resolution/12 samples. Trial: .22m, intensity .65, half resolution, 8 samples, existing DepthNormals and bilateral blur. No GPU speedup is claimed without measurement.

## Observed defects and chosen changes

The per-life result label was outside the match-result hierarchy and received the readable numeric font override. It now uses the display font, a cropped existing gold button for a win and the detailed dark button for loss/draw. Returning uses the same dark artwork in a compact 260x54 block. No external artwork is generated.

Cloud geometry was rendered after the seismic fullscreen composition. Each cloud shader now fades by the same seismic blend, including correct premultiplied fading for volume clouds. Production shader contribution is checked with post-processing disabled to exclude film-grain noise; normal visual captures retain post-processing.

Far-scene chromatic edges now use opposite luminance differences, so orange sandstone does not suppress the weak blue/green source channels. Violet and green contributions are bounded to 3 reference pixels and distant geometry. Halftone spacing varies slowly between 2.8 and 4.4 reference pixels in surface space.

Ambient ground wind and impact dust have different roles. The rejected procedural ground-mask experiment was removed. Ground drift retains the earlier shape/motion, with smaller warm low-opacity wisps. All LightDustMote users receive a celestial night exposure multiplier to prevent the deliberately bright gameplay fill from creating white night dust.

The supplied user analysis requires contact burst, main dust body, residual suspension, and inertial chips. These are presentation layers fed by existing typed cues. The implementation must be reviewed in motion and under both light and dark conditions before acceptance.

Actual follow-up captures rejected per-particle red rings in the majority flame mass. The final body uses height/heat-based orange-to-core color; outer tongues retain red bands. The controlled CPU profile uses fewer, larger parcels (220/s, width .45–.9) and still redirects existing particles on finite obstacle contacts. The native particle renderer rejected eight chip meshes (maximum four), so the final library deliberately uses four differing silhouettes rather than retaining a warning or claiming nonexistent variety.

Rock inspection also found that the computed diffuse response only weakly affected palette tint, and twilight fill followed SSAO, overwriting its contact shading. The optional broad form multiplier and pre-AO hemispheric fill address these concrete causes without new texture noise or fracture-coordinate changes. [Unity's custom lighting components](https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.shadergraph/Documentation~/Shader-Graph-Sample-Custom-Lighting-Components.md) provide the wrapped-lighting reference; source APIs are not copied across pipelines.

## Gates

Required: shader compilation; actual round outcomes and respawn; full sonar zero cloud contribution; sunset fog/cloud capture; cloud motion and reduced-motion behavior; god-ray on/off and night-off comparisons; impact dust/chip collision capture; fire at gameplay distance; targeted physics/animation regressions. Record exact evidence in the follow-up completion report. Performance remains scoped to measured markers, not inferred from fewer samples.
