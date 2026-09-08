# Dust flow and readable menu authoring

**Superseded group feature:** the user subsequently reported jumping coordinates and requested rollback. The `Buttons` wrapper, dedicated group controls and profile entries were removed. Buttons are again direct children of their original page, with the staircase preserved. The group-scaling implementation and evidence below are historical, not current acceptance. Readable labels and dust improvements remain.

Working tree: main `1235579`, uncommitted September 8 follow-up. Builds on the existing Wallcoeur dust and menu profiles; does not replace unrelated scene, animation or fog work.

## Dust

The burst material now samples the actual fire atlas `Assets/VFXPACK_FIRE_WALLCOEUR/Texture/a_VFX_flame.png` as monochrome coverage, tinted neutral sand. Premultiplied alpha and white mask RGB remain intact, avoiding the previous dark filtering rim. The material blends a small amount of the original soft sprite; contact events still allocate 40% of their existing budget to the original soft emitter.

Sheet speed is a material control. Burst playback reaches the final frame at half lifetime; atlas contact lifetimes are limited to 0.55–1.1 seconds. The final image holds rather than wrapping to the opening frame. Soft residual dust retains its longer life. The separate ground material uses the same atlas with 40% original soft coverage and 1.6× sheet speed, with particle lives of 1.05–1.55 seconds.

Night shading previously multiplied already weak ambient/key radiance by 0.08. It now uses 0.32 at full night and a restrained cool ambient floor, controlled by `Night Ambient Visibility`. Density/alpha stays independent from light. No emissive fire color is transferred into dust.

Final burst and ground materials use `Night Ambient Visibility = 0.16` after production review; other materials retain the shader default 0.09.

The existing single ground emitter now moves at 1.45 m/s, with stronger 4.5-second travelling gusts and a compact tangential curl around the settled stone that emitted each wake. Curl strength falls smoothly to zero outside 3 metres. Existing density-weighted stone selection and gap emission remain, with a 192-particle maximum and Reduced Motion disabling curls. No per-particle rigidbodies or additional steady-state allocations were introduced.

Final visual tuning uses 1.4–3.2 metre cards at 0.48 opacity. The curl center sits just behind the obstacle edge, rather than inside a large collider where its influence would already have faded before reaching the visible wisp.

## Menu authoring

Inspector entries have readable Russian names and editable personal labels. Internal binding paths remain stable and hidden behind a technical-path toggle. Main, Settings, Host, Join and Pause each have a real `Buttons` transform plus a saved profile entry. The top Inspector section scales, moves and rotates the entire button group, including spacing and hit targets, without changing the logo or elemental card. Per-button suffix bindings preserve previous saved overrides.

Scaling uses the group's top-center pivot. `Root` still controls all page content when that is wanted. Settings sliders remain page content; the Buttons control scales only its action buttons.

The user explicitly requires the original sidebar staircase. Restored authored `x = y / 3` for reference buttons (30 px additional horizontal offset per 90 px row). Group scaling preserves this arrangement. The prior menu report's aligned-row choice is superseded, not accepted art direction. Production tests assert the 30 px increments and capture the group at 70% scale.

## Evidence

Focused reports: `BuildReports/DustFlowFollowupEdit.json` and `DustFlowFollowupPlay.json`. Actual ground rendering/motion/CPU: `BuildReports/SurfaceWindDust`. Dust compositing, daylight and full-night shader tests: `BuildReports/DustCompositing`. Menu captures include `BuildReports/MenuLayouts/Buttons-group-scale-70-percent.png`.

Native Graphics Ring Buffer warnings during focused Edit launches remain a known Editor limitation. CPU wind measurements cover the whole adapter in Editor, not isolated GPU rendering cost.

Edit **14/14**, 0.2076039 s at `2026-09-08T09:24:31.6821610Z`; combined production **6/6**, 43.3448354 s at `2026-09-08T09:26:22.8943949Z`; final restored staircase/group scaling **4/4**, 26.847578 s at `2026-09-08T09:28:16.8596466Z`. Full-night compositing difference from empty background: 0.00815; day/night difference: 0.5211. These are isolated URP material measurements, not full-game contrast or GPU acceptance.

Final ground visual pass **1/1**, 24.4374415 s at `2026-09-08T09:29:51.2262406Z`: 71 live particles; 343 stone births / 95 open-ground births; 202 gap births; 25 matched moving samples travelled a mean 1.498 m over 0.8 seconds. Rendered on/off difference: 8142 pixels at 1280×800 (the earlier smaller pass changed 4186). Adapter mean 0.234 ms, peak 0.8871 ms in Editor. Particle maximum remains 192.

Additional real-camera day/dusk/night capture: `BuildReports/DustProductionVisualQa/20260908-093403-309`, status Captured, sameParticleLayout=true, restored=true. The sidebar covers part of these screenshots, so they do not prove unobstructed whole-effect appearance. An attempted follow-up during combat was rejected by line-of-sight validation because moving rubble occupied the sample point; it restored the runtime state and is not counted as acceptance. The QA helper now tries alternate viewing angles and samples farther from the arch, but deliberately still refuses an obstructed shot.

Final night material regression **5/5**, 0 failed/skipped, 0.8130053 s at `2026-09-08T09:36:16.2637992Z` (`StoneContactCompositingPlay.json`). Final full-night center RGB is approximately (0.086, 0.086, 0.080), with visible dust/background difference 0.0422. This supersedes the earlier 0.00815 difference before the final visibility adjustment.
