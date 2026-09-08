from pathlib import Path
base=Path('Tools/RockShadingFollowup'); rels=['Assets/Elemental/Content/Shaders/RumbleRockLit.shader','Assets/Elemental/Content/GraphicsV5/Materials/RumbleArenaSandstone.mat']
for rel in rels:
 for part in ['before','after']:
  p=base/part/rel;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(Path(rel).read_bytes())
p=base/'after'/rels[0];s=p.read_text()
s=s.replace('        _Roughness("Visual Roughness",', '        _FormLightStrength("Broad Form Lighting", Range(0.0, 1.0)) = 0.0\n        _Roughness("Visual Roughness",')
s=s.replace('                half _Roughness;', '                half _FormLightStrength;\n                half _Roughness;')
s=s.replace('                half3 direct = albedo * directTint * mainLight.color * shadow *','''                // Opt-in broad value separation follows the existing smooth light ramp.
                // Unlike albedo noise it describes the actual planes of the stone.
                half formLight = lerp(1.0h, lerp(.40h,1.0h,softDiffuse),
                    _FormLightStrength * (1.0h-characterMode));
                half3 direct = albedo * directTint * formLight * mainLight.color * shadow *''')
old='''                ambient *= (1.0h - stableFormOcclusion) *
                           lerp(1.0h,screenAo.indirectAmbientOcclusion,_OcclusionStrength);
                // A bounded rock-only sky-bounce floor near sunset; noon and deep night are unchanged.
                half twilight=smoothstep(-.22h,-.04h,_ElementalSolarAltitude)*(1.0h-smoothstep(.08h,.35h,_ElementalSolarAltitude));
                ambient=max(ambient,half3(.90h,.85h,1.0h)*_TwilightFill*twilight*(1.0h-characterMode));'''
new='''                // Bounded hemispheric sky fill: sky-facing planes stay readable,
                // downward cavities stay darker. The existing planet frame survives fracture swaps.
                half twilight=smoothstep(-.22h,-.04h,_ElementalSolarAltitude)*(1.0h-smoothstep(.08h,.35h,_ElementalSolarAltitude));
                half skyFacing = lerp(.48h,1.0h,saturate(radialUpAlignment*.5h+.5h));
                ambient=max(ambient,half3(.90h,.85h,1.0h)*_TwilightFill*twilight*skyFacing*(1.0h-characterMode));
                // Occlude the fill too; applying the floor after SSAO erased contact shadows.
                ambient *= (1.0h - stableFormOcclusion) *
                           lerp(1.0h,screenAo.indirectAmbientOcclusion,_OcclusionStrength);'''
assert old in s;s=s.replace(old,new);p.write_text(s)
p=base/'after'/rels[1];s=p.read_text().replace('    - _FacetContrast: 0.3','    - _FormLightStrength: 0.45\n    - _FacetContrast: 0.3').replace('    - _TwilightFill: 0.18','    - _TwilightFill: 0.24');p.write_text(s)
