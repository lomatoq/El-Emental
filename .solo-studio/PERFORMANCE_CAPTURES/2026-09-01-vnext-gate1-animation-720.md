# Performance capture — VNext Gate 1 animation graph

Date: 2026-09-01  
Integration commit: `6543585fcd9cf3ed02c23d1a4fd0e1e56e30f536`  
Unity: 6000.5.7f1  
Result: **PASS**

## Capture contract

The canonical PlayMode acceptance test runs the real external PlayableGraph on the EarthCoreSlice humanoid for `EarthAnimationGraph.CaptureFrameCapacity`, which is exactly 720 frames. It uses a `ProfilerRecorder` for `Elemental.Character.AnimationGraph`, the graph's allocation recorder, and a current-thread allocation window.

Evidence files:

- `TestResults/VNext-Gate1-PlayMode-Final12.xml`
- `TestResults/VNext-Gate1-PlayMode-Final12.log`
- `Assets/Elemental/Tests/PlayMode/EarthAnimationGraphRuntimeTests.cs`
- `Assets/Elemental/Presentation/Animation/EarthAnimationGraph.cs`

## Asserted results

Because `CanonicalRigRuntimeTogglePreservesStateAndRestoresLegacyGraph` passed, every hard assertion below held for the same 720-frame window:

| Metric | Result |
| --- | --- |
| Active graph Update samples | 720/720 |
| `AnimationScriptPlayable/IAnimationJob` evaluations | 720/720 |
| appended RigBuilder synchronization | 720/720 |
| profiler-marker frames | 720/720 |
| capture samples / graph-active frames | 720/720 |
| topology-failure frames | 0 |
| graph hot-path allocation samples | 720 |
| graph hot-path frames over zero bytes | 0 |
| graph total managed allocation | 0 bytes |
| graph maximum per-frame managed allocation | 0 bytes |
| isolated current-thread frames over zero bytes | 0 |

The same test also passed two OFF -> ON -> OFF cycles, state/time and pose continuity, downstream rig ordering, parameter/layer handoff, legacy graph reconstruction, and stale-handle rejection.

## Scope limit

This Gate 1 capture proves topology, execution, instrumentation, and zero steady-state managed allocation for the A1 hot path. It is not the final CPU/GPU p95 budget or the 20-minute soak; those remain required at final integration with all High-profile systems enabled.
