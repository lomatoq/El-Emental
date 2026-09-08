from pathlib import Path
import difflib,hashlib,json,math
root=Path('El-Emental'); lane=root/'Tools/FireIntegration/Staged/StoneLane'; before=lane/'before'; after=lane/'after'
manifest={}; patch=[]; stale=[]
for dest in sorted(after.rglob('*.cs')):
    rel=dest.relative_to(after).as_posix(); src=before/rel
    old=src.read_text(encoding='utf-8-sig') if src.exists() else ''; new=dest.read_text()
    patch.extend(difflib.unified_diff(old.splitlines(True),new.splitlines(True),fromfile='a/'+rel if src.exists() else '/dev/null',tofile='b/'+rel))
    manifest[rel]={'before_sha256':hashlib.sha256(src.read_bytes()).hexdigest() if src.exists() else None,'after_sha256':hashlib.sha256(dest.read_bytes()).hexdigest()}
    if src.exists() and (root/rel).read_bytes()!=src.read_bytes(): stale.append(rel)
(lane/'stone-lane.patch').write_text(''.join(patch),encoding='utf-8'); (lane/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
def strength(m,v): return 0 if not math.isfinite(m) or not math.isfinite(v) or m<=0 or v<.25 else min(2.4,max(.25,.25+.32*math.log2(1+.5*min(m,1e6)*min(v,100)**2/12)))
assert strength(1000,.6)>strength(2,.6)*3
assert strength(1e6,.24)==0 and strength(1e6,-4)==0
fines=sum(((i+.5)/1000)**3<.25 for i in range(1000)); large=sum(((i+.5)/1000)**3>.75 for i in range(1000))
assert 620<=fines<=640 and 85<=large<=100
report={'scope':'Python arithmetic reference and source freshness only; C#/Unity tests not run','files':len(manifest),'stale_base_files':stale,'mass_2kg_speed_0_6_strength':strength(2,.6),'mass_1000kg_speed_0_6_strength':strength(1000,.6),'fines_below_quarter_range_out_of_1000':fines,'large_above_three_quarters_range_out_of_1000':large}
(lane/'static-evidence.json').write_text(json.dumps(report,indent=2)); print(json.dumps(report,indent=2))
