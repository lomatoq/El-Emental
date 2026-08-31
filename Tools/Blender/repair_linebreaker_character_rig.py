#!/usr/bin/env python3
"""
Idempotent Linebreaker rig repair and validation for Blender 4.x.

Run:
  blender LinebreakerRigged.blend --background --python \
    Tools/Blender/repair_linebreaker_character_rig.py -- \
    --apply --apply-hair-lock --report /tmp/linebreaker-rig-report.json \
    --save-as LinebreakerRigged_repaired.blend

Without --apply the script is read-only and emits a validation report.
Hair reweighting is opt-in and only touches mesh objects whose names clearly
match the hair-name expression. It never guesses that the full body mesh is hair.
"""

from __future__ import annotations

import argparse
import json
import math
import re
import sys
import traceback
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Iterable, Optional

try:
    import bpy
    from mathutils import Vector
except ImportError as exc:  # pragma: no cover - executed only outside Blender.
    raise SystemExit("This script must be executed by Blender's Python runtime.") from exc


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
HELMET_ANCHOR = "Secondary_HelmetAnchor"
HAIR_LOCK = "Secondary_HairLock"

HEAD_ALIASES = (
    "mixamorig:Head",
    "mixamorig_Head",
    "Head",
    "head",
)
HIPS_ALIASES = (
    "mixamorig:Hips",
    "mixamorig_Hips",
    "Hips",
    "hips",
    "Pelvis",
    "pelvis",
)

DEFAULT_HAIR_PATTERN = (
    r"(^|[_.\-\s])"
    r"(hair|ponytail|pony_tail|bangs?|fringe|braids?|hairlock|hair_lock|strands?)"
    r"($|[_.\-\s])"
)


@dataclass
class Finding:
    severity: str
    code: str
    message: str
    subject: str = ""


@dataclass
class RepairReport:
    blend_file: str
    armature: str = ""
    applied: bool = False
    hair_lock_applied: bool = False
    changed_bones: list[str] = field(default_factory=list)
    changed_objects: list[str] = field(default_factory=list)
    hair_candidates: list[str] = field(default_factory=list)
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
    argv = sys.argv
    argv = argv[argv.index("--") + 1 :] if "--" in argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--armature", default="", help="Exact armature object name.")
    parser.add_argument("--apply", action="store_true", help="Apply safe hierarchy/constraint repairs.")
    parser.add_argument(
        "--apply-hair-lock",
        action="store_true",
        help="Rigidly bind clearly named hair mesh objects to the helmet anchor.",
    )
    parser.add_argument(
        "--hair-pattern",
        default=DEFAULT_HAIR_PATTERN,
        help="Case-insensitive regex used to identify separate hair mesh objects.",
    )
    parser.add_argument("--report", default="", help="Optional JSON report path.")
    parser.add_argument("--save", action="store_true", help="Save the current .blend in place.")
    parser.add_argument("--save-as", default="", help="Save to a new .blend path.")
    parser.add_argument(
        "--strict",
        action="store_true",
        help="Exit non-zero when validation warnings remain, not only errors.",
    )
    return parser.parse_args(argv)


def set_active_object(obj: bpy.types.Object) -> None:
    if bpy.context.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def find_named_bone(bones: Iterable, aliases: Iterable[str]):
    by_name = {bone.name: bone for bone in bones}
    for alias in aliases:
        if alias in by_name:
            return by_name[alias]
    lower = {bone.name.lower(): bone for bone in bones}
    for alias in aliases:
        match = lower.get(alias.lower())
        if match is not None:
            return match
    return None


def armature_score(obj: bpy.types.Object) -> tuple[int, int]:
    if obj.type != "ARMATURE":
        return (-1, -1)
    names = {bone.name for bone in obj.data.bones}
    humanoid_hits = sum(
        name in names
        for name in (
            "mixamorig:Hips",
            "mixamorig:Spine",
            "mixamorig:Head",
            "mixamorig:LeftUpLeg",
            "mixamorig:RightUpLeg",
        )
    )
    secondary_hits = sum(name in names for name in (*TAIL_BONES, *LEFT_BELT_BONES, *RIGHT_BELT_BONES))
    return humanoid_hits, secondary_hits


def resolve_armature(name: str, report: RepairReport) -> Optional[bpy.types.Object]:
    if name:
        obj = bpy.data.objects.get(name)
        if obj is None or obj.type != "ARMATURE":
            report.add("error", "ARMATURE_NOT_FOUND", f"Armature '{name}' was not found.", name)
            return None
        return obj

    active = bpy.context.view_layer.objects.active
    if active is not None and active.type == "ARMATURE":
        return active

    candidates = [obj for obj in bpy.data.objects if obj.type == "ARMATURE"]
    if not candidates:
        report.add("error", "NO_ARMATURE", "The blend contains no armature object.")
        return None
    candidates.sort(key=armature_score, reverse=True)
    winner = candidates[0]
    if len(candidates) > 1 and armature_score(candidates[0]) == armature_score(candidates[1]):
        report.add(
            "error",
            "AMBIGUOUS_ARMATURE",
            "Multiple armatures have the same Linebreaker score; pass --armature explicitly.",
        )
        return None
    return winner


def safe_axis(bone, fallback: Vector) -> Vector:
    axis = bone.tail - bone.head
    return axis.normalized() if axis.length > 1.0e-5 else fallback.normalized()


def ensure_edit_bone(
    armature: bpy.types.Object,
    name: str,
    parent,
    head: Vector,
    tail: Vector,
    use_deform: bool,
    report: RepairReport,
):
    bones = armature.data.edit_bones
    bone = bones.get(name)
    if bone is None:
        bone = bones.new(name)
        bone.head = head
        bone.tail = tail
        report.changed_bones.append(name)
    if (bone.tail - bone.head).length <= 1.0e-5:
        bone.head = head
        bone.tail = tail
        report.changed_bones.append(name)
    if bone.parent != parent:
        bone.parent = parent
        bone.use_connect = False
        report.changed_bones.append(name)
    bone.use_deform = use_deform
    return bone


def ensure_secondary_hierarchy(armature: bpy.types.Object, report: RepairReport) -> None:
    set_active_object(armature)
    bpy.ops.object.mode_set(mode="EDIT")
    bones = armature.data.edit_bones
    head_bone = find_named_bone(bones, HEAD_ALIASES)
    hips_bone = find_named_bone(bones, HIPS_ALIASES)
    if head_bone is None:
        report.add("error", "HEAD_BONE_MISSING", "Could not resolve the humanoid head bone.")
    if hips_bone is None:
        report.add("error", "HIPS_BONE_MISSING", "Could not resolve the humanoid hips bone.")
    if head_bone is None or hips_bone is None:
        bpy.ops.object.mode_set(mode="OBJECT")
        return

    head_axis = safe_axis(head_bone, Vector((0.0, 0.0, 1.0)))
    head_length = max((head_bone.tail - head_bone.head).length, 0.08)
    anchor_head = head_bone.head.lerp(head_bone.tail, 0.72)
    anchor_tail = anchor_head + head_axis * max(0.035, head_length * 0.22)
    helmet_anchor = ensure_edit_bone(
        armature,
        HELMET_ANCHOR,
        head_bone,
        anchor_head,
        anchor_tail,
        False,
        report,
    )

    # Existing secondary bone positions are preserved. Missing bones are created
    # in compact conservative chains so the script remains idempotent and does
    # not redesign the silhouette.
    tail_fallback_axis = Vector((0.0, -1.0, 0.0))
    tail_parent = helmet_anchor
    tail_start = helmet_anchor.tail
    tail_step = max(head_length * 0.22, 0.045)
    for index, name in enumerate(TAIL_BONES):
        existing = bones.get(name)
        if existing is not None:
            start = existing.head.copy()
            end = existing.tail.copy()
        else:
            start = tail_start + tail_fallback_axis * tail_step * index
            end = tail_start + tail_fallback_axis * tail_step * (index + 1)
        tail_parent = ensure_edit_bone(
            armature, name, tail_parent, start, end, True, report
        )

    hips_axis = safe_axis(hips_bone, Vector((0.0, 0.0, 1.0)))
    hips_length = max((hips_bone.tail - hips_bone.head).length, 0.12)
    lateral = hips_axis.cross(Vector((0.0, 1.0, 0.0)))
    if lateral.length <= 1.0e-5:
        lateral = Vector((1.0, 0.0, 0.0))
    lateral.normalize()
    down = -hips_axis
    belt_step = max(hips_length * 0.28, 0.055)

    for names, side in ((LEFT_BELT_BONES, 1.0), (RIGHT_BELT_BONES, -1.0)):
        parent = hips_bone
        root = hips_bone.head + lateral * side * hips_length * 0.22
        for index, name in enumerate(names):
            existing = bones.get(name)
            if existing is not None:
                start = existing.head.copy()
                end = existing.tail.copy()
            else:
                start = root + down * belt_step * index
                end = root + down * belt_step * (index + 1)
            parent = ensure_edit_bone(
                armature, name, parent, start, end, True, report
            )

    hair_bone = bones.get(HAIR_LOCK)
    if hair_bone is None:
        hair_bone = bones.new(HAIR_LOCK)
        hair_bone.head = helmet_anchor.head.copy()
        hair_bone.tail = helmet_anchor.tail.copy()
        report.changed_bones.append(HAIR_LOCK)
    hair_bone.parent = helmet_anchor
    hair_bone.use_connect = False
    hair_bone.use_deform = True

    bpy.ops.object.mode_set(mode="POSE")
    configure_pose_constraints(armature, report)
    bpy.ops.object.mode_set(mode="OBJECT")


def replace_limit_rotation(
    pose_bone,
    constraint_name: str,
    x_degrees: float,
    y_degrees: float,
    z_degrees: float,
) -> None:
    existing = pose_bone.constraints.get(constraint_name)
    if existing is not None and existing.type != "LIMIT_ROTATION":
        pose_bone.constraints.remove(existing)
        existing = None
    constraint = existing or pose_bone.constraints.new("LIMIT_ROTATION")
    constraint.name = constraint_name
    constraint.owner_space = "LOCAL"
    constraint.use_transform_limit = True
    limits = (
        ("use_limit_x", "min_x", "max_x", x_degrees),
        ("use_limit_y", "min_y", "max_y", y_degrees),
        ("use_limit_z", "min_z", "max_z", z_degrees),
    )
    for enabled_name, minimum_name, maximum_name, degrees in limits:
        setattr(constraint, enabled_name, True)
        radians = math.radians(abs(degrees))
        setattr(constraint, minimum_name, -radians)
        setattr(constraint, maximum_name, radians)


def configure_pose_constraints(armature: bpy.types.Object, report: RepairReport) -> None:
    for index, name in enumerate(TAIL_BONES):
        pose_bone = armature.pose.bones.get(name)
        if pose_bone is None:
            report.add("error", "TAIL_POSE_BONE_MISSING", f"Missing pose bone '{name}'.", name)
            continue
        replace_limit_rotation(
            pose_bone,
            "ELM_TailSafetyLimit",
            x_degrees=12.0 + index * 2.0,
            y_degrees=8.0,
            z_degrees=12.0 + index * 2.0,
        )
        pose_bone["elemental_secondary_role"] = "helmet_tail"
        pose_bone["elemental_chain_index"] = index

    for names, role in (
        (LEFT_BELT_BONES, "belt_left"),
        (RIGHT_BELT_BONES, "belt_right"),
    ):
        for index, name in enumerate(names):
            pose_bone = armature.pose.bones.get(name)
            if pose_bone is None:
                report.add("error", "BELT_POSE_BONE_MISSING", f"Missing pose bone '{name}'.", name)
                continue
            replace_limit_rotation(
                pose_bone,
                "ELM_BeltSafetyLimit",
                x_degrees=16.0 + index * 4.0,
                y_degrees=8.0,
                z_degrees=18.0 + index * 4.0,
            )
            pose_bone["elemental_secondary_role"] = role
            pose_bone["elemental_chain_index"] = index

    hair_bone = armature.pose.bones.get(HAIR_LOCK)
    if hair_bone is not None:
        replace_limit_rotation(hair_bone, "ELM_HairRigidLock", 0.0, 0.0, 0.0)
        hair_bone["elemental_secondary_role"] = "helmet_hair_lock"


def find_hair_candidates(pattern: re.Pattern[str], armature: bpy.types.Object) -> list[bpy.types.Object]:
    results: list[bpy.types.Object] = []
    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue
        if not pattern.search(obj.name):
            continue
        # Exclude known combined body names even if a careless user supplied an
        # overly broad expression.
        lowered = obj.name.lower()
        if any(token in lowered for token in ("body", "character", "linebreaker", "bender")) and not any(
            token in lowered for token in ("hair", "bang", "fringe", "braid", "ponytail")
        ):
            continue
        results.append(obj)
    return results


def ensure_armature_modifier(mesh_obj: bpy.types.Object, armature: bpy.types.Object) -> None:
    for modifier in mesh_obj.modifiers:
        if modifier.type == "ARMATURE":
            modifier.object = armature
            return
    modifier = mesh_obj.modifiers.new(name="ELM_LinebreakerArmature", type="ARMATURE")
    modifier.object = armature


def deform_bone_names(armature: bpy.types.Object) -> set[str]:
    return {bone.name for bone in armature.data.bones if bone.use_deform}


def rigid_weight_mesh_to_hair_lock(
    mesh_obj: bpy.types.Object,
    armature: bpy.types.Object,
    report: RepairReport,
) -> None:
    ensure_armature_modifier(mesh_obj, armature)
    deform_names = deform_bone_names(armature)
    vertex_count = len(mesh_obj.data.vertices)
    if vertex_count <= 0:
        report.add("warning", "EMPTY_HAIR_MESH", "Hair candidate has no vertices.", mesh_obj.name)
        return

    # Remove only deform-bone groups. Non-deform authoring groups are preserved.
    for group in list(mesh_obj.vertex_groups):
        if group.name in deform_names and group.name != HAIR_LOCK:
            mesh_obj.vertex_groups.remove(group)
    group = mesh_obj.vertex_groups.get(HAIR_LOCK)
    if group is None:
        group = mesh_obj.vertex_groups.new(name=HAIR_LOCK)
    group.add(list(range(vertex_count)), 1.0, "REPLACE")
    mesh_obj["elemental_rigid_hair_lock"] = True
    report.changed_objects.append(mesh_obj.name)


def validate_hair_mesh(
    mesh_obj: bpy.types.Object,
    armature: bpy.types.Object,
    report: RepairReport,
) -> None:
    deform_names = deform_bone_names(armature)
    group_by_index = {group.index: group.name for group in mesh_obj.vertex_groups}
    unweighted = 0
    outside_lock = 0
    for vertex in mesh_obj.data.vertices:
        deform_weights = [
            item
            for item in vertex.groups
            if group_by_index.get(item.group) in deform_names and item.weight > 1.0e-5
        ]
        if not deform_weights:
            unweighted += 1
            continue
        lock_weight = sum(
            item.weight
            for item in deform_weights
            if group_by_index.get(item.group) == HAIR_LOCK
        )
        if lock_weight < 0.999:
            outside_lock += 1

    if unweighted:
        report.add(
            "error",
            "HAIR_UNWEIGHTED_VERTICES",
            f"{unweighted} hair vertices have no deform weight.",
            mesh_obj.name,
        )
    if outside_lock:
        report.add(
            "warning",
            "HAIR_NOT_FULLY_LOCKED",
            f"{outside_lock} hair vertices are not rigidly assigned to {HAIR_LOCK}.",
            mesh_obj.name,
        )


def validate_hierarchy(armature: bpy.types.Object, report: RepairReport) -> None:
    bones = armature.data.bones
    head = find_named_bone(bones, HEAD_ALIASES)
    hips = find_named_bone(bones, HIPS_ALIASES)
    if head is None:
        report.add("error", "HEAD_BONE_MISSING", "Could not resolve the humanoid head bone.")
    if hips is None:
        report.add("error", "HIPS_BONE_MISSING", "Could not resolve the humanoid hips bone.")

    expected_parents = {
        HELMET_ANCHOR: head.name if head else "",
        TAIL_BONES[0]: HELMET_ANCHOR,
        TAIL_BONES[1]: TAIL_BONES[0],
        TAIL_BONES[2]: TAIL_BONES[1],
        LEFT_BELT_BONES[0]: hips.name if hips else "",
        LEFT_BELT_BONES[1]: LEFT_BELT_BONES[0],
        RIGHT_BELT_BONES[0]: hips.name if hips else "",
        RIGHT_BELT_BONES[1]: RIGHT_BELT_BONES[0],
        HAIR_LOCK: HELMET_ANCHOR,
    }
    for name, expected_parent in expected_parents.items():
        bone = bones.get(name)
        if bone is None:
            report.add("error", "SECONDARY_BONE_MISSING", f"Required bone '{name}' is missing.", name)
            continue
        if (bone.tail_local - bone.head_local).length <= 1.0e-5:
            report.add("error", "ZERO_LENGTH_BONE", f"Bone '{name}' has zero length.", name)
        actual_parent = bone.parent.name if bone.parent else ""
        if expected_parent and actual_parent != expected_parent:
            report.add(
                "error",
                "WRONG_SECONDARY_PARENT",
                f"Expected parent '{expected_parent}', found '{actual_parent or '<none>'}'.",
                name,
            )

    scale = armature.scale
    if any(abs(component - 1.0) > 1.0e-4 for component in scale):
        report.add(
            "warning",
            "ARMATURE_SCALE_NOT_APPLIED",
            f"Armature object scale is {tuple(round(value, 5) for value in scale)}.",
            armature.name,
        )
    if armature.rotation_mode == "QUATERNION":
        rotation_magnitude = abs(armature.rotation_quaternion.angle)
    else:
        rotation_magnitude = max(abs(value) for value in armature.rotation_euler)
    if rotation_magnitude > 1.0e-4:
        report.add(
            "warning",
            "ARMATURE_ROTATION_NOT_APPLIED",
            "Armature object rotation is not identity; verify FBX export transform settings.",
            armature.name,
        )


def write_report(report: RepairReport, path: str) -> None:
    payload = report.to_json()
    print(payload)
    if not path:
        return
    output = Path(path).expanduser().resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(payload + "\n", encoding="utf-8")


def save_blend(args: argparse.Namespace, report: RepairReport) -> None:
    if args.save_as:
        output = str(Path(args.save_as).expanduser().resolve())
        bpy.ops.wm.save_as_mainfile(filepath=output)
        report.changed_objects.append(f"saved:{output}")
    elif args.save:
        if not bpy.data.filepath:
            report.add(
                "error",
                "SAVE_PATH_MISSING",
                "--save was requested but the current blend has no file path; use --save-as.",
            )
            return
        bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
        report.changed_objects.append(f"saved:{bpy.data.filepath}")


def main() -> int:
    args = parse_args()
    report = RepairReport(
        blend_file=bpy.data.filepath or "<unsaved>",
        applied=bool(args.apply or args.apply_hair_lock),
        hair_lock_applied=bool(args.apply_hair_lock),
    )
    try:
        armature = resolve_armature(args.armature, report)
        if armature is None:
            write_report(report, args.report)
            return 2
        report.armature = armature.name

        hair_pattern = re.compile(args.hair_pattern, re.IGNORECASE)
        hair_candidates = find_hair_candidates(hair_pattern, armature)
        report.hair_candidates = [obj.name for obj in hair_candidates]
        if not hair_candidates:
            report.add(
                "warning",
                "NO_CLEAR_HAIR_OBJECT",
                "No separate hair mesh matched the conservative name pattern; body geometry was not guessed.",
            )

        if args.apply or args.apply_hair_lock:
            ensure_secondary_hierarchy(armature, report)
        if args.apply_hair_lock:
            for mesh_obj in hair_candidates:
                rigid_weight_mesh_to_hair_lock(mesh_obj, armature, report)

        validate_hierarchy(armature, report)
        for mesh_obj in hair_candidates:
            validate_hair_mesh(mesh_obj, armature, report)

        save_blend(args, report)
    except Exception as exc:  # Keep a machine-readable report for CI/automation.
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
