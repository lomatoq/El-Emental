# SONIC rolling-replan correction

This patch is staged outside `Assets`; it has not been imported or run by Unity.

The production-actor preview was accepting a new plan every 0.1 seconds, replacing
the active trajectory with frame zero and restarting its eight-frame blend. With
the observed 24-frame prediction this repeatedly exposed only the first few source
frames, explaining the upright numeric anatomy pass and visually inactive boxing.

The correction follows NVIDIA's deployment contract:

- the 4-frame planner context starts at the current generated motion plus the
  reference two-control-frame look-ahead (1.2 native 30 Hz source frames), then
  samples four consecutive future source frames;
- an accepted plan consumes the prefix elapsed while inference was pending rather
  than restarting at frame zero;
- the outgoing trajectory continues advancing during the eight-generated-frame
  crossfade instead of blending from a frozen pose;
- walk and boxing use the documented 1-second periodic cadence, while run uses
  0.1 seconds; short returned buffers schedule early when eight frames remain;
- all documented 6..16-token horizons are allowed, rather than forcing every plan
  to the shortest 24-frame horizon;
- a mode change keeps the currently visible trajectory until the generation-safe
  replacement arrives.

The model remains opt-in and experimental. This correction requires a fresh
production preview with visible limb excursion before it can be accepted.

Primary references:

- https://github.com/NVlabs/GR00T-WholeBodyControl/blob/main/docs/source/references/planner_onnx.md
- https://github.com/NVlabs/GR00T-WholeBodyControl/blob/main/gear_sonic_deploy/src/g1/g1_deploy_onnx_ref/include/localmotion_kplanner.hpp

The accompanying pure tests verify look-ahead context indices, consumed-prefix
alignment, mode cadence, depletion scheduling, and the prediction-horizon mask.

