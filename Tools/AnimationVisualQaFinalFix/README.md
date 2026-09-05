# Animation visual QA final staging fix

Copy the two files under `Assets/Elemental/Authoring/Editor` over their matching
project files, refresh once, then run:

`Elemental/QA/Capture Final Animation Actual Game Matrix`

The driver still uses the production motors, input commands, animation graph,
IK, camera controller and real MeshCollider contact. Its temporary uneven surface
is widened from 2.2 x 6 metres to 6.2 x 20 metres, each actor receives a separate
non-overlapping lane, and the initial locomotion segment is shorter. Pit, slope
and mantle features retain the same authored height function and positions.

Idle, walk, stop, both turns, pit, slope and all three ground-cast magic frames
must have `PlanetMotor.HasStableSupport` before capture. The capture session also
checks the sampled PNG telemetry before it can set `completeRequiredMatrix=true`;
any required grounded frame with `grounded=false` changes the report to `Failed`
and is listed in `invalidGroundedLabels`. Mantle phases remain allowed to be
airborne. The existing finally path restores bodies, inputs, bot controllers,
camera tuning, debug UI, probes and time scale.

The previous run at
`BuildReports/EnvironmentAnimationRescue/AnimationVisualFinal/20260905T092527486Z`
is intentionally not acceptance evidence: it timed out in `Approach`, and its
walk/stop/turn telemetry had already entered `Fall` / `FlightIkOff`.
