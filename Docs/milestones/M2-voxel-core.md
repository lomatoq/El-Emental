# M2 Voxel Core

Status: complete

## Deliverables

- Canonical analytic sphere SDF plus monotonic ordered sphere/capsule CSG edits, sparse chunk state, dirty-boundary propagation, FNV chunk hashes, and versioned binary saves.
- Replaceable mesh-cache contract with both block/Burst reference implementations and a continuous SDF surface extractor for the playable planet. The canonical field and edit log remain voxel-owned while the visible/collision shell uses interpolated vertices and gradient normals.
- Separate budgeted render/collider queues, profiler markers, collider debt and risk telemetry.
- `VoxelPlanetLab.unity` runtime cache view.

## Gate evidence

- 1,000 bounded edits keep the sparse chunk set under 100 and 1,000 steady-state density queries allocate 0 B.
- Adjacent chunks sample the same canonical field at their boundary and generate finite normals/valid topology.
- Save/load reproduces samples and chunk hashes; legacy v1 saves explicitly migrate to schema v2.
- Scheduled and synchronous topology counts match; advanced versions reject scheduled stale output.
- The full final suites pass: EditMode 73/73 and PlayMode 23/23.
- The smooth cache mesher now samples each padded chunk grid once and derives cached central-difference gradients instead of repeatedly querying the canonical SDF for every emitted vertex. The latest full-suite edited-chunk fixture measures a 13.11 ms render-queue peak, below the 30 ms regression budget and down from the earlier 36-45 ms captures, while keeping SDF/edit-log authority unchanged.
