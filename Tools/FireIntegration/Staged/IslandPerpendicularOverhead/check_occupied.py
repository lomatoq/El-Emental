from pathlib import Path
import re,json,itertools
import numpy as np
stage=Path(__file__).resolve().parent;root=stage.parents[3]
blocks=re.split(r'^--- !u!',(root/'Assets/Elemental/Content/Scenes/EarthCoreSlice.unity').read_text(encoding='utf-8'),flags=re.M)
names={};transforms={};go_transform={};meshes={}
def vector(b,key):return np.array([float(v) for v in re.search(key+r': \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}',b).groups()])
for b in blocks:
    head=re.match(r'(\d+) &(-?\d+)',b)
    if not head:continue
    kind,key=map(int,head.groups())
    if kind==1:
        m=re.search(r'^  m_Name: (.*)$',b,re.M)
        if m:names[key]=m.group(1)
    elif kind==4:
        if 'm_GameObject:' not in b:continue
        go=int(re.search(r'm_GameObject: \{fileID: (-?\d+)\}',b).group(1));parent=int(re.search(r'm_Father: \{fileID: (-?\d+)\}',b).group(1));x,y,z,w=map(float,re.search(r'm_LocalRotation: \{x: ([^,]+), y: ([^,]+), z: ([^,]+), w: ([^}]+)\}',b).groups())
        rot=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]]);matrix=np.eye(4);matrix[:3,:3]=rot@np.diag(vector(b,'m_LocalScale'));matrix[:3,3]=vector(b,'m_LocalPosition');transforms[key]=(go,parent,matrix);go_transform[go]=key
    elif kind==33:
        if 'm_GameObject:' not in b:continue
        go=int(re.search(r'm_GameObject: \{fileID: (-?\d+)\}',b).group(1));m=re.search(r'm_Mesh: \{fileID: [^,]+, guid: ([^,]+),',b)
        if m:meshes[go]=m.group(1)
def world(key):
    go,parent,local=transforms[key];return world(parent)@local if parent in transforms else local
guidpath={}
for meta in (root/'Assets/Elemental/Content/Environment/DistantStone').rglob('*.asset.meta'):
    guid=re.search(r'^guid: (.*)$',meta.read_text(),re.M).group(1);guidpath[guid]=meta.with_suffix('')
legacy=[]
for go,guid in meshes.items():
    if names[go]!='LOD0' or guid not in guidpath:continue
    tid=go_transform[go];parent=transforms[tid][1];owner=names[transforms[parent][0]]
    if not ('ValleyGroup_' in owner or owner.startswith('View_')):continue
    text=guidpath[guid].read_text();values=re.findall(r'm_(?:Center|Extent): \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}',text[text.index('  m_LocalAABB:'):])[:2];c,e=np.array(values,dtype=float);m=world(tid)
    points=np.array([(m@np.r_[c+e*np.array(sign),1])[:3] for sign in itertools.product([-1,1],repeat=3)]);legacy.append((owner,points.min(0),points.max(0)))
items=json.loads((stage/'analytical-projections.json').read_text());result=[]
for item in items:
 lo=np.array(item['worldCenter'])-item['worldExtent'];hi=np.array(item['worldCenter'])+item['worldExtent']
 blocked=[name for name,l,h in legacy if np.all(hi>=l) and np.all(lo<=h)]
 result.append({'name':item['name'],'blockedByAtAuthoredAnchor':blocked,'arenaClearance':item['exclusionClearance']})
(stage/'occupied-check.json').write_text(json.dumps({'savedExistingRendererBounds':len(legacy),'items':result},indent=2));print(json.dumps(result,indent=2))
