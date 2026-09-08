# Fire CPU optimization candidate — not integrated

Baseline: BuildReports/FireLabStandalone1080.json measured eight High groups p95 CPU 1.8037 ms, above the proposed 0.8 ms target. The baseline is not accepted. No new performance claim is made here.

Only after/Assets/Elemental/Presentation/Fire/FireCpuMeshBackend.cs is a replacement candidate. before/ contains the exact source to compare/revert. Baseline SHA256: 208AC44F1CF26B1FC69B538CB4FAC94A3D6E934A7E1E2DE468DEC0B8B4208030.

Changes:
- Batch all existing-particle integration into one synchronous Burst invocation per group, pinning arrays once. Existing motion, swept contact resolution, age advancement, reverse iteration, removal and redirect telemetry are retained.
- Fill the quad vertex stream and accumulate world bounds in Burst. Unity mesh uploads and object/material API calls stay on the main thread.
- Replace managed recursive sorting with allocation-free bounded iterative heap sorting, far-to-near. Equal-depth ordering can differ; particle motion and IDs do not.
- No reduction in emission, capacity, lifetime, substeps, shader complexity, width or contact count. No global settings or production scene edits.

Offline verification: verify.py compiled the candidate with the current Unity Presentation response file, zero C# errors. Its literal algorithm-level heap translation passed 2,052 cases, sizes 0–512, including duplicates and ascending/descending depths. The actual Assets source matched the before snapshot. This does NOT prove Burst compilation, rendering, runtime behavior, or performance. Unity was not accessed during this task.

Next owner steps, after exclusive Unity handoff:
1. Check actual source still matches before. Apply only the one replacement and refresh Unity. Reject/revert on Burst or C# errors.
2. Run Elemental/QA/Fire Lab Presentation Play. Require 4/4 plus zero console errors; confirm preserved existing-particle redirection/age, corner contacts, pause, destruction invalidation, drain and zero steady-state allocation.
3. Build standalone FireLab again; benchmark with explicit -screen-width 1920 -screen-height 1080 and --fire-benchmark using the same baseline method. Require eight High groups p95 summed backend CPU <=0.8 ms to call the CPU gate passed. Measure GPU separately; unavailable timing is not zero cost.
4. Compare captures Direct/Oblique/Corner/Moving with Bloom on/off and invalidation before/after. Source changes alone cannot establish appearance equivalence.

Visual review of existing captures (including Corner-no-bloom.png): bright white/yellow streak and teardrop units are conspicuously separated, with thin orange rims and repeated silhouettes. The plume has detached upper tongues and some framing loss. Contact steering is readable, but this is a functional fallback, not finished production fire. Reducing emission would likely worsen continuity. A later bounded appearance pass should vary opacity/core intensity and overlap without broadening global bloom or changing gameplay.

If this candidate still misses CPU budget, simplify presentation explicitly in coordination with the parent. A three-High-group ceiling estimates ~0.68 ms from the old eight-group measurement, but is unmeasured and is NOT satisfaction of the eight-High requirement. Do not silently reduce counts or relabel the gate. Prefer measuring phase-specific costs before additional compromises.


## Executed integration — 2026-09-07 08:25 UTC

Both stage baseline comparisons passed before integration. Final actual source and PresentationLane mirrors now agree. CPU optimization additionally compiles the birth-velocity field sample through Burst, preserving the same algorithm and random sequence. Visual refinement moderated width to 0.30–0.58, aspect 1.3–2.1 and age expansion 0.85–1.12; squared coverage and cooling suppress broad pale remnants. The earlier staging-only notes above are historical.

Actual Unity final PlayMode report BuildReports/FireLabPresentationPlay.json: 4/4 passed at 08:23:10Z, 12.445s. Contact redirection/positive preserved age, corner faces, pause/time scaling, moving/destruction invalidation, drain and zero allocation passed. Final editor sample p95 0.0867ms per group, 206 alive, 1103 redirected, 0 allocated bytes.

Final standalone BuildReports/FireLabFinal1080.json at 08:25:11Z: 1920x1080 D3D11 Gamma, eight High groups, 360 samples, 1538–1611 live particles. Summed cosmetic backend CPU p50 0.8397ms / p95 0.9711ms (old p95 1.8037ms). Whole-frame managed GC p95/max 0. GPU timings unavailable (0 valid samples), not zero cost. CPU stopwatch includes backend mesh work but excludes FireWorld/adapter; full Fire CPU budget remains unproven and even the cosmetic subtotal exceeds 0.8ms. No group/count reduction was used and the gate is NOT passed.

Actual final captures reviewed Direct-bloom and Corner-no-bloom: less dominant white/yellow cores, warmer orange and softer overlapping body. Direct still exposes separate pale translucent tail tongues and recognizable parcel silhouettes; these remain an art limitation, not a production-beauty pass. Existing wall materials are plain QA surfaces. No gameplay-context visual approval claimed. Tests and build preserved the prior scene; no global color/Bloom settings changed.

Bounded work ends here to return exclusive Unity to parent. Source frozen. Do not rerun benchmarks or call Unity concurrently with the next owner.
