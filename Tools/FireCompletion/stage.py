from pathlib import Path
base=Path('Tools/FireCompletion'); rels=['Assets/Elemental/Presentation/Fire/Shaders/FireCpuFlame.hlsl','Assets/Elemental/Content/VFX/Fire/Fire_CpuMesh.mat','Assets/Elemental/Content/VFX/Fire/Fire_Default.asset']
for rel in rels:
 for part in ['before','after']:
  p=base/part/rel;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(Path(rel).read_bytes())
p=base/'after'/rels[0];s=p.read_text()
s=s.replace('    float phase = Phase;', '    float body = 1.0 - step(0.66, frac(Phase * 2.173));\n    float phase = Phase;')
s=s.replace('    field -= pinch * 0.20 * smoothstep(0.05, 0.25, neck);\n    field -= smoothstep(0.58, 1.0, age) * (0.35 + 0.45 * y);', '''    field -= pinch * 0.20 * smoothstep(0.05, 0.25, neck);
    // The continuous body is a broad rounded parcel, never another narrow spear.
    // Its two large overlapping lobes break symmetry without producing texture grit.
    float2 bodyQ = float2(x, y - 0.44);
    float2 bodyEllipse = bodyQ / float2(0.46, 0.48);
    float bodyField = 1.0 - dot(bodyEllipse, bodyEllipse);
    float2 shoulder = (bodyQ - float2((n0 - 0.5) * 0.16, 0.15)) / float2(0.34, 0.34);
    bodyField = max(bodyField, 1.0 - dot(shoulder, shoulder));
    field = lerp(field, bodyField, body);
    // Old detached parcels contract and disappear instead of drifting as pink ghosts.
    field -= smoothstep(0.48, 0.84, age) * lerp(0.35 + 0.45 * y, 0.75, body);''')
s=s.replace('    float death = 1.0 - smoothstep(0.58, 1.0, age);','''    float death = 1.0 - smoothstep(lerp(0.50, 0.42, body), 0.84, age);
    death *= death;''')
s=s.replace('    if (frac(phase * 2.173) < 0.66)','    if (body > 0.5)')
p.write_text(s)
p=base/'after'/rels[1];s=p.read_text().replace('_Distortion: 1','_Distortion: 0.8').replace('_Opacity: 0.72','_Opacity: 0.84');p.write_text(s)
p=base/'after'/rels[2];s=p.read_text().replace('SpawnRate: 220','SpawnRate: 170').replace('FlameMinWidth: 0.45','FlameMinWidth: 0.65').replace('FlameMaxWidth: 0.9','FlameMaxWidth: 1.15').replace('FlameMinAspect: 1.5','FlameMinAspect: 1.2').replace('FlameMaxAspect: 2.4','FlameMaxAspect: 1.85');p.write_text(s)
