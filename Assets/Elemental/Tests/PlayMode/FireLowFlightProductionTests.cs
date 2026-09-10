using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Elemental.Runtime.Characters;
using Elemental.Presentation.MotionMatching;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Profiling;
namespace Elemental.Tests.PlayMode
{
    public sealed partial class HardPolishFireStreamBindingRuntimeTests
    {
        [UnityTest,Timeout(240000)]
        public IEnumerator SavedFighterLowFireFlightHoversBrakesAtWallAndCancels()
        {
            FireFlightMotionInput flightInput=null;MonoBehaviour original=null;System.IDisposable safeRival=null;
            FireVisualCaptureCamera capture=null;GameObject wall=null;float saved=Time.captureDeltaTime;
            ProfilerRecorder recorder=default;
            string folder="BuildReports/HardPolish/G05/LowFireFlight-"+System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            try
            {
                Time.captureDeltaTime=1f/60;yield return ReadyFireAbilities();
                safeRival=new FireVisualRivalIsolation(duel.BotTransform,flightMotor);
                original=(MonoBehaviour)typeof(PlanetMotor).GetField("inputSourceBehaviour",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(flightMotor);
                flightInput=flightMotor.gameObject.AddComponent<FireFlightMotionInput>();flightMotor.ConfigureInputSource(flightInput);
                Vector3 up=flightMotor.LocalUp,origin=flightMotor.Body.position,feet=flightMotor.SupportFeetPoint(up);
                Vector3 direction=flightMotor.FacingForward;float longest=0;
                // Select real arena support with a clear body corridor; never replace the production ground or fighter.
                for(int i=0;i<16;i++)
                {
                    Vector3 candidate=Quaternion.AngleAxis(i*22.5f,up)*flightMotor.FacingForward;
                    float clear=flightMotor.Body.SweepTest(candidate,out var block,4,QueryTriggerInteraction.Ignore)?block.distance:4;
                    if(clear<=longest)continue;
                    bool supported=true;
                    for(int j=1;j<=3;j++)
                    {
                        if(!Physics.Raycast(feet+candidate*j+up*.8f,-up,out var support,1.6f,flightMotor.GroundMask,QueryTriggerInteraction.Ignore)||
                           Vector3.Dot(support.normal,up)<.55f){supported=false;break;}
                    }
                    if(supported){direction=candidate;longest=clear;}
                }
                Assert.That(longest,Is.GreaterThan(3.1f),"Saved arena must provide a supported 3m corridor for this actual-body proof.");
                flightMotor.SetAimDirection(direction);flightInput.Move=new float2(0,1);
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Low fire flight swept wall fixture";
                Vector3 plane=origin+direction*2.85f;
                wall.transform.SetPositionAndRotation(plane+up*1.2f,Quaternion.LookRotation(direction,up));
                wall.transform.localScale=new Vector3(4,6,.25f);Physics.SyncTransforms();
                var view=All<Elemental.Presentation.Rendering.CelestialSystemBehaviour>().Single().TargetCamera;
                capture=view.gameObject.AddComponent<FireVisualCaptureCamera>();
                Vector3 side=Vector3.Cross(up,direction).normalized;
                capture.Place(origin+side*7+up*2+direction,origin+direction*.7f+up*.65f);
                Directory.CreateDirectory(folder);
                recorder=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"Elemental.Fire.LowFlight.Fixed",64);
                flightAbility.SetLowFlightHeld(true);
                float peakSpeed=0,peakClearance=0,peakLean=0;long peakNanoseconds=0;int blocked=0,fall=0;bool emitted=false;
                var driver=flightActor.GetComponent<Elemental.Presentation.Animation.EarthAnimationDriver>();
                for(int i=0;i<85;i++)
                {
                    yield return new WaitForEndOfFrame();
                    Assert.That(flightAbility.IsAvailable,Is.True,"Combat availability must remain alive throughout the isolated motion proof.");
                    Assert.That(flightMotor.FireLowFlightActive,Is.True,"Near-ground flight lost valid saved-arena support.");
                    peakSpeed=Mathf.Max(peakSpeed,Vector3.ProjectOnPlane(flightMotor.Body.linearVelocity,flightMotor.LocalUp).magnitude);
                    peakClearance=Mathf.Max(peakClearance,flightMotor.FireLowFlightClearance);
                    peakLean=Mathf.Max(peakLean,flightActor.FireFlightLeanDegrees);
                    peakNanoseconds=System.Math.Max(peakNanoseconds,recorder.LastValue);
                    if(flightMotor.FireLowFlightBlocked)blocked++;
                    emitted|=flightAbility.SourceCount>0;
                    Assert.That(Vector3.Dot(flightMotor.Body.position-plane,direction),Is.LessThan(-.1f),"Actual fighter root crossed the real opaque wall.");
                    if(i>24)
                    {
                        if(driver.GetCurrentAnimatorStateInfo(0).fullPathHash==Animator.StringToHash("Base Layer.Fall"))fall++;
                        Assert.That(flightActor.GetComponent<EAMMBasePoseBridge>().AppliedEammMasterWeight,Is.Zero,"Low flight must not play walking legs.");
                    }
                    if(i==20||i==65)
                    {
                        if(i==20)Assert.That(binding.GetComponent<Elemental.Presentation.Fire.FireAbilityEffects>().MeteorBowVisible,Is.True,"Fast flight must render its hot leading hemisphere.");
                        var image=ScreenCapture.CaptureScreenshotAsTexture();
                        try{File.WriteAllBytes(folder+"/"+(i==20?"moving":"wall-contact")+".png",image.EncodeToPNG());}
                        finally{Object.Destroy(image);}
                    }
                }
                Assert.That(peakSpeed,Is.GreaterThan(7),"Travel must accelerate beyond walking.");
                Assert.That(peakClearance,Is.InRange(.3f,.95f),"Body stays low above real support.");
                Assert.That(peakLean,Is.InRange(10,18.1f));Assert.That(blocked,Is.GreaterThan(2));Assert.That(fall,Is.GreaterThan(35));
                Assert.That(emitted,Is.True,"Backward wake must own actual short burning sources, not purely decorative gas.");
                Assert.That(flightMotor.FireLowFlightQuerySaturations,Is.Zero);
                File.WriteAllText(folder+"/measurements.txt",$"peakSpeed={peakSpeed}\npeakClearance={peakClearance}\npeakLean={peakLean}\nblockedFrames={blocked}\nfallFrames={fall}\npeakMarkerMs={peakNanoseconds/1000000.0}\n");
                flightAbility.CancelAll();flightInput.Move=float2.zero;
                Assert.That(flightMotor.FireLowFlightActive,Is.False);Assert.That(flightAbility.SourceCount,Is.Zero);
                flightMotor.Body.position+=up*4;flightMotor.ResetAfterTeleport();Physics.SyncTransforms();
                flightAbility.SetLowFlightHeld(true);yield return new WaitForFixedUpdate();yield return new WaitForEndOfFrame();
                Assert.That(flightMotor.FireLowFlightActive,Is.False,"Unsupported high-air position cannot acquire ground-flight hover.");
            }
            finally
            {
                recorder.Dispose();Time.captureDeltaTime=saved;
                if(capture!=null)Object.Destroy(capture);if(wall!=null)Object.Destroy(wall);
                if(flightMotor!=null&&original!=null)flightMotor.ConfigureInputSource(original);
                if(flightInput!=null)Object.Destroy(flightInput);safeRival?.Dispose();ReleaseFireAbilityFixture();
            }
        }
    }
}
