# ADR 0032 — Performance-aware atmosphere and semantic focus

Date: 2026-08-29; amended 2026-08-31
Status: accepted
Decision owner: solo studio

## Context

Earth Core needs stronger depth, sunlight hierarchy and tactile air without
obscuring combat information or spending the frame budget on general-purpose
volumetrics.

## Decision drivers

1. Explore and dangerous movement must remain immediately readable.
2. The same authored look must degrade predictably across NativeHigh, NativeLow
   and Web capability profiles.
3. Effects must stay bounded and easy to disable independently.

## Constraints

- URP Forward remains the renderer path.
- One persistent directional sun remains the lighting authority.
- Atmosphere must stay a single full-screen pass.
- Dust cannot use lights, collision, trails, noise or motion vectors.

## Options considered

### Option A — Froxel volumetrics and cinematic post stack

- Player impact: richest beams and haze, but unstable silhouettes during motion.
- Technical implications: multiple buffers/passes and much higher bandwidth.
- Production implications: costly tuning for every scene and quality tier.
- Risks: violates the current frame/readability budget.
- Reversibility: high.

### Option B — Depth-aware aerial perspective plus semantic focus

- Player impact: clearer distance hierarchy while action remains sharp.
- Technical implications: one RenderGraph blit, one bounded particle renderer,
  state-aware URP Volume overrides.
- Production implications: small data surface with deterministic tests.
- Risks: subtle atmosphere can need scene-specific tuning.
- Reversibility: high through profile values and component disable seams.

### Option C — Static fog and always-on blur

- Player impact: inexpensive mood, but weak spatial truth and poor combat UX.
- Technical implications: cheapest implementation.
- Production implications: simple but visually generic.
- Risks: double fog, washed silhouettes and input ambiguity.
- Reversibility: high.

## Decision

We will use one depth-aware transmittance/in-scatter atmosphere pass, a two-sample
sky-only cloud cue inside that pass, one bounded mote system and semantic depth of
field. NativeHigh gameplay uses the custom dual-subject Bokeh envelope through
locomotion, airborne, impact and recovery states; NativeLow uses Gaussian through
the same motion states. Web alone keeps DOF off. Broken Crown uses
DepthNormals SSAO plus analytic side-form depth instead of realtime sun shadows,
which produced travelling bands in Game view. Bloom and vignette remain restrained.

## Why

The decisive trade-off is to spend one predictable screen-space pass on real
distance communication, then reserve expensive lens language for moments where
it cannot hide control or collision information.

## Consequences

### Positive

- Aerial depth, warmer key light and focus language reinforce gameplay state.
- Quality fallbacks are explicit and testable.
- Motes stay cheap and visually tied to lit space.

### Negative

- This is not physically complete volumetric lighting.
- The NativeHigh Bokeh envelope must track both fighters, so camera-dependent
  validation is mandatory whenever framing changes.

### New risks

- A later arena may need profile tuning to keep the far field visible.
- Renderer-feature cost still requires target-device profiling before release.

## Implementation boundary

- State/data owner: `EarthVisualClaritySolver` and existing atmosphere/celestial profiles.
- Dependencies: URP Volume framework, camera depth texture, main-light shadow map.
- Migration: legacy `RenderSettings.fog` is disabled to avoid double extinction.
- Tests: deterministic solver tests plus focused runtime visual contract test.
- Fallback: first lower atmosphere resolution; then remove NativeLow motes; never
  weaken gameplay silhouette/contrast rules.
- Removal seam: disable the clarity component, renderer feature or mote object independently.

## Evidence

- Prototype: rebuilt `EarthCoreSlice` through Unity MCP.
- Benchmark/playtest: 139/139 focused EditMode; runtime shader/scene contract green;
  whole-frame shared-session results in `BuildReports/Mvp01PerformanceLatest.json`.
- Sources: Unity URP RenderGraph blit, DOF and performance guidance.
- Date verified: 2026-08-29.

## Revisit trigger

Reopen when a target-device capture exceeds the 1.5 ms atmosphere+motes GPU p95
budget at 1080p NativeHigh, or playtests show focus/haze hiding threats.

## Amendment — 2026-08-31 approved arena placement and dynamic debris

- The user-approved Broken Crown root is embedded `0.98 m` below the planet
  tangent surface. This hides the broad underside and removes the visible void
  created when the arena was balanced on its single lowest vertex.
- The opponent's saved authored spawn is restored after arena integration, so a
  generated-scene rebuild cannot move it back inside the gate or floor.
- Generated wall and platform fracture bodies retain the spherical
  `GravityWorldBehaviour` through `GravityBody`; kinematic intact bodies skip the
  force tick and released pieces immediately resume planet gravity.
