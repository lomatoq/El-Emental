from pathlib import Path
import shutil
lane=Path(__file__).resolve().parent; root=lane.parents[3]
def edit(rel,fn):
 p=root/rel;b=lane/'before'/rel;a=lane/'after'/rel
 b.parent.mkdir(parents=True,exist_ok=True);a.parent.mkdir(parents=True,exist_ok=True)
 shutil.copy2(p,b);a.write_text(fn(p.read_text(encoding='utf-8-sig')),encoding='utf-8')
edit('Assets/Elemental/Presentation/Rendering/ValleyAtmosphereController.cs',lambda s:s.replace('[Range(0,2)] public int DebugMode','[Range(0,3)] public int DebugMode').replace('new Vector4(FarArtEnabled?1:0,1000,1800,','new Vector4(FarArtEnabled?1:0,600,1100,'))
edit('Assets/Elemental/Content/Shaders/ValleyAtmosphereV2.hlsl',lambda s:s.replace('*cloudProtection*playableWindow*_ElementalValleyFarArt.x','*protect*planetProtect*_ElementalValleyFarArt.x').replace('if(_ElementalValleyDebug>1.5)return','if(_ElementalValleyDebug>2.5)return float4(farArtMask.xxx,source.a);\n    if(_ElementalValleyDebug>1.5)return').replace('(0.42*farArtMask)','(0.58*farArtMask)').replace('float polarity=lerp(-.7,1.0,jitter.x);','float polarity=lerp(-.55,1.0,jitter.x);').replace('lerp(.35,1.0,broad)*_ElementalValleyFarArt.w','lerp(.65,1.0,broad)*_ElementalValleyFarArt.w'))
edit('Assets/Elemental/Presentation/Rendering/ProceduralCloudBanks.cs',lambda s:s.replace('new Vector3(-760,100,1300)','new Vector3(-760,290,1500)').replace('new Vector3(680,180,1900)','new Vector3(680,440,2000)').replace('new Vector3(70,260,2500)','new Vector3(70,590,2850)').replace('new Vector3(-500,90,-1700)','new Vector3(30,300,-1550)').replace('new Vector3(850,170,-2000)','new Vector3(1050,450,-2050)').replace('new Vector3(200,300,-2800)','new Vector3(500,650,-2900)').replace('new Vector3(700+i*55,180+i*14,390+i*23)','new Vector3(780+i*55,260+i*14,430+i*23)'))
edit('Assets/Elemental/Tests/EditMode/ProceduralCloudBanksTests.cs',lambda s:s.replace('Is.InRange(80,320)','Is.InRange(250,680)'))
shutil.copy2(root/'Tools/FireIntegration/Staged/ProceduralCloudBanks/compile.py',lane/'compile.py')
