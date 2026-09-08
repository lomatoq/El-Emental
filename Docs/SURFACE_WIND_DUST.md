# Persistent wind dust along arena surfaces

Source checkpoint 2026-09-07, uncommitted main `1235579`; coordinator owns compile, saved-scene installation and measured acceptance.

**Install:** Elemental > VFX > Install Surface Wind Dust (Preserve Scene). The existing sky lighting anchor and planet are bound explicitly. Existing materials, arena geometry, HUD and animation assignments are preserved. Re-running does not reset the new profile's user edits.

**Tune:** Elemental > VFX > Edit Surface Wind Dust, selecting `Assets/Elemental/Content/Profiles/EarthSurfaceWindDustProfile.asset`.

Defaults: open ground 12 particles/s plus 36 particles/s in settled-stone wakes, 16 m area, 1.45 m/s tangential wind, .55–1.25 m puffs, 2.1–3.6 s lifetime, opacity .32 and centre height .14 m. Maximum 192 live particles (hard ceiling 256). Ground and stone rates, world wind direction, speed, size, lifetime, opacity, hover height and settled-body threshold are independent.

One existing-style ParticleSystem and the existing production dust material render the effect. No new rigid bodies, particle collisions, trails, lights or noise modules. Emission samples actual physics surface normals. Every particle follows its latest support plane; at most four support probes per frame update those planes as particles cross curved/uneven geometry. Unsupported particles retire quickly. Pausing freezes travel and emission. Disabled components clear particles. Flying/moving stones are excluded; nearby settled decor, loose rock debris, arena cells, fragments and wall pieces receive denser wakes. Candidate scanning uses one OverlapSphereNonAlloc with 256 entries every .8 s and caches at most 96 stones; no hierarchy/global search occurs each frame. At most four particles spawn per frame, and all particle/surface buffers are preallocated.

**Verification:** Elemental > QA > Surface Wind Dust Edit (two tangent/fallback tests), then Surface Wind Dust Visual Play (one actual saved arena fixture). Production QA records real sustained emission, higher stone-wake counts, particle displacement, live caps, named CPU samples and camera PNGs, including identical-frame particles on/off subtraction with only final output dithering disabled. Reports: BuildReports/SurfaceWindDust. Marker: Elemental.VFX.SurfaceWindDust. Asset-backed tests and rendered review must pass before this source checkpoint is called accepted; no GPU result is claimed.

Production acceptance: Play 1/1 passed at 2026-09-07 01:55:46 UTC. Measured 110 live particles, 94 ground / 264 stone-wake emissions, 1.169 m mean movement over 0.8 s, 7,754 changed pixels in the same-frame on/off comparison. CPU marker mean 0.126 ms, peak 0.476 ms; no GPU claim. Saved arena wall captures use the baked production pool and actual plucked geometry; foreground stones/dust partly occlude the detached piece.
