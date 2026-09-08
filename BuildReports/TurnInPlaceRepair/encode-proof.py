"""Encode the recorded turn frames with their sampled timestamps, no interpolation."""
import json
from pathlib import Path
import subprocess
import imageio_ffmpeg

folder = Path(__file__).resolve().parent / 'Proof'
report = json.loads((folder / 'TurnStepFrames.json').read_text(encoding='utf-8-sig'))
captures = []
scenario = None
next_capture = 0
for sample in report['frames']:
    if sample['scenario'] != scenario:
        scenario = sample['scenario']
        next_capture = 0
    if sample['time'] + 0.00001 >= next_capture:
        captures.append({'scenario': scenario, 'time': sample['time']})
        next_capture = sample['time'] + .1
images = sorted(folder.glob('frame-*.png'))
if len(captures) != len(images):
    raise SystemExit(f'Capture reconstruction mismatch: {len(captures)} timestamps / {len(images)} images. Do not encode stale frames.')
lines = ['ffconcat version 1.0']
for index, (sample, image) in enumerate(zip(captures, images)):
    duration = .1
    if index + 1 < len(captures) and captures[index + 1]['scenario'] == sample['scenario']:
        duration = captures[index + 1]['time'] - sample['time']
    sample['file'] = image.name
    sample['duration'] = duration
    lines += [f"file '{image.name}'", f'duration {duration:.8f}']
lines.append(f"file '{images[-1].name}'")
concat = folder / 'TurnSteps.ffconcat'
concat.write_text('\n'.join(lines) + '\n', encoding='utf-8')
output = folder / 'TurnSteps.mp4'
subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(), '-hide_banner', '-loglevel', 'error', '-y',
    '-safe', '0', '-f', 'concat', '-i', str(concat), '-fps_mode', 'vfr', '-c:v', 'libx264',
    '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(output)], check=True)
(folder / 'VideoFrames.json').write_text(json.dumps({'interpolation': False,
    'scenarioPausesTrimmed': True, 'frames': captures}, indent=2), encoding='utf-8')
print(f'{len(images)} original frames encoded: {output.name} ({output.stat().st_size} bytes)')
