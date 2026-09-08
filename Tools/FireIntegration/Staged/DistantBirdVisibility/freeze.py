from pathlib import Path
import hashlib,json
stage=Path(__file__).resolve().parent
root=stage.parents[3]
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest() if p.exists() else None
rows=[]
for p in sorted((stage/'after').rglob('*.cs')):
    relative=p.relative_to(stage/'after')
    before=stage/'before'/relative
    actual=root/relative
    if before.exists() and sha(before)!=sha(actual):
        raise RuntimeError('Actual source changed before freeze: '+str(relative))
    if not before.exists() and actual.exists():
        raise RuntimeError('New target already exists: '+str(relative))
    rows.append(dict(path=relative.as_posix(),before_sha256=sha(before),after_sha256=sha(p),actual_absent=not actual.exists()))
(stage/'mapping.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')
print('Frozen',len(rows),'source files; exact actual before hashes verified.')
