from pathlib import Path
import subprocess
lane=Path(__file__).resolve().parent;root=lane.parents[3];out=lane/'Reports';out.mkdir(exist_ok=True)
stub=out/'CommandHostStubs.cs';stub.write_text('public interface IRunCommand { void Execute(ExecutionResult result); } public sealed class ExecutionResult {}')
lines=[]
for line in (root/'Library/Bee/artifacts/1900b0aE.dag/Elemental.Authoring.Editor.rsp').read_text(encoding='utf-8-sig').splitlines():
 if line.startswith('-out:'):line='-out:"'+(out/'CutFaceQa.dll').as_posix()+'"'
 elif line.startswith('-refout:'):continue
 elif line.strip('"').endswith('.cs'):continue
 lines.append(line)
lines+=['"'+(lane/'CaptureActualCutSurface.cs').as_posix()+'"','"'+stub.as_posix()+'"']
rsp=out/'CutFaceQa.rsp';rsp.write_text('\n'.join(lines),encoding='utf-8')
unity=Path('C:/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Data/DotNetSdk')
run=subprocess.run([str(unity/'dotnet.exe'),str(unity/'sdk/8.0.318/Roslyn/bincore/csc.dll'),'@'+str(rsp)],cwd=root,capture_output=True,text=True)
print(run.stdout+run.stderr);print('exit',run.returncode);(out/'CutFaceQa.log').write_text(run.stdout+run.stderr,encoding='utf-8');raise SystemExit(run.returncode)
