import math, random, json
from pathlib import Path
r=random.Random(817233)
def unit(v):
    m=math.sqrt(sum(x*x for x in v)); return [x/m for x in v]
def cross(a,b): return [a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]]
before=after=0
cases=0
for _ in range(50000):
    up=unit([r.uniform(-1,1) for j in range(3)])
    right=unit(cross(up,[r.uniform(-1,1) for j in range(3)]))
    a=r.uniform(.85,1.55)
    expansion=r.uniform(.85,1.12)
    for body,ember in [(False,False),(True,False),(False,True)]:
        width=expansion*(1.12 if body else 1)*(.13 if ember else 1)
        aspect=a*(.72 if body else 1.45)
        extent=max(.5*width*(abs(right[j])+abs(up[j])*aspect) for j in range(3))
        fit=min(1,.99*a/max(extent,1e-6))
        before=max(before,extent/a)
        for u,v in [(-.5,-.5),(-.5,.5),(.5,-.5),(.5,.5)]:
            vertex=[width*fit*(right[j]*u+up[j]*v*aspect) for j in range(3)]
            ratio=max(abs(x) for x in vertex)/a
            after=max(after,ratio)
            assert ratio<=1
            cases+=1
report={'corners_checked':cases,'max_old_extent_to_padding':before,'max_guarded_extent_to_padding':after,'passed':True,'analytical_old_worst_at_aspect_0_85':math.sqrt((.56/.85)**2+.812**2)}
Path(__file__).with_name('bounds-evidence.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
