from pathlib import Path
import subprocess,json,shutil,hashlib
lane=Path(__file__).resolve().parent;root=lane.parents[4];out=lane/'Reports';out.mkdir(exist_ok=True)
unity=Path('C:/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Data');dotnet=unity/'DotNetSdk/dotnet.exe';csc=unity/'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'
rel='Assets/Elemental/Presentation/Fire/FireCpuField.cs';lines=[]
for line in (root/'Library/Bee/artifacts/1900b0aE.dag/Elemental.Presentation.rsp').read_text(encoding='utf-8-sig').splitlines():
 if line.startswith(('-out:','-refout:')):line=line.split(':',1)[0]+':"'+(out/('Elemental.Presentation'+('.ref.dll' if line.startswith('-refout:') else '.dll'))).as_posix()+'"'
 elif line.strip('"').replace(chr(92),'/')==rel:line='"'+(lane/'after'/rel).as_posix()+'"'
 lines.append(line)
rsp=out/'compile.rsp';rsp.write_text('\n'.join(lines),encoding='utf-8')
run=subprocess.run([str(dotnet),str(csc),'@'+str(rsp)],cwd=root,capture_output=True,text=True);(out/'compile.log').write_text(run.stdout+run.stderr);print('Unity Presentation compile',run.returncode)
if run.returncode:print(run.stdout);raise SystemExit(run.returncode)
for side,name in [('before','Reference'),('after','Candidate')]:
 text=(lane/side/rel).read_text(encoding='utf-8-sig').replace('FireCpuField','FireCpuField'+name);(out/(name+'.cs')).write_text(text)
refs=unity/'DotNetSdk/packs/Microsoft.NETCore.App.Ref/8.0.21/ref/net8.0'
math=root/'Library/ScriptAssemblies/Unity.Mathematics.dll';shutil.copy2(math,out/math.name)
math_module=unity/'Managed/UnityEngine/UnityEngine.MathematicsModule.dll';shutil.copy2(math_module,out/math_module.name)
sources=[lane/'Oracle.cs',out/'Reference.cs',out/'Candidate.cs']+[root/'Assets/Elemental/Simulation/Fire'/s for s in ['FireDomainState.cs','FirePresentationSnapshot.cs','FireContactMath.cs']]
lines=['-nologo','-target:exe','-unsafe+','-optimize+','-out:"'+(out/'Oracle.dll').as_posix()+'"','-r:"'+math.as_posix()+'"','-r:"'+math_module.as_posix()+'"']+['-r:"'+p.as_posix()+'"' for p in refs.glob('*.dll')]+['"'+p.as_posix()+'"' for p in sources]
rsp=out/'oracle.rsp';rsp.write_text('\n'.join(lines),encoding='utf-8');run=subprocess.run([str(dotnet),str(csc),'@'+str(rsp)],capture_output=True,text=True);print('Oracle compile',run.returncode,run.stdout)
if run.returncode:raise SystemExit(run.returncode)
(out/'Oracle.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.21'}}}))
run=subprocess.run([str(dotnet),str(out/'Oracle.dll')],cwd=out,capture_output=True,text=True);print('Oracle',run.returncode,run.stdout,run.stderr);(out/'oracle-result.txt').write_text(run.stdout+run.stderr)
actual=hashlib.sha256((root/rel).read_bytes()).hexdigest()==hashlib.sha256((lane/'before'/rel).read_bytes()).hexdigest();print('Actual baseline untouched',actual)
raise SystemExit(run.returncode or (not actual))
