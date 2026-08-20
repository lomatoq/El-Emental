# ADR 0021: Semantic Earth intents and shared surfaces

Status: accepted
Date: 2026-08-14

## Context

Earth controls had grown as independent button branches, while pillar and landing abilities assumed that the analytic planet collider was always the closest support. That made Shift/Space combinations order-dependent and prevented the same ability from behaving correctly on raised platforms or wall tops. The old 3 m platform cap also hid the intended large-scale construction space instead of expressing its cost and stability.

## Decision

- `EarthInputAdapter` remains the only Unity Input System boundary. It can capture one viewport-normalized `EarthGestureFrame`; the pure `EarthActionIntentResolver` applies a fixed priority from cancel through landing/surf/self-wave, controlled targets, quick actions, full bend, MMB field/repair and pillar jump.
- Tap/hold classification uses elapsed seconds and normalized viewport travel, never pixels.
- `EarthSurfaceSample` is the common simulation contract. It contains a stable kind/ID/generation handle, point, orthonormal frame, velocity, material, provenance and capability flags.
- `EarthSurfaceQueryService` owns a fixed 32-provider array. Providers are injected by authoring/pools; steady-state queries do not use scene searches, global registries or managed allocations.
- Planet, platform and wall-top providers expose only valid capabilities. The nearest valid constructed support wins over the farther base planet, and recycled generations invalidate old samples.
- Pillar launch and landing cushion query the same service. Pillars inherit tangent support velocity; landing damping is relative to the selected support velocity.
- Moving-support carry inherits only the support's bounded tangent velocity change;
  it never damps the rider's own tangent locomotion. Settled platforms stop
  registering airborne actors as riders, and temporary collision suppression is
  restored from the motor capsule's foot envelope rather than every ragdoll limb.
- Platforms use an 8 m soft target and configurable 22 m hard limit. Height and aspect ratio raise cost and reduce stability instead of silently clamping at 3 m. Serialized V1 profiles migrate without changing unrelated tuning.

## Consequences

Surface-aware abilities no longer need type-specific planet/platform branches. Future Surf and quick-cast phases can consume the same intent and surface contracts without reading devices or scanning the scene. The query budget is explicit (32 providers), so raising structure pool capacities requires an intentional budget review.

## Evidence

- EditMode: intent priority, normalized tap equivalence, complete sample frame, deterministic nearest-surface selection, soft/hard platform budget and authored gesture-profile binding.
- PlayMode: explicit provider registration, nearer platform selection, generation rejection, platform support for pillar/landing, production Humanoid locomotion/cast and self-target-safe wall push.
- The Release platform golden path lifts the rider `1.343 m`, preserves `0.698 m`
  of commanded walking, completes a pillar-jump return with `0.072 m` minimum
  descent clearance, and ends with the launch pillar retired and zero live chips.
- Standalone D3D11 wall-push QA: wall travelled `2.294 m`; caster travelled `0.421 m`; screenshot and log live in `BuildReports/EarthV2/Phase2/`.
- Windows Development: succeeded with `0` warnings and `0` errors.

## Rollback

Disable provider configuration in `M3EarthCoreSetup` and pass no query service to pillar/landing; their explicit analytic-planet compatibility path remains available for isolated legacy fixtures. Reverting the platform profile to a 3 m asset value restores the old cap without changing mesh topology. The pure intent resolver is additive until deferred quick-cast/surf consumers arrive.
