from pathlib import Path
import subprocess,re
s=Path(__file__).resolve().parent;r=s.parents[3]
t=(r/'Tools/FireIntegration/PreviewCapture/compile/CaptureTwelvePillars.rsp').read_text(encoding='utf-8-sig')
t=t.replace('PreviewCapture/CaptureTwelvePillars.cs','Staged/DistantValleyComposition/InspectActualProjectedBounds.cs').replace('PreviewCapture/compile/CaptureTwelvePillars','Staged/DistantValleyComposition/compile/ProjectedQa')
t=re.sub(r'Library/Bee/artifacts/1900b0aE.dag/(Elemental\.[^"/]+)\.ref.dll',r'Library/ScriptAssemblies/\1.dll',t)
p=s/'compile/ProjectedQa.rsp';p.write_text(t,encoding='utf-8')
base=Path('C:/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Data/DotNetSdk')
result=subprocess.run([str(base/'dotnet.exe'),str(base/'sdk/8.0.318/Roslyn/bincore/csc.dll'),'@'+str(p)],cwd=r,capture_output=True,text=True)
print(result.stdout,result.stderr);raise SystemExit(result.returncode)
