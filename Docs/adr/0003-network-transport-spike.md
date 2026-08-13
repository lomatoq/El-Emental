# ADR 0003: Transport-independent authority contracts

Status: accepted for the M8 spike; production transport deferred

## Evaluation

| Candidate | Strengths | Risks for this project | Spike decision |
|---|---|---|---|
| Unity Transport + Netcode for GameObjects | Direct MonoBehaviour integration, conventional host/client workflow | Prediction and large custom sparse-state replication need substantial bespoke layers | Viable production candidate |
| Netcode for Entities | Built-in server authority, snapshots and prediction | Would introduce an ECS architecture before profiling proves it is needed; current PhysX/MonoBehaviour runtime would need a broad migration | Do not adopt in M8 |
| SteamNetworkingSockets/custom relay | Flexible transport and P2P/relay options | Platform/service coupling and more protocol work | Evaluate when distribution platform is selected |
| Transport-independent simulation harness | Deterministic latency/loss testing, no package lock-in, proves payload and correction boundaries | Not a shipping socket transport | Selected for M8 architecture gate |

## Decision

Canonical online contracts live in `Elemental.Simulation.Networking` and do not depend on a transport package. Host/server authority validates ownership, tick windows and geometry; assigns global command/edit ordering; and publishes typed snapshots. The in-process `SimulatedTransport<T>` exercises 2–4 clients under deterministic latency, jitter, packet loss and queue budgets.

A production adapter may target Unity Transport/NGO after the spike, without changing command, terrain edit, field summary, rigidbody, character or objective payloads.

## Prediction boundaries

- Cast preview remains client-local.
- The local motor may reconcile through soft corrections and bounded snaps.
- Terrain preview is cosmetic; compact ordered CSG edits and chunk hashes are authoritative.
- Large fragments are server-spawned; fields replicate low-frequency summaries/events.
- Thermal/fluid corrections are region/phase events, not full rollback.
- Ordinary ragdoll snapshots carry root/mode/key errors; transition/correction checkpoints may request full pose.
