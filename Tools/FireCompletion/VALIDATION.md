# Close-obstacle fire completion — staged experiment

Source evidence: `Logs/FireLab/Captures/Direct-no-bloom.png` captured 2026-09-08 00:26 showed separated tapered leaves and a fan of detached translucent pink ghosts; the preceding color-only change did not change parcel silhouettes.

Three-file patch staged under `after/Assets` with exact live snapshots in `before/Assets`:

- FireCpuFlame: majority (66%) uses two broad overlapping rounded lobes. Minority retains the traveling neck/taper. Body is warm-to-hot from height, so it does not paint a dark rim inside each overlapping parcel. Existing phase and direction data continue to supply variation/motion.
- Visible parcels contract after normalized age .48; squared fade reaches zero at .84. Body fade begins .42, tongues .50. This suppresses old detached translucent ghosts while leaving simulation lifetime and collision/flow authority untouched.
- Fire_Default: fewer larger, less elongated parcels (rate170, width .65–1.15, aspect1.2–1.85). CpuCapacity, lifetimes, lift, drag, speed and substeps unchanged. Current CPU renderer computes billboard bounds from these existing profile values; shader never expands outside UV geometry.
- Fire_CpuMesh: opacity .84, distortion .8; HDR emission unchanged .65. No stronger bloom used to hide silhouette issues.

Validation pending parent integration: shader import, Direct-no-bloom and Moving-no-bloom captures, game column regression (its dedicated body branch remains outside this function), pause/time-scale and collision tests. Whitespace check reports existing EOL notices only. Static code review is not visual acceptance. New broad parcels can still look like separated blobs if the solver's source spacing is too sparse; judge the obstacle-contact footage, not a single favorable frame.
