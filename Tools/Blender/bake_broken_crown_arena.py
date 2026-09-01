"""Prepare the Tripo Broken Crown arena for deterministic Unity authoring.

Run inside Blender. The script is intentionally idempotent:

* imported source meshes are renamed and moved into semantic collections;
* source scale is normalized to a 16 metre combat footprint once;
* ordinary destructible architecture receives hidden baked render/collider pieces;
* the floor/base remains the intact gameplay proxy and receives a separate hidden
  36-piece set tagged for ``MeteorImpact`` only;
* loose rocks remain standalone authored targets; tiny rubble remains cosmetic.

The raw Tripo file is never touched. The caller must open the working copy.
"""

from __future__ import annotations

import json
import math
import os
import random
from dataclasses import dataclass
from typing import Iterable

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform


SCHEMA_VERSION = 1
TARGET_ARENA_WIDTH_METRES = 16.0
PREPROCESS_BACKUP_NAME = "BrokenCrownArena_Preprocess.blend"
EXPECTED_WORKING_NAME = "BrokenCrownArena_Working.blend"
WALL_SITES_NAME = "BrokenCrownArena.wall-sites.json"

ROOT_COLLECTION = "BrokenCrownArena"
STATIC_COLLECTION = "00_STATIC"
DESTRUCTIBLE_COLLECTION = "10_DESTRUCTIBLE_INTACT"
LOOSE_COLLECTION = "20_LOOSE_ROCKS"
COSMETIC_COLLECTION = "30_COSMETIC_RUBBLE"
FRACTURE_COLLECTION = "90_FRACTURE_BAKED"
AUTHORING_COLLECTION = "99_AUTHORING"
UNITY_EXPORT_RELATIVE = os.path.join(
    "Assets", "Elemental", "Content", "Arena", "BrokenCrown"
)
UNITY_MODEL_NAME = "BrokenCrownArena.fbx"
UNITY_SIDECAR_NAME = "BrokenCrownArena.fracture.json"

GENERATED_TAG = "arena_generated"
SOURCE_NAME_TAG = "arena_source_name"
SEAM_LAYER_NAME = "earth_seam_id"
SEAM_SIDE_LAYER_NAME = "earth_seam_side"
FRACTURE_BEVEL_MODIFIER_NAME = "Earth Fracture Micro Bevel"


@dataclass(frozen=True)
class SourceSpec:
    source_name: str
    semantic_name: str
    collection_name: str
    role: str
    damage_mode: str
    structure_id: str = ""
    piece_count: int = 0
    fracture_seed: int = 0


SPECS = (
    SourceSpec("tripo_part_0", "Arena_FloorBase_INTACT", STATIC_COLLECTION,
               "static_surface", "meteor_only", "arena_floor_base", 36, 850018),
    SourceSpec("tripo_part_1", "Arena_Gate_INTACT", DESTRUCTIBLE_COLLECTION,
               "destructible_structure", "normal", "arena_gate", 12, 1123),
    SourceSpec("tripo_part_2", "Arena_Wall_East_INTACT", DESTRUCTIBLE_COLLECTION,
               "destructible_structure", "normal", "arena_wall_east", 12, 1219),
    SourceSpec("tripo_part_3", "Arena_Wall_West_INTACT", DESTRUCTIBLE_COLLECTION,
               "destructible_structure", "normal", "arena_wall_west", 12, 1216),
    SourceSpec("tripo_part_4", "Arena_Column_NorthWest_INTACT", DESTRUCTIBLE_COLLECTION,
               "destructible_structure", "normal", "arena_column_north_west", 5, 1404),
    SourceSpec("tripo_part_5", "Arena_Column_NorthEast_INTACT", DESTRUCTIBLE_COLLECTION,
               "destructible_structure", "normal", "arena_column_north_east", 5, 1409),
    SourceSpec("tripo_part_6", "Arena_Column_SouthEast_INTACT", DESTRUCTIBLE_COLLECTION,
               "destructible_structure", "normal", "arena_column_south_east", 4, 1516),
    SourceSpec("tripo_part_7", "Arena_Column_SouthWest_INTACT", DESTRUCTIBLE_COLLECTION,
               "destructible_structure", "normal", "arena_column_south_west", 4, 1520),
    SourceSpec("tripo_part_8", "Arena_Rock_SouthEast_Large", LOOSE_COLLECTION,
               "loose_rock", "grabbable"),
    SourceSpec("tripo_part_9", "Arena_Rock_SouthWest_Slab", LOOSE_COLLECTION,
               "loose_rock", "grabbable"),
    SourceSpec("tripo_part_10", "Arena_Rock_NorthEast_Large", LOOSE_COLLECTION,
               "loose_rock", "grabbable"),
    SourceSpec("tripo_part_11", "Arena_Rock_SouthEast_Small", LOOSE_COLLECTION,
               "loose_rock", "grabbable"),
    SourceSpec("tripo_part_12", "Arena_Rock_NorthWest_Large", LOOSE_COLLECTION,
               "loose_rock", "grabbable"),
    SourceSpec("tripo_part_13", "Arena_Rubble_SouthWest_01", COSMETIC_COLLECTION,
               "cosmetic_rubble", "cosmetic"),
    SourceSpec("tripo_part_14", "Arena_Rock_SouthWest_Large", LOOSE_COLLECTION,
               "loose_rock", "grabbable"),
    SourceSpec("tripo_part_15", "Arena_Rock_West_Slab", LOOSE_COLLECTION,
               "loose_rock", "grabbable"),
    SourceSpec("tripo_part_16", "Arena_GateThreshold_STATIC", STATIC_COLLECTION,
               "static_surface", "indestructible"),
    SourceSpec("tripo_part_17", "Arena_Rubble_SouthWest_02", COSMETIC_COLLECTION,
               "cosmetic_rubble", "cosmetic"),
)


class BakeError(RuntimeError):
    pass


def fracture_profile(spec: SourceSpec) -> str:
    if spec.damage_mode == "meteor_only":
        return "meteor_radial_plane_v1"
    if spec.structure_id.startswith("arena_wall_"):
        return "masonry_watershed_power_v1"
    if spec.structure_id == "arena_gate":
        return "architectural_plane_split_v1"
    return "column_break_plane_split_v1"


def fracture_art_status(spec: SourceSpec) -> str:
    return "import_ready"


def ensure_object_mode() -> None:
    active = bpy.context.view_layer.objects.active
    if active is not None and active.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")


def save_preprocess_backup() -> str:
    filepath = bpy.data.filepath
    if not filepath:
        raise BakeError("The arena working file must be saved before baking.")
    if os.path.basename(filepath) != EXPECTED_WORKING_NAME:
        raise BakeError(
            f"Expected {EXPECTED_WORKING_NAME!r}, got {os.path.basename(filepath)!r}. "
            "Open the working copy, not the raw source."
        )
    backup = os.path.join(os.path.dirname(filepath), PREPROCESS_BACKUP_NAME)
    if not os.path.exists(backup):
        bpy.ops.wm.save_as_mainfile(filepath=backup, check_existing=False)
        bpy.ops.wm.save_as_mainfile(filepath=filepath, check_existing=False)
    return backup


def ensure_collection(name: str, parent: bpy.types.Collection | None = None) -> bpy.types.Collection:
    collection = bpy.data.collections.get(name)
    if collection is None:
        collection = bpy.data.collections.new(name)
    desired_parent = parent or bpy.context.scene.collection
    if collection.name not in {child.name for child in desired_parent.children}:
        desired_parent.children.link(collection)
    return collection


def move_to_collection(obj: bpy.types.Object, collection: bpy.types.Collection) -> None:
    if obj.name not in collection.objects:
        collection.objects.link(obj)
    for existing in list(obj.users_collection):
        if existing != collection:
            existing.objects.unlink(obj)


def remove_generated_content() -> None:
    generated_objects = [obj for obj in bpy.data.objects if bool(obj.get(GENERATED_TAG, False))]
    for obj in generated_objects:
        bpy.data.objects.remove(obj, do_unlink=True)
    generated_meshes = [mesh for mesh in bpy.data.meshes if bool(mesh.get(GENERATED_TAG, False))]
    for mesh in generated_meshes:
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    generated_collections = [
        collection for collection in bpy.data.collections
        if bool(collection.get(GENERATED_TAG, False))
    ]
    for collection in sorted(generated_collections, key=lambda item: item.name.count("/"), reverse=True):
        bpy.data.collections.remove(collection)


def find_source(spec: SourceSpec) -> bpy.types.Object:
    direct = bpy.data.objects.get(spec.source_name)
    if direct is not None and direct.type == "MESH":
        return direct
    matches = [
        obj for obj in bpy.data.objects
        if obj.type == "MESH" and obj.get(SOURCE_NAME_TAG) == spec.source_name
    ]
    if len(matches) != 1:
        raise BakeError(f"Could not resolve exactly one source object for {spec.source_name!r}.")
    return matches[0]


def normalize_scale_once(root: bpy.types.Object, sources: list[bpy.types.Object]) -> float:
    previous = root.get("arena_scale_factor")
    if previous is not None:
        return float(previous)
    floor = find_source(SPECS[0])
    current_width = max(float(floor.dimensions.x), float(floor.dimensions.y))
    if current_width <= 1e-6:
        raise BakeError("The floor has no usable authored width.")
    factor = TARGET_ARENA_WIDTH_METRES / current_width
    for obj in sources:
        obj.data.transform(Matrix.Scale(factor, 4))
        obj.location *= factor
        obj.data.update()
    root["arena_scale_factor"] = factor
    root["arena_width_metres"] = TARGET_ARENA_WIDTH_METRES
    root["arena_units"] = "metres"
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    return factor


def connected_components(bm: bmesh.types.BMesh) -> list[list[bmesh.types.BMVert]]:
    remaining = set(bm.verts)
    components: list[list[bmesh.types.BMVert]] = []
    while remaining:
        seed = remaining.pop()
        stack = [seed]
        component = [seed]
        while stack:
            vertex = stack.pop()
            for edge in vertex.link_edges:
                neighbour = edge.other_vert(vertex)
                if neighbour in remaining:
                    remaining.remove(neighbour)
                    stack.append(neighbour)
                    component.append(neighbour)
        components.append(component)
    return components


def remove_tiny_components(bm: bmesh.types.BMesh) -> int:
    components = connected_components(bm)
    if len(components) <= 1:
        return 0
    largest = max(len(component) for component in components)
    cutoff = max(4, int(math.ceil(largest * 0.005)))
    discarded = [vertex for component in components if len(component) < cutoff for vertex in component]
    if discarded:
        bmesh.ops.delete(bm, geom=discarded, context="VERTS")
    return len(discarded)


def remove_nonmanifold_flaps(bm: bmesh.types.BMesh) -> int:
    """Remove imported one-triangle flaps attached to an otherwise valid edge.

    Tripo occasionally emits a triangle with two exposed sides whose third side is
    already shared by two legitimate faces. Keeping that flap makes hole filling
    create a three-face edge. It carries no closed volume, so deleting it is the
    conservative repair before the fracture-only mesh is sealed.
    """
    removed = 0
    for _ in range(8):
        flaps = [
            face for face in bm.faces
            if any(len(edge.link_faces) == 1 for edge in face.edges)
            and any(len(edge.link_faces) > 2 for edge in face.edges)
        ]
        if not flaps:
            break
        removed += len(flaps)
        bmesh.ops.delete(bm, geom=flaps, context="FACES")
    return removed


def ensure_seam_layers(bm: bmesh.types.BMesh):
    seam = bm.faces.layers.int.get(SEAM_LAYER_NAME) or bm.faces.layers.int.new(SEAM_LAYER_NAME)
    side = bm.faces.layers.int.get(SEAM_SIDE_LAYER_NAME) or bm.faces.layers.int.new(SEAM_SIDE_LAYER_NAME)
    return seam, side


def repair_source_mesh(source: bpy.types.Object) -> tuple[bmesh.types.BMesh, dict]:
    bm = bmesh.new()
    bm.from_mesh(source.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
    discarded = remove_tiny_components(bm)
    removed_flaps = remove_nonmanifold_flaps(bm)
    seam_layer, side_layer = ensure_seam_layers(bm)
    for face in bm.faces:
        face[seam_layer] = 0
        face[side_layer] = 0
    boundary = [edge for edge in bm.edges if len(edge.link_faces) == 1]
    filled = []
    if boundary:
        filled = bmesh.ops.holes_fill(bm, edges=boundary, sides=0).get("faces", [])
        for face in filled:
            face.material_index = 0
            face[seam_layer] = 0
            face[side_layer] = 0
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.normal_update()
    nonmanifold = sum(1 for edge in bm.edges if not edge.is_manifold)
    if nonmanifold:
        bm.free()
        raise BakeError(f"{source.name}: repaired source still has {nonmanifold} non-manifold edges.")
    return bm, {
        "discarded_tiny_vertices": discarded,
        "removed_nonmanifold_flaps": removed_flaps,
        "filled_hole_faces": len(filled),
    }


def bounds(bm: bmesh.types.BMesh) -> tuple[Vector, Vector, Vector]:
    if not bm.verts:
        raise BakeError("Cannot calculate bounds of an empty fracture piece.")
    coords = [vertex.co for vertex in bm.verts]
    minimum = Vector(tuple(min(value[index] for value in coords) for index in range(3)))
    maximum = Vector(tuple(max(value[index] for value in coords) for index in range(3)))
    return minimum, maximum, maximum - minimum


def volume(bm: bmesh.types.BMesh) -> float:
    return abs(float(bm.calc_volume(signed=True)))


def copy_vertex_component(
    source: bmesh.types.BMesh,
    component: set[bmesh.types.BMVert],
) -> bmesh.types.BMesh:
    target = bmesh.new()
    source_seam, source_side = ensure_seam_layers(source)
    target_seam, target_side = ensure_seam_layers(target)
    vertex_map = {vertex: target.verts.new(vertex.co) for vertex in component}
    for face in source.faces:
        if not all(vertex in component for vertex in face.verts):
            continue
        try:
            copied = target.faces.new([vertex_map[vertex] for vertex in face.verts])
        except ValueError:
            continue
        copied.material_index = face.material_index
        copied[target_seam] = face[source_seam]
        copied[target_side] = face[source_side]
    target.verts.ensure_lookup_table()
    target.faces.ensure_lookup_table()
    if target.faces:
        bmesh.ops.recalc_face_normals(target, faces=list(target.faces))
    target.normal_update()
    return target


def keep_largest_connected_component(
    bm: bmesh.types.BMesh,
) -> tuple[bmesh.types.BMesh, float, int]:
    remaining = set(bm.verts)
    components: list[set[bmesh.types.BMVert]] = []
    while remaining:
        seed = remaining.pop()
        stack = [seed]
        component = {seed}
        while stack:
            vertex = stack.pop()
            for edge in vertex.link_edges:
                neighbour = edge.other_vert(vertex)
                if neighbour in remaining:
                    remaining.remove(neighbour)
                    component.add(neighbour)
                    stack.append(neighbour)
        components.append(component)
    if len(components) <= 1:
        return bm, 0.0, len(components)

    copied = [copy_vertex_component(bm, component) for component in components]
    copied.sort(key=volume, reverse=True)
    kept = copied[0]
    discarded_volume = sum(volume(component) for component in copied[1:])
    for component in copied[1:]:
        component.free()
    bm.free()
    return kept, discarded_volume, len(components)


def bisected_half(
    source: bmesh.types.BMesh,
    plane_co: Vector,
    plane_no: Vector,
    *,
    clear_outer: bool,
    seam_id: int,
    seam_side: int,
) -> bmesh.types.BMesh:
    bm = source.copy()
    geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
    result = bmesh.ops.bisect_plane(
        bm,
        geom=geom,
        dist=1e-5,
        plane_co=plane_co,
        plane_no=plane_no,
        use_snap_center=False,
        clear_outer=clear_outer,
        clear_inner=not clear_outer,
    )
    seam_layer, side_layer = ensure_seam_layers(bm)
    cut_edges = [
        element for element in result.get("geom_cut", [])
        if isinstance(element, bmesh.types.BMEdge)
        and element.is_valid
        and len(element.link_faces) == 1
    ]
    if cut_edges:
        cut_faces = bmesh.ops.holes_fill(bm, edges=cut_edges, sides=0).get("faces", [])
        for face in cut_faces:
            face.material_index = 1
            face[seam_layer] = seam_id
            face[side_layer] = seam_side
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
    if bm.faces:
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.normal_update()
    return bm


def choose_split_plane(
    bm: bmesh.types.BMesh,
    rng: random.Random,
    *,
    floor_mode: bool,
    serial: int,
) -> tuple[Vector, Vector]:
    minimum, maximum, dimensions = bounds(bm)
    axes = [0, 1, 2]
    if floor_mode:
        angle = rng.uniform(0.0, math.tau)
        plane_no = Vector((math.cos(angle), math.sin(angle), 0.0))
        plane_no.z = rng.choice((-1.0, 1.0)) * rng.uniform(0.12, 0.32)
        plane_no.normalize()
        plane_co = (minimum + maximum) * 0.5
        projected_extent = abs(plane_no.x) * dimensions.x + abs(plane_no.y) * dimensions.y
        plane_co += plane_no * projected_extent * rng.uniform(-0.10, 0.10)
        return plane_co, plane_no
    else:
        ranked = sorted(axes, key=lambda axis: dimensions[axis], reverse=True)
        primary = ranked[1] if serial % 3 == 2 else ranked[0]
    plane_co = (minimum + maximum) * 0.5
    plane_co[primary] += dimensions[primary] * rng.uniform(-0.13, 0.13)
    plane_no = Vector(tuple(rng.uniform(-0.24, 0.24) for _ in range(3)))
    plane_no[primary] += 1.0
    plane_no.normalize()
    return plane_co, plane_no


def split_piece(
    source: bmesh.types.BMesh,
    rng: random.Random,
    *,
    floor_mode: bool,
    serial: int,
    seam_id: int,
) -> tuple[bmesh.types.BMesh, bmesh.types.BMesh] | None:
    parent_volume = volume(source)
    for attempt in range(18):
        plane_co, plane_no = choose_split_plane(
            source, rng, floor_mode=floor_mode, serial=serial + attempt
        )
        negative = bisected_half(
            source, plane_co, plane_no,
            clear_outer=True, seam_id=seam_id, seam_side=-1,
        )
        positive = bisected_half(
            source, plane_co, plane_no,
            clear_outer=False, seam_id=seam_id, seam_side=1,
        )
        negative_volume = volume(negative)
        positive_volume = volume(positive)
        valid = (
            len(negative.faces) >= 4
            and len(positive.faces) >= 4
            and negative_volume > parent_volume * 0.055
            and positive_volume > parent_volume * 0.055
            and all(edge.is_manifold for edge in negative.edges)
            and all(edge.is_manifold for edge in positive.edges)
        )
        if valid:
            return negative, positive
        negative.free()
        positive.free()
    return None


def fracture_bmesh(
    source: bpy.types.Object,
    *,
    target_count: int,
    seed: int,
    floor_mode: bool,
    profile: str,
    structure_id: str,
) -> tuple[list[bmesh.types.BMesh], dict]:
    if profile == "masonry_watershed_power_v1":
        return fracture_masonry_power_bmesh(
            source,
            target_count=target_count,
            structure_id=structure_id,
        )
    repaired, repair_stats = repair_source_mesh(source)
    source_volume = volume(repaired)
    pieces = [repaired]
    rng = random.Random(seed)
    split_failures = 0
    serial = 0
    next_seam_id = 1
    while len(pieces) < target_count:
        candidates = sorted(range(len(pieces)), key=lambda index: volume(pieces[index]), reverse=True)
        created = False
        for index in candidates:
            pair = split_piece(
                pieces[index], rng,
                floor_mode=floor_mode,
                serial=serial,
                seam_id=next_seam_id,
            )
            serial += 1
            if pair is None:
                split_failures += 1
                continue
            previous = pieces.pop(index)
            previous.free()
            pieces.extend(pair)
            next_seam_id += 1
            created = True
            break
        if not created:
            break
    piece_volumes = [volume(piece) for piece in pieces]
    relative_error = abs(sum(piece_volumes) - source_volume) / max(source_volume, 1e-9)
    if len(pieces) != target_count:
        for piece in pieces:
            piece.free()
        raise BakeError(f"{source.name}: generated {len(pieces)}/{target_count} pieces.")
    if relative_error > 0.015:
        for piece in pieces:
            piece.free()
        raise BakeError(f"{source.name}: fracture volume error {relative_error:.2%} exceeds 1.5%.")
    return pieces, {
        **repair_stats,
        "source_volume": source_volume,
        "piece_volume_sum": sum(piece_volumes),
        "relative_volume_error": relative_error,
        "split_failures": split_failures,
        "seam_count": next_seam_id - 1,
    }


def load_wall_sites(structure_id: str, target_count: int) -> list[Vector]:
    path = os.path.join(os.path.dirname(bpy.data.filepath), WALL_SITES_NAME)
    if not os.path.exists(path):
        raise BakeError(f"Missing authored wall sites: {path}")
    with open(path, "r", encoding="utf-8") as handle:
        payload = json.load(handle)
    if int(payload.get("schemaVersion", 0)) != 1:
        raise BakeError(f"Unsupported wall-site schema in {path}")
    wall = payload.get("walls", {}).get(structure_id)
    rows = wall.get("sites", []) if isinstance(wall, dict) else []
    if len(rows) != target_count:
        raise BakeError(
            f"{structure_id}: wall-site count {len(rows)} does not match {target_count}."
        )
    return [Vector(tuple(float(value) for value in row["position"])) for row in rows]


def masonry_pair_seam_id(first: int, second: int, count: int) -> int:
    low, high = sorted((first, second))
    return 1 + low * count + high


def fracture_masonry_power_bmesh(
    source: bpy.types.Object,
    *,
    target_count: int,
    structure_id: str,
) -> tuple[list[bmesh.types.BMesh], dict]:
    repaired, repair_stats = repair_source_mesh(source)
    source_volume = volume(repaired)
    sites = load_wall_sites(structure_id, target_count)
    pieces: list[bmesh.types.BMesh] = []
    discarded_volume = 0.0
    disconnected_cells = 0
    for site_index, site in enumerate(sites):
        cell = repaired.copy()
        for other_index, other in enumerate(sites):
            if site_index == other_index:
                continue
            direction = other - site
            if direction.length_squared <= 1e-10:
                cell.free()
                repaired.free()
                raise BakeError(f"{structure_id}: coincident masonry sites.")
            clipped = bisected_half(
                cell,
                (site + other) * 0.5,
                direction.normalized(),
                clear_outer=True,
                seam_id=masonry_pair_seam_id(site_index, other_index, len(sites)),
                seam_side=-1 if site_index < other_index else 1,
            )
            cell.free()
            cell = clipped
            if not cell.faces:
                repaired.free()
                for piece in pieces:
                    piece.free()
                raise BakeError(f"{structure_id}: masonry site {site_index} produced no volume.")
        cell, discarded, component_count = keep_largest_connected_component(cell)
        discarded_volume += discarded
        disconnected_cells += int(component_count > 1)
        if not cell.faces or not all(edge.is_manifold for edge in cell.edges):
            cell.free()
            repaired.free()
            for piece in pieces:
                piece.free()
            raise BakeError(f"{structure_id}: masonry cell {site_index} is not closed manifold.")
        pieces.append(cell)

    repaired.free()
    piece_volumes = [volume(piece) for piece in pieces]
    relative_error = abs(sum(piece_volumes) - source_volume) / max(source_volume, 1e-9)
    if relative_error > 0.015:
        for piece in pieces:
            piece.free()
        raise BakeError(
            f"{structure_id}: masonry power fracture error {relative_error:.2%} exceeds 1.5%."
        )
    return pieces, {
        **repair_stats,
        "source_volume": source_volume,
        "piece_volume_sum": sum(piece_volumes),
        "relative_volume_error": relative_error,
        "split_failures": 0,
        "seam_count": len({
            seam_id
            for piece in pieces
            for seam_id in seam_metadata(piece)
        }),
        "discarded_disconnected_volume": discarded_volume,
        "disconnected_cells_cleaned": disconnected_cells,
    }


def ensure_material(name: str, color: tuple[float, float, float, float]) -> bpy.types.Material:
    material = bpy.data.materials.get(name)
    if material is None:
        material = bpy.data.materials.new(name)
    material.diffuse_color = color
    return material


def bmesh_centroid(bm: bmesh.types.BMesh) -> Vector:
    total = Vector((0.0, 0.0, 0.0))
    for vertex in bm.verts:
        total += vertex.co
    return total / max(1, len(bm.verts))


def seam_metadata(bm: bmesh.types.BMesh) -> dict[int, dict]:
    seam_layer, side_layer = ensure_seam_layers(bm)
    seams: dict[int, dict] = {}
    for face in bm.faces:
        seam_id = int(face[seam_layer])
        if seam_id <= 0:
            continue
        side = int(face[side_layer])
        area = float(face.calc_area())
        entry = seams.setdefault(
            seam_id,
            {
                "side": side,
                "area": 0.0,
                "centroid_sum": Vector((0.0, 0.0, 0.0)),
                "normal_sum": Vector((0.0, 0.0, 0.0)),
            },
        )
        entry["area"] += area
        entry["centroid_sum"] += face.calc_center_median() * area
        entry["normal_sum"] += face.normal * area
    for entry in seams.values():
        area = max(1e-9, float(entry["area"]))
        centroid = entry.pop("centroid_sum") / area
        normal = entry.pop("normal_sum")
        if normal.length_squared > 1e-10:
            normal.normalize()
        entry["centroid"] = [round(float(value), 7) for value in centroid]
        entry["normal"] = [round(float(value), 7) for value in normal]
    return seams


def create_mesh_object(
    name: str,
    bm: bmesh.types.BMesh,
    *,
    source: bpy.types.Object,
    collection: bpy.types.Collection,
    surface_material: bpy.types.Material,
    interior_material: bpy.types.Material,
) -> bpy.types.Object:
    centroid = bmesh_centroid(bm)
    for vertex in bm.verts:
        vertex.co -= centroid

    # The cut cap is a separate normal island.  Previously every cap stayed
    # smooth-connected to the weathered exterior, so a proxy swap changed the
    # exterior vertex normals and made a moving dark band crawl over the piece.
    # Keep source smoothing on material 0, but make every fresh-cut face and its
    # boundary hard before the render mesh is materialized.
    for face in bm.faces:
        if face.material_index == 1:
            face.smooth = False
    for edge in bm.edges:
        materials = {face.material_index for face in edge.link_faces}
        if 1 in materials or len(materials) > 1:
            edge.smooth = False

    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh[GENERATED_TAG] = True
    bm.to_mesh(mesh)
    mesh.materials.append(surface_material)
    mesh.materials.append(interior_material)
    # Export the fracture mask on POINT rather than CORNER.  The FBX exporter
    # evaluates the render-only bevel below; a corner-domain layer can be lost
    # or end up shorter than the evaluated vertex buffer when that modifier
    # creates chamfer loops.  A point layer is interpolated onto those vertices
    # and therefore reaches Unity as one Color32 per imported vertex.
    color_layer = mesh.color_attributes.new(
        name="EarthFaceMask", type="BYTE_COLOR", domain="POINT"
    )
    exterior_color = (1.0, 0.0, 0.0, 0.11)
    interior_color = (0.0, 1.0, 0.0, 0.58)
    for datum in color_layer.data:
        if hasattr(datum, "color_srgb"):
            datum.color_srgb = exterior_color
        else:
            datum.color = exterior_color
    for polygon in mesh.polygons:
        if polygon.material_index != 1:
            continue
        for vertex_index in polygon.vertices:
            datum = color_layer.data[vertex_index]
            if hasattr(datum, "color_srgb"):
                datum.color_srgb = interior_color
            else:
                datum.color = interior_color
    mesh.color_attributes.active_color_index = len(mesh.color_attributes) - 1
    mesh.update()

    # Re-project exterior loop normals from the intact authored mesh.  New cut
    # loops deliberately use their flat cap normal.  This makes the unbroken and
    # fractured proxies shade identically everywhere except the actual fracture.
    source_mesh = source.data
    source_mesh.calc_loop_triangles()
    source_vertices = [vertex.co.copy() for vertex in source_mesh.vertices]
    source_triangles = [tuple(triangle.vertices) for triangle in source_mesh.loop_triangles]
    source_tree = BVHTree.FromPolygons(source_vertices, source_triangles, all_triangles=True)
    custom_normals = [(0.0, 0.0, 1.0)] * len(mesh.loops)
    for polygon in mesh.polygons:
        interior = polygon.material_index == 1
        polygon.use_smooth = not interior
        for loop_index in polygon.loop_indices:
            if interior:
                custom_normals[loop_index] = tuple(polygon.normal.normalized())
                continue
            local_point = mesh.vertices[mesh.loops[loop_index].vertex_index].co + centroid
            nearest = source_tree.find_nearest(local_point)
            if nearest is None or nearest[2] is None:
                custom_normals[loop_index] = tuple(polygon.normal.normalized())
                continue
            triangle = source_mesh.loop_triangles[int(nearest[2])]
            source_polygon = source_mesh.polygons[triangle.polygon_index]
            if not source_polygon.use_smooth:
                custom_normals[loop_index] = tuple(source_polygon.normal.normalized())
                continue
            a, b, c = (source_mesh.vertices[index] for index in triangle.vertices)
            sampled = barycentric_transform(
                nearest[0], a.co, b.co, c.co, a.normal, b.normal, c.normal
            )
            if sampled.length_squared <= 1e-10:
                sampled = source_polygon.normal.copy()
            else:
                sampled.normalize()
            custom_normals[loop_index] = tuple(sampled)
    if len(custom_normals) == len(mesh.loops):
        mesh.normals_split_custom_set(custom_normals)
    mesh.update()

    obj = bpy.data.objects.new(name, mesh)
    obj[GENERATED_TAG] = True
    collection.objects.link(obj)
    obj.matrix_world = source.matrix_world @ Matrix.Translation(centroid)

    # A one-segment physical chamfer gives the fracture rim a readable highlight
    # and keeps wall/column chunks from looking like raw boolean output.  The
    # collider is built from the unmodified closed mesh below; only the render
    # export consumes this bounded bevel.
    _, _, dimensions = bounds_for_mesh(mesh)
    minimum_dimension = max(0.001, min(abs(value) for value in dimensions))
    bevel = obj.modifiers.new(FRACTURE_BEVEL_MODIFIER_NAME, "BEVEL")
    bevel.width = max(0.006, min(0.028, minimum_dimension * 0.035))
    bevel.segments = 1
    bevel.limit_method = "ANGLE"
    bevel.angle_limit = math.radians(24.0)
    if hasattr(bevel, "affect"):
        bevel.affect = "EDGES"
    if hasattr(bevel, "harden_normals"):
        bevel.harden_normals = True
    obj.hide_render = True
    obj.hide_set(True)
    return obj


def create_convex_collider(
    render_object: bpy.types.Object,
    *,
    collection: bpy.types.Collection,
) -> bpy.types.Object:
    source_mesh = render_object.data
    hull = bmesh.new()
    for vertex in source_mesh.vertices:
        hull.verts.new(vertex.co)
    hull.verts.ensure_lookup_table()
    result = bmesh.ops.convex_hull(hull, input=list(hull.verts), use_existing_faces=False)
    disposable = []
    for key in ("geom_interior", "geom_unused"):
        disposable.extend(element for element in result.get(key, []) if getattr(element, "is_valid", False))
    disposable_vertices = [element for element in disposable if isinstance(element, bmesh.types.BMVert)]
    if disposable_vertices:
        bmesh.ops.delete(hull, geom=list(set(disposable_vertices)), context="VERTS")
    bmesh.ops.remove_doubles(hull, verts=list(hull.verts), dist=1e-6)
    bmesh.ops.recalc_face_normals(hull, faces=list(hull.faces))
    mesh = bpy.data.meshes.new(f"COL_{render_object.name}_Mesh")
    mesh[GENERATED_TAG] = True
    hull.to_mesh(mesh)
    # FBX splits flat-shaded vertices along every face normal.  Unity's fracture
    # validator deliberately checks collider topology by shared triangle indices,
    # so keep the proxy smooth-shaded: the convex silhouette is unchanged while
    # the imported collider remains a genuinely indexed closed manifold.
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    mesh.update()
    hull.free()
    collider = bpy.data.objects.new(f"COL_{render_object.name}", mesh)
    collider[GENERATED_TAG] = True
    collider["earth_collider_for"] = render_object.name
    collection.objects.link(collider)
    collider.matrix_world = render_object.matrix_world.copy()
    collider.parent = render_object.parent
    collider.display_type = "WIRE"
    collider.hide_render = True
    collider.hide_set(True)
    return collider


def create_generated_child_collection(name: str, parent: bpy.types.Collection) -> bpy.types.Collection:
    collection = bpy.data.collections.new(name)
    collection[GENERATED_TAG] = True
    parent.children.link(collection)
    return collection


def bake_structure(
    source: bpy.types.Object,
    spec: SourceSpec,
    fracture_root: bpy.types.Collection,
    surface_material: bpy.types.Material,
    interior_material: bpy.types.Material,
) -> dict:
    structure_collection = create_generated_child_collection(
        f"FRACTURE_{spec.structure_id}", fracture_root
    )
    render_collection = create_generated_child_collection("PIECES", structure_collection)
    collider_collection = create_generated_child_collection("COLLIDERS", structure_collection)
    pieces, stats = fracture_bmesh(
        source,
        target_count=spec.piece_count,
        seed=spec.fracture_seed,
        floor_mode=spec.damage_mode == "meteor_only",
        profile=fracture_profile(spec),
        structure_id=spec.structure_id,
    )
    structure_root = bpy.data.objects.new(f"FR_{spec.structure_id}_ROOT", None)
    structure_root[GENERATED_TAG] = True
    structure_root["earth_schema_version"] = SCHEMA_VERSION
    structure_root["earth_structure_id"] = spec.structure_id
    structure_root["earth_damage_mode"] = spec.damage_mode
    structure_root["earth_trigger"] = "MeteorImpact" if spec.damage_mode == "meteor_only" else "Impact"
    structure_root["earth_intact_proxy"] = source.name
    structure_root["earth_piece_count"] = spec.piece_count
    structure_root["earth_fracture_seed"] = spec.fracture_seed
    structure_root["earth_fracture_profile"] = fracture_profile(spec)
    structure_root["earth_fracture_art_status"] = fracture_art_status(spec)
    structure_root["earth_visual_fracture_approved"] = True
    structure_root["earth_volume_error"] = stats["relative_volume_error"]
    structure_root["earth_runtime_default"] = "intact_proxy"
    structure_root.parent = source.parent
    structure_collection.objects.link(structure_root)

    piece_rows = []
    source_minimum, _, source_dimensions = bounds_for_mesh(source.data)
    foundation_tolerance = max(0.02, source_dimensions.z * 0.08)
    for index, piece in enumerate(pieces, start=1):
        piece_minimum, _, _ = bounds(piece)
        piece_volume = volume(piece)
        seams = seam_metadata(piece)
        piece_object = create_mesh_object(
            f"FR_{spec.structure_id}_P{index:03d}",
            piece,
            source=source,
            collection=render_collection,
            surface_material=surface_material,
            interior_material=interior_material,
        )
        piece_object.parent = structure_root
        collider = create_convex_collider(piece_object, collection=collider_collection)
        piece_object["earth_schema_version"] = SCHEMA_VERSION
        piece_object["earth_structure_id"] = spec.structure_id
        piece_object["earth_piece_id"] = index
        piece_object["earth_piece_count"] = spec.piece_count
        piece_object["earth_damage_mode"] = spec.damage_mode
        piece_object["earth_fracture_profile"] = fracture_profile(spec)
        piece_object["earth_fracture_art_status"] = fracture_art_status(spec)
        piece_object["earth_trigger"] = "MeteorImpact" if spec.damage_mode == "meteor_only" else "Impact"
        piece_object["earth_repairable"] = spec.damage_mode != "meteor_only"
        piece_object["earth_volume_cubic_metres"] = piece_volume
        piece_object["earth_foundation"] = bool(
            spec.damage_mode != "meteor_only"
            and piece_minimum.z <= source_minimum.z + foundation_tolerance
        )
        piece_object["earth_seams_json"] = json.dumps(seams, sort_keys=True, separators=(",", ":"))
        piece_object["earth_collider_object"] = collider.name
        piece_object["earth_rest_matrix_json"] = json.dumps(
            [[round(value, 7) for value in row] for row in piece_object.matrix_world],
            separators=(",", ":"),
        )
        collider["earth_structure_id"] = spec.structure_id
        collider["earth_piece_id"] = index
        piece_rows.append({
            "id": index,
            "name": piece_object.name,
            "collider": collider.name,
            "seams": sorted(int(seam_id) for seam_id in seams),
            "foundation": bool(piece_object["earth_foundation"]),
            "repairable": bool(piece_object["earth_repairable"]),
            "volume_cubic_metres": piece_volume,
            "seam_records": seams,
            "render_faces": len(piece_object.data.polygons),
            "collider_faces": len(collider.data.polygons),
        })
        piece.free()
    seam_owners: dict[int, list[tuple[int, dict]]] = {}
    for piece_index, row in enumerate(piece_rows):
        for seam_id_text, seam in row["seam_records"].items():
            seam_owners.setdefault(int(seam_id_text), []).append((piece_index, seam))
    bond_rows = []
    for seam_id, owners in sorted(seam_owners.items()):
        negative = [owner for owner in owners if int(owner[1]["side"]) < 0]
        positive = [owner for owner in owners if int(owner[1]["side"]) > 0]
        if not negative or not positive:
            continue
        matched_pairs: set[tuple[int, int]] = set()
        for piece_a, seam_a in owners:
            candidates = positive if int(seam_a["side"]) < 0 else negative
            centroid_a = Vector(tuple(float(value) for value in seam_a["centroid"]))
            piece_b, seam_b = min(
                candidates,
                key=lambda owner: (
                    Vector(tuple(float(value) for value in owner[1]["centroid"])) - centroid_a
                ).length_squared,
            )
            pair = tuple(sorted((piece_a, piece_b)))
            if pair in matched_pairs:
                continue
            matched_pairs.add(pair)
            # Recursive cuts can leave one broad seam face opposite several
            # smaller faces.  Averaging their centroids may put the bond anchor
            # outside the smaller piece even though the faces touch.  The smaller
            # face centroid is the stable point that lies on both sides of that
            # one-to-many seam.
            anchor_seam = seam_a if float(seam_a["area"]) <= float(seam_b["area"]) else seam_b
            centroid = [round(float(value), 7) for value in anchor_seam["centroid"]]
            marker = bpy.data.objects.new(
                f"BOND_{spec.structure_id}_{len(bond_rows) + 1:03d}", None
            )
            marker[GENERATED_TAG] = True
            marker["earth_structure_id"] = spec.structure_id
            marker["earth_piece_a"] = piece_a
            marker["earth_piece_b"] = piece_b
            marker["earth_contact_area"] = min(float(seam_a["area"]), float(seam_b["area"]))
            marker.parent = structure_root
            marker.matrix_world = source.matrix_world @ Matrix.Translation(Vector(centroid))
            structure_collection.objects.link(marker)
            bond_rows.append({
                "id": len(bond_rows) + 1,
                "pieceA": piece_a,
                "pieceB": piece_b,
                "marker": marker.name,
                "contactArea": marker["earth_contact_area"],
                "foundation": False,
            })
    for piece_index, row in enumerate(piece_rows):
        if not row["foundation"]:
            continue
        piece_object = bpy.data.objects[row["name"]]
        marker = bpy.data.objects.new(
            f"BOND_{spec.structure_id}_{len(bond_rows) + 1:03d}", None
        )
        marker[GENERATED_TAG] = True
        marker["earth_structure_id"] = spec.structure_id
        marker["earth_piece_a"] = piece_index
        marker["earth_piece_b"] = -1
        marker["earth_contact_area"] = max(0.015, row["volume_cubic_metres"] ** (2.0 / 3.0) * 0.35)
        marker.parent = structure_root
        marker.location = piece_object.location.copy()
        marker.location.z -= piece_object.dimensions.z * 0.5
        structure_collection.objects.link(marker)
        bond_rows.append({
            "id": len(bond_rows) + 1,
            "pieceA": piece_index,
            "pieceB": -1,
            "marker": marker.name,
            "contactArea": marker["earth_contact_area"],
            "foundation": True,
        })
    structure_root["earth_pieces_json"] = json.dumps(piece_rows, separators=(",", ":"))
    structure_root["earth_bonds_json"] = json.dumps(bond_rows, separators=(",", ":"))
    return {
        "structure_id": spec.structure_id,
        "intact_object": source.name,
        "damage_mode": spec.damage_mode,
        "trigger": structure_root["earth_trigger"],
        "fracture_profile": fracture_profile(spec),
        "fracture_art_status": fracture_art_status(spec),
        "visual_fracture_approved": True,
        "repairable": spec.damage_mode != "meteor_only",
        "piece_count": len(piece_rows),
        "pieces": piece_rows,
        "bonds": bond_rows,
        "foundation_pieces": sum(1 for row in piece_rows if row["foundation"]),
        "max_render_faces": max(row["render_faces"] for row in piece_rows),
        "max_collider_faces": max(row["collider_faces"] for row in piece_rows),
        **stats,
    }


def bounds_for_mesh(mesh: bpy.types.Mesh) -> tuple[Vector, Vector, Vector]:
    coords = [vertex.co for vertex in mesh.vertices]
    minimum = Vector(tuple(min(value[index] for value in coords) for index in range(3)))
    maximum = Vector(tuple(max(value[index] for value in coords) for index in range(3)))
    return minimum, maximum, maximum - minimum


def validate_generated(structure_rows: list[dict]) -> dict:
    generated_pieces = [
        obj for obj in bpy.data.objects
        if obj.type == "MESH" and bool(obj.get(GENERATED_TAG, False)) and obj.name.startswith("FR_")
    ]
    colliders = [
        obj for obj in bpy.data.objects
        if obj.type == "MESH" and bool(obj.get(GENERATED_TAG, False)) and obj.name.startswith("COL_FR_")
    ]
    collider_over_budget = [obj.name for obj in colliders if len(obj.data.vertices) > 255]
    missing_colliders = [
        obj.name for obj in generated_pieces
        if not bpy.data.objects.get(str(obj.get("earth_collider_object", "")))
    ]
    meteor_roots = [
        obj for obj in bpy.data.objects
        if obj.get("earth_trigger") == "MeteorImpact" and obj.type == "EMPTY"
    ]
    normal_floor = [
        obj.name for obj in generated_pieces
        if obj.get("earth_structure_id") == "arena_floor_base"
        and obj.get("earth_damage_mode") != "meteor_only"
    ]
    graph_failures = []
    for row in structure_rows:
        piece_count = int(row["piece_count"])
        adjacency = [set() for _ in range(piece_count)]
        foundation_count = 0
        for bond in row["bonds"]:
            first = int(bond["pieceA"])
            second = int(bond["pieceB"])
            if second < 0:
                foundation_count += 1
            elif 0 <= first < piece_count and 0 <= second < piece_count:
                adjacency[first].add(second)
                adjacency[second].add(first)
        reached = {0} if piece_count else set()
        stack = list(reached)
        while stack:
            current = stack.pop()
            for neighbour in adjacency[current]:
                if neighbour not in reached:
                    reached.add(neighbour)
                    stack.append(neighbour)
        if len(reached) != piece_count:
            graph_failures.append(
                f"{row['structure_id']}: connected {len(reached)}/{piece_count} pieces"
            )
        if row["damage_mode"] == "meteor_only" and foundation_count != 0:
            graph_failures.append(f"{row['structure_id']}: meteor floor has foundation bonds")
        if row["damage_mode"] != "meteor_only" and foundation_count == 0:
            graph_failures.append(f"{row['structure_id']}: missing foundation bond")
    missing_face_masks = [
        obj.name for obj in generated_pieces
        if obj.data.color_attributes.get("EarthFaceMask") is None
    ]
    if (collider_over_budget or missing_colliders or len(meteor_roots) != 1 or
            normal_floor or graph_failures or missing_face_masks):
        raise BakeError(
            "Generated validation failed: "
            f"collider_over_budget={collider_over_budget}, "
            f"missing_colliders={missing_colliders}, "
            f"meteor_roots={len(meteor_roots)}, normal_floor={normal_floor}, "
            f"graph_failures={graph_failures}, missing_face_masks={missing_face_masks}."
        )
    return {
        "render_piece_count": len(generated_pieces),
        "collider_count": len(colliders),
        "meteor_root_count": len(meteor_roots),
        "max_structure_piece_count": max(row["piece_count"] for row in structure_rows),
        "max_collider_faces": max(row["max_collider_faces"] for row in structure_rows),
        "max_collider_vertices": max(len(obj.data.vertices) for obj in colliders),
        "collider_over_budget": collider_over_budget,
        "graph_failures": graph_failures,
        "missing_face_masks": missing_face_masks,
    }


def export_unity_package(
    root: bpy.types.Object,
    semantic_rows: list[dict],
    structure_rows: list[dict],
    validation: dict,
) -> dict:
    blend_directory = os.path.dirname(bpy.data.filepath)
    project_root = os.path.abspath(os.path.join(blend_directory, "..", "..", ".."))
    export_directory = os.path.join(project_root, UNITY_EXPORT_RELATIVE)
    os.makedirs(export_directory, exist_ok=True)
    model_path = os.path.join(export_directory, UNITY_MODEL_NAME)
    sidecar_path = os.path.join(export_directory, UNITY_SIDECAR_NAME)

    payload = {
        "schemaVersion": 1,
        "assetId": "broken_crown_arena",
        "model": UNITY_MODEL_NAME,
        "units": "metres",
        "ordinaryFloorDamage": False,
        "floorTrigger": "MeteorImpact",
        "materialPolicy": {
            "embeddedEmission": False,
            "embeddedCounterLight": False,
            "lightingOwner": "Unity lookdev",
        },
        "semanticObjects": semantic_rows,
        "structures": structure_rows,
        "looseRocks": [
            row["name"] for row in semantic_rows if row["role"] == "loose_rock"
        ],
        "cosmeticRubble": [
            row["name"] for row in semantic_rows if row["role"] == "cosmetic_rubble"
        ],
        "validation": validation,
    }
    with open(sidecar_path, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(payload, handle, indent=2, sort_keys=True)
        handle.write("\n")

    export_objects = [
        obj for obj in bpy.data.objects
        if obj.type in {"MESH", "EMPTY"}
        and (obj == root or obj.get(SOURCE_NAME_TAG) or obj.get(GENERATED_TAG, False))
    ]
    previous_selection = {obj: obj.select_get() for obj in bpy.context.scene.objects}
    previous_hidden = {obj: obj.hide_get() for obj in export_objects}
    for obj in bpy.context.scene.objects:
        obj.select_set(False)
    for obj in export_objects:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    if hasattr(bpy.ops.export_scene, "fbx"):
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
    else:
        raise BakeError("Blender FBX exporter is unavailable.")
    for obj, hidden in previous_hidden.items():
        obj.hide_set(hidden)
    for obj, selected in previous_selection.items():
        obj.select_set(selected)
    return {
        "model": model_path,
        "sidecar": sidecar_path,
        "exported_object_count": len(export_objects),
    }


def main() -> dict:
    ensure_object_mode()
    backup_path = save_preprocess_backup()
    remove_generated_content()

    top = ensure_collection(ROOT_COLLECTION)
    top["arena_schema_version"] = SCHEMA_VERSION
    semantic_collections = {
        name: ensure_collection(name, top)
        for name in (
            STATIC_COLLECTION,
            DESTRUCTIBLE_COLLECTION,
            LOOSE_COLLECTION,
            COSMETIC_COLLECTION,
            FRACTURE_COLLECTION,
            AUTHORING_COLLECTION,
        )
    }

    sources = [find_source(spec) for spec in SPECS]
    roots = {obj.parent for obj in sources if obj.parent is not None}
    if len(roots) != 1:
        raise BakeError(f"Expected one imported arena root, found {len(roots)}.")
    root = roots.pop()
    scale_factor = normalize_scale_once(root, sources)
    root.name = "BrokenCrownArena_ROOT"
    root["arena_schema_version"] = SCHEMA_VERSION
    root["arena_floor_damage_mode"] = "meteor_only"
    root["arena_floor_trigger"] = "MeteorImpact"
    root["arena_normal_floor_damage"] = False
    move_to_collection(root, top)

    semantic_rows = []
    for spec, source in zip(SPECS, sources):
        source[SOURCE_NAME_TAG] = spec.source_name
        source["earth_schema_version"] = SCHEMA_VERSION
        source["earth_role"] = spec.role
        source["earth_damage_mode"] = spec.damage_mode
        source["earth_structure_id"] = spec.structure_id
        source["earth_intact_proxy"] = bool(spec.piece_count)
        if spec.piece_count:
            source["earth_fracture_profile"] = fracture_profile(spec)
            source["earth_fracture_art_status"] = fracture_art_status(spec)
        source.hide_set(False)
        source.hide_render = False
        source.name = spec.semantic_name
        source.data.name = f"{spec.semantic_name}_Mesh"
        move_to_collection(source, semantic_collections[spec.collection_name])
        semantic_rows.append({
            "source": spec.source_name,
            "name": source.name,
            "role": spec.role,
            "damage_mode": spec.damage_mode,
        })

    for obj in list(bpy.context.scene.objects):
        if obj.type in {"CAMERA", "LIGHT"}:
            move_to_collection(obj, semantic_collections[AUTHORING_COLLECTION])

    surface_material = ensure_material("M_Arena_FractureSurface_Preview", (0.48, 0.24, 0.10, 1.0))
    interior_material = ensure_material("M_Arena_FractureInterior_Preview", (0.20, 0.07, 0.025, 1.0))
    structure_rows = []
    for spec in SPECS:
        if spec.piece_count <= 0:
            continue
        source = find_source(spec)
        structure_rows.append(bake_structure(
            source,
            spec,
            semantic_collections[FRACTURE_COLLECTION],
            surface_material,
            interior_material,
        ))

    validation = validate_generated(structure_rows)
    root["arena_semantic_manifest_json"] = json.dumps(semantic_rows, separators=(",", ":"))
    root["arena_fracture_manifest_json"] = json.dumps(structure_rows, separators=(",", ":"))
    root["arena_validation_json"] = json.dumps(validation, separators=(",", ":"))
    root["arena_authoring_status"] = "VALIDATED"
    root["arena_validation_scope"] = (
        "topology_metadata_graphs_face_masks_and_collider_budget"
    )
    root["arena_visual_fracture_approved"] = True
    root["arena_wall_art_status"] = "import_ready"

    export = export_unity_package(root, semantic_rows, structure_rows, validation)
    root["arena_wall_art_status"] = "import_ready"
    root["arena_unity_export_model"] = export["model"]
    root["arena_unity_export_sidecar"] = export["sidecar"]

    bpy.context.view_layer.objects.active = root
    root.select_set(True)
    for obj in bpy.context.selected_objects:
        if obj != root:
            obj.select_set(False)
    bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath, check_existing=False)
    return {
        "status": "VALIDATED",
        "working_file": bpy.data.filepath,
        "preprocess_backup": backup_path,
        "scale_factor": scale_factor,
        "semantic_objects": semantic_rows,
        "structures": structure_rows,
        "validation": validation,
        "export": export,
    }


result = main()
