from pathlib import Path
import re, json, hashlib

base = Path(__file__).resolve().parent
repo = base.parents[1]
before = base/'Staged/GraphicsUI/preserved'
digest = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
checks = {}
for relative in ['Assets/Elemental/Content/UI/Frontend/ElementalHudLayout.asset',
                 'ProjectSettings/ProjectSettings.asset', 'Packages/manifest.json']:
    checks[relative] = {'unchanged':digest(before/relative)==digest(repo/relative)}

theme = 'Assets/Elemental/Content/UI/Frontend/ElementalUITheme.asset'
old = (before/theme).read_text(encoding='utf-8-sig')
new = (repo/theme).read_text(encoding='utf-8-sig')
checks[theme] = {'existing_fields_unchanged':old == re.sub(r'^  stoneSkin:.*\n', '', new, flags=re.M)}

scene = 'Assets/Elemental/Content/Scenes/EarthCoreSlice.unity'
def records(text):
    starts = list(re.finditer(r'^--- !u!(\d+) &(-?\d+).*$', text, flags=re.M))
    return {m.group(2):(m.group(1),text[m.start():starts[i+1].start() if i+1<len(starts) else len(text)]) for i,m in enumerate(starts)}
old = records((before/scene).read_text(encoding='utf-8-sig'))
new = records((repo/scene).read_text(encoding='utf-8-sig'))
removed = sorted(set(old)-set(new))
changed = [key for key in old.keys() & new.keys() if old[key]!=new[key]]
# SceneRoots is expected to append exactly the explicitly added decoration root.
def cloud_child_addition(key):
    if key != '102282334' or old[key][0] != '4': return False
    previous, current = old[key][1], new[key][1]
    match = re.search(r'  m_Children:\n(?:  - \{fileID: \d+\}\n)+', current)
    if not match or '  m_Children: []\n' not in previous: return False
    names = []
    for child in re.findall(r'fileID: (\d+)', match.group()):
        if child in old or child not in new or new[child][0] != '4': return False
        child_text = new[child][1]
        owner = re.search(r'm_GameObject: \{fileID: (\d+)\}', child_text)
        if not owner or owner.group(1) not in new: return False
        if '  m_Father: {fileID: 102282334}\n' not in child_text: return False
        name = re.search(r'^  m_Name: (.+)$', new[owner.group(1)][1], re.M)
        if not name: return False
        names.append(name.group(1))
    return (len(names) == len(set(names)) and set(names) <= {'Valley Cloud Strata', 'Valley Cumulus Art Banks', 'Valley Atmosphere V2 Frame'} and
            current[:match.start()] + '  m_Children: []\n' + current[match.end():] == previous)

expected_cloud_children = [key for key in changed if cloud_child_addition(key)]
# User-authorized menu framing was visually accepted in Main-orbit0.png.
# Only this exact field on its existing owner may differ; lens/countdown stay intact.
def approved_menu_orbit_only(key):
    if key != '1968201427' or old[key][0] != '114' or new[key][0] != '114': return False
    previous, current = old[key][1], new[key][1]
    script = '  m_Script: {fileID: 11500000, guid: 8310ec71fcbc56943bc2c082548b525c, type: 3}\n'
    old_line, new_line = '  presentationAzimuth: 35\n', '  presentationAzimuth: 0\n'
    return (script in previous and script in current and previous.count(old_line) == 1 and
            current.count(new_line) == 1 and current.replace(new_line, old_line, 1) == previous)
expected_menu_orbit = [key for key in changed if approved_menu_orbit_only(key)]
# Explicitly authorized cluster-dust profile and reduced-motion frontend binding only.
def approved_cluster_dust_only(key):
    if key != '680774320' or old[key][0] != '114': return False
    previous, current = old[key][1], new[key][1]
    normalized = current.replace('guid: 71e430e3de3f1274698daa172711b44b', 'guid: d5fe08a52c88a0a47a87426551f14fb2').replace('  frontend: {fileID: 1968201430}\n', '')
    return normalized == previous and 'EarthSurfaceWindDust' in current
expected_cluster_dust = [key for key in changed if approved_cluster_dust_only(key)]
unexpected = [key for key in changed if old[key][0]!='1660057539' and key not in expected_cloud_children and key not in expected_menu_orbit and key not in expected_cluster_dust]
checks[scene] = {'old_records':len(old),'new_records':len(new),'removed':removed,
                 'changed_existing_records':changed,'verified_cloud_child_only':expected_cloud_children,
                 'verified_menu_orbit_only':expected_menu_orbit,'verified_cluster_dust_only':expected_cluster_dust,
                 'unexpected_changed_records':unexpected,
                 'existing_objects_unchanged':not removed and not unexpected}
report = base/'Reports/GraphicsPreservation.json'
report.write_text(json.dumps(checks, indent=2), encoding='utf-8')
print(json.dumps(checks, indent=2))
assert all(item.get('unchanged',item.get('existing_fields_unchanged',item.get('existing_objects_unchanged',False))) for item in checks.values())
