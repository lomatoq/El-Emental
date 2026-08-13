# ADR-0014: Fracture and Reassembly V2

Status: accepted
Date: 2026-08-13

## Context

The original Earth wall generated a visual Voronoi split at runtime and inferred most structural state from active GameObjects. That representation can make a wall fall apart, but it cannot reliably distinguish persistent structural mass from disposable debris, find supported islands, restore exact provenance, or reassemble the original object. Timed collapse also makes an untouched wall fail without a physical cause.

The Earth Core polish requires cause-driven fracture, stable repairable pieces, deterministic reassembly and bounded runtime cost while preserving the repository boundary between pure simulation and Unity adapters.

## Decision

- Authoring produces pre-fractured structural assets. Each asset stores an intact proxy, bounded piece/collider meshes, stable piece and bond IDs, rest transforms, material metadata and a pure-data bond graph. Runtime procedural Voronoi generation remains a debug fallback, not the production source of topology.
- Canonical structure, piece and bond phases live in `Elemental.Simulation.Structures`. They never derive from renderer visibility, Rigidbody sleep, joint state or GameObject activation.
- Per-structure runtime storage is caller-owned and fixed-capacity. The hard validation ceiling is 256 structural pieces and 1,024 bonds; capability profiles impose lower active hero-piece budgets. Hot solvers allocate no collections and process input in stable array order.
- Piece endpoints in baked bonds are compact array indices. `EarthPieceId`, `EarthBondId` and `EarthStructureId` retain provenance across pooling, serialization and replay. Zero is invalid. A bond endpoint of `-1` is the structure/world anchor.
- Bond damage is event-driven. An impact is transformed into the structure-local frame and decomposed into tension, shear and compression relative to the baked bond normal. Smooth radial falloff, contact-area weighting and material response scale the result. Baked strength values make foundation and compression bonds tougher; an explicit unbreakable flag is reserved for authored invariants.
- Connected components are solved deterministically from the lowest present piece index. Healthy, damaged and repaired bonds connect pieces; broken and reforming bonds do not. World bonds and explicit foundation pieces mark supported islands. Missing pieces cannot bridge islands.
- Unity Rigidbodies are runtime adapters only. `EarthFractureBatchRunner` wraps damage and island batches in `Elemental.Earth.Fracture.Damage` and `Elemental.Earth.Fracture.Islands` profiler markers without taking ownership of storage.
- Representation has three explicit tiers: intact proxy, persistent structural pieces, and pooled cosmetic debris. A targetable repairable piece cannot silently become cosmetic debris or disappear on a timer.
- Reassembly is provenance-aware and restores bonds toward each piece's exact baked rest pose. One repair session owns a bounded set of pieces from one structure, stages them in a deterministic two-dimensional cloud, and seats them sequentially using a mass-aware bounded PD translation controller plus bounded shortest-arc rotation.
- Repair order starts at a foundation piece or the largest available island, then expands through repaired neighbours using stable graph depth, height, contact area, volume and piece ID tie-breakers. Missing components are never synthesized.
- Piece collisions stay enabled. Only bonded neighbours and static terrain required by an authored embedded seat are ignored temporarily. A pose/velocity settle gate reforms bonds; deterministic jam retries expand staging offsets instead of teleporting pieces.
- Full completion restores the intact proxy and original baked graph. Partial completion preserves the repaired remainder as physical structure state. Interruption releases unwelded pieces back to ordinary dynamics.

## Consequences

Damage and support decisions are deterministic, testable without UnityEngine and independent of visual particle counts. Runtime adapters can activate only unsupported islands and can later rebuild the exact original proxy. Array scanning is intentionally simple and bounded for MVP-sized hero structures; profiling determines whether a precomputed adjacency table is needed later.

The existing wall remains available during migration. The production wall now uses the baked graph for fracture and reassembly; the procedural path remains an explicit debug-only fallback. The repair controller adds bounded per-structure arrays and targeted collision-pair bookkeeping, so capability profiles still cap active hero pieces independently of the pure graph safety ceiling.
