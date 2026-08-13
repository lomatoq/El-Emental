# ADR-0004: Art-directed Earth walls

Status: accepted  
Date: 2026-08-12

## Context

Line Wall originally committed a chain of vertical and horizontal additive SDF capsules. On the smooth planet this produced an organic extrusion, exposed the final volume during drawing, dirtied several chunks, and caused visible frame spikes. The desired Earth language is closer to a deliberate rectangular stone construction with restrained chipped edges.

## Decision

- The pointer preview presents only a clipped ground footprint using small, dim pebble markers.
- A committed Line Wall acquires one object from a fixed-capacity runtime pool.
- The wall uses an art-directed unit mesh, transform scaling, and one box collider; it does not mutate the voxel SDF or request chunk remeshing.
- `MagicCommand` remains the replay authority and emits `WallRaisedEvent`. Pull Rock and impacts remain canonical terrain edits.
- Physical wall length is capped at 22 m, wide enough for the playable camera's visible planet chord. Height and thickness remain executor-owned balance parameters and are not revealed in the footprint preview.

## Consequences

Line Wall becomes cheap, readable, and directly customisable through its mesh/material. Voxel save files no longer need to encode its shape as many terrain edits; full world persistence must restore active wall-pool state from the command stream or a later wall snapshot section. Preview/commit validation now compares footprint endpoints, not hidden volume.
