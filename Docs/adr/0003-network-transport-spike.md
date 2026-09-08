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


## September 6, 2026: production adapter integration

Decision: use Unity Multiplayer Services 2.3.1 Sessions with Relay, Netcode for GameObjects 2.13.2 custom messages, and Unity Transport 2.7.4. The existing MonoBehaviour/PhysX actor and terrain architecture makes NGO the smallest integration boundary; no Entities migration is introduced. Sessions owns NGO startup. Hosting is authoritative for damage, stun, rigid bodies, structural detachment/repair, ordered terrain edits and match state. Clients send bounded semantic controls and predict their own motor, then consume accepted state and exact generated geometry.

The concrete adapter is now imported under `Assets/Elemental/NetworkingStage2`; two actors have separate owner graphs/executors/pools and share the canonical planet/kernel/query services. Scene installation is a targeted editor operation, preserving the authored arena and manually assigned clips. Compatibility is derived from actual build, world and geometry catalogue contents. Start requires two authenticated NGO peers, both Ready, geometry ACK and rig/terrain readiness. The host publishes one server-time countdown deadline. Cosmetic bones are not streamed.

Validation status at import: **implementation selected; online acceptance pending**. The previous M8 simulated-transport result remains valid only for the architecture spike. SDK semantic compilation, targeted scene installation, actual two-process Sessions/Relay gameplay, cancellation/disconnection, latency/loss and CPU/GC measurements must be recorded before calling the online stage complete. Host migration, late join and reconnect into an active round remain unsupported.

A development-only opt-in probe uses the real frontend Host/Join/Ready entry points and records live peer/readiness/countdown state; it never substitutes mock sessions or bypasses readiness. Passing that probe proves only its recorded connectivity/world/combat interval, not the full toolkit or visual acceptance.

References: [MPS 2.3.1 Sessions](https://docs.unity3d.com/Packages/com.unity.services.multiplayer@2.3/manual/index.html), [NGO 2.13.2](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/index.html), [Transport 2.7](https://docs.unity3d.com/Packages/com.unity.transport@2.7/manual/index.html).


SDK compatibility evidence: the original planned MPS1.2.1/NGO2.7.0 failed inside their package source on Unity6000.5.7f1 (`EndNameEditAction` and implicit `EntityId` conversion are errors). Official registry latest versions were checked against their real source; NGO2.13.2 uses EntityId-aware contact/transport APIs and MPS2.3.1 removes the obsolete Multiplay authoring. Upgrade avoids local package-cache patches or warning suppression. All four integrated Online runtime/editor/EditMode-test/PlayMode-test assemblies compiled against the actual newly resolved Unity Bee SDK references, exit0 and no compiler output. Root Unity tests, scene installation and real service/gameplay evidence are still required.
