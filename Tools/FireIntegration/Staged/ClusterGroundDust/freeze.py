from pathlib import Path
import hashlib,json,difflib
lane=Path('Tools/FireIntegration/Staged/ClusterGroundDust');manifest=[];patch=[]
for f in sorted((lane/'after').rglob('*.cs')):
 rel=f.relative_to(lane/'after');b=lane/'before'/rel;r={'path':rel.as_posix(),'afterSHA256':hashlib.sha256(f.read_bytes()).hexdigest()}
 if b.exists():r.update(beforeSHA256=hashlib.sha256(b.read_bytes()).hexdigest(),actualStillMatchesBefore=b.read_bytes()==Path(rel).read_bytes())
 manifest.append(r);patch.extend(difflib.unified_diff(b.read_text(encoding='utf-8-sig').splitlines(True) if b.exists() else [],f.read_text(encoding='utf-8').splitlines(True),fromfile='a/'+rel.as_posix(),tofile='b/'+rel.as_posix()))
(lane/'import-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8');(lane/'source.patch').write_text(''.join(patch),encoding='utf-8')
# Independent rate/weight arithmetic check (does not execute Unity/C#).
n=[2,2,2,0];w=[.2+.8*min(1,x/4) for x in n];bins=[0]*4
for j in range(1000):
 t=(j+.5)/1000*sum(w)
 for i,v in enumerate(w):
  t-=v
  if t<=0:bins[i]+=1;break
assert bins==[300,300,300,100]
assert 4+24*1.6==42.400000000000006 or abs(4+24*1.6-42.4)<1e-5
(lane/'offline-density-oracle.json').write_text(json.dumps({'scope':'Independent arithmetic only; Unity tests not run','neighbours':n,'weights':w,'stratifiedSamples':bins,'isolatedRatePerSecond':4+24*.2,'denseRatePerSecond':4+24*1.6,'expectedDensePopulationAtMeanLifetime':(4+24*1.6)*3.5,'capacity':192},indent=2))
print('files',len(manifest),'stale',[r['path'] for r in manifest if r.get('actualStillMatchesBefore') is False])
