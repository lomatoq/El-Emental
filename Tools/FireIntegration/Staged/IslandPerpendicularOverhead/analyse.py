from pathlib import Path
import math,re,json,shutil,itertools
import numpy as np
stage=Path(__file__).resolve().parent;root=stage.parents[3]
def norm(x):return np.array(x,dtype=float)/np.linalg.norm(x)
up=norm([-.0047114403,.9980326,.06251951]);forward=norm(np.array([0,0,1])-up*up[2]);right=np.cross(up,forward);frame=np.stack([right,up,forward],axis=1)
def cam(view):
 p=np.array([0,59,0]);f=norm([1,-.08,0] if view=='East' else [-1,-.08,0] if view=='West' else [0,1,0]);r=norm(np.cross([0,0,1] if view=='Overhead' else [0,1,0],f));u=np.cross(f,r)
 return p,f,r,u,math.tan(math.radians(30)),1676/776
# Top-origin screen centers. Existing slots only, geometry/yaw/count unchanged.
# Direct authored coordinates relative to existing staging frame. Existing slots untouched.
targets={
'EastIslandNear':(0,[680,165,-200],300),'EastIslandFar':(3,[980,270,230],270),
'EastSatellite0':(4,[620,255,-265],52),'EastSatellite1':(2,[745,180,-115],38),
'WestIslandNear':(1,[-680,170,210],300),'WestIslandFar':(5,[-960,250,-190],230),
'WestSatellite0':(0,[-620,255,275],50),'WestSatellite1':(3,[-745,180,125],36),
'OverheadIslandNear':(2,[70,360,80],130),'OverheadIslandFar':(4,[-170,500,-90],95),
'OverheadIslandAccent':(1,[230,630,-130],160),'OverheadSatellite':(5,[-260,410,120],70)}
profile=(root/'Assets/Elemental/Content/Environment/DistantStone/DistantBackdropProfile.asset').read_text();items=[]
for name,(variant,position,scale) in targets.items():
 text=(root/f'Assets/Elemental/Content/Environment/DistantStone/Meshes/Island_{variant+6}_LOD0.asset').read_text();values=re.findall(r'm_(?:Center|Extent): \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}',text[text.index('  m_LocalAABB:'):])[:2];c,e=np.array(values,dtype=float)
 view='East' if name.startswith('East') else 'West' if name.startswith('West') else 'Overhead';p,f,r,u,tan,a=cam(view)
 worldcenter=frame@np.array(position)+np.array([0,c[1]*scale,0]);radius=np.linalg.norm(e[[0,2]])*scale+1.64;extent=np.array([radius,e[1]*scale+1.64,radius]);points=np.array([worldcenter+extent*np.array(s) for s in itertools.product([-1,1],repeat=3)]);delta=points-p;z=delta@f;uv=np.stack([.5+(delta@r)/(2*tan*a*z),.5-(delta@u)/(2*tan*z)],axis=1)
 items.append(dict(name=name,variant=variant,position=position,scale=scale,view=view,sweptBounds=[uv.min(0).round(4).tolist(),uv.max(0).round(4).tolist()],depth=float(z.mean()),worldCenter=worldcenter.tolist(),worldExtent=extent.tolist(),exclusionClearance=float(np.linalg.norm(np.maximum(np.abs(worldcenter)-extent,0))-175.1)))
for i,x in enumerate(items):
 for y in items[:i]:
  if np.all(np.abs(np.array(x['worldCenter'])-y['worldCenter'])<np.array(x['worldExtent'])+y['worldExtent']):print('OVERLAP',x['name'],y['name'])
(stage/'analytical-projections.json').write_text(json.dumps(items,indent=2));
for x in items:print(x['name'],x['position'],x['scale'],x['sweptBounds'])
