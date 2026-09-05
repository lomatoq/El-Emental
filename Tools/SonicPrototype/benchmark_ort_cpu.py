#!/usr/bin/env python3
"""Isolated CPU evidence for the pinned official SONIC planner. No project assets are read or changed."""
from __future__ import annotations
import hashlib, json, math, os, platform, statistics, sys, time
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
import onnxruntime as ort
try:
    import psutil
except ImportError:
    psutil = None

ROOT = Path(__file__).resolve().parent
MODEL = ROOT / "Models" / "planner_sonic_6733128.onnx"
REPORT = ROOT / "Reports" / "OrtCpuBenchmark.json"
EXPECTED_SHA256 = "39b553e197f62f077975ba38512bc04781a3fc37c2af7c6756e04629f760edea"
EXPECTED_BYTES = 773_952_989


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(8 * 1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def inputs(mode: int, moving: bool) -> dict[str, np.ndarray]:
    context = np.zeros((1, 4, 36), dtype=np.float32)
    context[:, :, 2] = 0.78  # plausible standing G1 root height, Z-up
    context[:, :, 3] = 1.0   # MuJoCo quaternion order w,x,y,z
    allowed = np.zeros((1, 11), dtype=np.int64)
    allowed[0, 0] = 1        # shortest valid 6-token / 24-frame prediction
    return {
        "context_mujoco_qpos": context,
        "target_vel": np.array([-1.0], dtype=np.float32),
        "mode": np.array([mode], dtype=np.int64),
        "movement_direction": np.array([[1.0, 0.0, 0.0] if moving else [0.0, 0.0, 0.0]], dtype=np.float32),
        "facing_direction": np.array([[1.0, 0.0, 0.0]], dtype=np.float32),
        "random_seed": np.array([20260905], dtype=np.int64),
        "has_specific_target": np.array([[0]], dtype=np.int64),
        "specific_target_positions": np.zeros((1, 4, 3), dtype=np.float32),
        "specific_target_headings": np.zeros((1, 4), dtype=np.float32),
        "allowed_pred_num_tokens": allowed,
        "height": np.array([-1.0], dtype=np.float32),
    }


def percentile(samples: list[float], q: float) -> float:
    ordered = sorted(samples)
    if not ordered:
        return math.nan
    position = (len(ordered) - 1) * q
    lo, hi = math.floor(position), math.ceil(position)
    if lo == hi:
        return ordered[lo]
    return ordered[lo] * (hi - position) + ordered[hi] * (position - lo)


def memory_snapshot() -> dict[str, int | None]:
    if psutil is None:
        return {"rssBytes": None, "peakWorkingSetBytes": None}
    info = psutil.Process().memory_info()
    return {"rssBytes": int(info.rss), "peakWorkingSetBytes": int(getattr(info, "peak_wset", info.rss))}


def run_case(session: ort.InferenceSession, name: str, mode: int, moving: bool, samples: int = 10) -> dict:
    feed = inputs(mode, moving)
    # One explicit warmup per mode is outside the distribution.
    warm_start = time.perf_counter()
    warm_outputs = session.run(None, feed)
    warm_ms = (time.perf_counter() - warm_start) * 1000.0
    timings = []
    output = warm_outputs
    for _ in range(samples):
        start = time.perf_counter()
        output = session.run(None, feed)
        timings.append((time.perf_counter() - start) * 1000.0)
    qpos, valid = output
    valid_count = int(np.asarray(valid).reshape(-1)[0])
    return {
        "name": name,
        "mode": mode,
        "warmupMs": warm_ms,
        "samplesMs": timings,
        "p50Ms": statistics.median(timings),
        "p95Ms": percentile(timings, .95),
        "meanMs": statistics.fmean(timings),
        "minMs": min(timings),
        "maxMs": max(timings),
        "validFrames": valid_count,
        "qposShape": list(qpos.shape),
        "numPredFramesShape": list(np.asarray(valid).shape),
        "numPredFramesDtype": str(np.asarray(valid).dtype),
        "finiteValidOutput": bool(np.isfinite(qpos[:, :valid_count, :]).all()),
        "rootStart": qpos[0, 0, :7].astype(float).tolist(),
    }


def main() -> int:
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    result = {
        "utc": datetime.now(timezone.utc).isoformat(),
        "model": str(MODEL),
        "expectedBytes": EXPECTED_BYTES,
        "actualBytes": MODEL.stat().st_size,
        "expectedSha256": EXPECTED_SHA256,
        "actualSha256": sha256(MODEL),
        "python": sys.version,
        "platform": platform.platform(),
        "processor": platform.processor(),
        "logicalCpuCount": os.cpu_count(),
        "onnxRuntimeVersion": ort.__version__,
        "availableProviders": ort.get_available_providers(),
        "memoryBefore": memory_snapshot(),
        "status": "Starting",
    }
    if result["actualBytes"] != EXPECTED_BYTES or result["actualSha256"] != EXPECTED_SHA256:
        result["status"] = "ModelIdentityMismatch"
        REPORT.write_text(json.dumps(result, indent=2), encoding="utf-8")
        return 2
    try:
        options = ort.SessionOptions()
        options.graph_optimization_level = ort.GraphOptimizationLevel.ORT_ENABLE_ALL
        options.log_severity_level = 3
        start = time.perf_counter()
        session = ort.InferenceSession(str(MODEL), sess_options=options, providers=["CPUExecutionProvider"])
        result["sessionCreateMs"] = (time.perf_counter() - start) * 1000.0
        result["memoryAfterSession"] = memory_snapshot()
        result["runtimeInputs"] = [{"name": x.name, "shape": x.shape, "type": x.type} for x in session.get_inputs()]
        result["runtimeOutputs"] = [{"name": x.name, "shape": x.shape, "type": x.type} for x in session.get_outputs()]
        result["cases"] = [
            run_case(session, "walk", 2, True),
            run_case(session, "randomPunches", 13, False),
        ]
        result["memoryAfterRuns"] = memory_snapshot()
        result["status"] = "Passed" if all(x["finiteValidOutput"] and 24 <= x["validFrames"] <= 64 for x in result["cases"]) else "InvalidOutput"
    except Exception as exc:
        result["status"] = "Failed"
        result["error"] = repr(exc)
    REPORT.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result, indent=2))
    return 0 if result["status"] == "Passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
