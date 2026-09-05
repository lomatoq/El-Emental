"""Compare Unity Inference Engine CPU output with ONNX Runtime on identical inputs."""

from __future__ import annotations

import argparse
import hashlib
import json
import platform
from pathlib import Path

import numpy as np
import onnxruntime as ort


EXPECTED_SHA256 = "39b553e197f62f077975ba38512bc04781a3fc37c2af7c6756e04629f760edea"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(8 * 1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def to_ort_inputs(item: dict) -> dict[str, np.ndarray]:
    return {
        "context_mujoco_qpos": np.asarray(item["contextMujocoQpos"], np.float32).reshape(1, 4, 36),
        "target_vel": np.asarray([item["targetVelocity"]], np.float32),
        "mode": np.asarray([item["mode"]], np.int64),
        "movement_direction": np.asarray(item["movementDirection"], np.float32).reshape(1, 3),
        "facing_direction": np.asarray(item["facingDirection"], np.float32).reshape(1, 3),
        "random_seed": np.asarray([item["randomSeed"]], np.int64),
        "has_specific_target": np.asarray([[item["hasSpecificTarget"]]], np.int64),
        "specific_target_positions": np.asarray(item["specificTargetPositions"], np.float32).reshape(1, 4, 3),
        "specific_target_headings": np.asarray(item["specificTargetHeadings"], np.float32).reshape(1, 4),
        "allowed_pred_num_tokens": np.asarray(item["allowedPredictionTokenCounts"], np.int64).reshape(1, 11),
        "height": np.asarray([item["height"]], np.float32),
    }


def compare_case(session: ort.InferenceSession, case: dict, atol: float, rtol: float) -> dict:
    ort_qpos, ort_count = session.run(
        ["mujoco_qpos", "num_pred_frames"], to_ort_inputs(case["input"])
    )
    ort_frames = int(np.asarray(ort_count).reshape(-1)[0])
    unity_frames = int(case["validFrames"])
    unity = np.asarray(case["validQpos"], np.float32).reshape(unity_frames, 36)
    reference = np.asarray(ort_qpos, np.float32).reshape(64, 36)[:ort_frames]
    compared_frames = min(unity_frames, ort_frames)
    unity_compared = unity[:compared_frames]
    reference_compared = reference[:compared_frames]
    absolute = np.abs(unity_compared - reference_compared)
    relative = absolute / np.maximum(np.abs(reference_compared), np.float32(1e-6))
    per_frame_max = np.max(absolute, axis=1)
    per_frame_rmse = np.sqrt(np.mean(np.square(absolute), axis=1))
    close = bool(
        unity_frames == ort_frames
        and np.allclose(unity_compared, reference_compared, atol=atol, rtol=rtol)
    )
    return {
        "name": case["name"],
        "unityValidFrames": unity_frames,
        "ortValidFrames": ort_frames,
        "frameCountEqual": unity_frames == ort_frames,
        "comparedValues": int(absolute.size),
        "maxAbsoluteError": float(np.max(absolute)),
        "meanAbsoluteError": float(np.mean(absolute)),
        "p99AbsoluteError": float(np.quantile(absolute, 0.99)),
        "rmse": float(np.sqrt(np.mean(np.square(absolute)))),
        "maxRelativeErrorAbove1eMinus6": float(np.max(relative)),
        "maxAbsoluteErrorByFrame": per_frame_max.astype(float).tolist(),
        "rmseByFrame": per_frame_rmse.astype(float).tolist(),
        "withinTolerance": close,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--unity-report",
        type=Path,
        default=Path("BuildReports/SonicPrototype/UnityParityVectors.json"),
    )
    parser.add_argument(
        "--model",
        type=Path,
        default=Path("Tools/SonicPrototype/Models/planner_sonic_6733128.onnx"),
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path("BuildReports/SonicPrototype/UnityVsOrtParity.json"),
    )
    parser.add_argument("--atol", type=float, default=1e-4)
    parser.add_argument("--rtol", type=float, default=1e-3)
    args = parser.parse_args()

    actual_hash = sha256(args.model)
    if actual_hash != EXPECTED_SHA256:
        raise SystemExit(f"model SHA-256 mismatch: {actual_hash}")
    unity_report = json.loads(args.unity_report.read_text(encoding="utf-8"))
    if unity_report.get("status") != "Passed":
        raise SystemExit("Unity parity vector export did not pass")

    options = ort.SessionOptions()
    options.intra_op_num_threads = 0
    session = ort.InferenceSession(
        str(args.model), sess_options=options, providers=["CPUExecutionProvider"]
    )
    cases = [compare_case(session, case, args.atol, args.rtol) for case in unity_report["cases"]]
    output = {
        "status": "Passed" if all(case["withinTolerance"] for case in cases) else "Different",
        "meaning": "Passed means numeric agreement at the recorded tolerance; Different requires inspection and is not a retarget-quality result.",
        "modelSha256": actual_hash,
        "unityReport": str(args.unity_report),
        "onnxRuntimeVersion": ort.__version__,
        "python": platform.python_version(),
        "providers": session.get_providers(),
        "absoluteTolerance": args.atol,
        "relativeTolerance": args.rtol,
        "cases": cases,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(output, indent=2), encoding="utf-8")
    print(json.dumps(output, indent=2))
    return 0 if output["status"] == "Passed" else 2


if __name__ == "__main__":
    raise SystemExit(main())
