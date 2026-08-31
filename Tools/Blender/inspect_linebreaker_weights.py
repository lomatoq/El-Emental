#!/usr/bin/env python3
"""Report Linebreaker mesh islands and their proximity to secondary bones."""

from __future__ import annotations

import json
import sys
from collections import Counter, deque
from pathlib import Path

import bpy
from mathutils import Vector


SECONDARY_PREFIX = "Secondary_"


def parse_report_path() -> Path | None:
    args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    if "--report" not in args:
        return None
    index = args.index("--report")
    return Path(args[index + 1]).expanduser().resolve()


def armature_space_point(mesh_obj, armature, point: Vector) -> Vector:
    world = mesh_obj.matrix_world @ point
    return armature.matrix_world.inverted_safe() @ world


def segment_distance(point: Vector, start: Vector, end: Vector) -> float:
    axis = end - start
    length_squared = axis.length_squared
    if length_squared <= 1.0e-12:
        return (point - start).length
    t = max(0.0, min(1.0, (point - start).dot(axis) / length_squared))
    return (point - start.lerp(end, t)).length


def connected_components(mesh) -> list[list[int]]:
    adjacency: list[list[int]] = [[] for _ in mesh.vertices]
    for edge in mesh.edges:
        a, b = edge.vertices
        adjacency[a].append(b)
        adjacency[b].append(a)
    remaining = set(range(len(mesh.vertices)))
    components: list[list[int]] = []
    while remaining:
        root = remaining.pop()
        queue = deque([root])
        component = [root]
        while queue:
            current = queue.popleft()
            for neighbour in adjacency[current]:
                if neighbour not in remaining:
                    continue
                remaining.remove(neighbour)
                queue.append(neighbour)
                component.append(neighbour)
        components.append(component)
    return components


def component_report(mesh_obj, armature, indices: list[int], secondary_bones) -> dict:
    points = [
        armature_space_point(mesh_obj, armature, mesh_obj.data.vertices[index].co)
        for index in indices
    ]
    minimum = Vector((min(point.x for point in points), min(point.y for point in points), min(point.z for point in points)))
    maximum = Vector((max(point.x for point in points), max(point.y for point in points), max(point.z for point in points)))
    centroid = sum(points, Vector()) / len(points)
    group_names = {group.index: group.name for group in mesh_obj.vertex_groups}
    group_weights: Counter[str] = Counter()
    group_samples: dict[str, list[float]] = {}
    for index in indices:
        for membership in mesh_obj.data.vertices[index].groups:
            name = group_names.get(membership.group)
            if name:
                group_weights[name] += membership.weight
                group_samples.setdefault(name, []).append(membership.weight)
    nearest = sorted(
        (
            segment_distance(centroid, bone.head_local, bone.tail_local),
            bone.name,
        )
        for bone in secondary_bones
    )[:4]
    return {
        "vertex_count": len(indices),
        "min": [round(value, 6) for value in minimum],
        "max": [round(value, 6) for value in maximum],
        "centroid": [round(value, 6) for value in centroid],
        "nearest_secondary_bones": [
            {"name": name, "distance": round(distance, 6)}
            for distance, name in nearest
        ],
        "dominant_existing_groups": [
            {"name": name, "weight_sum": round(weight, 4)}
            for name, weight in group_weights.most_common(8)
        ],
        "secondary_group_stats": [
            {
                "name": name,
                "vertices": len(weights),
                "minimum": round(min(weights), 6),
                "maximum": round(max(weights), 6),
                "mean": round(sum(weights) / len(weights), 6),
            }
            for name, weights in sorted(group_samples.items())
            if name.startswith(SECONDARY_PREFIX)
        ],
    }


def main() -> int:
    armatures = [obj for obj in bpy.data.objects if obj.type == "ARMATURE"]
    meshes = [obj for obj in bpy.data.objects if obj.type == "MESH"]
    if len(armatures) != 1:
        raise RuntimeError(f"Expected one armature, found {len(armatures)}")
    armature = armatures[0]
    secondary_bones = [
        bone for bone in armature.data.bones if bone.name.startswith(SECONDARY_PREFIX)
    ]
    payload = {
        "blend_file": bpy.data.filepath,
        "armature": armature.name,
        "secondary_bones": [
            {
                "name": bone.name,
                "parent": bone.parent.name if bone.parent else "",
                "head": [round(value, 6) for value in bone.head_local],
                "tail": [round(value, 6) for value in bone.tail_local],
            }
            for bone in secondary_bones
        ],
        "meshes": [],
    }
    for mesh_obj in meshes:
        components = connected_components(mesh_obj.data)
        reports = [
            component_report(mesh_obj, armature, component, secondary_bones)
            for component in components
        ]
        reports.sort(key=lambda item: item["vertex_count"], reverse=True)
        payload["meshes"].append(
            {
                "name": mesh_obj.name,
                "vertex_count": len(mesh_obj.data.vertices),
                "component_count": len(reports),
                "components": reports,
            }
        )
    output = json.dumps(payload, indent=2, sort_keys=True)
    print(output)
    report_path = parse_report_path()
    if report_path is not None:
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_text(output + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
