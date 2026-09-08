"""Rebase pending networking seams onto the current UI, without changing Assets.

Uses Git's three-way text merge; conflicts abort before any baseline is replaced.
Run again after coordinator UI changes, then review FrontendBindings.patch.
"""
from pathlib import Path
import difflib
import json
import subprocess
import tempfile
import hashlib

pending = Path(__file__).resolve().parent
project = pending.parents[2]
files = (
    ("FrontendPatch", "FrontendFlowController.cs"),
    ("HudPatch", "EarthDuelHud.cs"),
)

prepared = []
for folder, name in files:
    relative = "Assets/Elemental/Presentation/UI/" + name
    live = project / relative
    original = pending / folder / "Original" / name
    modified = pending / folder / "Modified" / name
    current = live.read_text(encoding="utf-8-sig")
    before = original.read_text(encoding="utf-8-sig")
    after = modified.read_text(encoding="utf-8-sig")
    with tempfile.TemporaryDirectory(dir=pending) as temporary:
        paths = [Path(temporary) / label for label in ("Current.cs", "Base.cs", "Network.cs")]
        for path, value in zip(paths, (current, before, after)):
            path.write_text(value, encoding="utf-8", newline="\n")
        merge = subprocess.run(
            ["git", "merge-file", "-p", *map(str, paths)],
            capture_output=True, cwd=project,
        )
    if merge.returncode:
        conflict = pending / (name + ".rebase-conflict.txt")
        conflict.write_bytes(merge.stdout + merge.stderr)
        raise SystemExit(f"Rebase conflicted: {conflict}. Assets and pending baselines untouched.")
    result = merge.stdout.decode("utf-8")
    prepared.append((relative, original, modified, current, result))

patch = []
inventory = []
for relative, original, modified, current, result in prepared:
    original.write_text(current, encoding="utf-8", newline="\n")
    modified.write_text(result, encoding="utf-8", newline="\n")
    patch.extend(difflib.unified_diff(current.splitlines(keepends=True), result.splitlines(keepends=True),
        fromfile="a/" + relative, tofile="b/" + relative))
    inventory.append({"path": relative, "source_sha256": hashlib.sha256(current.encode()).hexdigest()})
(pending / "FrontendBindings.patch").write_text("".join(patch), encoding="utf-8", newline="\n")
(pending / "FrontendBindings.sources.json").write_text(json.dumps(inventory, indent=2) + "\n", encoding="utf-8")
print(f"Rebased {len(prepared)} pending UI seams. Assets untouched; review FrontendBindings.patch.")
