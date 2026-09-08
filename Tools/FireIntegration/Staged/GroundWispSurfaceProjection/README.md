# Ground-parallel wind wisps

Two-file overlay; baseline is the live scalar-depth-fade correction. The custom dust shader reads `_SoftParticleNearDistance` and `_SoftParticleInvDistance`, not the legacy URP vector. That correction was already integrated and passed the actual production Play test at 16:07:58 UTC, but screenshots still showed weak ground wisps.

This follow-up replaces clustered-only upright stretched cards with built-in Quad mesh particles aligned to the sampled surface normal and tangent wind. A card centered 18 cm over the ground previously buried much of its alpha below the surface. Broad surface-parallel alpha sheets remain visible and move through real stone gaps. Size range 1.4–2.5 m, anisotropic 0.85 by 1.8, opacity .42; cap192, emission rates, collision-free particles, impact material and ordinary dust remain unchanged.

Offline Presentation compilation: exit0. Import with integrate_stage.py, refresh, then run Surface Wind Dust Visual Play and inspect on/off captures. This overlay has not yet been visually accepted or measured in Unity.
