# ADR-0011: Continuous Earth vector field, gesture platforms, and landing cushion

Status: accepted  
Date: 2026-08-13

## Context

The release-only RMB shove did not communicate stored force and could barely translate a full wall. Wall fragments could not enter the existing continuous bend path, curved ground strokes were prematurely committed as walls, the Shift + Space wave read as a sparse static grid, and a charged pillar jump had no Earth-authored way to absorb its descent.

## Decision

- `EarthVectorFieldSolver` is the pure mass-aware contract for continuous RMB acceleration. The runtime locks one explicit Earth target, applies a bounded velocity change every physics tick, and adds a separately bounded final impulse on release. Rocks cap at 32 m/s and intact walls at 14 m/s by default.
- Runtime targets implement `IEarthPhysicalTarget`. Extracted fragments, intact walls, wall pieces, platform pieces and authored loose physics targets use the same acquire/release lifecycle. Acquiring a fractured structure piece breaks its remaining bonds and pauses debris shrink until release.
- `EarthStructureGestureSolver` defers wall/platform selection until a useful path exists. A straight path raises a wall; a curved, arched, П-shaped or otherwise nonlinear path builds an automatically closed platform.
- `EarthPlatformGeometrySolver` projects the path into one tangent plane and converts crossings into a bounded outer convex hull. A six-instance runtime pool owns persistent platforms. Each uses one walkable prism collider until a heavy impact swaps it for bounded irregular physical pieces.
- Platform geometry is normalized to one outward-facing winding, its centroid is reprojected to the sampled surface radius, and its chord embed is solved from the farthest hull point. A pooled platform starts fully buried, rises with a restrained mass tremor, and enables its walkable collider late in the emergence.
- `EarthCohesiveStructure` is the shared piece-grip/lifecycle component for walls and platforms. Wall-specific Voronoi bond damage remains in `EarthWall`; the shared component owns whether fractured pieces are currently protected by magic control.
- `EarthPillarWaveSolver` emits at most 96 samples with explicit width, height, start delay, hold duration and crest weight. Row density is derived from physical arc length, but width is capped below spacing so adjacent columns retain readable air gaps. A compact Gaussian-like height envelope creates one crest instead of a raised grid. Each column grows from its surface-anchored base with a smooth overshoot/settle curve and shrinks back below the surface after the crest passes.
- `EarthLandingCushionSolver` predicts contact with the local spherical surface. The runtime shows one pooled column at that point and applies a bounded upward velocity correction without emitting a character impact event.
- Wall render orientation uses the true endpoint chord. Runtime solves the inward shift against all four extruded bottom corners, using the smaller endpoint radius and authored clearance rather than a thickness approximation. While pushed, the wall reprojects its root and upright frame onto the local sphere so its complete lower edge remains buried instead of hovering after a long slide.
- Authored wall clearance includes an extra visible-voxel safety margin. This covers marching-cubes/noise displacement that is not represented by the analytic collision sphere, so the entire lower chord remains visually inside the planet.
- MMB owns one bounded `GravityWell` session around the aimed Earth point. It pulls explicit rocks and structural pieces with mass-independent acceleration, damped orbit and speed limits; sustained focus also loads the aimed wall/platform bonds. Characters and unrelated rigidbodies are excluded.
- Held rectangular Earth blocks use a local-up hover frame, bounded bob, angular damping and a PD restoring torque. They read as heavy controlled stone rather than free-spinning debris.
- Wave samples add deterministic row staggering, bounded lateral/radial breakup and time variation. Presentation swaps the former slab fence for beveled stone variants, crest kick, radial dust/chips/sparks and short light/camera pulses; particle gravity is evaluated toward the planet rather than global world down.

## Consequences

RMB now has continuous readable force without teleportation, small structures move farther than heavy ones, and fragmented Earth remains interactable. MMB adds an explicit Earth-only gravity verb without turning the physics world into an unbounded overlap field. Platforms and the moving wave add no per-frame voxel rebuild. Platform collider cooking occurs only once at a bounded cast event; wave, wall and landing-cushion updates reuse pre-created objects. The platform hull intentionally fills concavities so self-crossing input always produces one stable walkable top.
