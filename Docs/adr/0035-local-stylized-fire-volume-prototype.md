# ADR 0035: Local stylized Fire volume prototype

Status: proposed, 2026-09-09. Visual and performance acceptance pending.

The user rejects the camera-facing coherent ribbon body and requests convincing
stylized volume. A wider ribbon candidate is not accepted. Prototype a bounded
local raymarched density/emission body for the playable capsule stream using the
existing Fire presentation snapshot, source range, finite contacts and lifecycle.
FireWorld retains all simulation, damage and timing authority. No new engine
framework, fullscreen pass, fluid solver or gameplay readback is introduced.

Explicitly select this representation and record its active diagnostics. Legacy
wall/shell paths, if retained, must be named honestly; their ribbon geometry
tests do not validate the volume renderer. Pool proxy/material resources and
preserve retirement/disposal behavior. Camera-inside and scene-depth handling
are required. Reuse existing light budget.

The research, alternatives, art criteria and bounded experiment are recorded in
[the research note](../research/STYLIZED_VOLUME_FIRE_2026_09_09.md).
Adoption requires native moving visual evidence on the actual character and
standalone GPU/allocation results. Revert the prototype if those gates fail;
do not declare a rendering algorithm itself to be visual acceptance.
