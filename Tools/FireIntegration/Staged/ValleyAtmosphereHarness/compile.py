from pathlib import Path
import subprocess
root=Path(__file__).resolve().parents[4]
lane=Path(__file__).resolve().parent
out=lane/'Reports';out.mkdir(exist_ok=True)
unity=Path('C:/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Data')
for name,folder in [('Elemental.Presentation','Presentation'),('Elemental.Authoring.Editor','Authoring')]:
 rsp=root/'Library/Bee/artifacts/1900b0aE.dag'/f'{name}.rsp'
 lines=[]
 for line in rsp.read_text(encoding='utf-8-sig').splitlines():
  if line.startswith(('-out:', '-refout:')):line=line.split(':',1)[0]+':"'+(out/(name+('.ref.dll' if line.startswith('-refout:') else '.dll'))).as_posix()+'"'
  elif line.startswith('-r:'):
   local=out/Path(line[3:].strip('"')).name
   if local.exists():line='-r:"'+local.as_posix()+'"'
  lines.append(line)
 for path in (lane/'after/Assets/Elemental'/folder).rglob('*.cs'):lines.append('"'+path.as_posix()+'"')
 output=out/(name+'.rsp');output.write_text('\n'.join(lines),encoding='utf-8')
 run=subprocess.run([str(unity/'DotNetSdk/dotnet.exe'),str(unity/'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'),'@'+str(output)],cwd=root,capture_output=True,text=True)
 (out/(name+'.log')).write_text(run.stdout+run.stderr,encoding='utf-8');print(name,run.returncode,run.stdout[-4000:])
 if run.returncode:raise SystemExit(run.returncode)
