# ADR-0015: Earth material and fracture feedback

Status: accepted
Date: 2026-08-13

## Context

Structural physics now owns persistent, repairable pieces, but the previous single earth material could not distinguish a weathered face from a fresh break. World-space projection also made textures slide across moving stones, while switching every voxel chunk to object-local projection would create seams. Impact feedback was spread across legacy events and could over-emphasise bright particles.

## Decision

- Fracture schema 3 bakes separate compact collider meshes and duplicated render vertices with an explicit RGBA face contract. Exterior and interior surfaces are separate submeshes/material slots; validation rejects pieces missing either classification.
- `SG Earth Master` uses object-local, scale-correct triplanar projection for dynamic bodies and one planet-local projection frame for all voxel chunks. Voxel and loose-stone materials are separate assets.
- `EarthMaterialProfile` owns palette, metre-scale frequencies, cavity/dust response, smoothness, normal strength and the local low-detail shader variant. Presentation values never enter structural simulation.
- Rich `EarthImpactEvent` values drive bounded dust/chip counts, restrained exceptional-energy motes, camera response and pooled URP surface scars. Persistent scars live until ring-buffer eviction. Cosmetic effects do not alter the impact or bond damage that produced them.
- Production stays on URP Forward. Forward+ and GPU Resident Drawer are opt-in only after scene-specific CPU/GPU evidence; dynamic structural pieces are not candidates for static resident drawing.

## Consequences

Moving stones no longer swim under their texture, voxel chunk boundaries share one projection frame, and newly exposed fracture faces read differently without duplicating physical state. Render meshes are larger than collider meshes by design. The decal renderer feature and fixed pools add bounded presentation cost, with Native High/Low profile assets defining explicit ceilings.
