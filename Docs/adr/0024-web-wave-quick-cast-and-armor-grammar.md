# ADR 0024: web wave, quick cast and armor grammar

Date: 2026-08-14

## Status

Accepted and implemented for the Earth Core vertical slice.

## Context

The previous ground wave was a polar grid of scaled boxes. Platform fracture reused
scaled rectangular prefabs, fast convex stones could cross a collider transition,
and LMB/MMB/RMB had no unambiguous route for quick cast, structure weaving and armor.
Those failures were visible in the executable and could not be solved by VFX alone.

## Decision

- Ground Wave uses six deterministic radial/spiral topologies. Samples select
  3-8-sided, multi-scale geological cells. Production rendering is instanced in six
  mesh-family batches; pooled convex colliders remain available for analytical crest
  impacts and an aimed cell can still detach as an `IEarthPhysicalTarget`.
- Platforms precompute a 18-28 cell hierarchical Voronoi plan clipped to their exact
  convex gesture contour. Fracture activates every piece collider before retiring the
  solid collider. Wall and platform expose the common `IEarthReassemblableStructure`
  / `IEarthRepairController` boundary.
- MMB circular gestures are measured in viewport coordinates. Clockwise selects
  reassembly progress, counter-clockwise selects disassembly progress. The structure
  selected on press remains the session owner while its generated pieces are latched.
- A short empty LMB tap primes one small earth fragment for 0.42 seconds. A second LMB
  press launches it directly at 30-38 m/s; timeout releases it into ordinary physics.
- RMB keeps tap/continuous/flick semantics. Whole-wall flicks use a direct tangent
  target-speed solve with mass response instead of the generic force cap.
- `Shift+MMB` always owns MMB and starts armor. Wheel input moves a fixed 64-piece pool
  through body armor, dome and orbit. Two confirmed positive overscroll steps emit a
  radial physical burst and a full web wave. MMB release drops the pieces into their
  ballistic shrink lifecycle. Space is absent from this grammar.
- Fast earth fragments keep their convex collider and gain an oriented non-allocating
  box sweep across the previous physics displacement. A swept contact and a subsequent
  collision callback share the fragment's impact-frame dedupe gate.

## Consequences

- Cast-time work is bounded: wave topology has at most 96 cells, platform prefracture
  at most 28 cells, armor exactly 64 pieces and projectile sweep at most 16 hits.
- No wave or armor action edits the voxel SDF while animated.
- Runtime-created meshes are prepared at pool/platform initialization, never at the
  moment of a wave impact or platform fracture.
- The six instanced wave families preserve visual variety through cell topology,
  scale, yaw, chipped tops and six seeded layouts while keeping draw submission bounded.
- Full PlayMode and standalone visual evidence are required before the milestone may
  claim shipping completion; screenshots alone do not establish CPU/GPU percentiles.

## Rollback

The legacy column mesh path remains behind `useLegacyColumnMeshes` for comparison.
Quick cast and armor are isolated sessions and can be disabled by omitting their input
routing without changing canonical wall, platform or fragment physics.
