# Connected Fire Body — integration / verification contract

Two new source files staged in `after/Assets`. Parent owns profile fields, asset reference, backend wiring, compilation, play tests and captures. No existing live file changed by specialist.

## Ownership

`FireCoherentBodyMeshBackend(Transform owner, FireVisualProfile profile)` requires an assigned `profile.CoherentBodyShader` reference. Wire one instance per CPU presentation group only when the opt-in profile flag is true. Call Step(snapshot,delta,camera) after group begin, Clear on Retire, Dispose with the CPU backend. Keep existing cosmetic particle diagnostics and simulation. Recommended parent parcel multiplier .22, continuous body queue2998, particles3000. Decorative column profiles remain opted out.

This is a continuous spatial streamline renderer, not a particle shader variant and not a new simulation authority. Three connected camera-facing ribbons per active node are traced from the node source using `FireCpuField.Sample`, `FireCpuField.Steer`, `FireContactMath.ResolveSwept` and profile speed/lift/drag. Contact patch positions are frozen to the CURRENT snapshot during virtual integration; the geometry is not advanced into the future. This produces an instantaneous field visualization; it is not a temporally stored fluid solver. Shell nodes receive three surface streamlines, not a complete spherical envelope.

## Rendering

900 maximum vertices / 864 triangles per six-node group, one mesh/material/renderer. CPU explicitly bounds the final world-space vertices and overrides renderer world bounds. Width expands inside that actual mesh only. Shader uses continuous arc length for broad traveling bands and edge breathing. No per-parcel core/ring. Finite contact-disc clipping removes ribbon width on the solid side; regular depth rejection prevents drawing through the obstacle. Geometry flow redirects along contacts instead of continuing through the wall. The fragment clip remains a local finite planar approximation matching the existing authority, not arbitrary mesh SDF collision.

Lifecycle fade decreases using supplied canonical delta over profile.MaxLifetime when Draining. Pause produces the same trace and same shader clock. Retired clears immediately. Arrays/NativeArray/index topology/material objects allocate only during admission. Steady-state source contains no array/LINQ/list allocations. Unity/native upload and CPU cost still require measurement; this is not a profiler claim.

## Diagnostics / tests for parent

- Visible, ActiveTriangles, ActiveRibbons, SectionCount, RedirectedSegments, LastStepMilliseconds.
- TryGetCenterlinePoint(ribbon,section,out Vector3): bounded readback without allocations. Assert points inside each finite patch radius stay at or beyond skin + particle radius after contact. Test Direct, Corner, Opening and Moving. Check clipped width in actual frames because centreline tests alone cannot prove it.
- Snapshot pause delta0: identical centreline vertices and fade. Draining: visible after one positive small step, hidden after MaxLifetime. Clear: no triangles/renderer.
- Render Direct-no-bloom before/after: one connected body from source to wall then surface flow, several secondary tongues, no dotted orange cloud. Inspect from camera opposite the wall to prove depth occlusion.
- Run node six/contact eight budget, eight groups and GC profiler. Marker Fire.CoherentBody.Step; old CPU particle marker does not include this class automatically unless parent aggregates it.

No compilation, measured perf, or artistic acceptance is asserted by this handoff.
