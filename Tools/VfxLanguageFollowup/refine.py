from pathlib import Path
out=Path('Tools/VfxLanguageFollowup/after')
p=out/'Assets/Elemental/Presentation/VFX/EarthMaterialFeedbackPresenter.cs'; s=p.read_text().replace('= Physics.DefaultRaycastLayers','= UnityEngine.Physics.DefaultRaycastLayers').replace('&& Physics.Raycast(','&& UnityEngine.Physics.Raycast(')
fade='''                    float age = 1f-particle.remainingLifetime/Mathf.Max(.01f,particle.startLifetime);
                    Color32 tint = particle.startColor; tint.a = (byte)(255f*(1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.65f,1f,age))));
                    particle.startColor = tint;
'''
s=s.replace(fade,'').replace('                    int bounces = (int)(particle.randomSeed & 3u);',fade+'                    int bounces = (int)(particle.randomSeed & 3u);')
p.write_text(s)
p=out/'Assets/Elemental/Presentation/Fire/Shaders/ColumnDecorFlame.shader'; s=p.read_text().replace('input.positionWS.xz*2.1','input.positionWS.xz*1.15').replace('q.x+=(coherent-.5)*.38','q.x+=(coherent-.5)*.22').replace('input.positionWS.xy*3.2','input.positionWS.xy*1.7').replace('))-.5)*.48;', '))-.5)*.22;').replace('smoothstep(-edge,.55+edge,egg)','smoothstep(-edge,.24+edge,egg)').replace('*(1-smoothstep(.45,1,age))*_Opacity*.78','*(1-smoothstep(.62,1,age))*_Opacity*.9')
s=s.replace('float core=smoothstep(.45,1,egg)*(1-smoothstep(.2,.75,age))*.65;', '''// Broad hot base, distinct warm middle and thin red silhouette.
                    float core=smoothstep(.48,.82,egg)*(1-smoothstep(.36,.76,input.uv.y))
                        *lerp(1,.65,smoothstep(.35,1,age));''')
s=s.replace('smoothstep(.05,.8,egg)','smoothstep(.08,.28,egg)').replace('color=lerp(color,_CoreColor.rgb,core);','color=lerp(color,_CoreColor.rgb,core);\n                    color+=_CoreColor.rgb*core*_CoreEmission*saturate(input.fire.z);')
s=s.replace('color=min(color,_CoreColor.rgb*1.25);','color=min(color,_CoreColor.rgb*(1+_CoreEmission));')
p.write_text(s)
p=out/'Assets/Elemental/Presentation/Fire/Shaders/FireCpuFlame.hlsl'; s=p.read_text().replace('wider soft parcels with rare hot cores','broad, three-band parcels with a hot anchored base').replace('float2(5.0, 7.0)','float2(2.7, 4.0)').replace('(n1 - 0.5) * 0.7','(n1 - 0.5) * 0.32').replace('pinch * 0.28','pinch * 0.20').replace('smoothstep(-0.10-aa, 0.60+aa, field)','smoothstep(-0.06-aa, 0.22+aa, field)').replace('    coverage *= coverage;','    // Preserve a bold filled silhouette; derivatives soften its boundary only.').replace('smoothstep(0.40, 1.0, age)','smoothstep(0.58, 1.0, age)').replace('birth * death * death','birth * death').replace('lerp(0.56, 1.0','lerp(0.78, 1.0').replace('smoothstep(0.12, 0.70, field)','smoothstep(0.12, 0.32, field)').replace('smoothstep(0.72, 1.03, field)','smoothstep(0.55, 0.80, field)').replace('smoothstep(0.15, 0.70, age)) * lerp(0.35, 1.0','smoothstep(0.4, 1.0, age)) * lerp(0.72, 1.0'); p.write_text(s)
p=out/'Assets/Elemental/Content/VFX/Fire/Fire_ColumnDecor.asset'; s=p.read_text().replace('SpawnRate: 70','SpawnRate: 48').replace('FlameMinWidth: 0.28','FlameMinWidth: 0.42').replace('FlameMaxWidth: 0.72','FlameMaxWidth: 0.96').replace('MinLifetime: 0.42','MinLifetime: 0.58').replace('MaxLifetime: 0.82','MaxLifetime: 0.98'); p.write_text(s)
p=out/'Assets/Elemental/Content/VFX/Fire/Fire_ColumnDecor.mat'; s=p.read_text().replace('_CoreEmission: 0.45','_CoreEmission: 0.5').replace('_Distortion: 1.3','_Distortion: 0.8'); p.write_text(s)
