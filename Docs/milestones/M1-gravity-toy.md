# M1 Gravity Toy

Status: complete

## Deliverables

- `IGravityField`, finite/clamped `PointPlanetGravity`, isolated `GravityWorld`, and a Rigidbody adapter using `ForceMode.Acceleration` with global gravity disabled.
- Force-driven capsule motor with tangent movement, gravity-aligned grounding/adhesion/jump, air control, smoothed orientation, and local-up camera frame.
- `GravityToy.unity` with one planet and 32 dynamic bodies.
- Replayable input and two-world circumnavigation fixture covering poles, antipodes, grounding, jump, more than 300° travel, and finite state.

## Gate evidence

- 10,000 gravity samples allocate 0 B after warm-up.
- High-speed `ContinuousDynamic` collision regression reaches the planet, records contact, remains outside the collider, and stays finite.
- The full final suites pass: EditMode 73/73 and PlayMode 23/23.
