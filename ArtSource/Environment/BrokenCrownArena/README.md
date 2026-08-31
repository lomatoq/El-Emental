# Broken Crown Arena authoring source

`BrokenCrownArena_Working.blend` is the validated authoring file. Do not edit the
generated `FR_*` or `COL_FR_*` objects directly; edit the intact semantic meshes
and rerun `Tools/Blender/bake_broken_crown_arena.py`.

"Validated" means that the semantic split, closed intact and fracture volumes,
sandstone cap submeshes, bond graphs, face masks, convex-collider budgets and Unity import compiler pass. The generated v1
set is import-ready and remains deterministically rebakeable from intact meshes.

## Gameplay split

- `00_STATIC` contains the intact combat floor/base and gate threshold.
- `10_DESTRUCTIBLE_INTACT` contains the gate, wall sections and four columns.
- `20_LOOSE_ROCKS` contains seven independently grabbable authored rocks.
- `30_COSMETIC_RUBBLE` contains two non-authoritative rubble props.
- `90_FRACTURE_BAKED` contains hidden render pieces and convex collider proxies.
- `99_AUTHORING` contains the source camera and light.

The floor/base ignores ordinary damage. Its 36-piece baked set is tagged
`earth_damage_mode=meteor_only` and `earth_trigger=MeteorImpact`; its pieces are
neither foundation-supported nor repairable. Ordinary impact activates only the
gate, walls and columns.

## Wall fracture

Each imported wall object contains several visually separate masonry blocks and
boulders welded by Tripo into one surface. `BrokenCrownArena.wall-sites.json`
stores the deterministic depth-aware masonry sites derived from those masses.
The baker clips 3D power cells against the true wall volumes and creates twelve
macro pieces per wall. Tiny disconnected Tripo slivers are discarded instead of
becoming unstable gameplay bodies.

The intact wall objects and stable `arena_wall_*` IDs are preserved. A later hero
look pass may refine sites without changing the importer or simulation schema.
Uniform whole-wall Voronoi remains rejected because it ignores the quiet masonry
masses and produces glass-like noise.

Lighting is deliberately not baked into the arena materials: no counter-light,
second sun or compensating emission. The project lookdev owns its single
directional key, aerial perspective, focused-state DOF and sunlit dust.

## Safety and regeneration

- `BrokenCrownArena_TripoRaw.blend` is the untouched imported source.
- `BrokenCrownArena_Preprocess.blend` is the last pre-bake working backup.
- The baker is idempotent and removes only objects/collections tagged
  `arena_generated=true`.
- The working file is normalized to a 16 metre arena footprint.

Validate the saved file headlessly:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' `
  --background '.\ArtSource\Environment\BrokenCrownArena\BrokenCrownArena_Working.blend' `
  --python '.\Tools\Blender\validate_broken_crown_arena.py'
```

The expected result is 18 semantic meshes, 90 render pieces, 90 convex colliders,
36 meteor-only floor pieces, eight connected structure graphs, complete
`EarthFaceMask` vertex colors and no collider above 255 vertices.

The Unity package is exported to `Assets/Elemental/Content/Arena/BrokenCrown`.
Use `Elemental/Arena/Rebuild Broken Crown Import` to compile the FBX and sidecar
into eight validated `EarthFractureAsset` files plus
`Generated/BrokenCrownArenaCatalog.asset`. The catalog keeps the intact floor
ordinary-damage-proof and declares only `MeteorImpact` as its activation cause.
The runtime, not renderer visibility, remains gameplay authority.
