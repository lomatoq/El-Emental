"""Headless validation for the baked Broken Crown arena blend file."""

from __future__ import annotations

import json

import bmesh
import bpy


def signed_component_volumes(mesh: bmesh.types.BMesh) -> list[float]:
    """Return signed volume for every disconnected closed face island."""
    remaining = set(mesh.faces)
    volumes = []
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

        scratch = bmesh.new()
        vertex_map = {}
        for face in component:
            copied_vertices = []
            for vertex in face.verts:
                copied = vertex_map.get(vertex)
                if copied is None:
                    copied = scratch.verts.new(vertex.co)
                    vertex_map[vertex] = copied
                copied_vertices.append(copied)
            try:
                scratch.faces.new(copied_vertices)
            except ValueError:
                pass
        scratch.normal_update()
        volumes.append(float(scratch.calc_volume(signed=True)))
        scratch.free()
    return volumes


def validate() -> dict:
    root = bpy.data.objects.get("BrokenCrownArena_ROOT")
    semantic = [obj for obj in bpy.data.objects if obj.type == "MESH" and obj.get("earth_role")]
    render_pieces = [
        obj for obj in bpy.data.objects
        if obj.type == "MESH" and obj.name.startswith("FR_")
    ]
    colliders = [
        obj for obj in bpy.data.objects
        if obj.type == "MESH" and obj.name.startswith("COL_FR_")
    ]
    floor_pieces = [
        obj for obj in render_pieces
        if obj.get("earth_structure_id") == "arena_floor_base"
    ]
    failures = []
    if root is None or root.get("arena_authoring_status") != "VALIDATED":
        failures.append("missing validated arena root")
    if root is not None and bool(root.get("arena_normal_floor_damage", True)):
        failures.append("ordinary floor damage is enabled")
    if root is not None and root.get("arena_wall_art_status") != "import_ready":
        failures.append("wall macro-fracture is not marked import-ready")
    if root is not None and not bool(root.get("arena_visual_fracture_approved", False)):
        failures.append("fracture set is not visually approved for v1 import")
    if len(semantic) != 18:
        failures.append(f"semantic object count is {len(semantic)}, expected 18")
    intact = [obj for obj in semantic if "_INTACT" in obj.name]
    intact_topology = []
    for obj in sorted(intact, key=lambda candidate: candidate.name):
        mesh = bmesh.new()
        mesh.from_mesh(obj.data)
        boundary_edges = sum(1 for edge in mesh.edges if edge.is_boundary)
        nonmanifold_edges = sum(1 for edge in mesh.edges if not edge.is_manifold)
        interior_faces = sum(1 for face in mesh.faces if face.material_index == 1)
        component_volumes = signed_component_volumes(mesh)
        mesh.free()
        intact_topology.append({
            "name": obj.name,
            "boundaryEdges": boundary_edges,
            "nonManifoldEdges": nonmanifold_edges,
            "interiorFaces": interior_faces,
            "componentVolumes": component_volumes,
        })
        if boundary_edges or nonmanifold_edges:
            failures.append(
                f"{obj.name} is open: boundary={boundary_edges}, "
                f"non-manifold={nonmanifold_edges}"
            )
        if interior_faces != 0 or len(obj.data.materials) != 1:
            failures.append(
                f"{obj.name} must use one exterior material and no fracture-interior faces"
            )
        inverted_components = [volume for volume in component_volumes if volume <= 1e-8]
        if inverted_components:
            failures.append(
                f"{obj.name} has inward or degenerate closed islands: {inverted_components}"
            )
        if obj.data.color_attributes.get("EarthFaceMask") is None:
            failures.append(f"{obj.name} is missing EarthFaceMask")
    if len(render_pieces) != 90:
        failures.append(f"render piece count is {len(render_pieces)}, expected 90")
    if len(colliders) != 90:
        failures.append(f"collider count is {len(colliders)}, expected 90")
    if len(floor_pieces) != 36:
        failures.append(f"meteor floor piece count is {len(floor_pieces)}, expected 36")
    invalid_floor = [
        obj.name for obj in floor_pieces
        if obj.get("earth_damage_mode") != "meteor_only"
        or obj.get("earth_trigger") != "MeteorImpact"
        or bool(obj.get("earth_foundation", True))
        or bool(obj.get("earth_repairable", True))
    ]
    if invalid_floor:
        failures.append(f"invalid meteor floor pieces: {invalid_floor}")
    over_budget = [obj.name for obj in colliders if len(obj.data.vertices) > 255]
    if over_budget:
        failures.append(f"colliders over 255 vertices: {over_budget}")
    missing_face_masks = [
        obj.name for obj in render_pieces
        if obj.data.color_attributes.get("EarthFaceMask") is None
    ]
    if missing_face_masks:
        failures.append(f"pieces missing EarthFaceMask: {missing_face_masks}")
    invalid_piece_topology = []
    inverted_piece_islands = []
    for obj in render_pieces:
        mesh = bmesh.new()
        mesh.from_mesh(obj.data)
        boundary_edges = sum(1 for edge in mesh.edges if edge.is_boundary)
        nonmanifold_edges = sum(1 for edge in mesh.edges if not edge.is_manifold)
        interior_faces = sum(1 for face in mesh.faces if face.material_index == 1)
        volumes = signed_component_volumes(mesh)
        mesh.free()
        if boundary_edges or nonmanifold_edges or interior_faces <= 0 or len(obj.data.materials) < 2:
            invalid_piece_topology.append(obj.name)
        if any(volume <= 1e-8 for volume in volumes):
            inverted_piece_islands.append({"name": obj.name, "volumes": volumes})
    if invalid_piece_topology:
        failures.append(f"invalid fracture piece topology/material contract: {invalid_piece_topology}")
    if inverted_piece_islands:
        failures.append(f"inward fracture piece islands: {inverted_piece_islands}")
    visible_generated = [obj.name for obj in render_pieces + colliders if not obj.hide_render]
    if visible_generated:
        failures.append(f"generated objects visible in intact render: {visible_generated}")
    missing_colliders = [
        obj.name for obj in render_pieces
        if bpy.data.objects.get(str(obj.get("earth_collider_object", ""))) is None
    ]
    if missing_colliders:
        failures.append(f"pieces missing collider references: {missing_colliders}")
    floor = bpy.data.objects.get("Arena_FloorBase_INTACT")
    floor_width = max(floor.dimensions.x, floor.dimensions.y) if floor else 0.0
    if not 15.99 <= floor_width <= 16.01:
        failures.append(f"floor width is {floor_width:.6f} m, expected 16 m")

    report = {
        "status": "PASS" if not failures else "FAIL",
        "file": bpy.data.filepath,
        "rootStatus": root.get("arena_authoring_status") if root else None,
        "validationScope": root.get("arena_validation_scope") if root else None,
        "wallArtStatus": root.get("arena_wall_art_status") if root else None,
        "visualFractureApproved": root.get("arena_visual_fracture_approved") if root else None,
        "normalFloorDamage": root.get("arena_normal_floor_damage") if root else None,
        "semanticObjects": len(semantic),
        "intactTopology": intact_topology,
        "renderPieces": len(render_pieces),
        "colliders": len(colliders),
        "meteorFloorPieces": len(floor_pieces),
        "floorTriggers": sorted({str(obj.get("earth_trigger")) for obj in floor_pieces}),
        "floorWidthMetres": floor_width,
        "maxColliderFaces": max((len(obj.data.polygons) for obj in colliders), default=0),
        "maxColliderVertices": max((len(obj.data.vertices) for obj in colliders), default=0),
        "missingFaceMasks": missing_face_masks,
        "invalidPieceTopology": invalid_piece_topology,
        "invertedPieceIslands": inverted_piece_islands,
        "failures": failures,
    }
    return report


report = validate()
print("ARENA_VALIDATION_JSON=" + json.dumps(report, sort_keys=True, separators=(",", ":")))
if report["status"] != "PASS":
    raise SystemExit(1)
