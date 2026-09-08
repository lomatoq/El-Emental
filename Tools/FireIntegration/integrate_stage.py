"""Copy an owned staging lane only after an explicit Unity ownership handoff."""
from pathlib import Path
import argparse, hashlib, json, shutil

base = Path(__file__).resolve().parent
repo = base.parents[1]
p = argparse.ArgumentParser()
p.add_argument('lane')
p.add_argument('--apply', action='store_true')
args = p.parse_args()
lane = (base/'Staged'/args.lane).resolve()
assert lane.is_relative_to(base/'Staged')
after = lane/'after'
assert after.is_dir()
sha = lambda x: hashlib.sha256(x.read_bytes()).hexdigest()
changes = []
for src in after.rglob('*'):
    if not src.is_file(): continue
    rel = src.relative_to(after)
    assert rel.parts[0] == 'Assets', rel
    dest = repo/rel
    before = lane/'before'/rel
    if dest.exists() and sha(dest) == sha(src): continue
    if dest.exists() and (not before.exists() or sha(dest) != sha(before)):
        raise RuntimeError('Live source differs from staged baseline: '+str(rel))
    changes.append((src, dest, rel))
print(json.dumps({'lane':args.lane,'files':len(changes),'apply':args.apply}, indent=2))
if args.apply:
    for src, dest, rel in changes:
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dest)
    report = [{'file':str(rel),'sha256':sha(dest)} for _, dest, rel in changes]
    (base/'Reports'/(args.lane+'-import.json')).write_text(json.dumps(report, indent=2))
