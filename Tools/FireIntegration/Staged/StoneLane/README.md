# Stone lane — staged, not integrated

Scope: cosmetic stone chips and mass-aware impact dust. All files are outside Assets. The coordinator owns Unity refresh, tests, scene/save/build, integration and project-state updates. No Unity execution, compile result, captured appearance, performance or gameplay acceptance is claimed here.

Apply only stone-lane.patch against fresh sources. manifest.json includes before/after SHA256. before/ and after/ hold review snapshots, not permission to replace overlapping files blindly. Stage construction scripts are provenance; do not rerun stage.py after integration because it captures a new baseline. verify_stage.py regenerates the final patch and reports baseline drift without writing Assets.

## Actual route and ownership

- EarthDestructibleDecorRock.OnCollisionEnter already validates released ownership, initial-overlap protection and positive closing normal speed before its final surviving-impact emission. That final branch used speed-only strength clamped .4–1, preventing a heavy short drop from reading heavier. It now calls EmitStoneImpact using existing EarthMass. Fracture/detach decisions are unchanged; successful fracture still owns its own dust cue.
- EarthRockDebrisPool.HandleDebrisImpact used a .75m/s early cutoff and fixed .4 surviving-impact strength. Its existing damage branch uses the opposite relative-velocity dot convention from corrected decor. This patch deliberately preserves that damage calculation and handles cosmetics with the corrected positive closing convention. Below the old damage cutoff it can produce dust without running damage. Existing arming/grip/accretion guards remain in EarthRockDebris.OnCollisionEnter.
- EarthFragment.OnCollisionEnter resolves the canonical surface-contact policy before publishing/forwarding accepted impulses to MagicExecutor and TryShatter. EarthFragmentPool.PresentLooseImpact receives only this accepted-impulse path, uses its existing specific impulse as a closing-speed proxy and now includes mass and TargetHandle.Generation. The upstream contact-acceptance speed gate is unchanged. This is not a new low-speed contact acceptance rule for hero projectiles; verify actual low drops separately.
- EmitStoneImpact is injected through EarthMaterialFeedbackHub. A fixed 32-entry source+generation ring suppresses repeat impact presentation within .12s and clears on disable. Stable unknown ID zero bypasses cooldown to avoid hiding unrelated sources. No Stay emitter, instance IDs, density inference, Rigidbody creation or mass writes were added.
- Hub Emit still owns profile overrides/intensities, event coalescing and frame caps. Policy strength .25–2.4 remains within its 0–3 clamp. A production profile can intentionally disable or budget-limit effects; do not claim an unconditional emitted count.
- EarthMaterialFeedbackPresenter remains the particle owner. Existing UsesBroadDust (including neighbor Emerge/extraction/rise changes) is untouched. Seed now derives solely from typed cue kind/source/generation/quantized point, so unrelated prior events do not affect replay.

## Appearance changes

Four bounded cosmetic meshes are cached at initialization. The existing authored chip is cloned as the first variant when supplied; other entries are deterministic wedge/pebble families built by the existing RumbleRockMeshFactory. The fallback without an authored chip includes a slab. Clones/generated meshes are centered and normalized along the longest axis only, retaining distinct aspect ratios, then destroyed with the presenter. Original mesh assets, RumbleRockVariation and RumbleRockMeshFactory are unchanged.

Cubic sampling makes most cosmetic chips/motes small and a minority coarse instead of uniformly distributing sizes. Impact dust splits the SAME admitted particle count into approximately 76% short low lateral puffs and 24% slower lofted puffs. Deterministic unequal tangent lobes break an even radial spray. Spawn centers remain above the contact plane; the existing planet gravity integrator is preserved. Broad Emerge dust size multiplier remains.

Repairable physical fragments still use parent-contained convex partitions, exact inherited world scale and conserved volume/mass. No visual scaling/randomized physical children were introduced. Baked fragment-cell diversity and other presenters (arena fracture, MagicFeedback, wind) remain separate routes; do not claim every stone effect is replaced.

## Validation available and required

Executed: verify_stage.py arithmetic-reference/freshness checks. static-evidence.json records 2kg/.6m/s strength .263646 versus 1000kg/.6m/s strength 1.53; cubic 1000-sample distribution gives 630 below quarter-range and 91 above three-quarter-range. This is an independent Python formula check, NOT execution of C# or Unity tests.

Staged EditMode fixture EarthStoneImpactDustTests contains 10 cases: energy bounds/monotonic response, five invalid/rest/separation cases, size distribution, stable seed replay, per-source/per-generation/reset cooldown, and geometry validity. Staged PlayMode fixture EarthStoneImpactDustRuntimeTests contains one production-profile adapter test: native renderer.meshCount==4, native GetMeshIndex covers all four silhouettes, and admitted heavy dust exceeds light dust. Tests are UNRUN. Native mesh-index API reference: https://docs.unity3d.com/cn/6000.0/ScriptReference/ParticleSystem.Particle.GetMeshIndex.html

Coordinator must run those fixtures plus existing EarthMaterialPassTests, RumbleRockMeshFactoryTests, shared mass/fracture regressions and dust compositing tests after resolving source ownership. Run on the actual Unity version to prove the native four-mesh configuration and particle seed behavior.

Visual gate: production arena, same lighting/camera, repeated 0.05m/0.2m/1m drops of light/medium/heavy released decor and already-fractured debris. At gravity14, .05m drop gives ~1.18m/s before contact; .6m/s is a very short ~1.3cm drop. Capture before/after at 0.0/.08/.2/.5/1s and inspect contact footprint, normal hemisphere, mesh silhouette variety, density and lack of uniformly enlarged particles. Include a side wall contact on spherical terrain and 10s resting/rolling (no impact bursts from stationary contacts). Verify damaged repairable pieces remain repairable with identical mass. Recheck broad Emerge/rise and wind concurrently.

Profile cold initialization (4 mesh builds/clones), repeated impacts and MaterialParticles marker under a 10-impact burst; report CPU time, managed allocations and GPU overdraw rather than assuming bounded means cheap. Existing event/particle caps remain, but cold mesh baking and extra silhouette draw calls require measurement. The first authored mesh must be CPU-readable for normalization, as current generated V5 sources are expected to be; test it in the saved production scene.

Known deferred concern: prior debris damage normal sign deserves an independent physics regression and owner decision, not a cosmetic stealth fix. The 32-slot cooldown can evict an entry under more than 32 distinct sources in .12s; shared hub frame budgets still bound total output. Initial authored-chip array is retained across Configure calls; normal scene configuration occurs once, but runtime reconfiguration to a different mesh would need cache rebuild.

Once tests/visual evidence exist, coordinator should add a brief factual pending/accepted entry to PROJECT_TECHNICAL_STATE and PROJECT_EXECUTION_TRACKER with that evidence and the shared mass/presentation boundary. No acceptance text is staged prematurely.

