using System;
using System.Collections;
using System.IO;
using System.Linq;
using Elemental.Presentation.Fire;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
    public sealed partial class HardPolishFireStreamBindingRuntimeTests
    {
        private static void SaveFluidMotionFrame(string folder,string phase,int frame)
        {
            var image=ScreenCapture.CaptureScreenshotAsTexture();
            try{File.WriteAllBytes(Path.Combine(folder,phase+"-"+frame.ToString("D3")+".jpg"),image.EncodeToJPG(92));}
            finally{UnityEngine.Object.Destroy(image);}
        }
        private static void SaveRingLayerControl(Camera camera,string folder,string name)
        {
            var image=RenderFireDofSameFrame(camera);
            try{File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());}
            finally{UnityEngine.Object.Destroy(image);}
        }
        [UnityTest,Timeout(240000)]public IEnumerator SavedFighterContinuousFireMotionCorpus()
        {
            IDisposable safeRival=null;FireVisualCaptureCamera capture=null;float step=Time.captureDeltaTime;
            string folder="BuildReports/HardPolish/G05/FireMotion-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(folder);
            try
            {
                Time.captureDeltaTime=1f/60;yield return ReadyFireAbilities();safeRival=new FireVisualRivalIsolation(duel.BotTransform,flightMotor);
                var camera=All<Elemental.Presentation.Rendering.CelestialSystemBehaviour>().Single().TargetCamera;
                capture=camera.gameObject.AddComponent<FireVisualCaptureCamera>();
                Vector3 origin=binding.PlayerSession.MuzzlePosition,up=flightMotor.LocalUp,side=Vector3.Cross(up,flightMotor.FacingForward).normalized;
                capture.Place(origin+side*4-up,origin+up*2);
                Assert.That(flightAbility.TryShoot(origin+up*30,false),Is.True);
                int frames=0;
                for(int i=0;i<12;i++)
                {
                    yield return new WaitForSeconds(.05f);
                    var bolt=Enumerable.Range(0,flightAbility.ProjectileCapacity).Select(flightAbility.GetProjectile).FirstOrDefault(x=>x.Active);
                    if(!bolt.Active)continue;
                    Vector3 head=bolt.Position;capture.Place(head+side*4-up*2,head-up*2);
                    yield return new WaitForEndOfFrame();SaveFluidMotionFrame(folder,"bolt",frames++);
                }
                Assert.That(frames,Is.GreaterThanOrEqualTo(7));flightAbility.CancelAll();yield return new WaitForSeconds(.2f);
                origin=flightMotor.Body.position;capture.Place(origin+side*4+up*.9f,origin+up*.5f);
                flightAbility.SetLiftHeld(true);
                for(int i=0;i<12;i++){yield return new WaitForSeconds(.05f);yield return new WaitForEndOfFrame();SaveFluidMotionFrame(folder,"feet",i);}
                flightAbility.CancelAll();yield return new WaitForSeconds(1.5f);
                origin=flightMotor.Body.position;capture.Place(origin+side*7+up*5,origin);
                bool line=flightAbility.TryGroundLine(origin+side*.8f,origin+side*3);
                Assert.That(line,Is.True,"Actual arena must support the sequential ground fire motion sample.");
                for(int i=0;i<8;i++){yield return new WaitForSeconds(.035f);yield return new WaitForEndOfFrame();SaveFluidMotionFrame(folder,"ground",i);}
                yield return new WaitForSeconds(.8f);Assert.That(flightAbility.TryRing(),Is.True);yield return new WaitForSeconds(.8f);
                capture.Place(flightMotor.Body.position-flightMotor.FacingForward*10+up*8,flightMotor.Body.position);
                yield return new WaitForEndOfFrame();SaveFluidMotionFrame(folder,"held",0);flightAbility.ReleaseRing();
                float releaseTime=Time.time;bool sawCoolingRibbon=false;
                var ringEffects=binding.GetComponent<FireAbilityEffects>();
                var ribbonCsv=new System.Text.StringBuilder("age,spans,ribbonQueries,allQueries,active,alive\n");
                for(int i=0;i<15;i++)
                {
                    yield return new WaitForSeconds(.05f);yield return new WaitForEndOfFrame();SaveFluidMotionFrame(folder,"ring",i);
                    float age=Time.time-releaseTime;
                    if(i==9)
                    {
                        // Synchronous HDR layer isolation in the SAME production
                        // sequence that exposes the remaining foreground lobes.
                        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
                        var ribbon=typeof(FireAbilityEffects).GetField("ringRibbon",flags).GetValue(ringEffects);
                        var ribbonRenderer=(MeshRenderer)typeof(FireRingRibbonRenderer).GetField("renderer",flags).GetValue(ribbon);
                        var gas=(FireFlowVolumeBackend[])typeof(FireAbilityEffects).GetField("flows",flags).GetValue(ringEffects);
                        var medium=new Material[24];for(int j=0;j<24;j++)medium[j]=(Material)typeof(FireFlowVolumeBackend).GetField("smokeMaterial",flags).GetValue(gas[j+10]);
                        bool ribbonWasEnabled=ribbonRenderer.enabled;
                        try
                        {
                            SaveRingLayerControl(camera,folder,"ring-009-all");
                            ribbonRenderer.enabled=false;SaveRingLayerControl(camera,folder,"ring-009-no-ribbon");
                            ribbonRenderer.enabled=ribbonWasEnabled;
                            foreach(var m in medium)m.SetFloat("_RingMediumVisibility",0);
                            SaveRingLayerControl(camera,folder,"ring-009-no-parcel-medium");
                            ribbonRenderer.enabled=false;SaveRingLayerControl(camera,folder,"ring-009-emission-and-atlas");
                        }
                        finally{ribbonRenderer.enabled=ribbonWasEnabled;foreach(var m in medium)m.SetFloat("_RingMediumVisibility",1);}
                    }
                    ribbonCsv.AppendLine(age+","+ringEffects.RingRibbonSpans+","+ringEffects.RingRibbonQueries+","+ringEffects.QueryCount+","+ringEffects.ActiveEmitters+","+ringEffects.AliveParticles);
                    if(age>Elemental.Simulation.Fire.FireAbilityTuning.RingSeconds&&age<Elemental.Simulation.Fire.FireAbilityTuning.RingSeconds+.22f&&ringEffects.RingRibbonSpans>0)sawCoolingRibbon=true;
                }
                File.WriteAllText(Path.Combine(folder,"ring-continuity.csv"),ribbonCsv.ToString());
                Assert.That(sawCoolingRibbon,Is.True,"Validated continuous density must cool with the residual gas rather than disappear at source expiry.");
                var effects=binding.GetComponent<FireAbilityEffects>();
                File.WriteAllText(Path.Combine(folder,"last-frame.json"),JsonUtility.ToJson(ReadFireEffectsFrame(),true));
                Assert.That(effects.BudgetStops,Is.Zero);
            }
            finally{safeRival?.Dispose();Time.captureDeltaTime=step;if(capture!=null)UnityEngine.Object.Destroy(capture);ReleaseFireAbilityFixture();}
        }
    }
}
