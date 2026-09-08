from pathlib import Path
import hashlib,json,difflib
lane=Path('Tools/FireIntegration/Staged/ReferenceSpriteFidelity');records=[];patch=[]
for f in sorted((lane/'after').rglob('*')):
 if not f.is_file():continue
 rel=f.relative_to(lane/'after');before=lane/'before'/rel;actual=Path(rel)
 r={'path':rel.as_posix(),'afterSHA256':hashlib.sha256(f.read_bytes()).hexdigest()}
 if before.exists():
  r['beforeSHA256']=hashlib.sha256(before.read_bytes()).hexdigest();r['actualStillMatchesBefore']=actual.exists() and hashlib.sha256(actual.read_bytes()).hexdigest()==r['beforeSHA256']
 if f.suffix=='.cs':
  old=before.read_text(encoding='utf-8-sig').splitlines(keepends=True) if before.exists() else []
  new=f.read_text(encoding='utf-8-sig').splitlines(keepends=True)
  patch.extend(difflib.unified_diff(old,new,fromfile='a/'+rel.as_posix(),tofile='b/'+rel.as_posix()))
 records.append(r)
(lane/'import-manifest.json').write_text(json.dumps(records,indent=2),encoding='utf-8');(lane/'source.patch').write_text(''.join(patch),encoding='utf-8');print(len(records),'files; stale:',[r['path'] for r in records if r.get('actualStillMatchesBefore') is False])
