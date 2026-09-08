> Integration checkpoint 2026-09-06 21:34 UTC: 54 new files and 15 exact runtime/frontend patches imported into Assets. Protected scene/theme/layout assets were backed up and left unchanged. Package installation, SDK compile, scene installer and real two-process verification are still pending at this checkpoint. Older wording about pending-only source below describes the preparation history.

# Pending stage 2 validation

2026-09-06. Prepared outside Assets before stage 1 commit. No Unity mutation by this lane.

Completed checks:
- All 47 pending C# files parsed with Roslyn: zero syntax errors (includes the parent frontend bridge).
- Compile-RuntimeSlice.ps1 successfully compiles the full current Simulation, Runtime and Input assemblies with pending partials/patches against the actual Unity Bee reference lists. Seven existing CS0618 warnings remain in EarthPillarMobility, EarthRuntimeRescueSystems and EarthPlatformPool; no new compile errors/warnings in the pending seams.
- OnlineGeometryCatalog, OnlineMeshCodec and both editor authoring utilities compile against installed UnityEngine/UnityEditor assemblies. This caught and fixed the UnityEngine/System.IO CompressionLevel name ambiguity.
- RuntimeBindings.patch passes git apply --check against current live sources. Its thirteen files are precise partial/authority/view/actor-id hooks, not replacements of concurrent mass/geometry hunks.
- Earlier headless checks compiled the actual pure declarations: 15/15 readiness/sequence assertions and 8/8 atomic match replica assertions passed. These are not NGO connectivity tests.

Prepared Unity EditMode filters (not executed):
- Elemental.Tests.EditMode.OnlineProtocolTests: 9 cases, actual NGO serializer/command bounds/readiness/sequence.
- Elemental.Tests.EditMode.OnlineMatchReplicaTests: 5 cases, canonical atomic health/score/time apply.
- Elemental.Tests.EditMode.OnlineGeometryTests: 3 cases, bevel normals/UV/tangent/color/material submeshes, geometry checksum/length, fracture mapping/palette property blocks.
- Elemental.Tests.EditMode.OnlineSemanticInputTests: 2 cases, actual semantic wire edges/held/camera/scroll and maximum binary mesh packet budget.
- Elemental.Tests.EditMode.OnlineActorGraphAuthoringTests: 2 cases, cross-root owner references and both internal/cross-root shared-service remapping.

Prepared PlayMode filter (not executed): Elemental.Tests.PlayMode.OnlineAuthorityGateTests, 3 cases: replica rejects damage/restart/clock mutation, stone impact cannot apply impulse/outcome locally, repeated jump edge survives a lost packet without replaying.

Required integration evidence after importing SDK/packages and targeted scene installer:
1. SDK semantic compile and all prepared cases. No ignored failing tests or fake service-success mocks as multiplayer acceptance.
2. Two standalone processes, different anonymous auth profiles, identical build/world/catalog. Real Sessions code, Relay connection, two NGO peer IDs, both Ready plus world ACK and terrain/rig gates before Begin.
3. Invalid/expired code, offline auth, cancel during auth/create/join, late SDK completion, host quit and peer drop. Cancel completes only after late sessions are cleaned; no listening old manager/code/input survives. Create a new room after cleanup.
4. Independent actor movement/jumps and whole Earth toolkit on host actor 2. Both directions of stone hit/stun/localized accepted region, quick combo, grab/throw, vector/gravity, narrow/wide walls, platform, wave, armor, surf, repair and return. A KO/stunned actor cannot cast even while the other remains active. Client gameplay mutators stay gated if recovery re-enables their MonoBehaviour.
5. Wall 2-3 medium-hit detachment and both boulders: matching geometry/material/bevel/occlusion, no duplicate impulse/damage, correct repaired/returned nodes. Terrain extraction and return spanning chunks: identical ordered edits and all touched chunk hashes/versions, colliders committed before Ready.
6. 100 ms RTT, 20 ms jitter, 2% loss: motor correction, final resting node poses, semantic release and input timeout, no stuck holding. Reject forged damage/spawn/edit, wrong owner, NaN, excessive queue/rate, stale epoch/sequence/tick.
7. Same health/score/clock/respawn/results; local HUD and camera reference their actor. On End restore offline actor input, impact duel binding, pose and AI roots; dedicated online duel disabled. No extra active audio listener or shared owner executor.
8. Profile world scan, compression, reliable queue latency and GC. The generic exact geometry registry intentionally favors correctness, but its scan/mesh/bandwidth limits and large simultaneous fractures have no performance acceptance yet.

Unverified: all NGO/MPS compilation/runtime APIs, connectivity, two-process gameplay, visual acceptance, local cast-preview latency, GPU-only cosmetic effects and performance. Full cosmetic bone streaming, host migration and in-progress round rejoin are outside scope. Runtime in-place mesh changes with unchanged reference/bounds/count need an explicit revision seam if a live generator uses that pattern.
Additional prepared PlayMode filter: Elemental.Tests.PlayMode.OnlineSemanticRoutingTests (1 case), actual remote adapter coalesces stale continuous frames while delivering press and release on distinct routed frames. Not run.


2026-09-06 21:42 UTC integrated SDK semantic check: imported Elemental.Online, Elemental.Online.Editor, Elemental.Online.Tests.EditMode and Elemental.Online.Tests.PlayMode compile successfully against actual fresh NGO2.13.2/MPS2.3.1 Bee references. Four exit0 results, no compiler output. Logs: CompileCheck/Elemental.Online*.integrated.log. This supersedes the earlier SDK-not-compiled statement. Unity test execution, targeted installer and two-process services/gameplay remain pending. Planned older SDK versions failed due Unity6000.5 EntityId/EndNameEditAction removals and were replaced by official released versions, no package-cache modifications.


## Integrated Unity test checkpoint — 2026-09-06 22:22 UTC

- `BuildReports/OnlineStage2Edit.xml`: **30/30 passed**, current compiled source, ended 22:21:43 UTC.
- `BuildReports/OnlineStage2Play.xml`: **4/4 passed**, ended 22:22:48 UTC.
- The scene-resource identity regression now checks independently allocated equivalent meshes, changed vertices, and changed vertex colors. Canonical serialized-property traversal descends only into generic containers; typed references do not reintroduce Unity internal IDs.
- These tests cover protocol/geometry/authoring compatibility and real runtime authority/input gates. They do **not** establish Relay connectivity, two-player combat, disconnect recovery, or performance acceptance; those remain pending actual Development player processes.


## Actual Development player attempt — 2026-09-06 22:36 UTC

Saved-arena Development build completed at 22:35:30 UTC with 0 errors and 196 warnings (`BuildReports/OnlineDevelopmentSavedArena.json`). Root launched two real graphics-capable batch players using distinct authentication profiles; evidence is `BuildReports/OnlineProbe/Run-20260906T2236`.

- Simultaneous process startup exposed an exclusive `File.Open(...,FileMode.Open)` read of `EarthMotionLibraryData.mmfeatures` in embedded EAMM. Host logged a sharing violation, then crashed in a tag-query Burst job after incomplete initialization. Shared-read access and uninitialized query guards have now been applied; the second build must verify them.
- Sequential host restart loaded the actual arena and attempted Unity Services initialization. It failed with an aggregate initialization exception before room creation. This is a real service-init failure, not a successful Host/Join. Development inner-exception logging was added to expose its cause on the next build.
- The client's apparently frozen JSON was an early-return reporting omission while waiting for an actual host code. The opt-in probe now reports that wait and caps its own process at 30 fps. Normal launches retain user frame pacing.
- No real room, synchronized match, movement, damage, or disconnect acceptance has passed yet.
