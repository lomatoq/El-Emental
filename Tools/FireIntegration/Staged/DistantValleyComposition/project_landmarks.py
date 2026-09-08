from pathlib import Path
import math,re,json,itertools
import numpy as np
stage=Path(__file__).resolve().parent
root=stage.parents[3]
def norm(v):return np.asarray(v,dtype=float)/np.linalg.norm(v)
up=norm([0,1,.063]);fw=norm(np.array([0,0,1])-up*up[2]);right=np.cross(up,fw);frame=np.stack([right,up,fw],axis=1)
def camera(name,aspect=16/9):
    p=np.array([-.26,56.86,6.39] if name=='Main' else [.76,58.91,-11.33]);f=norm([.28,0,-.96] if name=='Main' else [0,-.13,.99]);r=norm(np.cross([0,1,0],f));u=np.cross(f,r)
    roll=math.radians(10 if name=='Main' else 0);r,u=r*math.cos(roll)+u*math.sin(roll),u*math.cos(roll)-r*math.sin(roll)
    return p,f,r,u,aspect
tan=math.tan(math.radians(19))
def mesh(path):
    text=(root/path).read_text();s=text[text.index('  m_LocalAABB:'):];values=re.findall(r'm_(?:Center|Extent): \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}',s)[:2]
    return np.array(values,dtype=float)
def corners(c,e):return np.array([c+e*np.array(signs) for signs in itertools.product([-1,1],repeat=3)])
def project(points,view,aspect=16/9):
    p,f,r,u,a=camera(view,aspect);delta=points-p;depth=delta@f
    uv=np.stack([.5+(delta@r)/(2*tan*a*depth),.5-(delta@u)/(2*tan*depth)],axis=1)
    return {'min':uv.min(axis=0).tolist(),'max':uv.max(axis=0).tolist(),'depth':[float(depth.min()),float(depth.max())]}
items=[]
for view,variants,slots in [('Main',[0,2,5],[(.59,.73,550,.22),(.90,.75,800,.15),(.79,.87,1100,.10)]),('Combat',[1,3,4],[(.22,.76,550,.22),(.82,.70,800,.15),(.58,.85,1100,.10)])]:
    p,f,r,u,a=camera(view)
    for index,(variant,(sx,sy,depth,screenheight)) in enumerate(zip(variants,slots)):
        path=f'Assets/Elemental/Content/Environment/DistantStone/Meshes/Island_{variant+6}_LOD0.asset';c,e=mesh(path);scale=2*tan*depth*screenheight/(2*e[1])
        center=p+f*depth+r*((2*sx-1)*tan*a*depth)+u*((2*sy-1)*tan*depth)
        position=frame.T@center-c*scale
        # Full solid underside stays above the opaque lower-fog transition.
        position[1]=max(position[1],55-(c[1]-e[1])*scale)
        items.append({'name':view+'Island'+str(index),'airborne':True,'variant':variant,'position':position.tolist(),'scale':scale,'mesh':path})
for view,variants,slots in [('Main',[0,4,8],[(.38,390,265),(1.10,570,350),(.69,760,260)]),('Combat',[2,6,10],[(.03,390,265),(.99,570,350),(.36,760,390)])]:
    p,f,r,u,a=camera(view)
    for index,(variant,(sx,depth,height)) in enumerate(zip(variants,slots)):
        path=f'Assets/Elemental/Content/Environment/DistantStone/Preview/Pillar_{variant:02}.asset';c,e=mesh(path);scale=height/(2*e[1]);base=-155
        fixed=frame@np.array([0,base,0])+frame@(c*scale)-p
        matrix=np.array([[f@frame[:,0],f@frame[:,2]],[r@frame[:,0],r@frame[:,2]]]);rhs=np.array([depth-f@fixed,(2*sx-1)*tan*a*depth-r@fixed]);x,z=np.linalg.solve(matrix,rhs)
        items.append({'name':view+'Ground'+str(index),'airborne':False,'variant':len([v for v in items if not v['airborne']]),'position':[x,base,z],'scale':scale,'mesh':path})
for item in items:
    c,e=mesh(item['mesh']);world=(corners(c,e)*item['scale']+np.array(item['position']))@frame.T
    item['projection']={view:project(world,view) for view in ['Main','Combat']}
    item['wideMain']=project(world,'Main',1681/785)
    item['worldAabb']={'min':world.min(axis=0).tolist(),'max':world.max(axis=0).tolist()}
    if not item['airborne']:
        lower=c-e;upper=c+e;lower[1]=max(lower[1],(33.1-item['position'][1])/item['scale']);visible=(corners((lower+upper)/2,(upper-lower)/2)*item['scale']+np.array(item['position']))@frame.T
        item['aboveFogProjection']={view:project(visible,view) for view in ['Main','Combat']}
    item['nearestToPlanet']=float(np.linalg.norm(np.maximum(np.maximum(world.min(axis=0),-world.max(axis=0)),0)))
stage.mkdir(exist_ok=True);(stage/'analytical-projections.json').write_text(json.dumps(items,indent=2),encoding='utf-8')
for v in items:
    box=v['projection']['Main' if v['name'].startswith('Main') else 'Combat'];print(v['name'],'pos',np.round(v['position'],2),'scale',round(v['scale'],2),'bbox',np.round([box['min'],box['max']],3),'exclusionDistance',round(v['nearestToPlanet'],1))
