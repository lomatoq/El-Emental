# Gate 2 Rendering Integration

This Wave R2 package is intentionally inert in the shipping profile. The renderer
feature is registered, but `DuelRenderingProfile.UseCapsuleContactShadows` is
`false`; its OFF path publishes zero strength and clears both capsule shader
vectors. No shipping scene, prefab, gameplay producer, package, or ProjectSettings
asset was changed.

## Deterministic material migration table

| Producer surface | Role | Owned target material | Mapping frame | Required source preservation |
| --- | --- | --- | --- | --- |
| Character skin/costume | `Character` | `UnifiedCharacter.mat` (or an explicit per-character duplicate) | `AuthoredUv` | `_BaseMap` with scale/offset, `_BaseColor`, `_NormalMap`, `_NormalStrength`, `_Fade`, mesh normals and tangents |
| Intact sandstone | `IntactSandstone` | `UnifiedSandstoneExterior.mat` | `CapturedStructureLocal` | Existing `EarthFractureMappingFrame`, whole `MaterialPropertyBlock`, mesh normals and tangents |
| Loose hero rock | `LooseRock` | `UnifiedSandstoneExterior.mat` | `ObjectLocal` | Same exterior family and property-block policy as intact sandstone |
| Fracture exterior | `FractureExterior` | `UnifiedSandstoneExterior.mat` | `CapturedStructureLocal` | Same exact material/property source as intact plus the pre-fracture structure frame |
| Fracture interior | `FractureInterior` | `UnifiedSandstoneInterior.mat` | `CapturedStructureLocal` | Captured structure frame, fresh-face submesh assignment, mesh normals and tangents |
| Planet ground | `PlanetGround` | `UnifiedPlanetGround.mat` | `PlanetLocal` | Shared `_PlanetCenter`, authored geometry normals and tangents |
| Magic construct | `MagicConstruct` | `UnifiedMagicConstruct.mat` | `ObjectLocal` | Existing tint/emission property block, authored normals and tangents |

`UnifiedLightingMigrationProfile.asset` is the executable table. Its validator
requires exactly one compatible entry for every role. `UnifiedLightingMaterialBinder`
never replaces a material: the director assigns an explicit table material first,
then calls `Configure(profile)` and `Bind(...)`. The binder reads and merges into
the existing property block for the exact `materialIndex`; exterior and interior
submeshes keep independent slot state. Character variants are created explicitly
with `UnifiedLightingMaterialMigration.CopyPreservedProperties`; the source
material is never mutated or silently replaced.

## Director wiring hooks

In the director-owned setup path, load the migration profile and assign/bind each
renderer according to the table. Do not apply a global shader replacement.

For `EarthArenaStructure` fracture, capture a matrix that maps each future piece's
object-local coordinates into the intact structure-local projection frame. Stage
both exterior/interior renderer slots with this same matrix while they are dormant.
The binder also publishes its inverse-transpose normal matrix, so rotated and
nonuniformly scaled pieces retain the intact triplanar blend weights.
Stage the new `DuelShadowCaster` and `CapsuleShadowCaster` generation under the
same canonical `uint` group/generation. Commit both registries in one director
Update immediately before the intact/fracture visibility swap. If either commit
cannot be admitted, keep the intact generation visible and active. The registries
each provide atomic generation changes; their combined cross-registry transaction
must remain owned by the fracture director.

On `EarthFragment` or hero-rock pool acquire, configure at most four large-form
capsule/sphere proxies and route the rock path through
`HeroRockCapsuleShadowProducer`. Call `HeroRockCapsuleShadowIdentity.TryCreate` with an
explicit `IntactHeroRock` or `LargeActiveFracture` kind, a stable pool-slot or
structure group ID, and the current acquisition/representation generation. Do
not use the ever-increasing logical `EarthFragment.FragmentId` as the fixed pool
slot group: the bounded generation table is intentionally sized for the prewarmed
hero pool, while the generation advances on every checkout. Pass the current
`DuelRenderingProfile.CapsuleContactShadows.CreateRuntimeSettings()` into
`TryAcquire`, then commit once the complete representation is staged. The adapter
calls both caster unregister and group-epoch release on explicit `Release` or
`OnDisable`, so re-enabling a pooled shell cannot resurrect its old epoch.

The exact director-owned integration hooks are:

- `EarthFragmentPool.CreateFragment`: add/configure one caster and producer only
  on the bounded hero slots. Assign one canonical stable group ID per prewarmed
  slot; never configure more than `CapsuleShadowBuffer.MaximumGenerationGroups`
  (16) across all capsule-shadow owners.
- `EarthFragmentPool.Acquire`: after `EarthFragment.Initialize` and `SetShape`
  have finalized active state and world scale, create the typed identity using
  the slot group plus `fragment.TargetHandle.Generation`, acquire, then commit
  that single-rock generation.
- `EarthFragmentPool.NotifyReleased`, reintegration, and shatter return: call
  producer `Release` before deactivation. `OnDisable` is the idempotent safety net,
  not the primary pool-return signal.
- `EarthArenaStructure` fracture staging: acquire only exterior large active
  pieces under the structure group/new representation generation, then commit
  once after every admitted piece is staged. Do not add this producer to
  `EarthRockDebrisPool` or any dust/VFX pool.

This analytic contact path does not enable URP cascades or arena-wide realtime
shadows; the existing no-realtime-shadow fallback remains unchanged.

The pure admission policy rejects character kinds, tiny/sub-threshold fragments,
debris, dust, particles, VFX, invalid IDs, and non-finite proxy bounds before they
reach the shared buffer. No public raw identity/classification bind exists. A
released group retains a bounded generation tombstone, so an equal/older epoch
cannot be committed again; exhausted group capacity fails closed. Player/bot/
ragdoll, intact hero rock, and large fracture classifications therefore cannot drift.
`Unknown`/default and undefined producer values fail closed. The caster has no
serialized identity and never registers from `OnEnable`; acquisition must occur
explicitly after every enable/pool checkout.
The raw buffer record, constructor, and registration method are assembly-internal;
only the dedicated EditMode/PlayMode friend assemblies can exercise them directly.

For player/opponent and ragdoll presentations, bind a stable fighter `uint` and a
small body set (pelvis, chest, and at most two limb capsules). Rebind on every pool
or ragdoll acquisition. Component re-enable alone cannot resurrect an old binding.

## Gate 2 proof procedure

1. Keep legacy realtime shadows disabled. Capture the unified materials with duel,
   capsule, and SSAO independently disabled; large-form readability must survive
   every single-feature-off case.
2. For contact-only proof, transiently begin
   `CapsuleContactShadowCaptureOverride` with `ShadowOnly`, bind only admitted
   character/hero proxies, capture diagnostics, dispose the token, and call
   `CapsuleContactShadowFeature.ClearGlobalState()` during restore.
3. Capture intact sandstone, then the staged fractured representation from the
   same camera/light without changing its exterior material/property block or
   captured projection frame. Compare the exterior surface; fresh interior faces
   are intentionally a different family.
4. Cycle a pooled caster through bind, disable, re-enable, and reacquire; the empty
   interval must preserve committed generation authority and reject stale proxies.
5. Profile `Elemental Capsule Contact Shadows` and
   `Elemental.Rendering.HeroRockCapsuleLifecycle` plus
   `Elemental.Rendering.UnifiedMaterialBind`. Bind is acquisition-time work; the
   buffer copy/upload hot path must report zero managed allocation after warmup.

Unity shader compilation, visual equivalence, GPU/CPU budgets, and zero-GC profiler
evidence remain unproven until the director runs the focused EditMode/PlayMode
suites and Gate 2 captures on the integration branch.
