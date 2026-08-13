# M8 Online Spike

Status: complete (architecture spike)

## Gate

Evaluate transport through ADR evidence; prove host/server command authority, snapshots, terrain edit replication, predicted preview, bounded correction, and a 2–4 client fixed scenario under latency/loss without divergent canonical terrain.

## Evidence

- ADR 0003 compares Unity Transport/NGO, Netcode for Entities, platform sockets, and the selected transport-independent harness without prematurely adding a networking package.
- `CommandAuthority` validates ownership, time windows, and bounded geometry, then assigns monotonic authority sequence numbers.
- Typed snapshot contracts cover terrain edits/chunk hashes, rigidbodies, characters, fields, phases, objectives, and ragdoll checkpoints.
- `PredictionReconciler` proves soft correction and a bounded snap threshold while cast and terrain previews stay cosmetic.
- `SimulatedTransport<T>` applies seeded latency, jitter, packet loss, queue limits, and explicit drop/debt counters.
- The fixed 30-second 2/3/4-client fixtures converge on one authority stream with packet loss present and no rejected valid commands.
- `ReplayAuditor` records subsystem hashes and reports the first divergent tick, subsystem, expected hash, and actual hash.
- `OnlineSpike.unity` exposes client, accepted, dropped, correction, and queue-debt telemetry through UI Toolkit.

## Acceptance result

- Focused EditMode: 7/7 passed before the cross-cutting replay-audit additions.
- Focused PlayMode: 1/1 passed.
- Production transport remains intentionally deferred; the spike proves stable payload and authority boundaries rather than claiming a shipping socket stack.
