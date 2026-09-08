# Dense dust, clouds and fire — 2026-09-08

Working tree implementation on main, base 1235579; not a clean committed build. User-authored UI layout assets are preserved.

## Research and adopted direction

- [Sucker Punch / PlayStation: Ghost of Tsushima VFX](https://blog.playstation.com/2021/01/12/how-stunning-visual-effects-bring-ghost-of-tsushima-to-life/): coherent wind drives environmental effects, including smoke and sparks. Our ground layer keeps a coherent travelling gust and adds local stone wakes; this does not claim a new unified wind service for every game effect.
- [Simon Trümpler: stylized VFX in RiME](https://simonschreibt.de/gat/stylized-vfx-in-rime/): primary artist's material/talk resources and discussion of shape-preserving alpha erosion. Our change uses softer coverage masks and continuous deformation. It does not reproduce RiME's material or claim inspection of the full video.
- [SideFX flipbook textures](https://www.sidefx.com/docs/houdini/nodes/out/labs--flipbook_textures-1.0.html): motion vectors can smooth interpolation between sparse frames. The imported fire atlas has no matching motion-vector texture; true optical-flow interpolation is not implemented. Alpha crossfade and continuous UV flow reduce popping but cannot reconstruct missing silhouette motion.
- [Riot VFX art education](https://www.riotgames.com/en/artedu/visual-effects): shape, value, color and timing should serve readability. Ground density is biased to settled stone clusters rather than filling the entire view uniformly.

## Implementation

Ground dust: 384-particle authored budget (512 preallocated capacity); open-ground rate 18/s, stone rate 80/s with cluster weighting up to 128/s; lifetime 2.8–4.2 s, size 2–3.5 m. Gap probability 0.8; perimeter sampling surrounds stones, with 35% of stone wisps tilted and raised to cover vertical seams. Existing single-sprite contact dust remains mixed with atlas dust inside the same event budget.

`LightDustMote` interpolates two alpha masks, retaining constant tinted RGB and premultiplied output (`One`, `OneMinusSrcAlpha`). Five normalized samples per frame soften the mask without adding brightness. Both frames share continuous deformation, and sample coordinates remain inside their atlas cell. Ground soft-mask contribution is 0.65, contact contribution 0.55. Material tint is authoritative; the old runtime color override was removed. Night visibility floor remains 0.16 on the two authored atlas materials.

Fire: 7% of CPU births are small warm embers inside the existing bounded particle budget. They use a smooth radial mask and fading lifetime, capped at 1.1 s. Embers do not distort the screen. Existing depth-aware heat haze is increased from 2 to 3 pixels on both production fire materials; existing HDR fire color continues to feed bloom.

Clouds: 22 procedural banks, of which eight are nearer and overhead; 52 image particles including small companions. Procedural density contains 15 asymmetric lobes. Existing continuous motion is retained. Eight soft ellipsoid proxies attenuate sun shafts; this is approximate cloud occlusion of shafts, not exact volume shadows on the arena. The proxy uses the explicit world-to-atmosphere transform and one lookup per pixel's integrated air segment, outside the 16-step shaft loop.

## Validation

See `BuildReports/DenseAtmosphereEdit.json`, `DenseAtmospherePlay.json`, `SurfaceWindDust/evidence.json`, `DustCompositing/Latest.json`, and production menu/fire captures. Current final run results are recorded in PROJECT_TECHNICAL_STATE and PROJECT_EXECUTION_TRACKER after completion. CPU particle update measurements do not establish GPU cost of the increased transparent coverage or cloud ray marching.

UI implementation details and controls: `SIDEBAR_SEQUENCE_2026_09_08.md`, `HUD_PROCEDURAL_MOTION_2026_09_08.md`.

Final evidence: Edit 21/21 at 10:34:37Z; Play 11/11 at 10:37:15Z, 95.0015 s. Ground dust 286 alive / 113988 changed pixels / 431 gap emissions; CPU 1.175 ms mean, 4.2314 ms peak. Night dust RGB (0.0872, 0.0933, 0.0985), visibility delta 0.05117. UI layout-only CPU 0.1871 ms mean. Fire CPU 0 allocated bytes across 128 ticks, p50 0.183 ms / p95 0.5847 ms. Overhead captures in BuildReports/DenseClouds; eight proxies and valid frame matrix confirmed. Final shader scan had no errors, scene nonplaying and clean.
