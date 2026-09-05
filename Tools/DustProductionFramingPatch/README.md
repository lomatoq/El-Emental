# Production dust framing correction (staged)

The completed `20260905-104115-528` capture proved immutable particle state and
day/dusk/night phase changes, but it did not prove the dust appearance. Its report
records the impact at viewport `(0.50, -0.314, 12.78)`, outside the bottom of every
image. The camera looked at `y=58.81` while the generated impact was `y=53.53`.

The full `DustProductionVisualQa.cs` candidate and its companion diff make the QA helper derive the impact from an
actual upward-facing, static arena ray hit just inward of `FRAME_outer_arch_02`.
The transient production camera is placed 4.2m inward and 1.8m above that surface,
looking through both clouds toward the column. Before particles are emitted, the
helper requires the impact and both cloud origins to lie inside viewport x/y
`[0.15, 0.85]` with positive depth and rejects any collider between the camera and
either cloud. Failure aborts and restores state instead of producing misleading
PNGs.

This is a Tools-only source diff. It has not been copied to `Assets`, compiled or
run. After integration, rerun
`Elemental.Authoring.Editor.DustProductionVisualQa.Run()` in ready Play Mode. Accept
only `status=Captured`, `sameParticleLayout=true`, three matching layout hashes,
three in-range `impactViewport` values, and images which visibly contain both dust
clouds plus the column/base context. Pixel brightness across phases is visual
evidence; the isolated `DustCompositing` shader test remains the numeric light gate.
