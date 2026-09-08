# Shift+Space high-fall earth slam

September8 implementation, coordinator owns routed input, saved actor/profile binding and Unity execution. Existing ordinary Space landing cushion remains separate.

## Contract

Hold Shift+Space during an already observed physical flight, through actual upward support contact. Default minimum apex-to-contact drop2m and support-relative descending speed7.5m/s. One commit per uninterrupted held chord; continued holding does not repeat the strike, even when the actor falls again into the newly cut hole. Releasing before contact cancels the armed flight. Initial airborne scene acquisition and teleports cannot manufacture impact energy. The pure state keeps a four-fixed-step grace between an early motor ground probe and actual PhysX contact.

The adapter executes at order800, after motor support and before existing hard-landing bridge900. Only an accepted physical slam suppresses that bridge for0.18s; holding, insufficient falls, missing source material, busy terrain or exhausted physical pools grants no fall immunity.

## Physical result and authority

At the actual contact point, the saved authored FloorBase mesh is activated into its existing baked cells and at most four nearby cells are released/ejected. This is a new explicit LandingSlam trigger; ordinary damage still cannot break meteor-only floor, while MeteorImpact retains its existing full-floor release. Local slam release disables unsupported-island propagation because the meteor-only floor has no foundations and generic propagation would incorrectly drop the entire floor.

The voxel authority receives one ordered spherical subtraction, default radius1.6m, below the contact. Physical terrain ejecta are reserved from the existing fragment pool before mutation and enabled only after the exact edit receipt confirms both render/collider readiness. Four fragments share a conservatively sampled original solid volume and its mass policy; no arbitrary visual rocks are spawned as free matter. The remaining removed volume is not duplicated as physical bodies. Authored floor debris retains original identities/masses. Existing TerrainEdited/FragmentSpawned/EarthImpact events and material feedback drive presentation.

The existing wave pool has a dedicated capacity-only 36-cell concentric radial landing pulse, independent of active row protection but still refusing a truly full pool; no live row/wave cells are overwritten. Wave failure rejects the slam before terrain/floor mutation and releases reserved fragments.

## Integration APIs

`EarthLandingSlam.Configure(body,motor,executor,waveAbility)`, `ConfigureProfile(EarthLandingSlamProfile)`, `SetHeld(bool)`, `Cancel()`. Profile is Runtime.Physics/EarthLandingSlamProfile and defaults2m/7.5m/s/radius1.6/ejection8m/s. Diagnostics expose IsArmed, IsHeld, CommitCount, LastLandingSpeed, LastImpactPoint, LastReleasedFloorPieces, LastEjectedRockCount, LastWaveColumnCount, LastRejection, HasPendingTerrain and SuppressesHardLanding.

## Focused verification

Public `EarthLandingSlamTestLauncher.Edit()` selects9 pure cases including held once, release, missing real contact, small/slow fall, spawn/cancel, probe/contact grace and bounded FloorBase trigger. `.Play()` performs a real upward physical launch and fall onto saved authored FloorBase, holds through impact, requires local cell release rather than whole-floor fracture, terrain edit, real pooled ejecta and radial wave, then checks no repeated commit. It records `BuildReports/LandingSlam/physics-trace.txt`, `result.txt`, and `Elemental.Bending.LandingSlam` peak CPU. The paired keyboard fixture and completed runtime evidence are recorded below.

Authored floor ejecta use a bounded0.65s caster-pair collision grace, matching terrain fragments. Original pair-ignore state is restored afterward or on piece disable/arena reset. This prevents immediate self-impact from the cells being ejected beneath the caster; it grants no general fall or enemy-damage immunity.

Initial runtime trace committed0 and never armed because the fixture incorrectly fetched MagicExecutor from the fighter, while production keeps it on Earth Magic Runtime. Both serialized new actor components also had a missing executor reference; coordinator corrects authoring through `MagicInputController.EarthExecutor`. The fixture now uses that explicit binding and logs held/observed-support/bindings state; runtime reports an actionable missing-binding reason. No fall threshold changed. Separate `EarthLandingSlamPlanetTestLauncher.Play()` proves a physical fall onto exposed planet terrain away from the arena, solid-to-air canonical sample, >0.5m committed collider depth, four reserved ejecta and36 wave columns.

## Production evidence

Saved profile: Assets/Elemental/Content/Profiles/EarthLandingSlamProfile.asset, explicitly bound on both saved player routers. The executor reference comes from MagicInputController.EarthExecutor on its separate runtime object. Real airborne paired Shift+Space routes to LandingSlam; ordinary Space and grounded Shift+Space retain their existing owners.

Final floor/keyboard Play2/2 at2026-09-08T14:14:34.3245776Z, BuildReports/LandingFinalPlay.json. Floor outcome:4 locally released authored cells,4 reserved terrain rocks,36 wave cells; the original spanning collider is disabled. Holding does not repeat the strike; key release reaches the runtime. Planet Play1/1 at14:12:28Z, BuildReports/LandingSlamPlanetPlay.json: the same SDF sample changes from -0.2473 solid to1.4103 air; committed collider depth1.991m,4 ejecta,36 wave cells. No forced knockout in either physical fixture. Contract suite45/45 at14:16:48Z includes prior input and the scoped fracture gate.

The landing trigger requires finite positive impulse after the flight/contact/drop/speed eligibility check. It does not reuse ordinary-impact thresholds: the authored caster mass is12kg, so a valid magical landing can be below250Ns. Ordinary meteor-only floor damage remains blocked. Earlier combined floor failures documented this duplicate admission error and are superseded by the final2/2.

Measured synchronous cast marker peak37.367ms on authored floor,26.877ms on planet (includes local topology/terrain admission and feedback); these are one-off cast costs, not steady-state frame timings. GPU cost and online two-player end-to-end replication were not measured by these checks.

Final broad runtime regression EarthInteractionFinalPlay at15:14:20Z passes both floor/keyboard cases and exposed-planet case. Latest one-off cast peaks: floor11.914ms, planet28.160ms; earlier cold floor37.367ms remains a material spike risk. Physical outcomes unchanged.
