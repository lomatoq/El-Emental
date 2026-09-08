from pathlib import Path
import shutil
lane=Path(__file__).resolve().parent;root=lane.parents[3]
def edit(rel,fn):
 p=root/rel;a=lane/'after'/rel;b=lane/'before'/rel;a.parent.mkdir(parents=True,exist_ok=True);b.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,b);a.write_text(fn(p.read_text(encoding='utf-8-sig')),encoding='utf-8')
def shader(s):
 s=s.replace('_AmbientStrength("Ambient Strength", Range(0.0, 2.0)) = 0.82','_AmbientStrength("Ambient Strength", Range(0.0, 2.0)) = 0.82\n        _OcclusionStrength("Contact Occlusion Strength",Range(0,1))=1\n        _TwilightFill("Twilight Readability Floor",Range(0,.3))=0')
 s=s.replace('half _AmbientStrength;','half _AmbientStrength;\n                half _OcclusionStrength;\n                half _TwilightFill;')
 s=s.replace('TEXTURE2D(_BaseMap);','float _ElementalSolarAltitude;\n            TEXTURE2D(_BaseMap);')
 s=s.replace('float3 objectMappingPosition = input.positionOS;','float3 objectMappingPosition = input.positionOS;\n                half3 objectMappingNormal = geometryNormalOS;')
 old='''                        float4(input.positionOS, 1.0)).xyz;
                }'''
 new='''                        float4(input.positionOS, 1.0)).xyz;
                    // Inverse-transpose rest-frame normal via cofactors, including nonuniform scale.
                    float3 c0=_FractureLocalToStructure._m00_m10_m20;
                    float3 c1=_FractureLocalToStructure._m01_m11_m21;
                    float3 c2=_FractureLocalToStructure._m02_m12_m22;
                    float3 co0=cross(c1,c2),co1=cross(c2,c0),co2=cross(c0,c1);
                    float determinant=dot(c0,co0);
                    objectMappingNormal=normalize((co0*geometryNormalOS.x+co1*geometryNormalOS.y+co2*geometryNormalOS.z)*(determinant<0?-1:1));
                }
                mappingNormal=normalize(lerp(objectMappingNormal,geometryNormalWS,saturate(_UsePlanetFrame)));'''
 assert old in s;s=s.replace(old,new)
 s=s.replace('screenAo.directAmbientOcclusion;','lerp(1.0h,screenAo.directAmbientOcclusion,_OcclusionStrength);')
 s=s.replace('screenAo.indirectAmbientOcclusion;','lerp(1.0h,screenAo.indirectAmbientOcclusion,_OcclusionStrength);\n                // A bounded rock-only sky-bounce floor near sunset; noon and deep night are unchanged.\n                half twilight=smoothstep(-.22h,-.04h,_ElementalSolarAltitude)*(1.0h-smoothstep(.08h,.35h,_ElementalSolarAltitude));\n                ambient=max(ambient,half3(.90h,.85h,1.0h)*_TwilightFill*twilight*(1.0h-characterMode));')
 return s
edit('Assets/Elemental/Content/Shaders/RumbleRockLit.shader',shader)
for name in ['RumbleArenaSandstone','RumbleSandstoneFractureInterior']:
 edit('Assets/Elemental/Content/GraphicsV5/Materials/'+name+'.mat',lambda s:s.replace('_BevelLight: 0.2','_BevelLight: 0.13').replace('_FacetContrast: 0.16','_FacetContrast: 0.12').replace('_Roughness: 0.92','_Roughness: 0.96').replace('_ShadowFloor: 0.56','_ShadowFloor: 0.66').replace('    - _AmbientStrength: 0.76','    - _AmbientStrength: 0.76\n    - _OcclusionStrength: 0.68\n    - _TwilightFill: 0.18'))
