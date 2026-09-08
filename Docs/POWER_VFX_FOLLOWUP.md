# Capture, extraction and charge presentation

Source checkpoint: 2026-09-06, uncommitted main `1235579`. Targeted installation and scene/profile save completed. Edit 3/3, particle Play 1/1 and production visual Play 1/1 (22:18:37 UTC) passed. Production proof includes routed charge/release, actual decor gravity grab, three released arena pieces, day shafts and zero night pixel difference. Capture temporarily disables and restores output dithering so unrelated quantization noise does not contaminate same-frame comparisons.

## Targeted installation and settings

Run **Elemental > VFX > Apply Capture Dust and Sunlight (Preserve Scene)** in the saved EarthCoreSlice, outside Play Mode. This saves only existing effects tuning, atmosphere material and explicit gravity-feedback hub bindings; it never regenerates arena geometry, assigns animations or edits UI assets.

- **Elemental > VFX > Edit Capture Dust and Chips** selects EarthEffectsTuningProfile. Material Events contains Extract (256 dust / 96 chips), Fracture (144 / 64), Extraction Surface Contact (22 / 12). Existing per-event intensity still scales these counts. A shared frame budget of 512 dust / 192 chips coalesces or drops excess work. Broad smoke is .32–1.25 m, 1.05–2.1 s with 2000 live particles; shared cosmetic mesh chips cap at 768, .045–.24 m, .65–1.35 s. No new physical debris bodies are created.
- Gravity feedback has **Capture Cloud Interval** (.32 seconds) and **Capture Cloud Strength** (.75). Capture/disassembly produces the same budgeted material cue at its actual focus; repair retains its existing seating feedback. It stops emission when inactive or paused. Existing rings/motes/materials remain.
- On the gameplay camera, **Earth Charge Camera Lookdev V2 > Charge Tension** exposes Maximum Charge Vignette (.43), Charge Vignette Pulse Depth (.10), Pulse Hz (.85). Existing charge channels, FOV and aberration remain the only source. The dark envelope clears on release, death/round reset and disable. Reduced Motion keeps a stable dark envelope without breathing. Pulse uses world time, so pause freezes it.
- **Elemental > VFX > Edit Sunlight Dust Shafts** selects AtmosphereFullscreen. Intensity .18, Distance 22 m, Width 3.5 m, Height 9 m and warm Tint are editable independently. Set Intensity to zero to remove only shafts.

## Rendering and performance boundary

The existing atmosphere pass integrates four fixed air samples along the view ray, limited by the nearest opaque depth. Slowly moving cloud noise is projected across the sunlight direction to produce elongated world-space dusty beams. It fades with height and daylight; it adds no light, volumetric framework, render target, material instance per frame or extra fullscreen pass. Atmosphere still owns the single color pass; seismic presentation remains composited afterwards.

This is an artistic sunlight haze approximation, as requested. It does **not** trace blocker shadows through the light volume. Opaque camera depth limits the integrated air, but an enclosed room would need a separate light-occlusion solution. The actual arena has one sun and the effect deliberately avoids reintroducing its unstable shadow-map bands. GPU cost of the four additional noise samples needs measurement in the actual viewport; no GPU budget acceptance is claimed by the source implementation.

## Focused verification

- **Elemental > QA > Power VFX Edit**: three pure charge-envelope cases (release/limits, Reduced Motion and finite inputs).
- **Elemental > QA > Power VFX Play**: asset-backed real ParticleSystems receive an extraction and saturated fracture burst; checks visible-layer counts, frame/live caps, retirement and absence of extra rigid bodies. JSON: BuildReports/PowerVfx/particles.json.
- Existing marker `Elemental.Earth.MaterialParticles` measures particle emission/integration, `Elemental.Presentation.Clarity` measures charge update, and `Elemental Atmosphere Fullscreen` is the GPU pass.
- Coordinator visual checks must capture actual gravity grab, arena/decor extraction and full charge/release; compare sunlight on/off with identical gameplay camera, then check night and pause. Particle fixture alone is not visual acceptance of these interactions.

Coordinator inspected full-charge frame02 and arena-extraction frame09: the charge darkens corners while fighters stay visible; extraction has distinct sandstone chips and warm smoke. The decor framing is partially blocked by a foreground column, so it proves the gameplay action/counts but is incomplete visual evidence. Sunlight is subtle dusty haze, not shadow-cast room beams. The 90 held-gravity frames measured material-particle mean/peak 0.102/0.277 ms and clarity 0.015/0.022 ms in Editor; these are named CPU markers, not total frame or GPU timings.

- **Elemental > QA > Power VFX Production Visual Play**: loads saved arena additively, starts bot round through frontend, physically routes Space hold/release, invokes real executor decor grab and arena disassembly, then captures sunlight on/off at identical camera/time and night suppression. Frames and 90-frame named CPU marker metrics: BuildReports/PowerVfx/Production. Temporary material value is restored in finally; no asset save. Gameplay calls are real but gravity input recognition is bypassed by this focused fixture.
