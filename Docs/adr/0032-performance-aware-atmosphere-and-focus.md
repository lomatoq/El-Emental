# ADR 0032 — Performance-aware atmosphere and focus

Status: accepted for M11 look development

## Context

The radius-36 arena reads too flat at gameplay distance. Legacy exponential fog,
warm ambient fill and the atmosphere overlay all affected the same pixels, while
continuous cinematic depth of field would make targets, gestures and the HUD harder
to parse. Full volumetric fog or VFX Graph dust would also spend GPU/CPU budget on a
scene whose readable key light is a single directional sun.

## Decision

- Keep URP Forward and one persistent directional light. No fill/rim light is added.
- Make the existing depth-texture atmosphere RenderGraph pass the sole fog authority.
  It performs one color blit and combines Beer-style transmittance with bounded
  Rayleigh/Mie in-scatter; legacy `RenderSettings.fog` stays disabled.
- Drive depth of field from the pure `EarthVisualClaritySolver`. Ordinary Explore
  and locomotion remain sharp on every capability tier. Deliberate charge or a
  semantic focus state may request the project-local dual-subject Bokeh lens on
  NativeHigh or bounded Gaussian on NativeLow; WebLab remains sharp. Airborne,
  impact and recovery cut either native lens immediately. Hysteresis prevents mode
  chatter; Gaussian high-quality sampling stays disabled.
- Keep bloom and vignette restrained and state-aware. Motion blur, chromatic
  aberration, film grain and SSAO remain absent because they do not improve input or
  silhouette comprehension in this slice.
- Add a two-sample, slowly scrolling cloud cue inside the existing atmosphere
  shader. It is evaluated only for sky pixels, adds no renderer or full-screen pass,
  and is profile-bounded by coverage and opacity.
- Add one camera-local built-in ParticleSystem capped at 64/32/0 motes for
  NativeHigh/NativeLow/WebLab. It has no noise, collision, trails or particle lights.
  Its procedural billboard shader samples the main-light shadow, so motes emerge in
  the brighter sunlit regions without a light-volume simulation or texture fetch.
- Profile the controller under `Elemental.Presentation.Clarity`.

## Acceptance and fallback

- Ordinary grounded traversal renders with DOF Off. Deliberate semantic focus uses
  the one custom dual-subject Bokeh owner on NativeHigh or bounded Gaussian on
  NativeLow. Unsafe motion and WebLab always render with DOF Off.
- Motes use one renderer, stay at or below 64 particles, and have all expensive
  ParticleSystem modules disabled.
- The atmosphere remains one RenderGraph color pass and requests only the existing
  camera depth texture.
- Target budgets are `<= 0.05 ms` CPU P95 for the clarity controller and
  `<= 1.5 ms` GPU P95 for atmosphere plus motes at 1920×1080 NativeHigh.
- If GPU evidence exceeds the target, first reduce aerial quality by moving the
  atmosphere to half resolution; then remove motes on NativeLow. Do not weaken
  silhouettes, gameplay light ownership or canonical simulation rules.

## Consequences

Depth separation now comes from distance/height extinction instead of extra lights.
The cinematic lens is reserved for deliberate focus, while traversal and unsafe
motion remain sharp. Capability degradation is explicit. The tradeoff is that
atmosphere and clouds are artistic screen-space approximations rather than
physically integrated volumetrics.

## NativeHigh dual-subject refinement — 2026-08-31

NativeHigh Bokeh is owned by the project-local
`EarthCinematicDepthOfFieldFeature`, a URP 17 RenderGraph pass. Stock URP depth of
field is forced Off while this feature is active. It is enabled only during
deliberate charge or semantic focus; NativeLow retains Gaussian for those same
states and WebLab remains Off. The pass binds the populated active depth attachment, builds a
signed CoC at half resolution, gathers near and far layers independently and
composites dilated foreground last.

Focus is an interval rather than one plane. The player and rival supply camera-eye
depths; `EarthCinematicDepthOfFieldController` expands a padded sharp envelope
immediately to contain both silhouettes and contracts it at a bounded rate to
avoid pumping when they cross or briefly occlude one another. Unsafe camera states
still cut the effect immediately. Its gameplay blur radius remains bounded by the
controller's seven-pixel NativeHigh ceiling. The pure solver and capture report use schema
`cinematic-dof-dual-subject-v1` and must prove both subject depths remain inside
the sharp interval while foreground and background samples retain opposite signed
CoC.
