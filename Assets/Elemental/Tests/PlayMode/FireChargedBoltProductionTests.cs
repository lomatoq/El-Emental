using System.Collections;
using System.IO;
using Elemental.Simulation.Fire;
using Elemental.Presentation.Fire;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
    public sealed partial class HardPolishFireStreamBindingRuntimeTests
    {
        [UnityTest,Timeout(240000)] public IEnumerator HeldChargeGrowsActualMuzzleAndReleasesSameMassOnce()
        {
            float previousCapture=Time.captureDeltaTime;System.IDisposable rival=null;FireVisualCaptureCamera camera=null;
            string folder="BuildReports/HardPolish/G05/ChargedBolt-"+System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            try
            {
                Time.captureDeltaTime=1f/60;yield return ReadyFireAbilities();
                rival=new FireVisualRivalIsolation(duel.BotTransform,flightMotor);
                var views=All<Elemental.Presentation.Rendering.CelestialSystemBehaviour>();
                camera=views[0].TargetCamera.gameObject.AddComponent<FireVisualCaptureCamera>();
                Vector3 up=flightMotor.LocalUp,side=Vector3.Cross(up,flightMotor.FacingForward).normalized;
                Vector3 aim=flightMotor.Body.position+up*35+flightMotor.FacingForward*30;
                int before=flightAbility.Shots;Assert.That(flightAbility.BeginBoltCharge(aim),Is.True);
                Assert.That(flightAbility.TryGetChargedBoltPose(out _,out _,out float small),Is.True);
                uint id=flightAbility.ChargingBoltId;Directory.CreateDirectory(folder);
                for(int frame=0;frame<120;frame++)
                {
                    yield return null;
                    Assert.That(flightAbility.Shots,Is.EqualTo(before),"Full hold must not auto-fire");
                    if(frame!=5&&frame!=36&&frame!=80&&frame!=119)continue;
                    Assert.That(flightAbility.TryGetChargedBoltPose(out Vector3 center,out _,out _),Is.True);
                    camera.Place(center+side*4+up*1.2f,center);
                    yield return new WaitForEndOfFrame();
                    var texture=ScreenCapture.CaptureScreenshotAsTexture();
                    try{File.WriteAllBytes(folder+"/hold-"+frame+".png",texture.EncodeToPNG());}finally{Object.Destroy(texture);}
                }
                Assert.That(flightAbility.TryGetChargedBoltPose(out Vector3 muzzle,out _,out float large),Is.True);
                Assert.That(large,Is.GreaterThan(small*5));Assert.That(flightAbility.ReleaseBoltCharge(),Is.True);
                Assert.That(flightAbility.Shots,Is.EqualTo(before+1));Assert.That(flightAbility.ReleaseBoltCharge(),Is.False);
                bool found=false;
                for(int i=0;i<flightAbility.ProjectileCapacity;i++)
                {
                    var bolt=flightAbility.GetProjectile(i);if(bolt.Id!=id||!bolt.Active)continue;found=true;
                    Assert.That(bolt.Power,Is.EqualTo(FireChargedBoltProfile.Evaluate(1).Power).Within(.001));
                    Assert.That(Vector3.Distance(bolt.Position,muzzle),Is.LessThan(.02f),"Release must retain the charged world muzzle");
                    Assert.That(FireChargedBoltProfile.FromPower(bolt.Power).VisualRadius,Is.EqualTo(large).Within(.001));
                }
                Assert.That(found,Is.True);
                Vector4 previousShape=default;bool sawShapeChange=false;
                for(int frame=0;frame<8;frame++)
                {
                    yield return new WaitForSeconds(.12f);
                    for(int i=0;i<flightAbility.ProjectileCapacity;i++){var bolt=flightAbility.GetProjectile(i);if(bolt.Id==id&&bolt.Active)camera.Place(bolt.Position+side*5+up,bolt.Position);}
                    yield return new WaitForEndOfFrame();
                    var effects=binding.GetComponent<FireAbilityEffects>();
                    bool active=false;for(int i=0;i<flightAbility.ProjectileCapacity;i++){var bolt=flightAbility.GetProjectile(i);active|=bolt.Id==id&&bolt.Active;}
                    if(active)Assert.That(effects.BoltTrailSpans,Is.GreaterThan(0),"Flying hot core must submit a connected flame trail before its smoke");
                    for(int i=0;i<flightAbility.ProjectileCapacity;i++)
                    {
                        var bolt=flightAbility.GetProjectile(i);if(bolt.Id!=id||!bolt.Active)continue;
                        Assert.That(effects.ProjectileUsesTearDrop(i),Is.True,"Flying core must select the directional body, not the held sphere");
                        Assert.That(effects.ProjectileVolumeRadius(i),Is.LessThanOrEqualTo(.2f*bolt.Power+.0001f));
                        Vector4 shape=effects.ProjectileShapeParameters(i);if(frame>0&&Vector4.Distance(shape,previousShape)>.01f)sawShapeChange=true;previousShape=shape;
                    }

                    File.AppendAllText(folder+"/tail-stats.csv",frame+","+effects.BoltTrailSpans+","+effects.BoltTrailQueries+","+effects.LastStepMilliseconds+"\n");
                    var texture=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(folder+"/flight-"+frame+".png",texture.EncodeToPNG());}finally{Object.Destroy(texture);}
                }
                Assert.That(sawShapeChange,Is.True,"Native flight frames must show evolving body parameters");
                flightAbility.CancelAll();Assert.That(flightAbility.TryGetChargedBoltPose(out _,out _,out _),Is.False);
            }
            finally{if(camera!=null)Object.Destroy(camera);rival?.Dispose();Time.captureDeltaTime=previousCapture;ReleaseFireAbilityFixture();}
        }
    }
}
