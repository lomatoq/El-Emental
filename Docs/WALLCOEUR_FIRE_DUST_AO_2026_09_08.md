# Wallcoeur fire, earth dust and contact AO

Working tree: main, base `1235579`, uncommitted. September 8, 2026.

## Applied assets and rendering

User-selected [VFX URP – Fire Package](https://assetstore.unity.com/packages/vfx/particles/fire-explosions/vfx-urp-fire-package-305098), by Cartoon VFX by Wallcoeur, was acquired in the user's Unity account after explicit EULA confirmation and imported into `Assets/VFXPACK_FIRE_WALLCOEUR`. Its actual flame material uses Mobile/Particles/Additive and a 3×3 authored animation; it does not contain a novel volumetric fire solver. The original files remain intact.

Controlled fire and all seven column fires now sample the original `a_VFX_flame.png` through shared `FireAuthoredFlipbook.hlsl`. The existing bounded CPU field/contact backend still owns movement, steering, lifetime and stop/drain. The discarded experimental connected ribbon is disabled in both saved profiles. Animation follows particle age; stable phase only mirrors shapes. A yellow core/orange body, soft depth intersection, shorter visible tails and late contraction reduce detached red fragments. Both passes preserve sonar masking; columns retain their post-atmosphere depth rejection.

Fire_Default: 90 births/s, widths1.25–1.85, aspect1.1–1.45, lifetime.55–.85. Columns:28 births/s, widths1.1–1.65, aspect1.1–1.45, lifetime.6–.9. Column installer reproduces these settings and copies the shared authored flame material parameters.

Impact/fracture dust uses the pack's **unoutlined `A_Smoke.png`** through new `WallcoeurEarthDust.mat`. The first outlined Smoke_2 trial was rejected after the production drop capture showed distracting wire contours. Final tint(.68,.46,.26), alpha.82. Shuriken owns3×3 UVs, one lifecycle, start0 and endpoint8/9, with UV2/AnimBlend interpolation. It never wraps the last frame into the first. Dust receives scene sunlight/shadows and SH, with no emission, preserved night attenuation and depth fade. Fracture density/sizes and impact counts were reduced to support broader animated shapes. Ground wind/surf remain on the original single-sprite material; their installer explicitly takes SurfDust.

## September 8 mixed-dust and heat follow-up

The black-edge atlas RGB correction is now rendered in `BuildReports/StonePhysicalDrop/heavy-2-effects-on.png`: the dark sprite outlines from the previous capture are absent. `EarthMaterialFeedbackPresenter` keeps the original SurfDust single sprite as a separately owned contact layer, splitting the existing event budget (four in ten births) instead of doubling it. It shares the event, gravity/drag integration and disable/clear lifecycle. The atlas supplies the body and residual roles; original ground wind remains intact. A new real-event Play test checks both materials, both live emitters, total birth count and clear-on-disable.

Both fire materials now use core emission1.4 (formerly.2), driving the existing scene bloom. The authored flame atlas has bounded opposing noise deformation driven by the simulation clock. Local Vefects HDRP package source was inspected: its flame uses scrolling noise erosion/distortion; its heat effect uses a separate screen-color distortion layer. The project stays on URP. The new `ElementalFireHeat` pass samples a separate, completed post-atmosphere image with a2px maximum offset and soft coverage. Scene-depth and shifted-depth checks suppress wall leakage; sonar and near-camera fades apply. The extra render-graph color target/pass is skipped when there are no visible CPU fire groups. This adds a full-size color copy while fire is visible; isolated pass GPU cost is not yet measured.

Image cloud banks increased16→28, adding side views, low wisps and high broad banks with widths280–1550 and heights25–610 in the authored valley frame.

Current combined Play report: **12/12 passed,2026-09-08T00:21:14.9880140Z**,58.4374s. This includes actual production atmosphere, contact dust/chips, mixed dust event, fire simulation/pause/drain and AO. All three edited effect shaders report no compiler errors. The initial mixed-dust run10/12 exposed stale tests: role inspection only examined the atlas emitter, and the legacy neutral-sprite regression compared the corrected atlas against the old black-padding behavior. Those tests now inspect both emitters and retain the strict original single-sprite pixel regression; no tolerances were relaxed. Latest CPU fire sample:218 live parcels,128 samples,0B measured allocation,p95.0914ms. Latest whole-frame AO comparison at1280×720:full median5.4702ms,p956.3273ms;half5.2920ms,p955.8010ms. These are editor whole-frame values, not isolated AO/heat timings.

Editor recovered asPID11736. The other animation/fog task finished its exclusive runs and released the editor before the final effects verification. Refreshed Edit23/23 passed00:25:20Z. Final production Play2/2 passed00:26:36.7796063Z,37.3471s. The dedicated same-frame heat-on/off proof reports visible pixel-channel difference93910 and sonar difference0 (`BuildReports/VisualPolishFollowup/fire-heat-comparison.txt`, paired PNGs). Current console error query is empty. The previous native Graphics Ring Buffer warning is not claimed fixed. The failed old DustProductionVisualQa framing remains a QA-helper issue, not a successful production three-phase capture.

## Contact AO and brightness

SSAO runs at full resolution with the existing intensity.95, direct-light share.30 and radius.32m. DepthNormals now reports actual geometry normals rather than forward-lighting artistic radial normals. Additional lights receive the same direct AO factor. Airborne cosmetic chips, which intentionally do not write depth/normals, no longer inherit AO from the terrain behind them. DebugMode6 exposes raw AO for inspection.

The seven rock materials retain ambient.80, shadow floor.66, twilight fill.55 and occlusion strength1. Local contact clarity is not produced by lowering overall material brightness.

## Evidence and limits

Final reports: `BuildReports/VisualResearchEdit.json`, `BuildReports/VisualResearchPlay.json`, `BuildReports/StoneContactCompositingPlay.json`. Production paired renders and raw AO: `BuildReports/VisualPolishFollowup`. Controlled flame contact sequences: `Logs/FireLab/Captures`. Physical dust drop sequences: `BuildReports/StonePhysicalDrop`.

Edit23/23 passed23:53:19 UTC; combined Play11/11 passed23:55:32 UTC; final unoutlined-dust compositing4/4 passed23:52:31 UTC. After the combined run, mask-only RGB was used for animated dust to remove filtering fringes from black transparent atlas padding; single-sprite RGB behavior is unchanged. That shader compiled, but the final three-phase production recapture did not complete. The old capture helper first confused the inactive online rig with the active camera (fixed), then selected a point occluded by Arena_FloorBase_INTACT. This is a capture failure, not evidence of successful framing. During investigation at00:00:41 UTC, editor process7024 entered a window titled `Unity Error`; MCP commands timed out. No unsupported claim about the native error's cause or a successful restart is made. Final visual QA and editor recovery remain pending.

AO GPU comparison uses the actual production camera at1280×720, alternating full/half/full/half,120 valid samples per mode. First measured full median3.6649ms/p954.1636ms versus half3.6721ms/p954.0745ms on RTX4070/D3D11. This is whole-frame GPU timing including editor overhead, not isolated pass cost or a high-resolution player guarantee. The difference is within measurement noise.

Fixed the `Camera` namespace compilation error in FireCoherentBodyMeshBackend and mixed shader line endings. Import bindings are saved and checked against actual Texture2D assets. The editor may still emit its existing Graphics Ring Buffer warning during heavy scene restoration; this is not a shader/compiler error, and no editor restart was forced.

Visual direction is based on the user's chosen package and reviewed rendered frames. Passing functional tests does not establish subjective artistic acceptance or full-game performance acceptance.
