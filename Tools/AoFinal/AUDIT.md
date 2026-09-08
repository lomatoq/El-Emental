# Final contact AO audit — bounded staged correction

Two changed files only in `after/Assets`: RumbleRockLit.shader and Assets/Settings/ElEmentalRenderer.asset. Exact live inputs copied to `before/Assets`. All seven stone materials and LooseEarthChipVfx inspected, unchanged.

## Existing state verified

- Stone family ambient .80, shadow floor .66, twilight fill .55, material occlusion strength1. Those values remain unchanged.
- SSAO active, BeforeOpaque, DepthNormals source, intensity .95, direct-light share .30, radius .32m, eight samples, bilateral blur. Existing downsample=true was the only changed renderer parameter.
- 00:52 noon AO-off/on captures already show contact shadows. Image measurements (RGB luma, positive darkening, 0–255): open floor mean .328; pillar base2.631; stone contact4.578; whole foreground2.155. This is localized occlusion, not a missing effect. Screenshots precede the new stage and are not validation of it.

## Causes / fixes

1. Half resolution softens narrow contacts. Full resolution preserves the same radius/intensity and sampling method; measure the GPU cost before accepting it.
2. AO depth-normal prepass inherited radial artistic smoothing from forward lighting. Five rock materials enable that smoothing, making contact integration use an invented rounded normal. Prepass now uses geometry normals; forward stylized normals, fracture mapping and shadow-receiver policy are unchanged.
3. Additional point lights bypassed AO; now use exactly the same direct AO factor as the main light. No broad shadow/ambient darkening added.
4. Cosmetic chips are intentionally rendered without depth writes or depth-normal passes. They cannot create SSAO in that design and should not sample the terrain AO behind them. The custom shader lacked URP transparent variants, so the runtime keyword alone was insufficient. Explicit material `_Surface > .5` guard gives these cosmetic surfaces AO1, keeping their light response without painted background occlusion.
5. `_DebugMode=6` outputs raw contact AO in grayscale for production captures. Albedo mode5 remains intact.

## Verification

Static checks passed: two material CBuffers identical; all ambient/shadow/twilight lines unchanged; SSAO .95/.30/.32 retained. Whitespace check only reported an existing line-ending notice. Unity compilation/rendering/performance not run by this agent.

Parent QA: identical camera and frame for AO off/on; raw AO debug view; fullres vs old halfres GPU timings at actual resolution. Compare open floor versus contact ROIs rather than total-image darkness. Foot/stone gaps should retain narrow gradients; open planes should stay light. Inspect airborne cosmetic chips over ground contacts for borrowed dark spots. If fullres costs too much, revert only Downsample to1 and keep the shader correctness fixes. Do not lower ambient/floors to manufacture stronger AO.
