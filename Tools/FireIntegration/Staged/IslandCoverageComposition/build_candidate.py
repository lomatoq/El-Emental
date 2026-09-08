from pathlib import Path
import math,re,json,shutil,itertools
import numpy as np
stage=Path(__file__).resolve().parent;root=stage.parents[3]
def norm(x):return np.array(x,dtype=float)/np.linalg.norm(x)
up=norm([-.0047114403,.9980326,.06251951]);forward=norm(np.array([0,0,1])-up*up[2]);right=np.cross(up,forward);frame=np.stack([right,up,forward],axis=1)
def cam(view):
 p=np.array([-.26,56.81,6.40] if view=='Main' else [.76,58.91,-11.33]);f=norm([.2837368,-.0006538,-.9589022] if view=='Main' else [0,-.13,.99]);r=norm(np.cross([0,1,0],f));u=np.cross(f,r)
 roll=math.radians(10 if view=='Main' else 0);r,u=r*math.cos(roll)+u*math.sin(roll),u*math.cos(roll)-r*math.sin(roll)
 return p,f,r,u,math.tan(math.radians((38 if view=='Main' else 60)/2)),1676/776
# Top-origin screen centers. Existing slots only, geometry/yaw/count unchanged.
targets={
'MainIsland0':(.49,.25,430,.24),'MainIsland1':(.87,.29,590,.18),'MainIsland2':(.69,.14,800,.12),
'MainUpper0':(.82,.065,1000,.07),'MainUpper1':(.46,.065,890,.075),'MainUpper2':(.61,.07,1150,.045),
'MainSideSmall0':(.955,.18,550,.09),'MainSideSmall1':(.395,.25,510,.105),
'MainSatellite0':(.425,.16,410,.04),'MainSatellite1':(.555,.315,445,.035),
'CombatIsland0':(.365,.24,420,.245),'CombatIsland1':(.80,.26,480,.21),'CombatIsland2':(.60,.125,720,.14),
'CombatUpper0':(.21,.085,850,.07),'CombatUpper1':(.875,.075,950,.075),'CombatUpper2':(.46,.07,1050,.06),
'CombatSideSmall0':(.18,.27,470,.11),'CombatSideSmall1':(.945,.32,530,.105),
'CombatSatellite0':(.295,.175,400,.045),'CombatSatellite1':(.425,.31,440,.038)}
profile=(root/'Assets/Elemental/Content/Environment/DistantStone/DistantBackdropProfile.asset').read_text();items=[]
for name,(sx,sy,depth,screenheight) in targets.items():
 variant=int(re.search(r'- name: '+name+r'\n    airborne: 1\n    variant: (\d+)',profile).group(1));text=(root/f'Assets/Elemental/Content/Environment/DistantStone/Meshes/Island_{variant+6}_LOD0.asset').read_text();values=re.findall(r'm_(?:Center|Extent): \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}',text[text.index('  m_LocalAABB:'):])[:2];c,e=np.array(values,dtype=float)
 view='Main' if name.startswith('Main') else 'Combat';p,f,r,u,tan,a=cam(view)
 scale=2*tan*depth*screenheight/(2*e[1]);assert scale<600
 worldcenter=p+f*depth+r*((2*sx-1)*tan*a*depth)+u*((1-2*sy)*tan*depth)
 # Center-offset horizontal components are sub-meter; yaw cannot change vertical offset.
 position=frame.T@(worldcenter-np.array([0,c[1]*scale,0]));radius=np.linalg.norm(e[[0,2]])*scale+1.64;extent=np.array([radius,e[1]*scale+1.64,radius]);points=np.array([worldcenter+extent*np.array(s) for s in itertools.product([-1,1],repeat=3)]);delta=points-p;z=delta@f;uv=np.stack([.5+(delta@r)/(2*tan*a*z),.5-(delta@u)/(2*tan*z)],axis=1)
 items.append(dict(name=name,variant=variant,position=position.round(4).tolist(),scale=round(scale,4),target=[sx,sy,screenheight],sweptBounds=[uv.min(0).round(4).tolist(),uv.max(0).round(4).tolist()],depth=depth,worldCenter=worldcenter.tolist(),worldExtent=extent.tolist()))
for i,x in enumerate(items):
 for y in items[:i]:
  if np.all(np.abs(np.array(x['worldCenter'])-y['worldCenter'])<np.array(x['worldExtent'])+y['worldExtent']):print('OVERLAP',x['name'],y['name'])
(stage/'analytical-projections.json').write_text(json.dumps(items,indent=2));
for x in items:print(x['name'],x['position'],x['scale'],x['sweptBounds'])
