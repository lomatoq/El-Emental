# Island continuity and depth-layer candidate

Stage only; actual Assets and Unity not touched. Four existing source files (listed in baseline.json). Preserve exact baseline SHA before import.

Latest reference overrides previous width-preserving shortening: a continuous central mass spans91% of canonical height; closed unequal caps are75–89% of central width with offsets at most10%. No alternating shelf or C-shaped void. Normalized height remains .415–.485, placements and authored scale remain unchanged. Ground Pillar/Group generation source is unchanged.

Import after/ then refresh once. Run `Elemental/Environment/Procedural Valley/6 Refine Floating Cores And Depth Layers` twice in Edit EarthCoreSlice. This updates only Island_6..11_LOD0/1 assets through explicit Mesh setters (avoids old CopySerialized GPU cache issue), appends six upper distant island slots and six Main thin ground slots by stable names, rebuilds the owned backdrop and saves profile/mesh assets. Existing twelve slots, material, motion settings, ground mesh assets, camera, lighting and clouds stay intact. Scene save remains a separate guarded root-owned action after visual acceptance.

Upper islands: three per Main/Combat, depth1500–1930, baseheight430–690, actual heights roughly85–119 (~3–7% vertical angular frame depending lens). Main infill: six ground pillars at655–1350 depth, floor-155, varying scales65–108; existing accepted pillar variants only. Bounds exclusion remains enabled; count rejected placements and inspect actual accepted cards. Absolute authored cap24, total floating cap12, ground cap24. No per-frame renderer allocation added; six extra floating drift entries.

Run `Elemental/QA/Procedural Valley Geometry Edit`. Updated floating test reflects latest explicit full-middle reference, replacing obsolete equal-width-to-original constraint; unchanged other fixtures. Capture actual1920 Main+Combat and compare reference crop; check middle mass continuity, upper-layer visibility, avatar clearance, infill not merging with primary silhouettes. This is a candidate, not yet visually accepted; upper layering and infill positions need actual projection proof. No GPU budget claim.

Root exclusively owns ValleyCloudParticles changes; no cloud file included. Suggested transient far-rock haze for550–1100m landmarks: keep nearClear300, FarHazeDistance120–180, opacity.90; pale cool DayFog around(.78,.88,.98). Evaluate foreground protections and shared top-fog tint before saving.
