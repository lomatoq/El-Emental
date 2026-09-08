# Visible FX diagnosis and integration, 2026-09-07

Root owns Unity again. Final state at handoff: Play, Main, daylight. All asset changes were saved in guarded Edit before entering Play; diagnostic cyan material properties were runtime-only and removed by exiting Play.

## Confirmed causes

- Contact and ground dust set the legacy URP `_SoftParticleFadeParams` vector. The actual LightDustMote shader reads `_SoftParticleNearDistance` and `_SoftParticleInvDistance`; the intended short contact fade therefore never applied. Both actual renderers now set the scalar properties, without changing the shared fracture material.
- Live ground dust was not missing: 122 particles, normal sizes around 1×2 to 2×4 metres, valid alpha, correct world meshes. A reversible cyan diagnostic revealed full render coverage. Its former orange albedo was almost indistinguishable from the orange arena. Creamier renderer-local tint and shortened ground-parallel wisps make movement readable. Latest user-requested visibility increase uses opacity .75 and brightness1.35, versus .42 and1.2 before; capacity192 and rates remain bounded. Final visual acceptance and performance are separate from these configuration facts.
- Original impact puffs now visibly render through the existing material-feedback presenter. `BuildReports/VisibleFX/Impact-restored-dust.png` records a diagnostic presentation cue, not a claimed physical/network hit.
- Four nearest flame lights were often behind the Main camera. Front-of-camera priority and placement just inward/down from the cap now illuminate the visible shafts; night captures show actual warm patches. RumbleRockLit also needed additional-light variants for its existing loop.
- Saved Bloom intensity was not effective: EarthChargeCameraLookdevV2 rewrote it to zero each frame. The existing owner now permits a .3 floor only for NativeHigh while decorative fires are active. Actual VolumeManager stack read .3 after the change. No extra volume or pass was added.

## Actual evidence and limits

`BuildReports/VisibleFX/Main-night-bloom-final.png`, `Main-day-wind-final.png`, `Main-sunset-final.png`, and `Impact-restored-dust.png` are production-camera captures. Night illumination and ordinary impact puffs are visually confirmed. CloudTimePalette and FinalVisualContracts stages were imported; root will run the focused tests.

The earlier ground Play test passed 1/1 at16:40:33 before the final tint/opacity/contact changes; it does not validate these later changes. Its temporal on/off changed-pixel count was insufficient art evidence. Root must not present it as final dust visual acceptance. Final preservation check retained all9983 original scene records with no unexpected changes. No new standalone GPU, overall fire budget, or network acceptance is claimed.
