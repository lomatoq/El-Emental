from pathlib import Path
import math,random,re,json
lane=Path(__file__).resolve().parent
shader=(lane/'after/Assets/Elemental/Content/Shaders/RumbleRockLit.shader').read_text()
buffers=re.findall(r'CBUFFER_START\(UnityPerMaterial\)(.*?)CBUFFER_END',shader,re.S)
assert len(buffers)==2 and re.sub(r'\s','',buffers[0])==re.sub(r'\s','',buffers[1]),'Forward/depthnormal SRP layout mismatch'
assert '#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS' in shader
def dot(a,b):return sum(x*y for x,y in zip(a,b))
def cross(a,b):return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def length(a):return math.sqrt(dot(a,a))
rng=random.Random(14550);max_error=0
for i in range(1000):
 angle=rng.uniform(-math.pi,math.pi);c=math.cos(angle);s=math.sin(angle)
 sx,sy,sz=[rng.uniform(.15,4)*(-1 if rng.random()<.25 else 1) for _ in range(3)]
 cols=[(c*sx,0,-s*sx),(0,sy,0),(s*sz,0,c*sz)]
 n=[rng.uniform(-1,1) for _ in range(3)];t=cross(n,(.24,.83,.47))
 co=[cross(cols[1],cols[2]),cross(cols[2],cols[0]),cross(cols[0],cols[1])]
 nt=[sum(co[j][k]*n[j] for j in range(3)) for k in range(3)]
 tt=[sum(cols[j][k]*t[j] for j in range(3)) for k in range(3)]
 error=abs(dot(nt,tt))/max(1e-8,length(nt)*length(tt));max_error=max(max_error,error);assert error<1e-6
paths=list((lane/'after/Assets/Elemental/Content/GraphicsV5/Materials').glob('*.mat'))
for p in paths:
 before=(lane/'before'/p.relative_to(lane/'after')).read_text();after=p.read_text()
 assert before.split('    m_Colors:')[1]==after.split('    m_Colors:')[1],'Palette changed'
 assert 'm_Texture: {fileID: 0}' in after and '_SideShadingSmoothness: 0' in after
report={'rest_normal_tangent_cases':1000,'maximum_orthogonality_error':max_error,'forward_depthnormal_cbuffer_match':True,'additional_light_variants_preserved':True,'both_palettes_unchanged':True,'shader_compilation':'requires Unity owner; no C# changes'}
(lane/'Reports').mkdir(exist_ok=True);(lane/'Reports/validation.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
