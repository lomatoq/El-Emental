from pathlib import Path
import subprocess,json,shutil,hashlib
lane=Path(__file__).resolve().parent;root=lane.parents[3];out=lane/'Reports';out.mkdir(exist_ok=True)
unity=Path('C:/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Data');dotnet=unity/'DotNetSdk/dotnet.exe';csc=unity/'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'
for assembly in ['Elemental.Presentation','Elemental.Authoring.Editor','Elemental.Tests.EditMode']:
 lines=[]
 for line in (root/'Library/Bee/artifacts/1900b0aE.dag'/f'{assembly}.rsp').read_text(encoding='utf-8-sig').splitlines():
  if line.startswith(('-out:','-refout:')):line=line.split(':',1)[0]+':"'+(out/(assembly+('.ref.dll' if line.startswith('-refout:') else '.dll'))).as_posix()+'"'
  elif line.startswith('-r:') and (out/Path(line[3:].strip('"')).name).exists():line='-r:"'+(out/Path(line[3:].strip('"')).name).as_posix()+'"'
  elif line.strip('"').endswith('.cs') and (lane/'after'/line.strip('"')).exists():line='"'+(lane/'after'/line.strip('"')).as_posix()+'"'
  lines.append(line)
 rsp=out/(assembly+'.rsp');rsp.write_text(chr(10).join(lines));run=subprocess.run([str(dotnet),str(csc),'@'+str(rsp)],cwd=root,capture_output=True,text=True);(out/(assembly+'.log')).write_text(run.stdout+run.stderr);print(assembly,run.returncode)
 if run.returncode:print(run.stdout);raise SystemExit(run.returncode)
refs=unity/'DotNetSdk/packs/Microsoft.NETCore.App.Ref/8.0.21/ref/net8.0'
maths=[root/'Library/ScriptAssemblies/Unity.Mathematics.dll',unity/'Managed/UnityEngine/UnityEngine.MathematicsModule.dll']
for m in maths:shutil.copy2(m,out/m.name)
old=lane/'before/Assets/Elemental/Presentation/Environment/RockShapeBuilder.cs';oldtext=old.read_text();oldtext=oldtext[:oldtext.index('    [Serializable] public struct RockShapeSettings')]+oldtext[oldtext.index('    public sealed class RockShapeData'):];(out/'Reference.cs').write_text(oldtext.replace('RockShapeBuilder','ReferenceBuilder').replace('RockShapeData','ReferenceData'))
sources=[lane/'Oracle.cs',out/'Reference.cs',lane/'after/Assets/Elemental/Presentation/Environment/RockShapeBuilder.cs',root/'Assets/Elemental/Presentation/Environment/RockPolyhedron.cs']
lines=['-nologo','-target:exe','-optimize+','-out:"'+(out/'Oracle.dll').as_posix()+'"']+['-r:"'+x.as_posix()+'"' for x in list(refs.glob('*.dll'))+maths]+['"'+x.as_posix()+'"' for x in sources]
rsp=out/'oracle.rsp';rsp.write_text(chr(10).join(lines));run=subprocess.run([str(dotnet),str(csc),'@'+str(rsp)],capture_output=True,text=True);print('Oracle compile',run.returncode,run.stdout)
if run.returncode:raise SystemExit(run.returncode)
(out/'Oracle.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.21'}}}))
run=subprocess.run([str(dotnet),str(out/'Oracle.dll')],cwd=out,capture_output=True,text=True);print(run.stdout,run.stderr);(out/'oracle-result.txt').write_text(run.stdout+run.stderr)
for rel,sha in json.loads((lane/'baseline.json').read_text()).items():assert hashlib.sha256((root/rel).read_bytes()).hexdigest()==sha,rel
print('Actual four source baselines unchanged')
raise SystemExit(run.returncode)
