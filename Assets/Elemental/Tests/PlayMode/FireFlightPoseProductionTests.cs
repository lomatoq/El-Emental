using System.Collections;
using System.IO;
using System.Reflection;
using Elemental.Presentation.Animation;
using Elemental.Presentation.MotionMatching;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
    public sealed partial class HardPolishFireStreamBindingRuntimeTests
    {
        [UnityTest,Timeout(240000)]
        public IEnumerator ActualHeldFireRingUsesExistingEarthPillarChargePose()
        {
            try
            {
                yield return ReadyFireAbilities();
                Assert.That(flightActor.FirePoseAbilities,Is.SameAs(flightAbility),"Production binding must explicitly connect this saved fighter to its session-owned fire abilities.");
                Assert.That(flightMotor.HasStableSupport,Is.True);
                var driver=flightActor.GetComponent<EarthAnimationDriver>();
                Assert.That(flightAbility.TryRing(),Is.True);
                yield return new WaitForSeconds(.8f);
                yield return new WaitForEndOfFrame();
                Assert.That(flightAbility.IsRingCharging,Is.True,"Ring remains held until explicit release.");
                Assert.That(driver.GetCurrentAnimatorStateInfo(0).fullPathHash,Is.EqualTo(Animator.StringToHash("Base Layer.Pillar Charge")),
                    "Fire ring must reuse the same authored Earth charge state.");
                Assert.That(driver.GetFloat(Animator.StringToHash("PillarCrouch")),Is.InRange(.52f,.72f));
                Assert.That(flightActor.GetComponent<EAMMBasePoseBridge>().AppliedEammMasterWeight,Is.Zero);
                flightAbility.ReleaseRing();
                yield return new WaitForSeconds(.5f);
                Assert.That(flightAbility.IsRingCharging,Is.False);
                Assert.That(driver.GetFloat(Animator.StringToHash("PillarCrouch")),Is.Zero);
            }
            finally{ReleaseFireAbilityFixture();}
        }

        [UnityTest,Timeout(240000)]
        public IEnumerator ActualLateralFireFlightKeepsFallPoseLeansAndClearsAtLandingAndDeath()
        {
            FireFlightMotionInput input=null;MonoBehaviour original=null;
            string folder="BuildReports/HardPolish/G05/FireFlightPose-"+System.DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(folder);
            try
            {
                yield return ReadyFireAbilities();
                original=(MonoBehaviour)typeof(PlanetMotor).GetField("inputSourceBehaviour",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(flightMotor);
                input=flightMotor.gameObject.AddComponent<FireFlightMotionInput>();flightMotor.ConfigureInputSource(input);
                var bridge=flightActor.GetComponent<EAMMBasePoseBridge>();
                var driver=flightActor.GetComponent<EarthAnimationDriver>();
                flightAbility.SetLiftHeld(true);
                yield return new WaitForSeconds(.6f);
                yield return new WaitForEndOfFrame();
                Transform hips=flightActor.Animator.GetBoneTransform(HumanBodyBones.Hips);
                Transform head=flightActor.Animator.GetBoneTransform(HumanBodyBones.Head);
                Vector3 baselineSpine=flightMotor.transform.InverseTransformDirection((head.position-hips.position).normalized);
                float renderedForwardTilt=0;
                input.Move=new float2(0,1);
                float start=Time.time;double deadline=Time.realtimeSinceStartupAsDouble+15;
                int fallingSamples=0;float peakSpeed=0,peakLean=0;
                while(Time.time-start<1.4f&&Time.realtimeSinceStartupAsDouble<deadline)
                {
                    yield return new WaitForEndOfFrame();
                    float speed=Vector3.ProjectOnPlane(flightMotor.Body.linearVelocity,flightMotor.LocalUp).magnitude;
                    peakSpeed=Mathf.Max(peakSpeed,speed);peakLean=Mathf.Max(peakLean,flightActor.FireFlightLeanDegrees);
                    Assert.That(bridge.AppliedEammMasterWeight,Is.Zero,"Moving EAMM legs cannot overwrite fire-flight Fall.");
                    Assert.That(flightActor.FootContactController.LeftFootIkWeight+flightActor.FootContactController.RightFootIkWeight,Is.LessThan(.01f));
                    if(Time.time-start>.3f)
                    {
                        Assert.That(driver.GetCurrentAnimatorStateInfo(0).fullPathHash,Is.EqualTo(Animator.StringToHash("Base Layer.Fall")));
                        Assert.That(flightActor.MotionPhase,Is.EqualTo(EarthAnimationPhase.Falling));fallingSamples++;
                    }
                    if(Time.time-start>1f)
                    {
                        Vector3 spine=flightMotor.transform.InverseTransformDirection((head.position-hips.position).normalized);
                        renderedForwardTilt=Vector3.SignedAngle(baselineSpine,spine,Vector3.right);
                    }
                    Assert.That(Vector3.Angle(flightMotor.transform.up,flightMotor.LocalUp),Is.LessThan(3f),"Visual lean must not tilt the physics root.");
                }
                Assert.That(fallingSamples,Is.GreaterThan(10));Assert.That(peakSpeed,Is.GreaterThan(2));Assert.That(peakLean,Is.InRange(15f,18.01f));
                Assert.That(renderedForwardTilt,Is.InRange(10f,28f),"Actual rendered hips-to-head axis must lean with forward flight, not just report a requested angle.");
                File.WriteAllText(folder+"/evidence.txt",$"speed={peakSpeed}; requestedLean={peakLean}; renderedForwardTilt={renderedForwardTilt}; fallingFrames={fallingSamples}");
                var shot=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(folder+"/lateral-flight.png",shot.EncodeToPNG());Object.Destroy(shot);
                input.Move=float2.zero;flightAbility.SetLiftHeld(false);
                deadline=Time.realtimeSinceStartupAsDouble+20;
                while(!flightMotor.HasStableSupport&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                yield return new WaitForEndOfFrame();Assert.That(flightMotor.HasStableSupport,Is.True,"Actual fall must land before pose-reset acceptance.");
                Assert.That(flightActor.FireFlightLeanDegrees,Is.Zero);Assert.That(flightActor.FireLiftPoseOwned,Is.False);
                flightAbility.SetLiftHeld(true);input.Move=new float2(0,1);yield return new WaitForSeconds(.4f);
                duel.RequestKnockout(EarthDuelFighterId.Player,RagdollHandoff.Uniform(Vector3.zero));
                yield return null;yield return new WaitForEndOfFrame();
                Assert.That(flightActor.FireLiftPoseOwned,Is.False);Assert.That(flightActor.FireFlightLeanDegrees,Is.Zero);
            }
            finally
            {
                if(flightMotor!=null&&original!=null)flightMotor.ConfigureInputSource(original);
                if(input!=null)Object.Destroy(input);
                ReleaseFireAbilityFixture();
            }
        }
    }
    public sealed class FireFlightMotionInput:MonoBehaviour,IPlanetMotorInputSource
    {
        public float2 Move;
        public PlanetMotorCommand SampleCommand(uint tick)=>new PlanetMotorCommand(tick,Move,false);
    }
}
