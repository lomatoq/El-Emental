# Fire presentation lane

Status: staged outside Assets. No Unity launch/import/graph execution occurred in this lane.

Copy the Assets mirror into project Assets only after the coordinator owns the Unity refresh window. RuntimeLane must be imported with this lane. No asmdef edits are required by the offline compiler.

## Generated assets and execution

1. Import staged RuntimeLane + PresentationLane sources.
2. Execute `Elemental/Fire/Build Graphs And Profile` (`Elemental.Authoring.Editor.Fire.FireGraphBuilder.Build`).
3. Execute `Elemental/Fire/Create Isolated FireLab` (`FireLabSetup.Create`).
4. Execute `Elemental/QA/Fire Lab Presentation Play` (`Elemental.Tests.EditMode.FireLabTestLauncher.RunPlay`).
5. Inspect captures in `Logs/FireLab/Captures` and exact Unity Console/import/test logs. Actual old-particle tracking still needs GPU visual review; alive count does not prove redirection by itself.
6. Optional coordinator-only standalone build: `Elemental/Fire/Build Standalone FireLab` (sole FireLab scene; no shipping scene changes).

The graph generator invokes installed Unity 17.5 internal model APIs via fail-loud reflection, because those types are internal. Shader Graph is authored as real GraphData with URP transparent unlit VFX-compatible target, file CustomFunction with fragment-only slots, and graph properties. VFX is authored by package CreateNewAsset/GetOrCreateGraph, World-space particle data, persistent custom attributes, HLSL Init/Update, native DeltaTime, manual position integration, bounds and modern VFXComposedParticleOutput. Serialization is exclusively package MultiJson/WriteAssetWithSubAssets. Reflection calls were researched against this exact Library/PackageCache but remain unexecuted. Runtime API mismatches are expected to require a small Unity iteration; do not call graphs compiled until that iteration passes.

Build preserves existing graph assets/profile edits. If a first failed generation left a partial generated graph, inspect its error, then delete ONLY that newly generated SG_FireFlame.shadergraph or VFX_FireGroup.vfx via AssetDatabase and rerun Build. Never patch graph YAML/JSON by hand.

## Runtime contract

FirePresentationController.Configure(FireVisualProfile, Camera, FireLightPool); Publish(FirePresentationSnapshot); Retire(). Presentation uploads bounded 6-node/8-patch float4 row buffers. Snapshot points are world-space; GPU points are group-origin-relative. Moving contact start point subtracts SurfaceVelocity * step delta. Manual VisualEffect.Simulate follows snapshot Time and effect.pause=true, keeping aging/spawning/shape in one scaled clock. Reinit only occurs at group birth and explicit retirement, not impact. A new generation cannot overwrite a live old group. World snapshot lifetime envelope drives bounds.

FireVisualProfile is in Presentation to avoid Authoring/Presentation dependency cycles. Its lifetime/speed settings are copied to FireWorldSettings in FireLab. FireLightPool explicitly owns at most two unshadowed lights. Distance LOD reduces emission without dropping fields/contacts; EightGroups lab forcibly selects High emission for all 8 groups.

FireLabDriver public API: SetScenario(Direct/Oblique/Corner/Edge/Opening/Moving/Invalidation/EightGroups), SetBloom(bool), SetTimeScale(float), StopEmission(). Driver constructs only isolated lab children and publishes in LateUpdate. No Earth scene/input/UI wiring. Runtime-owned BoxCollider surface bindings provide stable lab identity. Invalidation removes the wall at 3 seconds.

## Evidence and remaining gates

`python Tools/FireIntegration/compile_staged.py --assemblies Elemental.Presentation Elemental.Authoring.Editor Elemental.Tests.EditMode Elemental.Tests.PlayMode` passed all four offline Roslyn compiles. Presentation/Test assemblies emitted no warnings; Authoring has existing unrelated obsolete FindObject warnings. This proves C# syntax/reference compatibility, not Unity graph import, HLSL compilation or GPU behavior.

3 Play tests staged: live particles+contacts+pause/slow clock; direct/oblique/corner/moving captures with Bloom on/off + geometry invalidation; drain retirement. Tests not executed here. Do not claim captures exist until actual run. No standalone CPU/GPU timing was measured. Direct3D/Metal checks not executed. Shader Graph native output currently lacks the soft-depth/near-camera fade included in the standalone FirePreview shader. Native profile graph artistic defaults use the provided 0.9 opacity / 3 HDR boost; native shape parameter editing can be expanded after import proof. No secondary backend implemented: unsupported native compute/SSBO/Linear conditions visibly reject this backend.

## Actual Unity follow-up 2026-09-07 09:07 local editor log time

Supersedes the earlier unexecuted graph status above. Coordinator granted this lane the editor refresh window. Real Shader Graph, VFX Graph, Fire_Default and FireLab.unity were generated/saved successfully via installed package APIs. Fixed GraphData.AddContexts missing setup, Internal.UVChannel type namespace, HLSL reserved `point`, duplicate loop variable warnings, positive pow bases, and consistent include line endings. `Elemental/Fire/Refresh Graph HLSL Sources` now updates the real graph's embedded Init/Update through its model API and recompiles. Current D3D11 shader compilation emits no Fire errors after refresh; structural exposed-property validator passes.

Native Play remains blocked by this checkout's actual Gamma (`ProjectSettings.asset m_ActiveColorSpace: 0`). Installed VFX 17.5 package Documentation~/System-Requirements.md line19 explicitly excludes URP Gamma. Global color settings were preserved. Authoring validation now separates graph structure from platform capability and emits an explicit warning. Runtime still rejects unsupported native backend; it caches the failure, avoiding per-frame retry allocations. No Play or captures claimed. A coordinator decision is required between isolated Linear validation or a Gamma-compatible cosmetic fallback.

## Gamma fallback complete and executed (2026-09-07 07:36 UTC)

Coordinator authorized a Gamma-compatible CPU cosmetic backend rather than changing project color space. This supersedes the previous Gamma blocker.

- FireVisualProfile.Backend = Automatic / GpuVfx / CpuMesh. Automatic chooses CPU mesh for this Gamma checkout. Explicit GpuVfx keeps strict compute/SSBO/Linear validation. Native .vfx/.shadergraph remain actual saved assets for future Linear use.
- FireCpuMeshBackend: exactly one mesh/renderer per admitted group, 256 or 512 slots, persistent arrays, immutable particle ID/phase/heat/width/aspect, world-space positions, scaled age, finite contact steering and shared FireContactMath.ResolveSwept. Existing particles are redirected in place. Mesh sorting and camera-facing velocity-oriented quads affect rendering only.
- Heavy particle stepping compiles through existing Burst to a synchronous function pointer; pointers are pinned only for each synchronous call. No jobs, callbacks after return, or ownership races. Bounded frame counts are validated before unsafe access.
- CPU material uses FireCpuMesh.shader + FireFlame.hlsl: procedural 3-zone HDR color, straight alpha, optional depth fade, near fade. Profile gives editable flame width/aspect; actual capture review widened and oriented flames to read as a flow rather than identical upright candles.
- FireLabShader surface material is a real serialized scene reference. This fixed an actual first standalone failure caused by Unity stripping a shader only located with Shader.Find at runtime. Failure log preserved as BuildReports/FireLabStandalone-first-stripping-failure.log.

Actual PlayMode report: BuildReports/FireLabPresentationPlay.json / .xml, 4/4 passed, 07:33:04Z, 12.471s. Tests verify live particles, existing-particle redirection with positive preserved age, exact paused clock after current-frame flush, 0.1x time, two separate corner contacts, direct/oblique/corner/moving captures with Bloom on/off, destruction invalidation, graceful drain, and no steady-state managed allocations. Captures are present in Logs/FireLab/Captures and were visually inspected.

Editor microbenchmark: Logs/FireLab/cpu-mesh-editor-sample.json. Ryzen7 5700G / RTX4070 D3D11, 512 slots / 206 alive, 128 warmed steps, 0 B managed; p50 0.1962ms / p95 0.2075ms. Before Burst the same sample was p95 1.6056ms. These are measured editor cosmetic-backend steps, not the whole FireWorld or GPU budget.

Standalone: Builds/FireLab/FireLab.exe generated from FireLab only; no shipping scene mutation. Use `--fire-benchmark --fire-report="<absolute report path>" -force-d3d11 -screen-width 1920 -screen-height 1080 -screen-fullscreen 0`. Application writes report and exits after warm-up/baseline/8High windows. All own benchmark player processes exited.

True1080 report: BuildReports/FireLabStandalone1080.json, 07:36:04Z, Unity6000.5.7f1, D3D11/Gamma, 1920x1080, 8 High groups, 360 measured frames, 1538–1611 alive. All 8 cosmetic mesh backends CPU p50 1.6273ms / p95 1.8037ms; whole-frame managed GC p95/max 0 B; baseline/high frame p95 about8.34ms with 120 target. GPU timings were unavailable (0/360 valid), NOT zero GPU cost.

Earlier real desktop-resolution report BuildReports/FireLabStandalone.json was 3440x1440 despite runtime resolution request; it is preserved without relabeling as1080. It had 360 valid GPU samples: total-frame GPU p95 baseline3.75296ms / high3.770368ms. That includes Bloom/scene/presentation and does not isolate Fire GPU work. CLI width/height/fullscreen flags produced the verified1080 second run.

Acceptance limits: CPU fallback exceeds the proposed native all-Fire CPU0.8ms target; do not claim that performance gate passed. Standalone GPU1080 gate unavailable. Native Linear Play/Metal not executed. Sparse finite box probes still have documented geometric limits. Fire remains an isolated infrastructure/lab with no new gameplay damage/input or shipping-arena wiring. Root owns production integration/remaining visual decisions.


## Executed integration — 2026-09-07 08:25 UTC

Both stage baseline comparisons passed before integration. Final actual source and PresentationLane mirrors now agree. CPU optimization additionally compiles the birth-velocity field sample through Burst, preserving the same algorithm and random sequence. Visual refinement moderated width to 0.30–0.58, aspect 1.3–2.1 and age expansion 0.85–1.12; squared coverage and cooling suppress broad pale remnants. The earlier staging-only notes above are historical.

Actual Unity final PlayMode report BuildReports/FireLabPresentationPlay.json: 4/4 passed at 08:23:10Z, 12.445s. Contact redirection/positive preserved age, corner faces, pause/time scaling, moving/destruction invalidation, drain and zero allocation passed. Final editor sample p95 0.0867ms per group, 206 alive, 1103 redirected, 0 allocated bytes.

Final standalone BuildReports/FireLabFinal1080.json at 08:25:11Z: 1920x1080 D3D11 Gamma, eight High groups, 360 samples, 1538–1611 live particles. Summed cosmetic backend CPU p50 0.8397ms / p95 0.9711ms (old p95 1.8037ms). Whole-frame managed GC p95/max 0. GPU timings unavailable (0 valid samples), not zero cost. CPU stopwatch includes backend mesh work but excludes FireWorld/adapter; full Fire CPU budget remains unproven and even the cosmetic subtotal exceeds 0.8ms. No group/count reduction was used and the gate is NOT passed.

Actual final captures reviewed Direct-bloom and Corner-no-bloom: less dominant white/yellow cores, warmer orange and softer overlapping body. Direct still exposes separate pale translucent tail tongues and recognizable parcel silhouettes; these remain an art limitation, not a production-beauty pass. Existing wall materials are plain QA surfaces. No gameplay-context visual approval claimed. Tests and build preserved the prior scene; no global color/Bloom settings changed.

Bounded work ends here to return exclusive Unity to parent. Source frozen. Do not rerun benchmarks or call Unity concurrently with the next owner.
