import numpy as np, math
from scipy.spatial import HalfspaceIntersection, ConvexHull
class R:
 def __init__(self,s): self.s=s
 def f(self):
  v=self.s;v^=(v<<13)&0xffffffff;v^=v>>17;v^=(v<<5)&0xffffffff;self.s=v&0xffffffff;return (self.s>>8)/16777216

def sites(seed):
 r=R(seed^0xB35A7D19);n=32;k=round(n*.58);layers=max(3,min(5,round(math.sqrt(n)*.65)));s=[]
 for i in range(k):
  radius=math.sqrt((i+.65)/k)*.92;angle=2.39996323*i+r.f()*.34
  x=math.cos(angle)*4*radius;z=math.sin(angle)*.275*radius
  layer=(i*2+i//layers)%layers; j=(r.f()-.5)*.24/layers;y=-2+4*max(.08,min(.92,(layer+.5)/layers+j));s.append([x,y,z])
 typical=math.sqrt(4.4/n)*.31
 for i in range(k,n):
  parent=min(k-1,math.floor(r.f()*k));angle=2.39996323*i+r.f()*.9;radius=typical*(.12+(.72-.12)*(r.f()*r.f()))
  x=s[parent][0]+math.cos(angle)*radius;z=s[parent][2]+math.sin(angle)*radius
  # PullInside takes repeated midpoint if outside.
  for _ in range(16):
   if -4<=x<=4 and -.275<=z<=.275: break
   x=(x+s[parent][0])*.5;z=(z+s[parent][2])*.5
  layer=max(0,min(layers-1,math.floor((s[parent][1]+2)/4*layers)))
  layer=max(0,min(layers-1,layer+(1 if i%2==0 else -1)));y=-2+4*(layer+.5)/layers;s.append([x,y,z])
 r=R(seed^0x51ED270B)
 for pair in range(8):
  parent=pair*7%k;x,y,z=s[parent];mid=(r.f()-.5)*.55*.14;half=.55*(.27+(.36-.27)*r.f());dx=(r.f()-.5)*.55*.055;dy=(r.f()-.5)*.55*.055
  s[parent]=[x-dx,y-dy,mid-half];s.append([x+dx,y+dy,mid+half])
 return np.array(s)
base=np.array([[1,0,0,-4],[-1,0,0,-4],[0,1,0,-2],[0,-1,0,-2],[0,0,1,-.275],[0,0,-1,-.275],[.10/8,1/4,0,-.5],[-.08/8,1/4,0,-.49],[0,1/4,1/.55,-.95],[0,1/4,-1/.55,-.95],[1/8,0,1/.55,-.95],[1/8,0,-1/.55,-.95],[-1/8,0,1/.55,-.95],[-1/8,0,-1/.55,-.95]])
for attempt in range(24):
 seed=(0xE17F1002+attempt*0x9E3779B9)&0xffffffff;s=sites(seed);asp=[];vol=[];depth=[]
 for i,p in enumerate(s):
  others=np.delete(s,i,axis=0);norm=others-p;d=((others*others).sum(1)-(p*p).sum())*.5
  h=np.vstack((base,np.column_stack((norm,-d))))
  if (h[:,:3]@p+h[:,3]).max()>=0:print('seed outside',i);break
  v=HalfspaceIntersection(h,p).intersections;size=v.max(0)-v.min(0);asp.append(size.max()/size.min());vol.append(ConvexHull(v).volume);depth.append(size[2]/.55)
 if len(asp)<40:continue
 asp.sort();vol.sort();partial=sum(x<.85 for x in depth);spread=max(depth)-min(depth);tail=vol[math.floor(39*.9)]/vol[math.floor(39*.1)]
 penalty=max(0,8-partial)*20+max(0,.2-spread)*100+max(0,asp[20]-3.5)*4+max(0,asp[-1]-6)*2+max(0,3-tail)*3
 print(attempt,{'median':round(asp[20],3),'max':round(asp[-1],3),'tail':round(tail,3),'partial':partial,'spread':round(spread,3),'penalty':round(penalty,4)},flush=True)
 if penalty<.0001:break
