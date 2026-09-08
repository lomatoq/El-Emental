from pathlib import Path
import csv
import subprocess
import imageio_ffmpeg

root = Path(__file__).resolve().parents[2]
folder = root / "BuildReports/SequentialRepair/Wall"
rows = list(csv.DictReader((folder / "Frames.csv").open(encoding="utf-8-sig")))
assert rows, "No completed capture metadata"

def stamp(seconds):
    milliseconds = round(seconds * 1000)
    hours, milliseconds = divmod(milliseconds, 3600000)
    minutes, milliseconds = divmod(milliseconds, 60000)
    seconds, milliseconds = divmod(milliseconds, 1000)
    return f"{hours:02}:{minutes:02}:{seconds:02},{milliseconds:03}"

labels = []
for index, row in enumerate(rows):
    labels.append(f"{index + 1}\n{stamp(index / 10)} --> {stamp((index + 1) / 10)}\n"
                  f"{row['stage']} | seated {row['welded']}/{row['total']} | "
                  f"piece {row['active']} {row['phase']} | simulation {row['time']}s\n")
(folder / "RepairLabels.srt").write_text("\n".join(labels), encoding="utf-8")
subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(), "-y", "-loglevel", "error",
                "-framerate", "10", "-i", "frame-%03d.png", "-frames:v", str(len(rows)),
                "-vf", "subtitles=RepairLabels.srt:force_style='FontSize=16,Alignment=2,MarginV=12'",
                "-c:v", "libx264", "-crf", "19", "-pix_fmt", "yuv420p", "-movflags", "+faststart",
                "SequentialWallRepair.mp4"], cwd=folder, check=True)
print(f"Encoded {len(rows)} actual frames at 10fps")
