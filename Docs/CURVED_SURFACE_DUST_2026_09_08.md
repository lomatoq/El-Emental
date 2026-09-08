# Procedural wind dust and shared ordinary-dust tint — 2026-09-08

Uncommitted main, base 1235579. The user's WallcoeurGroundDust.mat remains byte-for-byte unchanged (SHA256 7A5612DA965F25323079852A11936A5C6A98A98F8206CD460F05A5E68C1EF4D9).

The first curved dome implementation was rejected by the user: static rounded carriers still looked like moving cushions. That approach is superseded by shallow asymmetric deforming wisps.

## Current motion and rendering

- One reusable 81-vertex / 128-triangle shallow asymmetric carrier per emitter; no per-particle meshes. StableRandomXYZ supplies persistent individual shader phases. Shader waves deform the carrier continuously. No return to flat quads or the old 24-degree tilt.
- Width and length vary separately. Speed factors vary from 0.45 to 1.5 within the coherent spatial gust field. Each particle has independent breathing and a lifetime-based rise/settle envelope. Most wisps remain low; seam wisps can rise farther.
- Velocity, rotation, support normal and support-plane changes interpolate continuously. Rays that find an obstacle top more than 0.55m from the previous floor retire the wisp instead of snapping it upwards.
- Lost support previously shortened remainingLifetime to 0.25s, which also jumped the lifetime-driven atlas animation. Retirement now fades particle alpha over 0.3s while its lifetime clock continues normally, then removes the invisible particle.
- The shader advects the soft texture with two staggered phases and normalized crossfade weights. The resetting phase is hidden by its partner. Atlas UVs also deform continuously. The carrier's softly curved boundary and grazing-angle fade conceal hard sheet edges. This is procedural transparent geometry, not a volumetric fluid simulation or true optical-flow reconstruction.
- No material asset colors, density or authored wind profile values are overwritten.

## Ordinary dust color

A hard-coded MaterialPropertyBlock Tint (.93,.78,.59,.8) and Brightness 1.1 had overridden the ordinary dust materials. It is removed. EarthMaterialFeedbackPresenter now follows the profile's SurfDust material (RumbleDustLit): Tint including alpha, Brightness and Night Visibility propagate to the soft, animated impact and fracture layers live. Existing contact depth properties are preserved. The new animated texture/material still supplies its atlas, blending and mask parameters. Wind dust keeps its separate user-edited WallcoeurGroundDust color.

The tint regression clones the source profile/material and changes the copy live, checking all three renderer property blocks; it never edits the user's source asset.

## Evidence

CurvedDustEdit covers mesh UV bounds/curvature and atlas contract. CurvedDustPlay covers live shared tint, production compositor lighting/occlusion, varied widths and speeds, real ground-dust motion and CPU cost. Production captures are in BuildReports/SurfaceWindDust. Final report timestamps and measurements are appended after completion. CPU measurements cover the adapter, not GPU transparency or vertex deformation.
Final validation: CurvedDustEdit 2/2 passed at 11:18:55Z; CurvedDustPlay 3/3 passed at 11:23:49Z (24.4971s). 352 live wisps, speed .376–2.741m/s, width .431–4.377m, 42541 changed pixels, 243 matched unique births moved 1.127m on average in .8s. Whole CPU adapter .9973ms mean /1.6353ms peak. Material SHA256 unchanged and shader errors=false. Low/high production views inspected. Per-birth identity now includes a generation, preventing reused emitter slots from being mistaken for the same trajectory or repeating shader phases.
