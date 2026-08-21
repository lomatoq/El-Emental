# Graphics V5 — Slice 1: RUMBLE-style visual proof

## Status

This branch is an isolated visual vertical slice based on `codex/earth-core-polish`.
It does **not** claim that the existing Earth gameplay scene has been visually approved.
The slice is accepted only after fresh Unity Game View captures from the generated
`RumbleLookdevLab` scene.

## Open the proof

The Editor bootstrap creates and opens the scene once the new shader has imported:

`Assets/Elemental/Content/Scenes/RumbleLookdevLab.unity`

Manual commands:

- `Elemental/Graphics V5/Build and Open Slice 1`
- `Elemental/Graphics V5/Open Rumble Lookdev Lab`
- `Elemental/Graphics V5/Rebuild Baked Rock Library`
- `Elemental/Graphics V5/Apply One-Sun Policy To Open Scene`

## Play-mode controls

- `1` — explore lens, almost no depth of field
- `2` — near focus
- `3` — mid focus
- `4` — far focus
- hold `C` — charge lens: visible rack focus, longer focal length, wider FOV and tiny render-only vibration
- `F1` — day
- `F2` — sunset
- `F3` — night
- `Tab` — seam-debug modes: metric position, normals, triplanar weights and baked face data
- `Space` — raise the five-piece wall with layered source/ground/gravel feedback
- `H` — heavy impact with pressure dust, rolling dust and physical debris
- `R` — reset the wall

## What is materially different from V4

### Baked rocks instead of noisy runtime blobs

`RumbleRockMeshFactory` starts with a convex block, clips it with large deterministic
planes, preserves a stable base and then builds actual inset face, edge and vertex
bevel polygons. The resulting twenty meshes are ordinary `.asset` meshes generated
in the Editor. Runtime stone selection can later use this approved library without
rebuilding hero geometry every spawn.

The first corpus contains:

- 8 boulders
- 4 slabs
- 4 wedges
- 4 pebbles

The EditMode corpus test validates deterministic output, non-degenerate geometry,
a stable base and distinct signatures.

### Softer material hierarchy

`RumbleRockLit` deliberately avoids the previous all-frequency procedural noise.
It uses:

- large baked planes and real bevel normals as the primary read
- one low-frequency macro variation
- very weak optional metric triplanar texture modulation
- broad wrapped diffuse
- weak broad specular
- soft shadow tint
- stable object-space mapping for loose rocks
- one shared planet-relative metric frame for independent terrain tiles
- screen-dither fade for visible-debris retirement

### One-sun court

The proof scene contains one directional key light and no persistent point, rim or
magic fill lights. `RumbleLookdevSceneGuard` disables legacy camera-lookdev volumes
and extra lights if old global rescue installers attempt to add them to this scene.

### Explicit DOF

The Volume Profile is a committed, inspectable asset generated under
`Content/GraphicsV5/Profiles`; it is not a hidden runtime-created profile.
The HUD reports focus distance, aperture, focal length and FOV. Near, mid and far
states exist specifically to make a missing or incorrectly routed DOF obvious.

### Seam truth court

The ground is four independent mesh tiles. Border vertices and normals are sampled
from the same continuous metric height field, and all four use the same planet-frame
material domain. `Tab` exposes the mapping and normal channels rather than asking an
artist to guess whether a visible line comes from geometry, normals or material
coordinates.

### Layered Earth feedback

The proof separates:

1. dense source pressure dust
2. slower ground-hugging dust
3. weighted gravel
4. physical hero debris

Hero debris remains physical until it sleeps, then sinks and dither-fades before
being destroyed. It is never pooled in mid-air or removed while fully opaque.

## Evidence required before integration

The slice remains draft until all of the following are attached to PR #2:

1. day, sunset and night fixed-camera captures
2. near/mid/far DOF comparison
3. seam-debug traversal across all four ground tiles
4. twenty-rock contact sheet
5. wall emergence in real time and slow motion
6. heavy impact and debris retirement in real time and slow motion
7. Game View proof that there is one enabled Light only
8. Windows and Apple Silicon CPU/GPU frame captures
9. Unity EditMode result for `RumbleRockMeshFactoryTests`

Only after visual approval should the material, rock library, lighting policy and VFX
recipes be integrated into `EarthPolishLab` and the gameplay systems.
