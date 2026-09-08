# Fire finite convex surface follow-up

Prepared against the integrated Assets versions on September 7. `before/` contains the two modified source baselines; `after/` mirrors final Assets paths. All work and offline compile output stayed inside this follow-up directory. Unity was not launched or controlled.

## Integration map

Replace:

- `Assets/Elemental/Simulation/Fire/FireDomainState.cs` — adds an explicit Piece member to the existing surface identity, with equality/hash participation and backwards-compatible optional constructor argument.
- `Assets/Elemental/Runtime/Fire/FireSurfaceResolver.cs` — accepts proven convex MeshColliders, maintains a bounded geometry cache and checks exact current mesh contents before publishing; recognizes released wall/arena/platform pieces before their intact parent owners.

Add:

- `Assets/Elemental/Runtime/Fire/FireConvexMeshGeometry.cs` — closed-manifold edge check, supporting-plane convexity proof, coplanar plane deduplication and finite disc clipped against every remaining hull plane. This eliminates artificial triangle-diagonal seams while retaining actual face edges. World-space planes use inverse-transpose normals for nonuniform transforms.
- `Assets/Elemental/Tests/EditMode/FireConvexSurfaceTests.cs` — seven test cases: triangle diagonal, scaled edge footprint, same-count vertex/index mutation, open hull rejection, piece identity isolation, moving pose/generation invalidation.
- `Assets/Elemental/Tests/PlayMode/FireEarthFractureContactTests.cs` — actual `EarthWall` lifecycle source fixture: await emergence, publish intact contact, call `ApplyRockImpact`, reject the old contact, resolve the resulting `EarthWallPiece` MeshCollider using its owner identity, then reject that patch on `Initialize` pool reuse. This is a real API path, not reflection into fracture flags.

No modifications to Earth, its meshes, collider ownership, physics queries, VFX, scene setup, authoring, networking or input.

## Identity and immediate invalidation

Piece identity is the tuple of existing owner kind, owner stable ID, owner generation and owner-local PieceIndex + 1. It is not the collision-prone hashed StableEarthId, and never a GameObject InstanceID. Owner piece slots are stable within their generation. Each individual face remains a separate constraint.

Checks precede every CopyCurrent publication: collider enabled/active; current canonical owner and generation; released/repair validity for arena pieces; current piece slot; current mesh object, vertices and indices; geometry cache version. Missing/disabled/repaired/reused colliders immediately lose their anchors. Mesh mutation does not wait for another discovery, the next physics step or the 0.05 s discovery age. Original box behavior remains intact.

Exact mesh comparison uses preallocated lists/arrays. A later discovery rebuilds changed source geometry and increments its cache version; references to an evicted/rebuilt cache slot fail validation. The cache is fixed at 32 distinct source meshes, each limited to 512 vertices / 1536 triangle indices / eight submeshes. Over-budget or unreadable/nonconvex/open/nonmanifold sources fail explicitly through UnsupportedGeometry, without an invented plane. MeshCacheRebuilds counts cold preparation/rebuild work. No additional PhysX calls were introduced.

## Evidence and remaining gates

`python validate.py` compiled the final overlays with Unity 6000.5.7f1's own Roslyn and current Bee assembly response files. Exit code **0** for Simulation, Runtime, EditMode tests and PlayMode tests. The logs contain no warning/error located in this follow-up source. Existing unrelated project obsolete API warnings remain in the full Runtime compile. Exact outputs are under `compile/`; this is offline C# evidence only.

The seven Edit and one Play cases have **not been executed** by this lane. Coordinator must import, run them, exercise existing production fracture fixtures and collect visuals. Cold convexity/closed-edge validation is bounded but quadratic; steady publication performs exact linear vertex/index comparisons. Neither cost has yet been profiled on shipping hardware, and the fixed 32-mesh cache can churn in scenes with more simultaneous unique meshes.

The subset now covers intact box walls and readable, closed, source-convex MeshColliders with actual existing Earth owners, including released pieces. It does not silently turn nonconvex arena/terrain meshes into convex walls. Terrain has no invented identity or binding. Nonconvex surfaces, unreadable sources and meshes outside the explicit limits remain unsupported; a separate validated patch provider is needed for those surfaces. Source/cooked-hull correspondence, sparse-probe coverage and fast angular motion still require actual Unity visual/runtime acceptance.
