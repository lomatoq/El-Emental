# Wallcoeur burst-dust integration (staged)

All seven files are in `after/Assets/`. No live files or Unity state changed by this agent.

## Imported source / exact sheet contract

`Assets/VFXPACK_FIRE_WALLCOEUR/Prefab/VFX_Smoke.prefab` → `A_SmokeAlpha_1.mat` → `Texture/A_Smoke_2.png` (texture GUID `296919e0817a9e944873a1f2c15b4a4c`). This is the pack's actual nine-frame stylized smoke, 3072×3072 RGBA with transparent background, visually inspected. It contains broad graphic lobes with rough black outline accents, not photographic smoke or fire recolored as dust.

Prefab settings: Grid/WholeSheet, **3×3**, **start frame 0**, **one cycle**, **Lifetime mode (`timeMode: 0`)**, frame curve **0→0.9999**. The serialized **fps: 30 is inactive** in Lifetime mode. No universal 30 FPS playback is claimed.

Integrated playback retains all nine images and lifetime timing. The normalized endpoint is **8/9** because linear interpolation adds a next-frame sample: this reaches frame eight without blending the terminal frame back into frame zero. Start remains zero, one lifetime cycle. UV slicing stays in Shuriken, not in a global shader clock; each emitted puff evolves independently.

## Runtime / material boundary

- New `WallcoeurEarthDust.mat` (GUID `46021582908d47fa866f6f3f7c0a8e11`) derives from RumbleDustLit and keeps its existing earth tint, nonemissive illumination, opacity and soft-depth settings. Only the atlas and opt-in `_FlipbookBlending=1`, `_FlipbookColumns=3`, `_FlipbookRows=3` differ.
- `LightDustMote.shader` adds current/next UV sampling from `TEXCOORD0.xy/zw` and blend from `TEXCOORD1.x`. Frames blend in premultiplied space, avoiding dark transparent-border cross-fades. No new bitmap or HLSL dependency.
- `ConfigureDustFlipbook` configures the module and streams at setup, invoked by `UseMaterialDustColor` so the existing arena-fracture presenter also receives the animation. An emitter switched back to an ordinary dust material releases atlas UV animation.
- Existing `GetMainLight` shadow attenuation, SH ambient, `.08` night exposure, soft depth and `Blend One OneMinusSrcAlpha` remain. Dust receives real scene shadows; it does not emit light or add billboard shadow casters.
- New material is assigned **only** to profile `FractureDust` and `ImpactDust`. Old `RumbleDustLit`, ambient motes, surf, stone-fade and serialized ground-wind material remain. `EarthSurfaceWindDustSetup` now explicitly takes `SurfDust`, so future installs cannot silently replace the preferred ground flow with an atlas.

## Puff sizing / density

Saved fracture tuning changes size from `.32–1.25` to `.45–1.45 m`, minimum/maximum count from `180–340` to `64–144`, base count `105→48`, per released piece `34→12`. Saved impact dust size `.12–.46→.24–.70 m`, maximum dust `56→24`, per-frame dust cap `85→40`. This trades many small overlapping cards for broad animated clusters. These are initial art-tuning values; no visual acceptance claimed before the parent's production capture.

## Verification ready

`WallcoeurDustIntegrationTests.BurstDustUsesRealNineFrameAtlasButGroundWindKeepsOriginalMaterial` verifies exact imported atlas, independent ground/surf material, runtime grid/lifetime/start/end/cycle, UV2+AnimBlend streams, and clearing atlas mode when reusing the emitter with the old sprite.

Staging script executed and Python syntax checked. Unity compilation, shader compilation, rendered sheet progression, production impact/fracture bursts at day/night and ground-wind regression remain parent-owned. Run existing dust compositing tests too; their material lighting/soft-depth contract stays unchanged. Counts/shape changed intentionally, so old raster images are not an equality reference for the new atlas.

Primary API references checked: [Unity particle vertex stream packing](https://docs.unity3d.com/2018.3/Documentation/Manual/PartSysVertexStreams.html), [animation time modes](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ParticleSystemAnimationTimeMode.html), [UV channel mask](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ParticleSystem.TextureSheetAnimationModule-uvChannelMask.html).
