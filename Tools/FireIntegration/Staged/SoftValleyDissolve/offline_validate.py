from pathlib import Path
import math,json,re,hashlib
lane=Path(__file__).resolve().parent;root=lane.parents[3]
def smooth(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
def integral(h0,h1,d,f):
 lo,hi=sorted((h0,h1))
 def above(a,b,length):
  x=(b-a)/f;ratio=1-x*.5+x*x/6 if x<.001 else -math.expm1(-x)/x
  return length*math.exp(-a/f)*ratio
 if hi<=0:return d
 if lo>=0:return above(lo,hi,d)
 below=d*(-lo)/(hi-lo);return below+above(0,hi,d-below)
def compose(veil,aerial,height,radius,planet,protection):
 member=1-smooth(planet+80,planet+140,radius)
 lower=(1-smooth(-planet*.25,planet*.60,height))*member
 return max(lower,1-(1-max(0,min(1,veil)))*(1-max(0,min(1,aerial))))*max(0,min(1,protection))
def sky_opacity(height,slope,falloff,density):
 if density<=0:return 0
 if slope<=0:return 1
 optical=(max(-height,0)+falloff*math.exp(-max(height,0)/falloff))/slope*density
 return -math.expm1(-optical)
far=[compose(.3,.4,h,1000,55,1) for h in range(-100,101)]
assert all(abs(v-.58)<1e-9 for v in far)
endpoints={'physical_opaque_distant':compose(1,.78,-150,1000,55,1),'planet_bottom_sealed':compose(0,0,-55,55,55,1),'playable_protected':compose(1,.78,55,55,55,0),'lower_sky':sky_opacity(125,-.01,75,.012)}
assert list(endpoints.values())==[1,1,0,1]
profile=[]
for h in range(-200,501):
 veil=-math.expm1(-.012*integral(125,h+70,1000,75))
 profile.append(compose(veil,.6,h,1000,55,1))
jumps=[profile[i-1]-profile[i] for i in range(1,len(profile))]
assert min(jumps)>=-1e-9 and max(jumps)<.012
shader=root/'Assets/Elemental/Content/Shaders/ValleyAtmosphereV2.hlsl';src=re.sub(r'\s+','',shader.read_text())
assert 'floatlowerTerrain=(1-smoothstep(-radius*0.25,radius*0.60,surfaceLocal.y))*(1-planetProtect);' in src
assert 'alpha=max(lowerTerrain,1-(1-veil)*(1-aerial))*cloudProtection;' in src
assert 'floatplanetProtect=smoothstep(radius+80,radius+140,length(localOrigin+localRay*distanceMetres));' in src
report={'method':'Python double-precision numerical equivalent of actual C# math; NOT Unity NUnit/shader execution','far_column_height_cases':len(far),'far_constant_opacity':far[0],'endpoints':endpoints,'column_height_samples':len(profile),'column_height_range_m':[-200,500],'column_opacity_endpoints':[profile[0],profile[-1]],'maximum_opacity_drop_per_metre':max(jumps),'maximum_drop_at_height_m':-199+jumps.index(max(jumps)),'maximum_jump_in_8bit_alpha_levels':max(jumps)*255,'monotonic':True,'actual_shader_composition_matches_oracle':True,'shader_sha256':hashlib.sha256(shader.read_bytes()).hexdigest()}
path=lane/'Reports/offline-dissolve-validation.json';path.parent.mkdir(exist_ok=True);path.write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report,indent=2))
