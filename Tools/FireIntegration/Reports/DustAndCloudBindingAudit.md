# Prepared dust and clouds: actual binding audit

Read-only source/scene/art audit, 2026-09-07. No Assets edits or Unity calls. No new dust implementation needed to repair a missing binding was found.

## Already integrated: stone impact dust

The actual presenter is `Assets/Elemental/Presentation/VFX/EarthMaterialFeedbackPresenter.cs` (there is no separate EarthStoneImpactPresentation class). Its current bytes exactly match `Tools/FireIntegration/Staged/StoneQa/validated/Assets/Elemental/Presentation/VFX/EarthMaterialFeedbackPresenter.cs`.

Saved EarthCoreSlice scene lines133925–133935 bind the enabled presenter to the real hub, EarthEffectsTuningProfile, planet center, impact dust, chips and broad fracture dust systems. The route is:

`EarthDestructibleDecorRock.OnCollision…` / `EarthFragmentPool` / `EarthRockDebrisPool` → `EarthMaterialFeedbackHub.EmitStoneImpact` → `EarthStoneImpactDust.Strength` and cooldown → `EarthMaterialFeedbackPresenter.Handle` → native ParticleSystem emission.

This is real collision/mass input, not a test-only direct Emit shortcut. Current behavior includes deterministic cue seeds, fine-biased sizes, two uneven dust lobes, mostly low contact particles with24% slower lofted puffs, particle center lifted by half its size and a **contact-renderer-only** short soft-depth fade. Broad fracture/extraction dust keeps its separate renderer and authored material settings.

Material binding is complete:

- `Content/Profiles/EarthEffectsTuningProfile.asset`: impactDust and fractureDust point to GUID `cc90a2dce6ef7d747955d2b1c251e5b2`.
- That is `Content/GraphicsV5/Materials/RumbleDustLit.mat`.
- Its BaseMap points to `RumbleDustSoft.asset`, GUID `b17e40cdf5d01df408ddfdc477469adf`, a64×64 Texture2D.
- Impact profile is enabled, size0.12–0.46m, lifetime0.35–0.95s; broad profile size0.32–1.25m. There is no unbound new impact-dust sprite waiting in the Fire integration staging inventory.

## Physical-drop evidence and limit

Viewed `BuildReports/StonePhysicalDrop/heavy-3-dust-only.png`: a visible soft gray/brown puff extends above and behind the stone. It is not only chips. `evidence.json` is dated2026-09-07T08:12:22Z and passed the real4cm clearance drop:

| Case | Canonical mass | Closing speed | Dust/chips | Settled repeat impacts |
|---|---:|---:|---:|---:|
| light |145.282kg|0.79068m/s|31/8|0|
| heavy |304.601kg|0.79001m/s|40/10|0|

The saved evidence reports zero callback allocations for those two emissions. It is not full-frame/GPU acceptance. Heavy dust-only changed pixels6914 versus light10886 does **not** establish a larger visible cloud: occlusion/framing differs. The current proof establishes real collision emission and readable dust, not final natural-art quality under today's changed atmosphere.

## Prepared but unused UI dust mask

`Reference/StoneUI/EL_Emental_StoneUI/Assets/ElEmentalStoneUI/Art/FX/dust_soft.png` was copied through `Staged/GraphicsUI/after` to actual `Content/UI/Stone/Art/FX/dust_soft.png`.

The source manifest calls it a procedural, independent tintable FX asset. It is256×256 RGBA with247 alpha levels; visually it is a regular white circular mask, not an irregular volumetric smoke atlas. No serialized material/prefab/scene/asset references its actual GUID `8b41475c0ec53f044b657889d3cc5391`, and runtime source has no dust_soft binding.

Do not automatically replace RumbleDustSoft with this UI mask: that would be a new art choice, not recovery of a missing world-dust setup, and its circular shape does not solve the user's natural-variation complaint. The Fire source kit search found no additional dust/cloud shader or sprite contract for Earth impacts.

## Already integrated: surface wind dust

`EarthSurfaceWindDust` is enabled in the saved scene with its real profile/arena/planet/material binding. `Content/Profiles/EarthSurfaceWindDustProfile.asset` has enabledEffect1, maximum192 particles, groundRate12 and stoneRate36. This is the separate existing drifting surface-dust system, not the short impact puff.

Prior actual proof `BuildReports/SurfaceWindDust/evidence.json` (01:55UTC) records110 particles,79 moving samples and mean travel1.1689m. Those old captures are not current atmosphere visual acceptance.

## Already integrated: latest image particle clouds

The prepared source art is `Tools/FireIntegration/Staged/CloudArt/reference-bank-v1.png`; actual texture is `Content/Textures/Clouds/reference-bank-v1.png` (GUID `313ea21355a393c42927c7d2fba6c970`).

Actual saved EarthCoreSlice contains **active** `Valley Image Cloud Particles` (line17388), enabled `ValleyCloudParticles`, and bound `ValleyImageCloudParticles.mat` (GUID `ab7a076eb0d7d5f42bb008a656e118d9`). That material's BaseMap is the generated bank texture. The controller saves `UseParticleClouds: 1` (line68193); source suppresses analytical-bank opacity while particle mode is active. The old `Valley Cloud Strata` object is saved inactive (line231686).

Current code uses16 native particles with deterministic layout/noise, anchored to the atmosphere frame, with pause/ReducedMotion handling. Latest source is beyond the original staged README: actual code has a LateUpdate motion-state gate, so the old README's “no C# Update callbacks” wording is stale. The environment owner is validating its render pass and current frames; this audit only confirms assets and serialized activation, not rendered acceptance.

## Next useful action

1. Keep the current connected impact material/presenter and current image-cloud bindings; no blind re-import of older StoneLane/ValleyCloudLane snapshots.
2. After the environment owner returns Unity, run the existing physical4cm drop capture again with today's atmosphere and inspect dust-only/on/off frames. Also trigger an ordinary gameplay drop to check art in real camera framing.
3. If dust still looks too uniform, identify an actual irregular source/flipbook or author one as a separate art task. No unused suitable new smoke atlas was located here; UI dust_soft is not evidence that one exists.
