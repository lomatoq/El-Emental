> **Не закрыто:** после этих проверок пользователь подтвердил, что ареол остаётся. Требуется дальнейшая диагностика; опубликованные изменения не считаются окончательным исправлением.

# Dust highlights against the sky

The contact-dust renderer produced a luminous yellow halo over the blue sky. The material's unrestricted outgoing diffuse RGB entered HDR compositing and tonemapping like an emissive source. Native116 isolated the new `Material Contact Dust` renderer: disabling it removed the main halo; disabling ambient motes, the old soft-dust renderer, point lights, Bloom, or post-processing individually did not.

`ElementalDustShared.hlsl` now compresses only diffuse RGB peaks above 0.75 with a smooth, common-channel gain, asymptotically bounded by 1 before premultiplication. This preserves RGB ratios and leaves dim values, opacity, authored lighting, textures and particle counts unchanged. Shared cooling smoke receives the same diffuse bound; hot flame emission shaders remain independent. The approach follows the color-preserving peak-scaling principle illustrated by [Khronos PBR Neutral](https://github.com/KhronosGroup/ToneMapping/blob/main/PBR_Neutral/pbrNeutral.glsl), but uses a separate exponential shoulder, not that complete tone mapper.

## Actual-scene acceptance

`BuildReports/HardPolish/Native118-DustShoulderDuel/results.xml`: **2/2 Play Mode tests passed**, including the saved-production dust source diagnostic and an actual duel recording. The diagnostic freezes time and camera, renders through HDR targets, scopes camera dithering off, and restores every diagnostic override. No particle replacement or different-pose comparison is used.

Evidence in `BuildReports/HardPolish/G05/DustHalo-20260910-141607/`:

- `01-all.png` versus `01-legacy-unbounded.png`: the warm puff remains visible while its yellow glow is reduced.
- `01-all-repeat-control.png`: repeated unchanged rendering has mean absolute RGB difference **0**, below the original 0.001 threshold.
- `02-no-dust.png` and `source-off-1.png`: identify the material contact-dust contribution; **18,572** pixels differ with dust enabled, **17,494** for contact dust alone.
- Positive added luminance falls from **2,105.354** to **323.5964**, ratio **0.1537017**. This is a same-frame composited-image metric, not a measurement of physical radiance.
- The native screen capture and independent review of `BuildReports/Showcase/elemental-duel-20260910-141545/frame0000.jpg`, `frame0035.jpg`, and `frame0045.jpg` show warm dust without the former luminous yellow halo or olive rim.

Two pure tests also pass for exact low-radiance preservation, a continuous monotonic bound, and RGB-ratio preservation; staged C# assemblies compile cleanly.

## Limits and cost

The isolated torch view reproduced excessive yellow glow rather than the strongest green reported by the user; the separate real duel provides the sky-edge visual check. This is acceptance for the captured daytime scenes, not proof for every future light configuration. Low-light behavior is protected mathematically but has not been visually rerun at every night phase after this final change. Dust can still become saturated orange near strong fire by design.

The shader adds one scalar exponential per visible dust pixel, with no new particle resources, draws, or runtime CPU allocations. A GPU cost delta was not separately profiled. A preceding normalized-scattering candidate was rejected after Native117 because it made ordinary dust too dark; it is not the accepted implementation.
