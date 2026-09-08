"""Compile staged overlays with Unity's existing response files; never refresh Unity.

This verifies C# only. Graph import, shaders, execution and performance need Unity.
Run from any directory after staging owners freeze their source files.
"""
from pathlib import Path
import argparse
import subprocess
import json
import time

HERE = Path(__file__).resolve().parents[2]
ROOT = HERE.parents[1]
UNITY = Path('C:/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Data')
parser = argparse.ArgumentParser()
parser.add_argument('--assemblies', nargs='+', default=['Elemental.Simulation', 'Elemental.Runtime', 'Elemental.Presentation', 'Elemental.Authoring.Editor', 'Elemental.Tests.EditMode', 'Elemental.Tests.PlayMode'])
args = parser.parse_args()
output = HERE / 'Staged/ProductionRestoreMatterTrace/compile'
output.mkdir(parents=True, exist_ok=True)
overlays = {}
for base in [HERE/'Staged/ProductionRestoreMatterTrace/after']:
    for source in (base/'Assets').rglob('*.cs'):
        relative = source.relative_to(base).as_posix()
        if relative in overlays:
            raise RuntimeError('Overlapping stage ownership: '+relative)
        overlays[relative] = source

def assembly_for(path):
    directory = (ROOT/path).parent
    while directory != ROOT:
        definitions = list(directory.glob('*.asmdef')) if directory.exists() else []
        if definitions:
            return json.loads(definitions[0].read_text(encoding='utf-8-sig'))['name']
        directory = directory.parent
    raise RuntimeError('No assembly owner for '+path)

report = []
for assembly in args.assemblies:
    source_rsp = ROOT/'Library/Bee/artifacts/1900b0aE.dag'/f'{assembly}.rsp'
    lines = source_rsp.read_text(encoding='utf-8-sig').splitlines()
    result = []
    seen = set()
    for line in lines:
        if line.startswith(('-out:', '-refout:')):
            suffix = '.ref.dll' if line.startswith('-refout:') else '.dll'
            result.append(line.split(':',1)[0]+':"'+(output/(assembly+suffix)).as_posix()+'"')
        elif line.startswith('-r:'):
            referenced = Path(line[3:].strip('"')).name
            local = output/referenced
            result.append('-r:"'+local.as_posix()+'"' if local.exists() else line)
        elif line.strip('"').endswith('.cs'):
            path = line.strip('"').replace('\\','/')
            if path in overlays:
                result.append('"'+overlays[path].as_posix()+'"')
                seen.add(path)
            else:
                result.append(line)
        else:
            result.append(line)
    for path, staged in overlays.items():
        if path not in seen and assembly_for(path) == assembly:
            result.append('"'+staged.as_posix()+'"')
    rsp = output/(assembly+'.rsp')
    rsp.write_text('\n'.join(result), encoding='utf-8')
    csc = UNITY/'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'
    start = time.time()
    run = subprocess.run([str(UNITY/'DotNetSdk/dotnet.exe'), str(csc), '@'+str(rsp)], cwd=ROOT, capture_output=True, text=True, encoding='utf-8', errors='replace')
    log = output/(assembly+'.log')
    log.write_text(run.stdout+run.stderr, encoding='utf-8')
    report.append(dict(assembly=assembly, exit_code=run.returncode, elapsed_seconds=round(time.time()-start,2), log=str(log)))
    print(assembly, run.returncode, run.stdout[-6000:], run.stderr[-1000:], flush=True)
    if run.returncode:
        break
(output/'report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
raise SystemExit(any(item['exit_code'] for item in report))
