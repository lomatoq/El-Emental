# Stone physical drop QA — staged new files only

This contains a new PlayMode production fixture and a menu launcher. They have NOT been imported, compiled or run. No Assets or scene files changed while authoring these sources. Coordinator/presentation owner runs Unity after source integration.

Sources mirror their intended Assets destinations:
- Assets/Elemental/Tests/PlayMode/EarthStonePhysicalDropProductionTests.cs
- Assets/Elemental/Tests/EditMode/EarthStoneDustQaLauncher.cs

## What the test actually does

Loads the saved EarthCoreSlice additively from an empty test scene, waits for its readiness gate and follows ProductionCombatTestFlow.BeginBotAfterReadiness. Existing bot is disabled for isolation. Test-created stones, pad and camera exist only in the additive scene. Teardown writes progress/evidence even on failure, unloads its scene, restores the previous active scene and timeScale. It never saves a scene or changes an asset.

A flat pad is oriented tangent to the ACTUAL radial gravity at its location above/beside the arena. Two stones use the existing pool's real normalized mesh, normal mesh colliders, production GravityBody/GravityWorld, production shared mass policy, and actual EarthDestructibleDecorRock Configure/grab/release. Both start with 0.04m bottom clearance and no assigned impulse/velocity/mass. Sizes .8m and1.15m keep this focused on surviving contacts rather than the small-shatter branch. Real mass comes from existing collider->shared-policy code. Each test-authored stone is registered through the existing EarthMatterIdentity/kernel with that real mass and volume; policy/body/record agreement and after-impact conservation are asserted.

The saved EarthMaterialFeedbackPresenter, particle systems, profile and materials are used. Its injected hub is rebound to a test-owned hub to exclude unrelated atmospheric/gameplay event traffic from attribution. The actual decorated stone supplies that hub through ConfigureMaterialFeedback. The test NEVER calls Emit or EmitStoneImpact; it observes the natural collision->decor->hub->presenter route. The public grab path's unrelated Extract puff is flushed/cleared before the first physics step. Captures use a new clean URP camera at layer31, saved material/light environment, no post-processing or HUD, and a test-only floor. This is production-material rendering with actual physics, not a full game-camera composition claim.

Assertions include actual support collider contact, positive closing normal speed, surviving stone, admitted impact dust/chips, nonzero native particle counts, heavier FIRST impact admitting more dust, all four native chip mesh indices across the two drops, and same-frame rendered on/off particle difference >32 pixels. Record actual impact count and later settling separately so bounce count cannot counterfeit heavier initial response. Once the body naturally sleeps, half a second must add no Impact events. Gallery renders the actual four selected renderer meshes with the saved stone material for clean silhouette review; native particle selection is separately verified, so the gallery is not presented as emission proof.

CPU evidence: a before/after listener pair brackets the real presenter callback for target Impact cues (no inner allocations in instrumentation), records timestamp duration and thread allocated bytes. It includes delegate/probe overhead. The existing MaterialParticles profiler marker records frame values during observation. GPU/full-frame/cold-init performance is not claimed. Screenshots and JSON serialization are outside callback allocation brackets.

Outputs: BuildReports/StonePhysicalDrop/evidence.json; each mass before PNG + four effects-on/effects-off pairs; gallery-four-actual-chip-silhouettes.png. Actual frame scheduling may skip a later optional snapshot if rendering takes unusually long; counts and failure progress still persist. Review the images for framing, lighting, dust footprint and distinct silhouettes; numeric pixel differences alone do not establish attractive visuals.

## Run sequentially through coordinator

After copying these two new files into Assets and one refresh, use:
1. Elemental/QA/Stone Dust/Edit Policy and Meshes — StoneDustEdit.xml/json.
2. Elemental/QA/Stone Dust/Play Physical Drops and Captures — StonePhysicalDropPlay.xml/json and captures.
3. Elemental/QA/Stone Dust/Play Adapter Compositing and Mass — StoneDustRegressionPlay.xml/json.

The launcher invokes the existing Mvp01FocusedTestLauncher.Run via the same reflection seam other QA launchers use, retaining its empty-play-scene and original-scene restoration behavior. Run one menu at a time. Regression set includes existing direct hub/mesh adapter proof, dust compositing, shared-mass production and EarthPhysicsRegressionTests (10 physics cases). No automatic chained runs or competing Unity instance.

## Failure interpretation

If small rock fractures, check actual saved break profile/geometry before weakening the test. If heavy mass pushes contact above production shatterImpulse, this scenario no longer isolates surviving-impact dust; reduce both size range or clearance coherently, retaining heavier actual mass and same height. If positive normal sign fails, record actual collision vectors and investigate the producer, not inject a fake cue. If the fixture cannot settle, inspect real support normal/gravity/rolling rather than sleeping/zeroing the body artificially. If production profile disables particles, treat the clear failed assertion as a real configuration result.

Runtime risks before first execution: additive-scene service initialization; whether two natural masses at the saved policy produce distinct counts rather than budget saturation; whether all four meshes are sampled in those bounded counts; pad/camera lighting/framing; current source profile's fracture thresholds. These sources are review-ready but require actual compile/Play evidence before acceptance.
