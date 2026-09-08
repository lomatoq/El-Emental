from pathlib import Path
import subprocess,json
lane=Path(__file__).resolve().parent;root=lane.parents[3];out=lane/'Reports';out.mkdir(exist_ok=True)
unity=Path('C:/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Data')
overlays={p.relative_to(lane/'after').as_posix():p for p in (lane/'after').rglob('*.cs')}
def owner(rel):
 path=(root/rel).parent
 while path!=root:
  definitions=list(path.glob('*.asmdef'))
  if definitions:return json.loads(definitions[0].read_text(encoding='utf-8-sig'))['name']
  path=path.parent
 raise RuntimeError(rel)
for name in ['Elemental.Simulation','Elemental.Presentation','Elemental.Authoring.Editor','Elemental.Tests.EditMode']:
 lines=[];seen=set()
 for line in (root/'Library/Bee/artifacts/1900b0aE.dag'/f'{name}.rsp').read_text(encoding='utf-8-sig').splitlines():
  if line.startswith(('-out:', '-refout:')):line=line.split(':',1)[0]+':"'+(out/(name+('.ref.dll' if line.startswith('-refout:') else '.dll'))).as_posix()+'"'
  elif line.startswith('-r:'):
   local=out/Path(line[3:].strip('"')).name
   if local.exists():line='-r:"'+local.as_posix()+'"'
  elif line.strip('"').endswith('.cs'):
   rel=line.strip('"').replace(chr(92),'/')
   if rel in overlays:line='"'+overlays[rel].as_posix()+'"';seen.add(rel)
  lines.append(line)
 for rel,path in overlays.items():
  if rel not in seen and owner(rel)==name:lines.append('"'+path.as_posix()+'"')
 rsp=out/(name+'.rsp');rsp.write_text('\n'.join(lines),encoding='utf-8')
 run=subprocess.run([str(unity/'DotNetSdk/dotnet.exe'),str(unity/'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'),'@'+str(rsp)],cwd=root,capture_output=True,text=True)
 (out/(name+'.log')).write_text(run.stdout+run.stderr,encoding='utf-8');print(name,run.returncode,run.stdout[-2500:] if run.returncode else '')
 if run.returncode:raise SystemExit(run.returncode)
