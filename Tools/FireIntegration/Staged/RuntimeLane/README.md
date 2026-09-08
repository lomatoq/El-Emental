# Fire runtime lane — integration map and verification status

All paths under this lane's `Assets/` mirror their eventual project paths. No existing Assets source, assembly definition, scene, material, gameplay/input routing, network state, or Earth authority was edited by this lane.

## Files and dependency direction

- `Assets/Elemental/Simulation/Fire/FireDomainState.cs`: pure typed group/surface identities, field nodes, copied settings.
- `Assets/Elemental/Simulation/Fire/FireWorld.cs`: bounded admission, generation-safe pool, six nodes/eight contacts, atomic validation, density normalization, scaled Active/Draining/Retired lifecycle, fixed temporal tail bounds, snapshot copy.
- `Assets/Elemental/Simulation/Fire/FirePresentationSnapshot.cs`: caller-owned fixed transfer storage; mutating a copied snapshot cannot change canonical state.
- `Assets/Elemental/Simulation/Fire/FireContactMath.cs`: provided swept finite-plane reference plus finite input guards; Mathematics only.
- `Assets/Elemental/Runtime/Fire/FireWorldBehaviour.cs`: explicitly injected GravityWorld; physics queries at FixedUpdate; scaled clock at Update; immediate geometry validation when CopySnapshot publishes; no renderer reference.
- `Assets/Elemental/Runtime/Fire/FireEnvironmentAdapter.cs`: at most six wide-domain probes and twelve total PhysX calls per group/tick, preallocated overlap/hit buffers and disabled query sphere, nearest hit selection, initial overlap handling and conservative coarse blocking on saturation/unsupported geometry.
- `Assets/Elemental/Runtime/Fire/FireSurfaceResolver.cs`: exact finite inscribed discs on orthogonal box faces; local anchors, point/angular velocity, read-only current EarthWall/Platform/Arena owner identity checks.
- `Assets/Elemental/Runtime/Fire/FireSurfaceBinding.cs`: explicit stable ID/generation/revision for standalone lab obstacles; owners must notify geometry mutation/pool reuse.
- `Assets/Elemental/Runtime/Fire/FireContactCache.cs`: fixed eight anchors, separate faces, known identity/shape invalidation before publication, 0.05 s maximum unconfirmed discovery retention.
- Three EditMode test source files: lifecycle/generation, allocation bracket, atomic validation/snapshot isolation, split intensity, tail expiry, finite fallbacks, moving contact/overlap/edge math, finite box edges, separate corner constraints, generation/revision/resize invalidation, moving anchors.
- One PlayMode test source: actual PhysX sweep + overlap, query bounds, finite contact publication and disabling surface invalidation.

Uses the existing Elemental.Simulation, Elemental.Runtime and test assemblies. Adds no new package, assembly reference or global registry. Simulation has no UnityEngine object dependency. Runtime does not reference Presentation. This is an additive presentation-domain infrastructure seam; it adds no damage, heat, input, moves or network authority.

## Integration API

Configure `FireWorldBehaviour` explicitly with `GravityWorldBehaviour`, copied `FireWorldSettings` and collision mask before Start. Match MaximumParticleLifetime/MaximumSpeed to the visual profile; the defaults are 0.85 s / 24 m/s with a 0.10 s drain margin. An eight-group world admits at most eight total Active + Draining groups.

`TryCreate(seed, energy, node, out handle)` initializes identity and first contact query. `TrySetNodes(handle, nodes, count)` queues author intent for the physics tick. The requested shape remains separate from contact-clipped shape, so an invalidated wall can reopen the requested domain on the next physics tick. `Stop(handle)` stops births through the Draining snapshot without reinitializing existing particles. Repeated Stop does not extend the drain. Do not call `World.TryCreate` directly through the public inspection property; use the behaviour facade so requested node storage and contact caches are initialized.

Presentation owns a reusable `FirePresentationSnapshot`, calls `CopySnapshot` from LateUpdate, and retires its backend if the group is no longer current. Snapshot Time is the scaled frame clock. Patch positions are current transformed anchors: a manually advanced VFX interval must reconstruct start position by subtracting point velocity times its interval before upload, avoiding a second complete movement extrapolation. Normal is frozen within the interval; this is not exact rotational CCD.

For lab BoxColliders, configure `FireSurfaceBinding` with a unique nonzero stable ID. Non-Earth geometry mutations must call `InvalidateGeometry()` synchronously, and pooled incarnations `BeginGeneration()`. Box size/center mutation is also detected directly. Existing EarthWall/Platform/Arena current state takes priority over optional authored binding: destroyed/reused owners cannot continue publishing stale planes. No timer substitutes for those known invalidations.

## Limits requiring explicit acceptance / follow-up

1. **Mesh/convex/planet contact is unsupported in this slice.** UnsupportedGeometry/MissingIdentity/UnresolvedContacts counters expose rejected geometry. The domain is shortened conservatively; no fake plane is created. Production fractured pieces and MeshCollider platforms need canonical piece/generation/revision bindings plus a geometrically validated finite proxy. This does not claim full production arena Fire support.
2. Six sparse probes are an approximation. The 12-call budget includes overlaps and ComputePenetration, so an overlap-heavy tick can process fewer than six probes. Safety counters record saturation; full buffers cannot be treated as complete hit sets. Shape shell/vortex has the same coarse sparse support approximation.
3. Rigidbody-backed surface movement provides exact sampled point/angular velocity. Transform-only moving obstacles expose zero velocity and are not accepted as moving-surface fixtures; configure a Rigidbody.
4. Group energy remains a copied canonical scalar; field densities are normalized artistic weights. Rendering spawn rate must remain per-group, not multiplied by node count. There is no heat/damage/network command integration in this seam.
5. Bounds retain 16 preallocated time bins of emitter history; expired emitter paths are removed. Bounds remain conservative with maximum speed times maximum tail lifetime; transparency/performance must still be measured.
6. CopySnapshot revalidation currently performs only analytic / direct owner checks, no additional PhysX calls. Known fracture/generation/revision/disable changes invalidate before buffer publication even between FixedUpdate ticks.
7. All test files are authored source only until the coordinator runs the real Unity runner. Offline Roslyn compilation is separate evidence from Unity graph/shader import, actual test execution, image review and hardware/API performance acceptance. No production or performance gate is claimed by this README.

No Unity editor, test runner, build, branch mutation or remote operation was launched by this lane.
