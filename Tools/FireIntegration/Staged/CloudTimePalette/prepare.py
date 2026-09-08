from pathlib import Path
import shutil
lane=Path(__file__).resolve().parent;root=lane.parents[3]
def edit(rel,fn):
 p=root/rel;a=lane/'after'/rel;b=lane/'before'/rel;a.parent.mkdir(parents=True,exist_ok=True);b.parent.mkdir(parents=True,exist_ok=True)
 shutil.copy2(p,b);a.write_text(fn(p.read_text(encoding='utf-8-sig')),encoding='utf-8')
edit('Assets/Elemental/Presentation/Rendering/ValleyAtmosphereController.cs',lambda s:s.replace('invalidReported=false;','''invalidReported=false;
                var palette=Elemental.Simulation.Rendering.ValleyTimePalette.Evaluate(Shader.GetGlobalFloat("_ElementalNight01"),Shader.GetGlobalFloat("_ElementalSolarAltitude"),
                    new Unity.Mathematics.float3(profile.DayFog.r,profile.DayFog.g,profile.DayFog.b),new Unity.Mathematics.float3(profile.DayFogBottom.r,profile.DayFogBottom.g,profile.DayFogBottom.b));
                Shader.SetGlobalVector("_ElementalValleyTimeFogTop",new Vector4(palette.FogTop.x,palette.FogTop.y,palette.FogTop.z,1));
                Shader.SetGlobalVector("_ElementalValleyTimeFogBottom",new Vector4(palette.FogBottom.x,palette.FogBottom.y,palette.FogBottom.z,1));
                Shader.SetGlobalVector("_ElementalValleyTimeCloudTop",new Vector4(palette.CloudTop.x,palette.CloudTop.y,palette.CloudTop.z,1));
                Shader.SetGlobalVector("_ElementalValleyTimeCloudBottom",new Vector4(palette.CloudBottom.x,palette.CloudBottom.y,palette.CloudBottom.z,1));'''))
edit('Assets/Elemental/Content/Shaders/ProceduralCloudBanks.shader',lambda s:s.replace('float4 _ElementalValleyFog;','float4 _ElementalValleyFog,_ElementalValleyTimeCloudTop,_ElementalValleyTimeCloudBottom;').replace('tint*=lerp(float3(.22,.28,.40),float3(1,1,1),day);','tint*=lerp(_ElementalValleyTimeCloudBottom.rgb,_ElementalValleyTimeCloudTop.rgb,smoothstep(-.35,.35,p.y));'))
edit('Assets/Elemental/Content/Shaders/ValleyCloudParticles.shader',lambda s:s.replace('float4 _ElementalValleyFog,_ElementalValleyFar,_ElementalPlanetCenterRadius;','float4 _ElementalValleyFog,_ElementalValleyFar,_ElementalPlanetCenterRadius;\n            float4 _ElementalValleyTimeCloudTop,_ElementalValleyTimeCloudBottom;').replace('float3 tint=lerp(float3(0.22,0.28,0.38),float3(0.98,0.99,1),day);','float3 tint=lerp(_ElementalValleyTimeCloudBottom.rgb,_ElementalValleyTimeCloudTop.rgb,smoothstep(.15,.85,i.uv.y));'))
edit('Assets/Elemental/Content/Shaders/ValleyAtmosphereV2.hlsl',lambda s:s.replace('float4 _ElementalValleyDay,_ElementalValleyBottom,_ElementalValleyNight,_ElementalValleyDusk;','float4 _ElementalValleyDay,_ElementalValleyBottom,_ElementalValleyNight,_ElementalValleyDusk;\nfloat4 _ElementalValleyTimeFogTop,_ElementalValleyTimeFogBottom,_ElementalValleyTimeCloudTop,_ElementalValleyTimeCloudBottom;').replace('float3 tint=lerp(float3(0.22,0.28,0.38),float3(0.98,0.99,1.0),day);','float3 tint=lerp(_ElementalValleyTimeCloudBottom.rgb,_ElementalValleyTimeCloudTop.rgb,smoothstep(.15,.85,uv.y));').replace('float3 fog=lerp(_ElementalValleyNight.rgb,dayFog,day);\n    fog=lerp(fog,_ElementalValleyDusk.rgb,saturate(_ElementalTwilight01)*0.6);','float3 fog=lerp(_ElementalValleyTimeFogBottom.rgb,_ElementalValleyTimeFogTop.rgb,heightColor);'))
shutil.copy2(root/'Tools/FireIntegration/Staged/ProceduralCloudBanks/compile.py',lane/'compile.py')
