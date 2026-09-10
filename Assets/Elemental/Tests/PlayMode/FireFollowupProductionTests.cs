using System;
using System.Collections;
using System.IO;
using System.Linq;
using Elemental.Runtime.Fire;
using Elemental.Runtime.Characters;
using Elemental.Presentation.Fire;
using Elemental.Simulation.Fire;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
    public sealed partial class HardPolishFireStreamBindingRuntimeTests
    {
        [UnityTest,Timeout(240000)] public IEnumerator SavedRingChargesBeforeReleasingAndCancelsWithoutPhantomBlast()
        {
            Action<FireAbilityCue> handler=null;FireVisualCaptureCamera ringCamera=null;float captureStep=Time.captureDeltaTime;
            Time.captureDeltaTime=1f/60f; // PNG encoding must not skip the short release phase.
            try
            {
                yield return ReadyFireAbilities();flightLease.Dispose();flightLease=null;int released=0,charged=0;
                var view=All<Elemental.Presentation.Rendering.CelestialSystemBehaviour>().Single().TargetCamera;
                ringCamera=view.gameObject.AddComponent<FireVisualCaptureCamera>();
                ringCamera.Place(flightMotor.Body.position-flightMotor.FacingForward*10+flightMotor.LocalUp*9,flightMotor.Body.position);
                handler=cue=>{if(cue.Kind==FireAbilityEffectKind.Ring)released++;if(cue.Kind==FireAbilityEffectKind.RingWindup)charged++;};flightAbility.Effect+=handler;
                Assert.That(flightAbility.TryRing(),Is.True);Assert.That(flightAbility.IsRingCharging,Is.True);
                yield return new WaitForSeconds(.25f);Assert.That(released,Is.Zero);yield return new WaitForEndOfFrame();SaveFireAbilityFrame("ring-charge");
                yield return new WaitForSeconds(1.8f);Assert.That(released,Is.Zero);Assert.That(flightAbility.RingCharge01,Is.EqualTo(1));
                var look=view.GetComponent<Elemental.Presentation.VFX.EarthChargeCameraLookdevV2>();
                Assert.That(look.SampleChargeSources().FireRing,Is.EqualTo(1));
                Assert.That(look.ChargeFeedback.FieldOfViewDelta,Is.GreaterThan(8));
                Assert.That(look.ChargeFeedback.ChromaticAberration,Is.GreaterThan(.2f));
                Assert.That(look.ChargeVignetteIntensity,Is.GreaterThan(.3f));
                yield return new WaitForEndOfFrame();SaveFireAbilityFrame("ring-held-full-charge");
                flightAbility.ReleaseRing();yield return null;Assert.That(released,Is.EqualTo(1));Assert.That(charged,Is.EqualTo(1));Assert.That(flightAbility.IsRingCharging,Is.False);
                yield return new WaitForEndOfFrame();SaveFireAbilityFrame("ring-dense-release");
                Assert.That(binding.GetComponent<FireAbilityEffects>().RingSectors,Is.EqualTo(24));
                yield return new WaitForSeconds(.33f);yield return new WaitForEndOfFrame();SaveFireAbilityFrame("ring-late-coverage");
                yield return new WaitForSeconds(1f);Assert.That(flightAbility.TryRing(),Is.True);flightAbility.CancelAll();yield return new WaitForSeconds(.7f);Assert.That(released,Is.EqualTo(1));
            }
            finally{Time.captureDeltaTime=captureStep;if(ringCamera!=null)UnityEngine.Object.Destroy(ringCamera);if(handler!=null&&flightAbility!=null)flightAbility.Effect-=handler;ReleaseFireAbilityFixture();}
        }
        [UnityTest,Timeout(240000)] public IEnumerator SavedLivingOpponentReceivesPhysicalFireShove()
        {
            try
            {
                yield return ReadyFireAbilities();var opponent=duel.BotTransform.GetComponentInChildren<PlanetMotor>(true);
                Assert.That(opponent,Is.Not.Null);var before=opponent.Body.linearVelocity;
                Vector3 direction=Vector3.ProjectOnPlane(duel.BotTransform.position-duel.PlayerTransform.position,opponent.LocalUp).normalized;
                if(direction.sqrMagnitude<.5f)direction=opponent.FacingForward;
                opponent.ApplyFireImpulse(direction*7+opponent.LocalUp*2);
                yield return new WaitForFixedUpdate();yield return null;
                Assert.That(Vector3.Dot(opponent.Body.linearVelocity-before,direction),Is.GreaterThan(2));Assert.That(opponent.HasDirectedExternalMotion,Is.True);
            }
            finally{ReleaseFireAbilityFixture();}
        }
        [UnityTest,Timeout(240000)] public IEnumerator ProductionSmolderCharsEmitsSmokeAndCools()
        {
            GameObject surface=null;FireWorldImpact response=null;FireVisualCaptureCamera capture=null;float originalScale=Time.timeScale;
            try
            {
                yield return ReadyFireAbilities();response=binding.PlayerSession.GetComponent<FireWorldImpact>();
                surface=GameObject.CreatePrimitive(PrimitiveType.Cube);surface.name="Production smolder receiver";surface.transform.position=duel.PlayerTransform.position+flightMotor.LocalUp*8;
                var renderer=surface.GetComponent<MeshRenderer>();
#if UNITY_EDITOR
                renderer.sharedMaterial=UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Elemental/Content/GraphicsV5/Materials/RumbleSandstone.mat");
#endif
                var collider=surface.GetComponent<Collider>();var properties=new MaterialPropertyBlock();
                var camera=All<Elemental.Presentation.Rendering.CelestialSystemBehaviour>().Single().TargetCamera;
                capture=camera.gameObject.AddComponent<FireVisualCaptureCamera>();capture.Place(surface.transform.position+new Vector3(2f,1f,3.5f),surface.transform.position);
                yield return new WaitForEndOfFrame();SaveFireAbilityFrame("smolder-clean");
                for(int i=0;i<80;i++){Physics.SyncTransforms();response.ApplyContact(collider,collider.ClosestPoint(surface.transform.position+Vector3.forward*2),Vector3.forward,Vector3.back,.05f,1);yield return new WaitForSeconds(.05f);}
                yield return new WaitForEndOfFrame();renderer.GetPropertyBlock(properties);
                Assert.That(properties.GetFloat("_FireChar"),Is.GreaterThan(.7f));Assert.That(properties.GetFloat("_FireHeat"),Is.GreaterThan(.8f));
                Assert.That(binding.GetComponent<FireSmolderPresentation>().ActiveIgnitions,Is.GreaterThan(0),"Continuous heat keeps attached flame alive beyond its first three seconds.");
                SaveFireAbilityFrame("smolder-hot-char-smoke");
                var smoke=binding.GetComponent<FireSmolderPresentation>().GetComponentsInChildren<ParticleSystem>().Single(p=>p.name=="Bounded smolder smoke");Assert.That(smoke.particleCount,Is.GreaterThan(0));
                Time.timeScale=0;var smokeRenderer=smoke.GetComponent<ParticleSystemRenderer>();
                yield return new WaitForEndOfFrame();var on=ScreenCapture.CaptureScreenshotAsTexture();
                smokeRenderer.enabled=false;yield return new WaitForEndOfFrame();var off=ScreenCapture.CaptureScreenshotAsTexture();smokeRenderer.enabled=true;
                try{var a=on.GetPixels32();var b=off.GetPixels32();int changed=0;
                    for(int pixel=0;pixel<a.Length;pixel++)if(Mathf.Abs(a[pixel].r-b[pixel].r)+Mathf.Abs(a[pixel].g-b[pixel].g)+Mathf.Abs(a[pixel].b-b[pixel].b)>3)changed++;
                    Directory.CreateDirectory("BuildReports/HardPolish/Smolder");File.WriteAllText("BuildReports/HardPolish/Smolder/visible-smoke.txt","Smoke on/off affected pixels="+changed);
                    Assert.That(changed,Is.GreaterThan(100),"Emitted particles must actually render visible smoke");
                }finally{UnityEngine.Object.Destroy(on);UnityEngine.Object.Destroy(off);Time.timeScale=originalScale;}
                yield return new WaitForSeconds(8f);yield return new WaitForEndOfFrame();renderer.GetPropertyBlock(properties);
                Assert.That(properties.GetFloat("_FireHeat"),Is.Zero);Assert.That(properties.GetFloat("_FireChar"),Is.Zero,"Char must smoothly clear within the new short recovery window.");
                SaveFireAbilityFrame("smolder-cooled-char");
            }
            finally{Time.timeScale=originalScale;if(capture!=null)UnityEngine.Object.Destroy(capture);if(surface!=null)UnityEngine.Object.Destroy(surface);ReleaseFireAbilityFixture();}
        }
    }
}
