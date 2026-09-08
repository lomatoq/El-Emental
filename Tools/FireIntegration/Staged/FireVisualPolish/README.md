# Fire visual polish candidate — staged, unseen in Unity

Separate from FireCpuOptimization. Actual Assets and Unity were not touched.

Changes: CPU-only flame include derived from existing FireFlame reference, softer coverage, weaker moving neck separation, phase-varied opacity and hot-core intensity, cooling from midlife, lower HDR core boost (3 to 0.65). Quads expand from 0.85 to 1.24 with age; profile width increases from 0.24–0.5 to 0.34–0.68 and aspect changes from 1.4–2.3 to 1.3–2.1. These seek overlapping orange body and fewer repeated white teardrops. Native Shader Graph reference remains unchanged. No particle-count, spawn-rate, lifetime, contact, global Bloom or project setting changes.

Three staged files: replacement FireCpuMesh.shader, new FireCpuFlame.hlsl, new authoring FireCpuPolishSetup.cs. Keep existing shader meta when applying. Back up serialized Fire_Default.asset and Fire_CpuMesh.mat before migration. Execute Elemental/Fire/Apply CPU Flame Polish Candidate after imports; this updates only isolated Fire content profile/material, not graph rebuild defaults. Previous tuning: width 0.24/0.5, aspect 1.4/2.3, material _CoreEmission 3 and _Opacity 0.9.

Offline authoring compilation: zero errors; existing unrelated obsolete API warnings. Shader/Burst/runtime/appearance have NOT been validated. No new screenshot exists; do not describe the candidate as visually improved until capture review.

Validation window: apply candidate and performance optimization, refresh, check shader errors, run Fire presentation tests, compare Direct/Oblique/Corner/Moving Bloom on/off plus invalidation/drain. Look specifically for cohesion, edge softness, white-core dominance, detached tail, camera clipping, and translucent gray appearance. Iterate only from captures. The larger quads increase overdraw and must be included in standalone 1080p eight-High CPU/GPU measurements. Do not claim the 0.8 ms CPU gate passed by reducing group count. CPU simulation count remains identical; GPU cost may rise despite unchanged fragment noise count.


## Executed integration — 2026-09-07 08:25 UTC

Both stage baseline comparisons passed before integration. Final actual source and PresentationLane mirrors now agree. CPU optimization additionally compiles the birth-velocity field sample through Burst, preserving the same algorithm and random sequence. Visual refinement moderated width to 0.30–0.58, aspect 1.3–2.1 and age expansion 0.85–1.12; squared coverage and cooling suppress broad pale remnants. The earlier staging-only notes above are historical.

Actual Unity final PlayMode report BuildReports/FireLabPresentationPlay.json: 4/4 passed at 08:23:10Z, 12.445s. Contact redirection/positive preserved age, corner faces, pause/time scaling, moving/destruction invalidation, drain and zero allocation passed. Final editor sample p95 0.0867ms per group, 206 alive, 1103 redirected, 0 allocated bytes.

Final standalone BuildReports/FireLabFinal1080.json at 08:25:11Z: 1920x1080 D3D11 Gamma, eight High groups, 360 samples, 1538–1611 live particles. Summed cosmetic backend CPU p50 0.8397ms / p95 0.9711ms (old p95 1.8037ms). Whole-frame managed GC p95/max 0. GPU timings unavailable (0 valid samples), not zero cost. CPU stopwatch includes backend mesh work but excludes FireWorld/adapter; full Fire CPU budget remains unproven and even the cosmetic subtotal exceeds 0.8ms. No group/count reduction was used and the gate is NOT passed.

Actual final captures reviewed Direct-bloom and Corner-no-bloom: less dominant white/yellow cores, warmer orange and softer overlapping body. Direct still exposes separate pale translucent tail tongues and recognizable parcel silhouettes; these remain an art limitation, not a production-beauty pass. Existing wall materials are plain QA surfaces. No gameplay-context visual approval claimed. Tests and build preserved the prior scene; no global color/Bloom settings changed.

Bounded work ends here to return exclusive Unity to parent. Source frozen. Do not rerun benchmarks or call Unity concurrently with the next owner.
