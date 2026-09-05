# Outer stone columns — September 4, 2026

The user's edited `ArtSource/Environment/OuterStoneRing/OuterStoneRing_Working.blend`
is imported independently from Broken Crown. The shipping `EarthCoreSlice` scene
contains a separate `Outer Stone Ring` root with seven inward-facing columns,
85 structural cells and eight loose, grabbable/destructible stones. The empty mesh
deleted by the artist is excluded. Three artist-moved cells are correctly treated
as loose stones rather than remote structural bonds or foundations.

## Authoring and runtime contract

- `Tools/Blender/export_outer_stone_ring.py` reads current geometry/transforms and
  exports temporary copies through Blender MCP; it does not regenerate fracture
  or replace the artist's meshes. Closed convex collision and supported bond
  graphs are validated before the export is accepted.
- `OuterStoneRing.fbx` and `OuterStoneRing.fracture.json` live under
  `Assets/Elemental/Content/Arena/OuterStoneRing`, with an independent generated
  catalog. Optional sidecar `frame_object` makes piece rest transforms local to
  each column. Existing Broken Crown sidecars retain their model-root frame and
  eight-entry validation through the existing import entrypoint.
- `OuterStoneRingImporter.Import` builds assets using the existing fracture
  compiler. `Place` adds content to the existing scene and refuses to silently
  replace an existing ring. No full M3 scene rebuild is run. A pre-placement scene
  backup is retained in `BuildReports/OuterStoneRing`.
- Each column uses `EarthArenaStructure`, `EarthArenaPiece`,
  `EarthArenaSurfaceProvider`, planet gravity, material feedback and the existing
  secondary-breakup pool. Closed cuts remain capped. The intact collision follows
  the actual hook mesh rather than filling the opening with a bounding box.
- Repair restores the current authored damaged silhouette. Already detached
  decorative chunks remain independent stones; they do not restore an earlier,
  undamaged source model.
- Exterior and interior reference the existing `RumbleArenaSandstone` and
  `RumbleSandstoneFractureInterior` assets. Their contents were not retuned.
- Native Y-up FBX placement is handled separately from the old arena's legacy
  orientation. Each column follows radial ground-up while preserving authored
  tilt. Final projected silhouette-to-arena-floor clearance is 2.5 m (measured
  2.4988–2.5005 m), after accounting for the spherical ground.
- Every sampled bottom-cap vertex sits at least 0.08 m inside the analytic
  planet sphere. Loose rubble instead rests just above ground and is separated
  from column/other-rubble collision, preventing immediate shatter on release.

## Evidence

Working tree on `d2174ed`, Unity 6000.5.7f1; this is scoped content acceptance,
not a wider M11 or performance acceptance upgrade.

- `BuildReports/OuterStoneRingEdit.json`: **2/2 passed**, UTC
  `2026-09-04T14:45:50.1602449Z`. Seven validated assets, sidecar counts, materials,
  gameplay references, cap burial and final clearance.
- `BuildReports/OuterStoneRingPlay.json`: fresh **7/7 passed**, UTC
  `2026-09-05T10:42:17.9513652Z`. Every column accepts impact, every one of the
  85 cells completes grab and exact repair, and all eight loose rocks wake under
  radial gravity and can be reacquired. Fast released cells and thrown rocks
  fracture on their first valid post-separation collision. Initial-overlap
  protection expires after separation. Partial repair seats only cells with a
  path to the foundation, while removing foundations releases every unsupported
  connected island.
- Maximum measured repair call: **0.2842 ms** in Editor, marker
  `Elemental.QA.OuterStoneRing.PieceRepairCycle`. This excludes rendering,
  startup and physics-step costs and is not a frame-time benchmark.
- The first loose-stone check exposed initial collision overlap and failed;
  seating/contact separation was corrected before the final passing run.
- Existing `EarthArmorPiece` missing-required-component warnings still occur
  on scene load; these predate this content change. No new column compiler
  warning remains. A transient Unity editor GUID mismatch also occurred during
  import/domain reload; final catalog and gameplay validation passed.

Re-run the focused menus under `Elemental/QA/Run Outer Stone Ring ... Tests`.
The test launcher restores the saved production scene after Play Mode. The
legacy Broken Crown runtime test now scopes its eight-structure assertion to
the arena root; its unrelated shadow expectations remain unchanged.

## 2026-09-04 artist normal fix and intact shading validation

The artist corrected fracture-piece shading in the live Blender source using
Smooth by Angle geometry-node modifiers after the existing bevels. The exporter
now copies each original object and its mesh, preserving the complete authored
modifier stack, node-group settings, order and render visibility for both standing
pieces and loose rubble. The FBX export evaluates those modifiers. Original
artist meshes and shared node groups are not edited by this workflow.

Generated intact proxies use raw closed solids for the union, then the existing
8 mm, one-segment bevel. Their inherited custom-normal layers are removed.
After the final coordinate transform, significant faces are smoothed across
angles up to 30 degrees, matching the authored Smooth by Angle treatment.
Boolean/bevel output contains microscopic degenerate and sliver faces, so polygons
smaller than 10^-6 square metres are flat and all their incident edges are sharp.
A second guard isolates only polygons whose smoothing produced zero-length corner
normals; the final arch05 needed this guard on two polygons. This retains smooth
inner curves while preventing invalid normal fans. No automatic welding or
geometry cleanup was applied to the artist source.

An intermediate global-flat export passed orientation checks but produced visible
faceting on curved surfaces and was rejected by the user. It is superseded by the
selective smoothing above; numerical normal validity alone is not visual acceptance.
The user also identified an accidental displacement of FR05_001, which was restored
to its stored rest transform before the final export of 85 cells and eight loose
stones. The final validation below uses the resulting current BAKED proxies.

The final read-only Blender validation is
`BuildReports/EnvironmentAnimationRescue/BlenderIntactNormalsFinal.json`,
recorded at **2026-09-04T17:23:54Z** in Blender 5.2.1 LTS. It supersedes the
misleading count of corner-normal versus `MeshPolygon.normal` discrepancies:
different normal calculations are unstable on nearly zero-area polygons.
Instead, the report compares each tessellated triangle's geometric winding,
computed with double-precision cross products, against its corner normals and
weights residuals by triangle area. The installed FBX exporter confirms that
FACE/CORNER domains export `mesh.corner_normals` as `ByPolygonVertex`.

All seven proxies have **zero backward-facing triangles and zero zero-length
triangle-corner normals on triangles of area at least 10^-6 square metres**.
The minimum significant corner-to-geometric-normal dot is 0.8125. Each proxy has
820–1375 smooth edges joining non-coplanar significant faces within the 30-degree
threshold; global-flat shading had none. No custom-normal layers remain.
The largest residual backward triangle is 6.90e-8 square metres; their combined
area per proxy is below 0.000001% of its surface. This is scoped Blender shading
evidence: the FBX binary was not reimported by this check. The subsequent Unity
import check found 20,201 significant triangles across the seven current source
FBX intact meshes and zero backward triangles; every intact collider shared the
same mesh as its MeshFilter. The final `ColumnCurveDetail` Game-view frame was
also inspected after import: its concave inner shaft shows a continuous broad
gradient without the rejected global-flat faceting or black inverted faces.
This accepts the scoped imported curve view, not every possible lighting angle.

