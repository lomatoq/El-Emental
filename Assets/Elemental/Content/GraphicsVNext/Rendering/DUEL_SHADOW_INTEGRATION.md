# Duel shadow R1 integration

The renderer asset already contains `DuelShadowRendererFeature`, but
`DuelRenderingProfile.UseDuelShadowMap` is intentionally serialized **off**. The existing
no-realtime-shadow presentation path remains the default and rollback.

Director-owned scene/prefab wiring:

1. Add one `DuelShadowBoundsProvider` to the durable Earth Core presentation root and assign
   the directional key light, player root, opponent root, and arena-center transform. Do not
   use the camera as a bound source.
2. Add `DuelShadowCaster` only to opaque player, opponent, arena, hero-rock, and eligible
   active-fragment renderer roots. Assign positive stable group IDs. `TinyDebris`, `Vfx`, and
   `Other` classifications are deterministically rejected; hero rocks below 0.45 m and active
   fragments below 0.8 m are rejected by the default profile.
3. For an intact-to-fractured swap, enable/register every generation-N fracture caster first,
   call `DuelShadowCaster.CommitGeneration(stableGroupId, N)` exactly once, then disable/pool
   generation N-1. The commit rejects a generation with no registered representation. Pool
   disable unregisters renderer handles but deliberately preserves the committed generation,
   preventing a stale N-1 object from reactivating after an empty interval. Call
   `TryReleaseGroup` only when permanently retiring an ID, never during ordinary pooling.
4. Enable `UseDuelShadowMap` only after the bounds provider and caster set are present. Set the
   profile's diagnostic view to `ShadowOnly` and capture its grayscale, depth-reconstructed
   receiver output; then restore it to `None`. This is the R1 visible A/B proof seam. The custom
   pass does not change the URP main light or cascade settings.

R1 is opaque-only. The override caster shader does not reproduce alpha clipping, two-sided
foliage, vertex-displacement, or transparent/VFX silhouettes. Do not register those renderers;
author a dedicated compatible caster pass before admitting any such asset. Existing production
receiver shaders are intentionally untouched in R1. VNext receiver work can include
`ElementalDuelShadow.hlsl` and call `ElementalSampleDuelShadow(positionWS)`. Until that production
wiring is performed, the owned shadow-only diagnostic pass is the receiver proof and ordinary
legacy shading remains unchanged.

## Contract and acceptance evidence

- Presentation owns the registry, generation commit, stabilized bounds, map, globals, and
  diagnostics. Gameplay/destruction owns stable structure IDs and decides when a fracture has
  completed; its only rendering call is the one-way `CommitGeneration` handoff.
- Missing profile, shader, bounds provider, valid matrices, or admitted casters prevents pass
  enqueue and writes `_ElementalDuelShadowParams = 0`; receivers therefore return fully lit and
  the existing no-realtime-shadow image remains the explicit fallback.
- Same-commit director validation must import with zero compiler/shader warnings, run
  `DuelShadowRenderingTests`, verify no missing renderer-feature script/reference, capture
  `ShadowOnly` at rest and during a camera orbit, and exercise intact -> fracture -> pooled reuse.
- Profile the named marker `Elemental Duel Shadow Map` at 1080p for Low/Balanced/Cinematic. The
  R1 code establishes fixed resolutions, a maximum 256-entry registry, a default 160 draws, and
  bounded 3x3/5x5/7x7 PCF; it makes no GPU/CPU timing claim until those captures exist.
