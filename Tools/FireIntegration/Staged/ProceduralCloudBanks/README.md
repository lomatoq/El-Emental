# Complementary procedural cloud candidate

Existing non-image implementation is `Assets/Elemental/Content/Shaders/ValleyCloudStrata.shader`, driven by `ValleyCloudStrata.cs` and `ValleyCloudStrataSetup.cs`. Its old 7 km volume is disabled in the reviewed scene. It uses a noise texture and random per-pixel ray-step jitter; enabling it unchanged can reintroduce the rejected grain. Do not reinstall that legacy layer.

This candidate adds six separate volumetric banks. Density is four connected smooth analytic ellipsoids per bank, fixed 12 midpoint integration samples per intersecting pixel, no bitmap, noise texture, pixel jitter, new render texture or fullscreen pass. It shares the existing post-fog cloud render list and does not modify image cloud objects/materials/shaders. Perspective and orthographic depth stop rays against opaque geometry. A height attenuation avoids glowing through the lower fog. Bank colour is cool-base/white-blue-top with day/night tint.

Install after root import and shader compile: `Elemental/Graphics/Install Complementary Procedural Cloud Banks`. Run twice to verify six children remain. Owned root is `Valley Procedural Cloud Banks` under the authored V2 frame. Toggle that root off to A/B or roll back while keeping all image clouds. No scene save by installer.

Motion is bounded 18 m lateral / 3 m vertical, using unscaled time, paused by Reduced Motion and atmosphere cloud animation toggles. No steady-state managed allocations; marker `ProceduralCloudBanks.Update`.

Budget: six cube renderers, 12 samples × 4 analytic lobes per covered pixel, no nested light ray march. It is an art candidate, not measured GPU acceptance. Six overlap-heavy banks could be expensive; compare fixed-camera 1080 day/night on/off and GPU timing. Fixed samples avoid grain but shallow intersections may show low-sample contouring; inspect orbit and lookdown. The legacy image clouds remain primary detailed silhouettes. Shader is deliberately sculpted and soft, not high-frequency photoreal noise.

Root must validate Unity shader import, main/combat/reverse/lookdown day/night images, controls, six-bank idempotence, and profiler timings before accepting. C# offline compilation is separate from GPU shader validation.
