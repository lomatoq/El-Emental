# M5 Air + FieldWorld

Status: complete

## Gate

Implement a sparse fixed-rate FieldWorld, Air velocity/pressure regions, spatial queries, Gust Corridor, Vortex, Lift Column, Air Brake, bounded aerodynamic responses, WindLab, replay tolerance checks, and the target overlap/query budget fixture.

## Delivered evidence

- `FieldWorld` is a capacity-bounded, priority-ordered sparse registry with fixed-rate round-robin update debt and bounded query debt.
- `FieldWorldBehaviour` schedules the world independently of render frames and exposes update/query telemetry plus scene gizmos.
- Gust Corridor, Vortex, Lift Column, and Air Brake are compiled `SpawnField` recipes executed through typed commands, events, and replay recording.
- `AirFieldBody` applies capped drag/lift acceleration from relative air velocity to rigidbodies, including the active ragdoll and lightweight debris.
- `WindLab.unity` contains projectiles, a physical puppet, smoke/line presentation proxies, occluders, and more than sixty aerodynamic bodies; keys 1–4 select its abilities.
- EditMode field tests and four focused PlayMode runtime tests pass. The runtime fixture proves four typed abilities, finite/capped responses, a live WindLab scene, and 100 bodies querying 64 overlapping fields with a 16-region per-body cap and visible debt.
