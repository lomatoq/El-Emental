#!/usr/bin/env python3
"""Paint deterministic secondary-bone weights on the combined Linebreaker mesh.

The script deliberately uses the already-authored secondary vertex groups as the
semantic mask. It never selects body vertices from a loose spatial guess:

* vertices already touched by Secondary_Tail_* become a smooth tail/HairLock map;
* vertices already touched by Secondary_Belt_L_* become the left belt map;
* vertices already touched by Secondary_Belt_R_* become the right belt map.

Run without --apply for a dry-run report. Use --apply with --save-as to write a
parallel .blend. Re-running against the result is idempotent.
"""

from __future__ import annotations

import argparse
import json
import math
import sys
import traceback
from dataclasses import asdict, dataclass, field
from pathlib import Path

import bpy
from mathutils import Vector


TAIL_BONES = (
    "Secondary_Tail_01",
    "Secondary_Tail_02",
    "Secondary_Tail_03",
)
LEFT_BELT_BONES = (
    "Secondary_Belt_L_01",
    "Secondary_Belt_L_02",
)
RIGHT_BELT_BONES = (
    "Secondary_Belt_R_01",
    "Secondary_Belt_R_02",
)
HAIR_LOCK = "Secondary_HairLock"
SECONDARY_GROUPS = set(TAIL_BONES + LEFT_BELT_BONES + RIGHT_BELT_BONES + (HAIR_LOCK,))


@dataclass
class Finding:
    severity: str
    code: str
    message: str
    subject: str = ""


@dataclass
class WeightReport:
    blend_file: str
    mesh: str = ""
    armature: str = ""
    applied: bool = False
    saved_as: str = ""
    plume_vertices: int = 0
    left_belt_vertices: int = 0
    right_belt_vertices: int = 0
    changed_vertices: int = 0
    maximum_weight_delta: float = 0.0
    maximum_normalization_error: float = 0.0
    group_stats: dict[str, dict[str, float | int]] = field(default_factory=dict)
    findings: list[Finding] = field(default_factory=list)

    def add(self, severity: str, code: str, message: str, subject: str = "") -> None:
        self.findings.append(Finding(severity, code, message, subject))

    @property
    def error_count(self) -> int:
        return sum(item.severity == "error" for item in self.findings)

    @property
    def warning_count(self) -> int:
        return sum(item.severity == "warning" for item in self.findings)

    def to_json(self) -> str:
        payload = asdict(self)
        payload["error_count"] = self.error_count
        payload["warning_count"] = self.warning_count
        return json.dumps(payload, indent=2, sort_keys=True)


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--armature", default="LinebreakerRig")
    parser.add_argument("--mesh", default="LinebreakerBody")
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--save-as", default="")
    parser.add_argument("--report", default="")
    parser.add_argument("--strict", action="store_true")
    return parser.parse_args(argv)


def resolve_object(name: str, object_type: str, report: WeightReport):
    exact = bpy.data.objects.get(name)
    if exact is not None and exact.type == object_type:
        return exact
    candidates = [obj for obj in bpy.data.objects if obj.type == object_type]
    if len(candidates) == 1:
        return candidates[0]
    report.add(
        "error",
        f"{object_type}_AMBIGUOUS",
        f"Expected '{name}' or exactly one {object_type}; found {[obj.name for obj in candidates]}.",
        name,
    )
    return None


def armature_space_point(mesh_obj, armature, point: Vector) -> Vector:
    return armature.matrix_world.inverted_safe() @ (mesh_obj.matrix_world @ point)


def segment_distance(point: Vector, start: Vector, end: Vector) -> float:
    axis = end - start
    length_squared = axis.length_squared
    if length_squared <= 1.0e-12:
        return (point - start).length
    t = max(0.0, min(1.0, (point - start).dot(axis) / length_squared))
    return (point - start.lerp(end, t)).length


def smoothstep01(value: float) -> float:
    value = max(0.0, min(1.0, value))
    return value * value * (3.0 - 2.0 * value)


def current_weights(mesh_obj, vertex) -> dict[str, float]:
    names = {group.index: group.name for group in mesh_obj.vertex_groups}
    return {
        names[item.group]: float(item.weight)
        for item in vertex.groups
        if item.group in names and item.weight > 1.0e-8
    }


def selected_vertices(mesh_obj, groups: tuple[str, ...]) -> list[int]:
    indices = {
        mesh_obj.vertex_groups[name].index
        for name in groups
        if mesh_obj.vertex_groups.get(name) is not None
    }
    result: list[int] = []
    for vertex in mesh_obj.data.vertices:
        if any(item.group in indices and item.weight > 1.0e-6 for item in vertex.groups):
            result.append(vertex.index)
    return result


def gaussian_bone_mix(point: Vector, bones, radius: float) -> dict[str, float]:
    safe_radius = max(1.0e-4, radius)
    raw: dict[str, float] = {}
    for bone in bones:
        distance = segment_distance(point, bone.head_local, bone.tail_local)
        raw[bone.name] = math.exp(-((distance / safe_radius) ** 2))
    total = sum(raw.values())
    if total <= 1.0e-12:
        nearest = min(
            bones,
            key=lambda bone: segment_distance(point, bone.head_local, bone.tail_local),
        )
        return {bone.name: 1.0 if bone == nearest else 0.0 for bone in bones}
    return {name: value / total for name, value in raw.items()}


def desired_plume_weights(point: Vector, tail_bones, hair_bone) -> dict[str, float]:
    tail_distance = min(
        segment_distance(point, bone.head_local, bone.tail_local) for bone in tail_bones
    )
    lock_distance = segment_distance(point, hair_bone.head_local, hair_bone.tail_local)
    denominator = max(1.0e-6, tail_distance + lock_distance)
    tail_share = smoothstep01(lock_distance / denominator)
    tail_mix = gaussian_bone_mix(point, tail_bones, radius=0.085)
    desired = {name: tail_share * weight for name, weight in tail_mix.items()}
    desired[HAIR_LOCK] = 1.0 - tail_share
    return desired


def desired_belt_weights(point: Vector, belt_bones) -> dict[str, float]:
    return gaussian_bone_mix(point, belt_bones, radius=0.055)


def maximum_delta(current: dict[str, float], desired: dict[str, float]) -> float:
    names = set(current) | set(desired)
    return max((abs(current.get(name, 0.0) - desired.get(name, 0.0)) for name in names), default=0.0)


def set_exclusive_deform_weights(mesh_obj, armature, vertex_index: int, desired: dict[str, float]) -> None:
    deform_names = {bone.name for bone in armature.data.bones if bone.use_deform}
    for group in mesh_obj.vertex_groups:
        if group.name in deform_names:
            group.remove([vertex_index])
    for name, weight in desired.items():
        if weight <= 1.0e-7:
            continue
        group = mesh_obj.vertex_groups.get(name)
        if group is None:
            group = mesh_obj.vertex_groups.new(name=name)
        group.add([vertex_index], float(weight), "REPLACE")


def collect_group_stats(mesh_obj, groups: set[str]) -> dict[str, dict[str, float | int]]:
    result: dict[str, dict[str, float | int]] = {}
    by_index = {group.index: group.name for group in mesh_obj.vertex_groups if group.name in groups}
    samples: dict[str, list[float]] = {name: [] for name in groups}
    for vertex in mesh_obj.data.vertices:
        for item in vertex.groups:
            name = by_index.get(item.group)
            if name is not None and item.weight > 1.0e-7:
                samples[name].append(float(item.weight))
    for name in sorted(groups):
        values = samples[name]
        result[name] = {
            "vertices": len(values),
            "minimum": min(values) if values else 0.0,
            "maximum": max(values) if values else 0.0,
            "mean": sum(values) / len(values) if values else 0.0,
        }
    return result


def validate_weights(mesh_obj, armature, selections: dict[str, list[int]], report: WeightReport) -> None:
    deform_names = {bone.name for bone in armature.data.bones if bone.use_deform}
    expected_by_region = {
        "plume": set(TAIL_BONES + (HAIR_LOCK,)),
        "left_belt": set(LEFT_BELT_BONES),
        "right_belt": set(RIGHT_BELT_BONES),
    }
    for region, indices in selections.items():
        if not indices:
            report.add("error", "EMPTY_WEIGHT_REGION", f"No vertices found for {region}.", region)
            continue
        expected = expected_by_region[region]
        for index in indices:
            weights = current_weights(mesh_obj, mesh_obj.data.vertices[index])
            deform = {name: weight for name, weight in weights.items() if name in deform_names}
            total = sum(deform.values())
            report.maximum_normalization_error = max(
                report.maximum_normalization_error,
                abs(1.0 - total),
            )
            unexpected = sum(weight for name, weight in deform.items() if name not in expected)
            if unexpected > 1.0e-5:
                report.add(
                    "error",
                    "SECONDARY_REGION_LEAK",
                    f"Vertex {index} in {region} retains {unexpected:.6f} non-region deform weight.",
                    mesh_obj.name,
                )
                return
    if report.maximum_normalization_error > 1.0e-5:
        report.add(
            "error",
            "WEIGHTS_NOT_NORMALIZED",
            f"Maximum selected-region normalization error is {report.maximum_normalization_error:.8f}.",
            mesh_obj.name,
        )


def write_report(report: WeightReport, path: str) -> None:
    payload = report.to_json()
    print(payload)
    if not path:
        return
    output = Path(path).expanduser().resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(payload + "\n", encoding="utf-8")


def main() -> int:
    args = parse_args()
    report = WeightReport(blend_file=bpy.data.filepath or "<unsaved>", applied=args.apply)
    try:
        armature = resolve_object(args.armature, "ARMATURE", report)
        mesh_obj = resolve_object(args.mesh, "MESH", report)
        if armature is None or mesh_obj is None:
            write_report(report, args.report)
            return 2
        report.armature = armature.name
        report.mesh = mesh_obj.name

        required = TAIL_BONES + LEFT_BELT_BONES + RIGHT_BELT_BONES + (HAIR_LOCK,)
        missing = [name for name in required if armature.data.bones.get(name) is None]
        if missing:
            report.add("error", "SECONDARY_BONES_MISSING", f"Missing bones: {missing}", armature.name)
            write_report(report, args.report)
            return 2

        for name in required:
            if mesh_obj.vertex_groups.get(name) is None:
                mesh_obj.vertex_groups.new(name=name)

        plume = selected_vertices(mesh_obj, TAIL_BONES + (HAIR_LOCK,))
        left_belt = selected_vertices(mesh_obj, LEFT_BELT_BONES)
        right_belt = selected_vertices(mesh_obj, RIGHT_BELT_BONES)
        report.plume_vertices = len(plume)
        report.left_belt_vertices = len(left_belt)
        report.right_belt_vertices = len(right_belt)
        selections = {"plume": plume, "left_belt": left_belt, "right_belt": right_belt}

        tail_bones = [armature.data.bones[name] for name in TAIL_BONES]
        left_bones = [armature.data.bones[name] for name in LEFT_BELT_BONES]
        right_bones = [armature.data.bones[name] for name in RIGHT_BELT_BONES]
        hair_bone = armature.data.bones[HAIR_LOCK]
        desired_by_vertex: dict[int, dict[str, float]] = {}
        for index in plume:
            point = armature_space_point(mesh_obj, armature, mesh_obj.data.vertices[index].co)
            desired_by_vertex[index] = desired_plume_weights(point, tail_bones, hair_bone)
        for index in left_belt:
            point = armature_space_point(mesh_obj, armature, mesh_obj.data.vertices[index].co)
            desired_by_vertex[index] = desired_belt_weights(point, left_bones)
        for index in right_belt:
            point = armature_space_point(mesh_obj, armature, mesh_obj.data.vertices[index].co)
            desired_by_vertex[index] = desired_belt_weights(point, right_bones)

        for index, desired in desired_by_vertex.items():
            current = current_weights(mesh_obj, mesh_obj.data.vertices[index])
            delta = maximum_delta(current, desired)
            report.maximum_weight_delta = max(report.maximum_weight_delta, delta)
            if delta > 1.0e-6:
                report.changed_vertices += 1
            if args.apply:
                set_exclusive_deform_weights(mesh_obj, armature, index, desired)

        if args.apply:
            mesh_obj.data.update()
        validate_weights(mesh_obj, armature, selections, report)
        report.group_stats = collect_group_stats(mesh_obj, SECONDARY_GROUPS)

        if args.apply:
            if not args.save_as:
                report.add("error", "SAVE_AS_REQUIRED", "--apply requires --save-as; source is never overwritten.")
            elif report.error_count == 0:
                output = str(Path(args.save_as).expanduser().resolve())
                bpy.ops.wm.save_as_mainfile(filepath=output)
                report.saved_as = output
    except Exception as exc:
        report.add("error", "UNHANDLED_EXCEPTION", f"{type(exc).__name__}: {exc}")
        traceback.print_exc()

    write_report(report, args.report)
    if report.error_count:
        return 2
    if args.strict and report.warning_count:
        return 3
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
