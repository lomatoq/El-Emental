# Rock form lighting — research and staged experiment

Two-file opt-in patch; no live Assets changed. Baseline taken after parent ambient .64 / facet .30 / shadow floor .48 and twilight .18.

## Evidence in this project

- Inspected `BuildReports/VisualPolishFollowup/sunset.png`: near-black playable foreground against a bright saturated sky. Silhouette is clear; overlapping rock planes and player are not.
- RumbleRockLit currently computes wrapped N dot L but uses it only in a weak palette tint. It does not attenuate direct light by diffuse response. With facet contrast .30, a fully unlit normal still gets roughly 76% of main-light tint. This limits broad form separation in daylight.
- Arena material already has only .012 macro noise, .025 texture strength and no BaseMap. More textures will not solve this lighting hierarchy.
- Twilight ambient floor was applied after SSAO. Wherever the floor wins it replaces occluded ambient, removing the very contact contrast the parent added.

## Primary references and decisions

- [Roystan: Toon Shader](https://roystan.net/articles/toon-shader/): light bands driven by N dot L, smooth boundaries, a separate ambient contribution to keep unlit planes readable. Adaptation: one optional broad form multiplier from our existing smooth ramp; no new texture, hard posterization, outline, or normal noise. Keep the rocky geometry authoritative.
- [Unity Graphics custom lighting components](https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.shadergraph/Documentation~/Shader-Graph-Sample-Custom-Lighting-Components.md): exposes wrapped Half Lambert response as a stylized lighting component. Decision: retain existing wrapped response and integrate a bounded value separation instead of importing a new lighting framework.
- [Madhur Arora: Go With The Flow shaders](https://madhurarora.com/projects/rJvmQ5): artist's Unity procedural-rock shader work is a shape/variation reference. It does not establish a transferable production lighting algorithm; no source code imported.
- [Wayne Darby: Multi-Stylized Rock Shader](https://randalldarby3.artstation.com/projects/b5oo3r?album_id=7076166): artist portfolio reference only, not evidence that buying a new shader would fix this scene.

## Patch

1. `_FormLightStrength` defaults to zero (unchanged other materials/characters); arena opts in at .45. Fully unlit main-light contribution gets an additional factor .73, facing-light stays 1. This strengthens planes without adding texture contrast.
2. Twilight fill becomes a hemisphere in the existing planet frame, applied before AO. Arena .24 yields unoccluded top .24, side .178, underside .115, instead of flat .18 everywhere. Actual shaded values also include AO, albedo, exposure and grading.
3. Fracture mapping, normals, shadow receiver branching, stable-side handling, shadow floor and depth-normal policy unchanged. Identical added field position in both material CBuffers.

## Acceptance / risk

No Unity compile or visual acceptance yet. Parent should A/B noon/sunset/night with identical camera, test fracture swap mapping and move camera across shadow cascades. Expect readable top/side/cavity planes and contact darkening without new grain. Turning form strength back to zero isolates ambient change. AO can still crush deep cavities if too strong; if sunset remains dark, adjust exposure/sky-to-foreground hierarchy before adding more material effects. Shader adds scalar ALU only, no samples or loops.
