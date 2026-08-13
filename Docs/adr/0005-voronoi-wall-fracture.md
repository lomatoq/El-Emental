# ADR-0005: Aspect-aware physical Voronoi wall fracture

Status: accepted  
Date: 2026-08-12

## Context

The first temporary-wall collapse split the rendered slab into a fixed 3×3 grid. A later normalized Voronoi partition removed the straight seams, but non-uniform wall scaling stretched its cells into narrow strips. Its collapse still moved transforms manually, so pieces had no mass, collision or physical response.

Generating and cooking arbitrary fracture meshes at the moment of collapse would create a visible frame spike. Unity also requires moving non-kinematic mesh colliders to be convex.

## Decision

- `VoronoiFractureSolver` remains a pure deterministic half-plane clipping solver in `Elemental.Simulation`.
- It now also partitions a rectangle in world-aspect space and normalizes the result afterward. Applying wall width/height scaling therefore produces chunky cells instead of stretched strips.
- Two bounded Lloyd-relaxation passes suppress needle-like cells without turning the pattern into a regular grid.
- Each pooled wall prebuilds 20 Voronoi cells with two depth layers: 40 closed convex prism meshes. Meshes, convex colliders and rigidbodies are created during pool initialization, never during casting or collapse.
- The solid wall keeps one box collider. On collapse, its renderer and collider are disabled and the cached pieces detach while preserving their world dimensions.
- A radial delay from a lower fracture origin releases the pieces as a crack wave. Piece mass comes from its volume fraction; an outward impulse, the last push direction, bounded lift, torque and local planetary acceleration drive the physical collapse.
- A sufficiently strong `EarthFragment` collision starts the wave at its contact point. A magic shove translates the wall along the surface with drag but never applies a visual lean.
- `EarthWallProfile` is the authored gameplay configuration for emergence duration, automatic collapse delay, fracture-wave time, impact threshold, slide drag/speed, debris free-motion time, shrink duration and fracture force.
- Fragments remain bounded by the existing eight-wall pool. After their free-motion window they keep their rigidbodies, radial gravity, collision and momentum while easing their scale to zero. Collision is disabled only when the piece actually returns to the pool.

## Consequences

The wall breaks into irregular volumetric chunks instead of rows or thin bars. Collapse responds to mass, contact location and its last shove; pieces continue to fall, roll and collide throughout their visible shrink instead of freezing or popping away. No runtime mesh creation or collider cooking happens in the cast/collapse hot path.
