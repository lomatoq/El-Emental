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
the renderer's whole existing property block. Character variants are created
explicitly with `UnifiedLightingMaterialMigration.CopyPreservedProperties`; the
source material is never mutated or silently replaced.

## Director wiring hooks

In the director-owned setup path, load the migration profile and assign/bind each
renderer according to the table. Do not apply a global shader replacement.

For `EarthArenaStructure` fracture, capture a matrix that maps each future piece's
object-local coordinates into the intact structure-local projection frame. Stage
both exterior/interior renderers with this same matrix while they are dormant.
Stage the new `DuelShadowCaster` and `CapsuleShadowCaster` generation under the
same canonical `uint` group/generation. Commit both registries in one director
Update immediately before the intact/fracture visibility swap. If either commit
cannot be admitted, keep the intact generation visible and active. The registries
each provide atomic generation changes; their combined cross-registry transaction
must remain owned by the fracture director.

On `EarthFragment` or hero-rock pool acquire, configure at most four large-form
capsule/sphere proxies, then call `CapsuleShadowCasterBinder.Bind` with the current
stable matter/structure `uint`, generation, and classification. Disable/unbind on
pool release and call `ReleaseGroup` only on permanent retirement. A released
group retains a bounded generation tombstone, so an equal/older epoch cannot be
committed again; exhausted group capacity fails closed. Never bind tiny debris,
dust, particles, or VFX. Prefer `TryAcquire` with the typed producer kind so
player/bot/ragdoll, intact hero rock, and large fracture classification cannot drift.

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
   `Elemental.Rendering.UnifiedMaterialBind`. Bind is acquisition-time work; the
   buffer copy/upload hot path must report zero managed allocation after warmup.

Unity shader compilation, visual equivalence, GPU/CPU budgets, and zero-GC profiler
evidence remain unproven until the director runs the focused EditMode/PlayMode
suites and Gate 2 captures on the integration branch.
