# EAMM quality report

## Implemented gates

- Pure arbitrary-gravity frame round-trip test.
- 30/120 FPS deterministic database-clock equivalence test.
- Pure impact-lane threshold tests.
- Motion-library validation test.
- PlayMode ownership assertion: EAMM owns neither gameplay root nor foot IK.
- Generated `EAMMQualityLab` authoring command with flat ground, 15°/30° slopes, stairs, convex seam, narrow passage and moving-support marker.

## Required capture pass

After a production motion library is assigned, capture player and bot at 30/60/120 FPS for start/stop/reverse/pivot, slopes, stairs, seam, moving support, narrow passage, medium hit, magic interrupt and ragdoll recovery. Compare EAMM on/off with the same camera and input replay. Record root drift, foot drift, pose discontinuities, search time, animation-job time and GC.

Acceptance remains: no EAMM root write, zero steady-state GC, no visible foot-writer conflict, no discontinuity outside authored action transitions, and no more than 10% metric spread between 30/60/120 FPS.

## 2026-09-02 implementation status

The production code pass now contains the unified animation graph, cached per-bone inertialization, planted-foot exclusion, 2D locomotion, front/back recovery selection, bounded slope response and bounded magic reach. The project catalog migration and Animator-tree migration are editor authoring operations and do not rebuild `EarthCoreSlice`.

Per the explicit implementation request, no PlayMode, visual capture, telemetry matrix or performance run was started in this pass. The prior editor import reported no C# compiler diagnostics before the final catalog-bootstrap edit; final visual and runtime acceptance therefore remains pending and must not be described as green.

The first headless Unity verification attempt on 2026-09-02 was blocked before script compilation because the local Unity Licensing Client did not expose `com.unity.editor.headless`. This is an environment gate, not a green test result; the compile/test cells must be rerun from a licensed Editor session.
