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
for rel,sha in json.loads((lane/'baseline.json').read_text()).items():assert hashlib.sha256((root/rel).read_bytes()).hexdigest()==sha,rel
print('Actual three sources unchanged; profile asset untouched')
