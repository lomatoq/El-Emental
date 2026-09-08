"""Encode the real 30 FPS PlayMode contact/recovery frames, without interpolation.

Run after LocalPhysicsAcceptancePlay passes. Requires imageio-ffmpeg (encoder only).
The original PNG frames and CSV measurements remain the acceptance source.
"""
import json
from pathlib import Path
import subprocess

import imageio_ffmpeg


def main():
    root = Path(__file__).resolve().parent.parent
    report_path = root / "BuildReports/LocalPhysicsAcceptancePlay.json"
    report = json.loads(report_path.read_text(encoding="utf-8-sig"))
    if report["failed"] or report["result"] != "Passed":
        raise SystemExit("The regional contact acceptance must pass before encoding its delivery videos.")
    folder = root / "BuildReports/LocalPhysicsAcceptance"
    outputs = []
    for sequence in ("Head", "LeftArm", "RightArm", "LeftLeg", "RightLeg"):
        source = folder / sequence
        frames = sorted(source.glob("frame-*.png"))
        if len(frames) != 48 or frames[-1].name != "frame-047.png":
            raise SystemExit(f"{sequence}: expected all 48 recorded frames, found {len(frames)}")
        output = folder / f"{sequence}.mp4"
        subprocess.run([
            imageio_ffmpeg.get_ffmpeg_exe(), "-hide_banner", "-loglevel", "error", "-y",
            "-framerate", "30", "-i", str(source / "frame-%03d.png"),
            "-frames:v", "48", "-c:v", "libx264", "-crf", "17", "-pix_fmt", "yuv420p",
            "-movflags", "+faststart", str(output),
        ], check=True)
        outputs.append({"sequence": sequence, "frames": 48, "fps": 30,
                        "file": str(output.relative_to(root)), "bytes": output.stat().st_size})
    (folder / "videos.json").write_text(json.dumps({"testUtc": report["utc"],
        "source": str(report_path.relative_to(root)), "interpolation": False,
        "videos": outputs}, indent=2), encoding="utf-8")
    print(json.dumps(outputs, indent=2))


if __name__ == "__main__":
    main()
