# ADR 0025: Earth Core V3 volumetric fracture and authoritative action grammar

- Status: Accepted
- Date: 2026-08-14

## Context

The V2 slice had several individually functional input paths, but ownership was split between `PlanetInputReader`, `MagicInputController`, and telemetry-only intent resolution. Platform and wall presentation could also collapse into full-height extruded 2D cells, producing the repeated “straw” silhouette. Fixing individual visuals without fixing ownership and topology repeatedly reintroduced the same failures.

## Decision

`EarthActionRouter` is the canonical owner of overlapping Earth input. Its runtime adapter is the only component allowed to commit `Shift+Space` wave/resonance, `Shift+MMB` armor, `Shift+W` surf, MMB gravity/repair, and ordinary Space mobility. The 0.15 second Shift+Space chord is deterministic and tested in the pure simulation assembly.

Walls and platforms use deterministic precomputed three-dimensional power/Voronoi cells. Sites occupy X, Y, and Z, cells are closed convex polyhedra, shared planes produce matched boundaries, and cooking occurs while the structure is created or authored—not on impact. Runtime damage breaks local bonds and releases unsupported islands. Supported foundation pieces stay physical and may receive later damage.

Earth targets share capabilities and stable generation handles. A fixed MMB session subscribes to fracture activation in the same physics tick. Drawing fixes one surface handle and local plane for the complete gesture; only stable top faces allow platforms, while stable side faces allow perpendicular walls. Attached child structures retain a physical support handle when the parent fractures.

Resonance, the 64-segment Humanoid armor shell, and the surf plough are separate bounded sessions executed by the router. They use pooled physics bodies, ignore the caster until safe separation, and do not rebuild the voxel SDF every frame. Armor uses a dedicated broad beveled tile mesh, follows bone axes, covers the head explicitly, and keeps both controlled and released plates out of the camera obstacle mask.

## Validation

- `EarthVolumetricFractureTests`: closed topology, shared faces, at least three spatial layers, volume conservation, and volume/aspect distribution.
- `EarthActionRouterTests`: priority, chord exclusivity, and ordinary Space isolation.
- `EarthResonanceSessionTests` and `EarthSurfSessionTests`: nonlinear charge/lifetime/speed contracts.
- `EarthPolishLab`: editor-only golden path with reset, deterministic control wall, local/heavy impacts, and live input/session telemetry.
- Full EditMode and PlayMode Test Runner passes remain release gates. V3 does not require a Windows player build.

Latest acceptance on 2026-08-15: EditMode `236/236` and PlayMode `90/90` passed. The runtime gate now includes aimed fire from an expanded armor shell, release-layer transitions and zero caster recoil, plus the body-readable shell, filled resonance dome, web wave, quick-stone path, constructed-surface drawing and production-player plough. `EarthPolishLab` enters Play Mode without runtime exceptions; the local DX11 editor Game View capture remains white, so this pass does not claim a fresh visual frame. No Windows build was run by design.

Armor follow-up acceptance adds outward-winding validation, 12-way head coverage, bounded inter-plate penetration, and a production-camera regression covering compact armor, the expanded dome, and released debris.
The complete follow-up suites pass `237/237` EditMode and `91/91` PlayMode.

## Camera sightline follow-up (2026-08-19)

Ignoring controlled-magic colliders prevents Cinemachine from pulling into the avatar,
but it does not stop a rendered rear plate from covering the head or torso. The gameplay
camera now evaluates two narrow, hysteretic sightline corridors during `Camera.onPreCull`.
Only armor renderers intersecting those corridors are temporarily suppressed; their
colliders, mass, damage interception and Earth target handles remain active. This keeps
the physical spell authoritative while guaranteeing a readable third-person frame.

## Consequences and rollback

Prefracture increases authoring work and pooled mesh memory, but impact cost is bounded and visual topology is truthful. Surface attachments currently bind to the nearest active support cell; a future solver may distribute foundation bonds over several cells without changing the input or damage contracts.

Rollback is local: disable `EarthActionRouterBehaviour` to restore legacy readers, switch wall authoring back to the prior fracture asset, and omit the V3 session components from M3 setup. The V2 assets and command/replay schema remain readable.
