"""Repair Broken Crown winding and intact-material contracts in the working blend.

This is deliberately narrower than a rebake: authored fracture sites, transforms,
bonds and piece names stay unchanged. Closed islands with negative signed volume
are reversed; repaired source caps are folded back into the exterior material.
"""

from __future__ import annotations

import json

import bmesh
import bpy


def face_components(mesh: bmesh.types.BMesh) -> list[list[bmesh.types.BMFace]]:
    remaining = set(mesh.faces)
    output = []
    while remaining:
        seed = remaining.pop()
        stack = [seed]
        component = [seed]
        while stack:
            face = stack.pop()
            for edge in face.edges:
                for linked in edge.link_faces:
                    if linked not in remaining:
                        continue
                    remaining.remove(linked)
                    stack.append(linked)
                    component.append(linked)
        output.append(component)
    return output


def component_volume(faces: list[bmesh.types.BMFace]) -> float:
    scratch = bmesh.new()
    vertex_map = {}
    for face in faces:
        copied = []
        for vertex in face.verts:
            target = vertex_map.get(vertex)
            if target is None:
                target = scratch.verts.new(vertex.co)
                vertex_map[vertex] = target
            copied.append(target)
        try:
            scratch.faces.new(copied)
        except ValueError:
            pass
    scratch.normal_update()
    value = float(scratch.calc_volume(signed=True))
    scratch.free()
    return value


def orient_closed_islands(obj: bpy.types.Object) -> int:
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    if any(not edge.is_manifold for edge in mesh.edges):
        mesh.free()
        raise RuntimeError(f"{obj.name} is not closed manifold; refusing winding repair.")
    reversed_count = 0
    for component in face_components(mesh):
        if component_volume(component) < -1e-8:
            bmesh.ops.reverse_faces(mesh, faces=component)
            reversed_count += 1
    if reversed_count:
        mesh.normal_update()
        mesh.to_mesh(obj.data)
        obj.data.update()
    mesh.free()
    return reversed_count


def make_intact_exterior_only(obj: bpy.types.Object) -> int:
    changed_faces = 0
    for polygon in obj.data.polygons:
        if polygon.material_index != 0:
            polygon.material_index = 0
            changed_faces += 1
    while len(obj.data.materials) > 1:
        obj.data.materials.pop(index=1)
    mask = obj.data.color_attributes.get("EarthFaceMask")
    if mask is not None:
        exterior = (1.0, 0.0, 0.0, 0.11)
        for datum in mask.data:
            if hasattr(datum, "color_srgb"):
                datum.color_srgb = exterior
            else:
                datum.color = exterior
    obj.data.update()
    return changed_faces


def main() -> dict:
    candidates = [
        obj for obj in bpy.data.objects
        if obj.type == "MESH"
        and (
            "_INTACT" in obj.name
            or obj.name.startswith("FR_")
            or obj.name.startswith("COL_FR_")
        )
    ]
    reversed_rows = {}
    for obj in candidates:
        count = orient_closed_islands(obj)
        if count:
            reversed_rows[obj.name] = count

    intact_rows = {}
    for obj in candidates:
        if "_INTACT" not in obj.name:
            continue
        intact_rows[obj.name] = make_intact_exterior_only(obj)

    root = bpy.data.objects.get("BrokenCrownArena_ROOT")
    if root is not None:
        root["arena_intact_material_contract"] = "exterior_only_v1"
        root["arena_winding_contract"] = "positive_closed_islands_v1"

    bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath, check_existing=False)
    return {
        "status": "PASS",
        "file": bpy.data.filepath,
        "reversed": reversed_rows,
        "intactFacesRemapped": intact_rows,
    }


report = main()
print("ARENA_REPAIR_JSON=" + json.dumps(report, sort_keys=True, separators=(",", ":")))
