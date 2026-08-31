# Duel shadow R1 integration

The renderer asset already contains `DuelShadowRendererFeature`, but
`DuelRenderingProfile.UseDuelShadowMap` is intentionally serialized **off**. The existing
no-realtime-shadow presentation path remains the default and rollback.

Director-owned scene/prefab wiring:

1. Add one `DuelShadowBoundsProvider` to the durable Earth Core presentation root and assign
   the directional key light, player root, opponent root, and arena-center transform. Do not
   use the camera as a bound source.
2. Add `DuelShadowCaster` only to opaque player, opponent, arena, hero-rock, and eligible
   active-fragment renderer roots. Stable group and generation are canonical `uint` values;
   never cast an `EarthPhysicalTargetHandle` through `int`, because high-bit IDs are valid.
   Group `0u` is the only invalid/unbound identity. Static casters may serialize a nonzero group,
   generation, classification, and explicit renderer array; the default serialized group is zero,
   so a newly enabled or pooled component cannot register an accidental identity. `TinyDebris`,
   `Vfx`, and `Other` classifications are deterministically rejected; hero rocks below 0.45 m
   and active fragments below 0.8 m are rejected by the default profile. Leave the serialized
   group at zero on every runtime-bound pooled caster; static and runtime binding modes must not
   be mixed on one component.
3. Put one `DuelShadowCasterBinder` on the durable presentation root. Runtime producers pass a
   `DuelShadowCasterIdentity` into `Bind` for every acquisition. `Bind`/`Rebind` always unregister
   the old handles before registering the supplied identity. Disabling a runtime-bound caster
   unregisters it and clears that runtime binding; re-enabling remains unregistered until the
   current producer identity is explicitly rebound. Call `Unbind` before a producer permanently
   releases a live object; ordinary GameObject disable is already safe and idempotent.
4. For an intact-to-fractured swap, bind every generation-N fracture caster first, call
   `DuelShadowCasterBinder.CommitGeneration(stableGroupId, N)` exactly once, then disable/pool
   generation N-1. The commit rejects a generation with no registered representation. Pool
   disable unregisters renderer handles but deliberately preserves the committed generation,
   preventing a stale N-1 object from reactivating after an empty interval. Call `ReleaseGroup`
   only when permanently retiring an ID, never during ordinary pooling.
5. Enable `UseDuelShadowMap` only after the bounds provider and caster set are present. Set the
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

- Presentation owns `DuelShadowCasterBinder`, the caster registry, generation commit, stabilized
  bounds, map, globals, and diagnostics. Gameplay/destruction remains authoritative for stable
  IDs, generations, and the moment a representation handoff is complete. Runtime assemblies do
  not reference Presentation; a director-owned presentation adapter invokes the binder with those
  already-published values.
- `EarthFragment` hook: preinstall `DuelShadowCaster` while the fragment pool is warmed. In the
  director integration adapter, after `EarthFragment.Initialize(...)` and `SetShape(...)`, bind
  `fragment.TargetHandle.StableId`, `fragment.TargetHandle.Generation`, and `ActiveFragment`.
  On return/shatter/reintegration, cache that handle, disable or `Unbind` before the slot is
  considered reusable, then call `ReleaseGroup(oldStableId, oldGeneration)` because the current
  `EarthFragmentPool` assigns a new stable ID on every acquisition. A producer that intentionally
  reuses one stable ID across generations must preserve the group instead.
- Player/opponent and ragdoll hook: use the character's director-owned canonical nonzero `uint`
  presentation ID as the group. Bind all animated-body renderers to the current generation. Before
  switching to the visible ragdoll, bind all ragdoll renderers to the next generation, commit once,
  then hide the animated representation. Recovery performs the same register-all/commit/hide order
  with another generation. Classification remains `Player` or `Opponent`; never derive identity
  from `GetInstanceID`.
- Intact/fracture hook: use the producer's existing `uint` `StructureId`/`WallId`/`PlatformId` and
  its published generation without signed conversion. Bind every eligible large piece before the
  producer exposes the fractured representation, commit once at the representation flip, then
  disable the intact caster. Reassembly is another new generation and uses the same atomic order.
- If identities from different producer domains are not already globally unique, the director must
  assign a persisted canonical `uint` presentation ID at spawn/setup. Hashing or narrowing IDs at
  the rendering boundary is forbidden because it would make generation authority ambiguous.
- Missing profile, shader, bounds provider, valid matrices, or admitted casters prevents pass
  enqueue and writes `_ElementalDuelShadowParams = 0`; receivers therefore return fully lit and
  the existing no-realtime-shadow image remains the explicit fallback.
- Same-commit director validation must import with zero compiler/shader warnings, run
  `DuelShadowRenderingTests` and `DuelShadowRenderingLifecycleTests`, verify both owned hidden
  shaders import and the debug receiver still calls `ElementalSampleDuelShadow`, verify no missing
  renderer-feature script/reference, capture `ShadowOnly` at rest and during a camera orbit, and
  exercise high-bit identity, bind -> disable -> rebind, and intact -> fracture -> pooled reuse.
- Profile the named marker `Elemental Duel Shadow Map` at 1080p for Low/Balanced/Cinematic. The
  R1 code establishes fixed resolutions, a maximum 256-entry registry, a default 160 draws, and
  bounded 3x3/5x5/7x7 PCF; it makes no GPU/CPU timing claim until those captures exist.
