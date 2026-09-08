from pathlib import Path
import re
base=Path('Tools/AoFinal'); files=['Assets/Elemental/Content/Shaders/RumbleRockLit.shader','Assets/Settings/ElEmentalRenderer.asset']
for rel in files:
 for part in ['before','after']:
  p=base/part/rel;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(Path(rel).read_bytes())
p=base/'after'/files[0];s=p.read_text()
s=s.replace('FaceData,4,Albedo,5)', 'FaceData,4,Albedo,5,ContactAO,6)')
s=s.replace('                half _SurfaceMode;','                half _SurfaceMode;\n                half _Surface;')
s=s.replace('                if (_DebugMode >= 4.5h)','                if (_DebugMode >= 4.5h && _DebugMode < 5.5h)')
needle='''                AmbientOcclusionFactor screenAo = GetScreenSpaceAmbientOcclusion(
                    GetNormalizedScreenSpaceUV(input.positionCS));'''
replace=needle+'''
                // Cosmetic chips are deliberately absent from depth/normal buffers.
                // Their runtime _Surface=1 must not sample AO belonging to terrain behind them.
                // The shared custom shader does not compile URP's transparent keyword variants.
                if (_Surface > 0.5h)
                {
                    screenAo.indirectAmbientOcclusion = 1.0h;
                    screenAo.directAmbientOcclusion = 1.0h;
                }
                if (_DebugMode >= 5.5h)
                    return half4(screenAo.indirectAmbientOcclusion.xxx, 1.0h);'''
assert needle in s;s=s.replace(needle,replace)
s=s.replace('light.distanceAttenuation * light.shadowAttenuation;', '''light.distanceAttenuation * light.shadowAttenuation *
                                  lerp(1.0h,screenAo.directAmbientOcclusion,_OcclusionStrength);''')
start=s.index('                half3 geometryNormalOS = normalize(input.normalOS);',s.index('            DepthNormalsVaryings DepthNormalsVert'))
end=s.index('                return output;',start)
s=s[:start]+'''                // AO describes geometric contact and creases, not the stylized radial
                // normal used to soften forward-lit slab sides. Keep those policies separate.
                output.normalWS = NormalizeNormalPerVertex(
                    TransformObjectToWorldNormal(input.normalOS));
'''+s[end:]
s=s.replace("        // in Scene view. This pass mirrors the forward pass' authored/radial normal\n        // policy and keeps the depth-normal buffer SRP-batcher compatible.","        // in Scene view. This pass uses geometry normals for occlusion while the\n        // forward pass may use radial artistic smoothing. Material layouts remain identical.")
p.write_text(s)
p=base/'after'/files[1];s=p.read_text();assert '    Downsample: 1' in s;s=s.replace('    Downsample: 1','    Downsample: 0');p.write_text(s)
blocks=re.findall(r'CBUFFER_START\(UnityPerMaterial\)(.*?)CBUFFER_END',(base/'after'/files[0]).read_text(),re.S)
assert len(blocks)==2 and blocks[0].strip()==blocks[1].strip()
for rel in files:
 old=(base/'before'/rel).read_text();new=(base/'after'/rel).read_text()
 for token in ['_AmbientStrength','_ShadowFloor','_TwilightFill']:
  assert [l for l in old.splitlines() if token in l]==[l for l in new.splitlines() if token in l]
renderer=(base/'after'/files[1]).read_text()
for line in ['    Intensity: 0.95','    DirectLightingStrength: 0.3','    Radius: 0.32']:
 assert line in renderer
print('PASS: CBuffers identical; ambient/shadow/twilight lines unchanged; SSAO .95/.30/.32 preserved.')
