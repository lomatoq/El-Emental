from pathlib import Path
import subprocess,json,random,hashlib
here=Path(__file__).resolve().parent
root=here.parents[3]
out=here/'Reports';out.mkdir(exist_ok=True)
relative='Assets/Elemental/Presentation/Fire/FireCpuMeshBackend.cs'
lines=(root/'Library/Bee/artifacts/1900b0aE.dag/Elemental.Presentation.rsp').read_text(encoding='utf-8-sig').splitlines()
result=[]
for line in lines:
    if line.startswith(('-out:','-refout:')):
        suffix='.ref.dll' if line.startswith('-refout:') else '.dll'
        line=line.split(':',1)[0]+':"'+(out/('Elemental.Presentation'+suffix)).as_posix()+'"'
    elif line.strip('"').replace('\\','/')==relative:
        line='"'+(here/'after'/relative).as_posix()+'"'
    result.append(line)
rsp=out/'candidate.rsp';rsp.write_text('\n'.join(result),encoding='utf-8')
unity=Path('C:/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Data')
run=subprocess.run([str(unity/'DotNetSdk/dotnet.exe'),str(unity/'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'),'@'+str(rsp)],cwd=root,capture_output=True,text=True,encoding='utf-8',errors='replace')
(out/'compile.log').write_text(run.stdout+run.stderr,encoding='utf-8')
# Algorithm-level translation of the candidate's iterative min-heap sort.
def heap(depth):
    order=list(range(len(depth)))
    def sift(r,n):
        while r<n//2:
            c=r*2+1
            if c+1<n and depth[order[c+1]]<depth[order[c]]:c+=1
            if depth[order[r]]<=depth[order[c]]:return
            order[r],order[c]=order[c],order[r];r=c
    for i in range(len(order)//2-1,-1,-1):sift(i,len(order))
    for end in range(len(order)-1,0,-1):
        order[0],order[end]=order[end],order[0];sift(0,end)
    return [depth[i] for i in order]
rng=random.Random(703)
cases=0
for n in range(513):
    for values in ([rng.uniform(-100,100) for _ in range(n)],[rng.randrange(5) for _ in range(n)],list(range(n)),list(range(n-1,-1,-1))):
        assert heap(values)==sorted(values,reverse=True);cases+=1
report=dict(csharp_exit=run.returncode,heap_cases=cases,actual_unchanged=hashlib.sha256((root/relative).read_bytes()).hexdigest()==hashlib.sha256((here/'before'/relative).read_bytes()).hexdigest(),unity_executed=False,burst_validated=False,performance_validated=False)
(out/'verification.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(run.stdout+run.stderr);print(json.dumps(report,indent=2))
raise SystemExit(run.returncode)
