from pathlib import Path
root=Path.cwd(); out=root/'Tools/VfxLanguageFollowup/after'; before=root/'Tools/VfxLanguageFollowup/before'
for rel in ['Assets/Elemental/Tests/EditMode/EarthStoneImpactDustTests.cs','Assets/Elemental/Tests/PlayMode/EarthStoneImpactDustRuntimeTests.cs']:
 p=root/rel
 for dst in [out,before]:
  (dst/rel).parent.mkdir(parents=True,exist_ok=True);(dst/rel).write_bytes(p.read_bytes())
 p=out/rel;s=p.read_text().replace('Is.EqualTo(4)','Is.EqualTo(8)').replace('UsesFourMeshes','UsesEightMeshes').replace('Is.InRange(0,3)','Is.InRange(0,7)').replace('mask,Is.EqualTo(15)','mask,Is.EqualTo(255)').replace('All four silhouettes','All eight silhouettes')
 if 'RuntimeTests' in rel:
  s=s.replace('                int lightDust=dust.particleCount;', '''                int lightDust=dust.particleCount;''')
  s=s.replace('                // Several separated identities', '''                var layerParticles = new ParticleSystem.Particle[dust.main.maxParticles];
                int layerCount = dust.GetParticles(layerParticles), layerMask = 0;
                float longestContact = 0f, shortestResidual = float.MaxValue;
                for (int i=0;i<layerCount;i++)
                {
                    int role=(int)(layerParticles[i].randomSeed&3u); layerMask|=1<<role;
                    if(role==0) longestContact=Mathf.Max(longestContact,layerParticles[i].startLifetime);
                    if(role==2) shortestResidual=Mathf.Min(shortestResidual,layerParticles[i].startLifetime);
                }
                Assert.That(layerMask,Is.EqualTo(7),"Heavy impacts need contact, main cloud and residual roles.");
                Assert.That(shortestResidual,Is.GreaterThan(longestContact),"Residual haze must outlive contact grit.");
                // Several separated identities''')
 p.write_text(s)
