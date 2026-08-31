# ADR 0031: Broken Crown arena authoring and meteor-only floor fracture

Status: accepted (2026-08-29)

## Context

The Tripo Broken Crown arena arrived as eighteen anonymous meshes at an
approximately 0.82 metre footprint. The combat floor and base were one open mesh;
the gate, walls, columns, loose stones and rubble had no stable gameplay roles.
The existing Earth fracture runtime expects baked 3D pieces, stable provenance and
bounded convex colliders rather than renderer or Rigidbody state as authority.

The combat surface must remain stable during ordinary Earth magic and impacts.
The requested exception is a rare meteor strike that can destroy the entire floor.

## Decision

- Normalize the authoring source to a 16 metre footprint and preserve untouched
  raw plus pre-bake backups.
- Keep `Arena_FloorBase_INTACT` and `Arena_GateThreshold_STATIC` as ordinary
  indestructible collision/presentation proxies.
- Permit the floor/base proxy switch only after the typed `MeteorImpact` cause.
  Its dormant baked representation has 36 closed 3D render pieces and 36 convex
  collider proxies. Those pieces are non-repairable and have no foundation bonds.
- Bake v1 `Impact` sets for the gate and four columns with deterministic
  architectural/column plane profiles. They are closed, bounded, visually
  reviewed and may be rebaked without changing stable structure IDs.
- Segment each wall into twelve large masonry chunks with deterministic,
  depth-aware watershed sites and weighted 3D power cells clipped to the true
  source volume. This separates the wall's major masses and avoids a uniform
  whole-wall Voronoi pattern. Tiny disconnected Tripo slivers are cosmetic and
  are not promoted into physics bodies.
- Preserve `BrokenCrownArena.wall-sites.json` as the editable wall-fracture input.
  A later hero-art pass may refine only these sites; the intact objects, stable
  structure IDs and runtime activation contract remain unchanged across rebakes.
- Keep seven large stones as independent grabbable targets. Route their shatter
  through the existing destructible decor/debris path rather than adding a second
  structural graph for every rock.
- Keep two tiny rubble props cosmetic and non-authoritative.
- Store stable structure IDs, piece IDs, rest transforms, seam metadata, collider
  references, activation mode and trigger as Blender custom properties. Generated
  pieces stay hidden while the intact proxy is active.
- Export one FBX plus a JSON fracture sidecar. The Unity editor compiler creates
  eight schema-v3 `EarthFractureAsset` files and one
  `EarthArenaFractureCatalog`, and runs `EarthFractureValidator` before saving.
  Visibility, object activation and Rigidbody sleep do not decide canonical
  destruction.
- Do not bake a counter-light, second sun or atmosphere compensation into the
  arena materials. Lookdev owns the single directional key, aerial perspective,
  focused-state DOF and cheap sunlit dust.

## Consequences

Ordinary play retains one stable combat surface and avoids activating dozens of
floor bodies. A meteor can still produce the requested arena-scale signature event
through a precomputed bounded swap. Architecture remains locally destructible, and
loose rocks remain useful Earth-magic material.

The Blender artifact and Unity interchange are import-ready v1 content. Each wall
has twelve coherent macro pieces, while gate and columns retain simpler,
silhouette-preserving break sets. The generated catalog records the floor as
ordinary-damage-disabled and `MeteorImpact`-only. Its dormant virtual support
bonds satisfy the immutable graph contract; the scene integration adapter must
break them atomically when it swaps the intact meteor floor proxy.

## Evidence

`Tools/Blender/validate_broken_crown_arena.py` reopens the saved working file in
Blender 5.2.1 LTS and reports:

- 18 semantic meshes;
- 90 baked render pieces and 90 collider proxies;
- 36 floor pieces with the sole trigger `MeteorImpact`;
- ordinary floor damage disabled;
- 16.0 metre floor width;
- maximum convex collider complexity of 86 vertices under the 255-vertex gate;
- eight connected bond graphs and complete `EarthFaceMask` vertex colors;
- zero validation failures.

Unity import additionally rebuilds eight fracture assets and validates their
schema, manifold colliders, face masks, rest seams, foundations and connected
supported graphs before writing `BrokenCrownArenaCatalog.asset`.

## Rollback

Discard the working file and reopen `BrokenCrownArena_Preprocess.blend` for the
scaled semantic state or `BrokenCrownArena_TripoRaw.blend` for the untouched
import. Rebuilding the Unity import deterministically replaces only the generated
catalog/fracture assets; the intact source and stable IDs remain unchanged.
