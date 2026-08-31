"""Derive deterministic masonry macro-cell sites from exported wall meshes.

This is an offline authoring helper, not a Unity or Blender runtime dependency.
It uses a filled voxel volume and distance-transform watershed to find the visual
lobes of Tripo's welded masonry piles. The resulting compact JSON is committed as
the editable input consumed by ``bake_broken_crown_arena.py``.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
import trimesh
from scipy import ndimage
from skimage.feature import peak_local_max
from skimage.segmentation import watershed


def derive_sites(path: Path, target_count: int) -> dict:
    data = np.load(path)
    mesh = trimesh.Trimesh(
        vertices=np.asarray(data["vertices"], dtype=np.float64),
        faces=np.asarray(data["faces"], dtype=np.int64),
        process=True,
        validate=True,
    )
    longest = float(np.max(mesh.extents))
    pitch = longest / 112.0
    voxels = mesh.voxelized(pitch).fill()
    occupied = np.asarray(voxels.matrix, dtype=bool)
    distance = ndimage.distance_transform_edt(occupied)

    peaks = np.empty((0, 3), dtype=np.int64)
    for separation_metres in (0.42, 0.36, 0.30, 0.24, 0.18, 0.12):
        peaks = peak_local_max(
            distance,
            labels=occupied,
            min_distance=max(1, int(round(separation_metres / pitch))),
            threshold_rel=0.10,
            num_peaks=target_count,
            exclude_border=False,
        )
        if len(peaks) >= target_count:
            break
    if len(peaks) < target_count:
        raise RuntimeError(f"{path}: found only {len(peaks)}/{target_count} masonry lobes")

    peaks = peaks[:target_count]
    markers = np.zeros_like(distance, dtype=np.int32)
    for label, index in enumerate(peaks, start=1):
        markers[tuple(index)] = label
    labels = watershed(-distance, markers=markers, mask=occupied)

    rows = []
    for label in range(1, target_count + 1):
        indices = np.argwhere(labels == label)
        if len(indices) == 0:
            raise RuntimeError(f"{path}: empty watershed region {label}")
        points = voxels.indices_to_points(indices)
        centroid = points.mean(axis=0)
        peak_radius = float(distance[tuple(peaks[label - 1])] * pitch)
        rows.append({
            "position": [round(float(value), 7) for value in centroid],
            "radius": round(peak_radius, 7),
            "voxelCount": int(len(indices)),
        })

    rows.sort(key=lambda row: (
        round(row["position"][2], 4),
        round(row["position"][1], 4),
        round(row["position"][0], 4),
    ))
    return {
        "source": path.name,
        "targetCount": target_count,
        "voxelPitch": pitch,
        "sites": rows,
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("east", type=Path)
    parser.add_argument("west", type=Path)
    parser.add_argument("--target", type=int, default=12)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    payload = {
        "schemaVersion": 1,
        "generator": "voxel_distance_watershed",
        "walls": {
            "arena_wall_east": derive_sites(args.east, args.target),
            "arena_wall_west": derive_sites(args.west, args.target),
        },
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(payload, separators=(",", ":")))


if __name__ == "__main__":
    main()
