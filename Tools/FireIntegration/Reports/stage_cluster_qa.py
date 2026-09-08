from pathlib import Path
import shutil
lane=Path('Tools/FireIntegration/Staged/ClusterGroundDust')
for rel in ['Assets/Elemental/Tests/EditMode/EarthSurfaceWindDustTestLauncher.cs','Assets/Elemental/Tests/PlayMode/EarthSurfaceWindDustProductionTests.cs']:
 for side in ['before','after']:
  p=lane/side/rel;p.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(rel,p)
p=lane/'after/Assets/Elemental/Tests/EditMode/EarthSurfaceWindDustTestLauncher.cs';s=p.read_text(encoding='utf-8-sig').replace('"Elemental.Tests.EditMode.EarthSurfaceWindPolicyTests");','"Elemental.Tests.EditMode.EarthSurfaceWindPolicyTests", "Elemental.Tests.EditMode.EarthGroundDustDensityTests");');p.write_text(s,encoding='utf-8')
p=lane/'after/Assets/Elemental/Tests/PlayMode/EarthSurfaceWindDustProductionTests.cs';s=p.read_text(encoding='utf-8-sig').replace('public float meanTravelMetres;','public float meanTravelMetres,meanStoneNeighbours,effectiveStoneRate;\n            public int clusteredCandidates,gapEmitted;');s=s.replace('Assert.That(report.stoneCandidates, Is.GreaterThan(0));','''Assert.That(report.stoneCandidates, Is.GreaterThan(0));
                if(dust.Profile.clusteredWisps)
                {
                    report.meanStoneNeighbours=dust.MeanStoneNeighbours;report.effectiveStoneRate=dust.EffectiveStoneRate;
                    report.clusteredCandidates=dust.ClusteredStoneCandidates;report.gapEmitted=dust.GapEmitted;
                    Assert.That(report.effectiveStoneRate,Is.InRange(0,48));
                    Assert.That(dust.Profile.maximumParticles,Is.LessThanOrEqualTo(192));
                    if(report.clusteredCandidates>0)Assert.That(report.gapEmitted,Is.GreaterThan(0),"Actual nearby rocks should receive between-stone wisps.");
                    var pref=flow.Preferences;bool savedReduced=pref.ReducedMotion;
                    try
                    {
                        pref.Set(pref.MasterVolume,pref.UIVolume,pref.Sensitivity,false);yield return null;yield return null;
                        float fullRate=dust.EffectiveStoneRate;
                        pref.Set(pref.MasterVolume,pref.UIVolume,pref.Sensitivity,true);yield return null;yield return null;
                        Assert.That(dust.EffectiveStoneRate,Is.LessThanOrEqualTo(fullRate*dust.Profile.reducedMotionRate+.01f));
                    }
                    finally{pref.Set(pref.MasterVolume,pref.UIVolume,pref.Sensitivity,savedReduced);}
                    yield return null;
                }''');p.write_text(s,encoding='utf-8')
p=lane/'after/Assets/Elemental/Presentation/VFX/EarthSurfaceWindDust.cs';s=p.read_text(encoding='utf-8').replace('Reduced ? profile.reducedMotionSpeed : 1','Reduced ? Mathf.Clamp01(profile.reducedMotionSpeed) : 1').replace('Reduced?profile.reducedMotionRate:1','Reduced?Mathf.Clamp01(profile.reducedMotionRate):1').replace('Reduced?profile.reducedMotionSpeed:1','Reduced?Mathf.Clamp01(profile.reducedMotionSpeed):1');s=s.replace('point=Vector3.Lerp(a,b,.3f+.4f*Next())+side*((Next()-.5f)*.3f);gap=true;','if((b-a).sqrMagnitude>.04f){point=Vector3.Lerp(a,b,.3f+.4f*Next())+side*((Next()-.5f)*.3f);gap=true;}');p.write_text(s,encoding='utf-8')
