# Loose stones and oblique arena fragments — 2026-09-04

Follow-up: the original direct-collider tests missed a cursor-selection failure.
See [arena floor acquisition fix](ARENA_GRAVITY_ACQUISITION_FIX.md) for the later
user-reproduced case and full screen-point regression.

Scope: dirty working tree on `d2174eded114dd022e4a9c442abadda7a0e44555`.

## Reproduced failures

`BuildReports/LooseStones/Before/LooseStonePlay.json` (UTC 12:37:39): 3/5 passed.
The target query rejected a valid sleeping fragment solely because its ancestor
arena was camera-suppressed. Three fallen bodies stayed awake after six seconds:
the radial-gravity adapter called AddForce every fixed tick, waking them again.
Wall/platform fracture loops also applied fallback gravity to pieces already
driven by GravityBody, producing two acceleration authorities.

## Implementation

- Resolve the actual physical target before filtering a camera-suppressed arena.
  A valid loose descendant remains selectable; intact suppressed structures still
  fail this filter. Explicit target locking and capabilities remain in force.
- Pure `EarthBodyRestState` requires sustained quiet support for 0.6 seconds,
  linear speed below 0.07 m/s and angular speed below 0.12 rad/s. The runtime
  adapter derives support from contact normals, ignores moving supports, and
  lets PhysX sleep the body. Unchanged radial gravity no longer wakes it each
  tick. Impact, grip and removal of support can wake it normally.
- Wall/platform fallback acceleration runs only when no operational GravityBody
  owns gravity. There are no frozen constraints, kinematic conversions or per-frame
  transform snapping in the rest fix.
- Convex fracture cuts use deterministic oblique planes. The render mesh has
  broader chipped edges, bounded inside its own collision cell. Primary arena
  detachment keeps the authored piece; subsequent splitting partitions that
  piece's actual convex volume. No generic oversized replacement stones.

## Verification

`ContainedFractureEdit.json` (UTC 12:43:59): **12/12 passed**, including sustained
rest/airborne/motion reset and 2/3/4-way rectangular-source angled-face coverage,
closedness, containment, volume and recursive splitting.

`LooseStonePlay.json` (UTC 12:44:53): **5/5 passed**. All three fallen stones
sleep, position/rotation drift is zero over 60 physics ticks, all three wake and
rise on explicit gravity grip, and removal of support makes the stone fall.
The hidden-parent regression and existing one-target/session tests pass.
`LooseStones/Rest.json` records the result; its final-frame gravity recorder
value is unsampled (zero), and is not a performance-cost claim.

The separate production fracture run and visual capture are recorded in
`CONTAINED_FRACTURE_FIX.md`. These scoped tests do not upgrade whole-project/M11
acceptance or resolve the older wider-suite failures in `HEAD_ARMOR_FIX.md`.

## User state

Saved scene and user edits were retained. Wave profile SHA256 before/after:
`CDF2D3C2B546204AE088EAAFD1E4156B51B2DAC4947BCF0FAF436FA30B82BE1F`.
User values: rise 0.4 s, settle 0.07994324 s, hold 0 s, retreat 0.2398297 s,
speed 8.54 m/s. Backup: `BuildReports/LooseStones/Before/UserWaveProfile.asset`.
