# Stone impact dust validation — 2026-09-07

Actual Unity 6000.5.7f1 validation completed. Editor returned stopped, not compiling/updating, active EarthCoreSlice.unity dirty=False; Console had zero errors at handoff. No graphics sources imported during this ownership window. No physics/pool implementation changed during validation; no scene/theme/material assets edited.

## Final evidence

| Check | Result | UTC | Report |
|---|---|---|---|
| Stone policy, mesh validity, hub contracts |33/33|08:04:12|BuildReports/StoneDustEdit.json|
| True physical low drops, canonical mass, isolated native particles, dust-only pixels |1/1|08:12:40|BuildReports/StonePhysicalDropPlay.json|
| Actual lifecycle/mesh adapter + dust compositing after soft-fade fix |2/2|08:16:32|BuildReports/StoneContactCompositingPlay.json|
| Current mass-policy wall push and bounded lazy shell reuse |2/2|08:17:22|BuildReports/StoneCurrentWallContractsPlay.json|
| Fire convex follow-up, existing resolver and world |19/19|08:14:03|BuildReports/FireConvexFollowupEdit.json|
| Actual Earth wall fracture/reuse contact invalidation |1/1|08:16:01|BuildReports/FireConvexFollowupPlay.json|

Earlier StoneDustRegressionPlay ran14 cases:12 passed and2 failed stale wall expectations. Its shared-mass production, other physics, adapter and dust compositing cases passed. Only the2failed wall tests were corrected and rerun; no claim of a new full14/14 run. PhysicalDrop had one transient setup interruption from the Multiplayer Editor Matchmaker package ApiOperation.SetTaskResult InvalidOperationException; retry passed without suppressing logs or modifying packages.

## Real physical observations

Both test-authored stones start4cm above a flat pad tangent to actual planet gravity, use the saved pool mesh, production GravityBody, shared mass policy and real decor collision route. No mass/velocity/impulse override and no direct EmitStoneImpact call.

|Measurement|Lighter|Heavier|
|---|---:|---:|
|Shared-policy/body/canonical mass kg|145.28183|304.60098|
|Actual closing speed m/s|0.790681|0.790011|
|First impact admitted AND native dust|31|40|
|First impact admitted AND native chips|8|10|
|Changed pixels with dust alone|10886|6914|
|Measured presenter callback peak ms|0.0587|0.0723|
|Callback thread allocated bytes|0|0|
|Named MaterialParticles marker mean ms|0.01187|0.01461|
|Named marker peak ms|0.0164|0.0221|

Both mass records and body mass remain unchanged; both produce one actual contact Impact and zero further Impact after naturally sleeping. Four native mesh indices observed across both cases (lightmask15, heavymask14). Screenshot pixel count is not a monotonic mass metric: source seeds and stone occlusion differ. These are bounded callback/marker measurements, not frame/GPU/cold-init cost acceptance.

Capture directory: BuildReports/StonePhysicalDrop. Inspected heavy-3-dust-only.png and heavy-4-dust-only.png show actual soft dust behind/above stone; heavy-4-effects-on.png adds varied flying chips. gallery-four-actual-chip-silhouettes.png shows the four actual selected renderer meshes in isolation. Root independently reviewed the corrected dust-only image and accepted the scoped readability improvement. Camera uses saved materials/lighting with clean layer31 framing and a test-only tangent pad; no claim that it is an unaltered gameplay camera screenshot.

## Validation fixes

1. Initial test native counts were contaminated because EarthDestructibleDecorRock.ConfigureMaterialFeedback and Start also rebound the shared debris pool to its test hub. Fixture now restores pool's original sink after startup before release. Native counts now exactly match target impact counts. `unrelatedPresentedEvents` includes initialization-before-clear events, not attributed drop events; record light7 preparation events and heavy0. No extra native particles survived the clear.
2. Existing dust material had soft fade near.12m/far1.5m; centimetre-scale contact puffs disappeared against the floor. EarthMaterialFeedbackPresenter now overrides only its contact dust renderer's `_SoftParticleFadeParams` with near.0125m/far.3m using a property block. Shared RumbleDustLit material and broad Emerge/fracture renderer remain unchanged. The minority24% lofted impact puffs uses the existing broader fracture size range; the majority retains fine contact sizes. Added dust-only capture/pixel gate so visible chips cannot substitute for invisible dust.
3. Ordinary MonoBehaviour activation in EditMode does not dispatch runtime OnDisable. Edit test now checks source/generation cooldown only; actual disabled/enabled reset assertion resides in the passing PlayMode adapter fixture.
4. Authorized tests-only correction for current wall contracts: EqualMagicImpulseSlidesSmallWallFartherThanHeavyWall computes shared-policy masses, asserts inverse-mass velocity ratio and equal transferred momentum, retains actual travel and upright gates. The old raw-volume speed ratio>4 was incompatible with compressed/capped mass:704.3819kg vs1800kg yields2.555 ratio. WallPoolRaisesRectangularCollidersWithoutGrowingTerrainEditCost now proves all40sequential acquisitions reuse the same shell, allocations never grow after first acquire and stay within configured capacity3. It retains current WallId, live collider/emergence/push/fracture/persistent repairable-piece assertions. Old expectation of3eager roots conflicted with lazy prewarm1. EarthWall/EarthWallPool sources unchanged.

## Imported Fire follow-up

Before import, exact SHA256 matched both RuntimeFollowup baseline files: FireSurfaceResolver2876731c... and FireDomainState6d6313ab.... Imported the5staged after files (two existing sources and three additions). Unity compilation and19Edit+1Play tests pass. Follow-up cost and full gameplay visual coverage retain the runtime lane's documented limits.

## Source freshness/handoff

Actual Assets are authoritative. Prevalidation StoneLane/after snapshots are historical and must not be copied over the newer validated presenter/tests. `Tools/FireIntegration/Staged/StoneQa/validated` contains exact current validated source snapshots and SHA256 manifest. No further Unity or Assets writes occur after the handoff.
