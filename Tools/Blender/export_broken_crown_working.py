"""Validate and export the current Broken Crown working file without rebaking fracture pieces."""

from __future__ import annotations

import json
import os

import bmesh
import bpy


TARGET_WIDTH_METRES = 16.0
GENERATED_TAG = "arena_generated"
SOURCE_NAME_TAG = "arena_source_name"


def main() -> dict:
    root = bpy.data.objects.get("BrokenCrownArena_ROOT")
    floor = bpy.data.objects.get("Arena_FloorBase_INTACT")
    if root is None or floor is None:
        raise RuntimeError("Broken Crown root/floor is missing.")

    intact = sorted(
        [obj for obj in bpy.data.objects if obj.type == "MESH" and "_INTACT" in obj.name],
        key=lambda obj: obj.name,
    )
    if len(intact) != 8:
        raise RuntimeError(f"Expected 8 intact meshes, found {len(intact)}.")
    topology = []
    for obj in intact:
        mesh = bmesh.new()
        mesh.from_mesh(obj.data)
        row = {
            "name": obj.name,
            "boundaryEdges": sum(1 for edge in mesh.edges if edge.is_boundary),
            "nonManifoldEdges": sum(1 for edge in mesh.edges if not edge.is_manifold),
            "interiorFaces": sum(1 for face in mesh.faces if face.material_index == 1),
        }
        mesh.free()
        if row["boundaryEdges"] or row["nonManifoldEdges"] or row["interiorFaces"] != 0:
            raise RuntimeError(f"Invalid intact topology: {row}")
        if len(obj.data.materials) != 1 or obj.data.color_attributes.get("EarthFaceMask") is None:
            raise RuntimeError(f"{obj.name} must keep one exterior material/mask contract.")
        topology.append(row)

    current_width = max(float(floor.dimensions.x), float(floor.dimensions.y))
    if current_width <= 1e-6:
        raise RuntimeError("Broken Crown floor has no usable width.")
    scale_factor = TARGET_WIDTH_METRES / current_width
    root.scale *= scale_factor
    bpy.context.view_layer.update()
    normalized_width = max(float(floor.dimensions.x), float(floor.dimensions.y))
    if abs(normalized_width - TARGET_WIDTH_METRES) > 0.001:
        raise RuntimeError(f"Arena normalization failed: {normalized_width:.6f} m.")

    bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath, check_existing=False)

    blend_directory = os.path.dirname(bpy.data.filepath)
    project_root = os.path.abspath(os.path.join(blend_directory, "..", "..", ".."))
    model_path = os.path.join(
        project_root,
        "Assets",
        "Elemental",
        "Content",
        "Arena",
        "BrokenCrown",
        "BrokenCrownArena.fbx",
    )
    export_objects = [
        obj
        for obj in bpy.data.objects
        if obj.type in {"MESH", "EMPTY"}
        and (obj == root or obj.get(SOURCE_NAME_TAG) or obj.get(GENERATED_TAG, False))
    ]
    previous_selection = {obj: obj.select_get() for obj in bpy.context.scene.objects}
    previous_hidden = {obj: obj.hide_get() for obj in export_objects}
    previous_active = bpy.context.view_layer.objects.active
    for obj in bpy.context.scene.objects:
        obj.select_set(False)
    for obj in export_objects:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=model_path,
        check_existing=False,
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        use_triangles=True,
        use_custom_props=True,
        add_leaf_bones=False,
        bake_anim=False,
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        path_mode="AUTO",
        embed_textures=False,
    )
    for obj, hidden in previous_hidden.items():
        obj.hide_set(hidden)
    for obj, selected in previous_selection.items():
        obj.select_set(selected)
    if previous_active is not None:
        bpy.context.view_layer.objects.active = previous_active

    return {
        "status": "PASS",
        "workingFile": bpy.data.filepath,
        "model": model_path,
        "scaleFactor": scale_factor,
        "floorWidthMetres": normalized_width,
        "exportedObjects": len(export_objects),
        "intactTopology": topology,
    }


report = main()
print("ARENA_EXPORT_JSON=" + json.dumps(report, sort_keys=True, separators=(",", ":")))
