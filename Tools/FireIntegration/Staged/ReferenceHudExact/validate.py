from pathlib import Path
import subprocess
import json
stage = Path(__file__).resolve().parent
root = stage.parents[3]
unity = Path('C:/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Data')
out = stage/'compile'
out.mkdir(exist_ok=True)
sources = {p.relative_to(stage/'after').as_posix(): p for p in (stage/'after/Assets').rglob('*.cs')}
skin_key='Assets/Elemental/Presentation/UI/ElementalStoneSkin.cs'
sources[skin_key]=stage.parent/'ReferenceSpriteFidelity/after'/skin_key
# Neighbor's newly imported source is not yet listed in the cached Bee response
# file while Unity remains in Play. Include the actual file, never a stub.
cloud_key='Assets/Elemental/Presentation/Rendering/ValleyCloudParticles.cs'
if (root/cloud_key).exists(): sources[cloud_key]=root/cloud_key
report=[]
for assembly, folder in [('Elemental.Presentation','Presentation'), ('Elemental.Authoring.Editor','Authoring/Editor'), ('Elemental.Tests.EditMode','Tests/EditMode'), ('Elemental.Tests.PlayMode','Tests/PlayMode')]:
    original = root/'Library/Bee/artifacts/1900b0aE.dag'/f'{assembly}.rsp'
    lines=[]; seen=set()
    for line in original.read_text(encoding='utf-8-sig').splitlines():
        if line.startswith(('-out:', '-refout:')):
            suffix='.ref.dll' if line.startswith('-refout:') else '.dll'
            line=line.split(':',1)[0]+':"'+(out/(assembly+suffix)).as_posix()+'"'
        elif line.startswith('-r:'):
            replacement=out/Path(line[3:].strip('"')).name
            if replacement.exists(): line='-r:"'+replacement.as_posix()+'"'
        elif line.strip('"').endswith('.cs'):
            key=line.strip('"').replace('\\','/')
            if key in sources: line='"'+sources[key].as_posix()+'"'; seen.add(key)
        lines.append(line)
    for key,path in sources.items():
        if key not in seen and key.startswith('Assets/Elemental/'+folder+'/'): lines.append('"'+path.as_posix()+'"')
    rsp=out/f'{assembly}.rsp'; rsp.write_text('\n'.join(lines),encoding='utf-8')
    result=subprocess.run([str(unity/'DotNetSdk/dotnet.exe'), str(unity/'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'), '@'+str(rsp)],cwd=root,capture_output=True,text=True,encoding='utf-8',errors='replace')
    (out/f'{assembly}.log').write_text(result.stdout+result.stderr,encoding='utf-8')
    report.append({'assembly':assembly,'exit_code':result.returncode})
    print(assembly,result.returncode)
    if result.returncode: print(result.stdout[-5000:]); break
(out/'report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
raise SystemExit(any(r['exit_code'] for r in report))
