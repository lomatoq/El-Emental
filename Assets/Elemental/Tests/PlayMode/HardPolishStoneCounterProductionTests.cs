#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elemental.Input.Gestures;
using Elemental.Presentation.Animation;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.UI;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        [Serializable] private sealed class CounterFrame
        { public int pass; public float elapsed,handTravel; public uint sequence,presented; public Vector3 actorPosition,stonePosition,stoneVelocity; public bool stoneActive; public string rejection; }
        [Serializable] private sealed class CounterEventCapture
        {public uint sequence,sourceId,generation;public string tier;public float time,mass,radius;}
        [Serializable] private sealed class CounterCapture
        { public string scope="Actual saved Linebreaker, real incoming pooled stone and runtime guard/punch while idle or moving. Hold is injected at semantic guard adapter; keyboard chord is independently tested. Existing animation fixture suppresses actor damage, so this does not establish damage immunity or arbitrary crowd performance.";
          public Vector3 ingress,selectedHeading,finalHeading,initialShapePosition,initialBodyPosition;
          public Bounds initialPhysicsBounds,finalPhysicsBounds; public string clearance;
          public uint primarySourceId,primaryGeneration;public List<CounterEventCapture> counterEvents=new(); public List<CounterFrame> frames=new(); }
        [UnityTest]
        public IEnumerator ActualLinebreakerMovingCounterPunchUsesRealStonePartitions()
        { yield return CaptureActualCounter(0); }
        [UnityTest] public IEnumerator ActualLinebreakerMovingCounterPunchMediumUsesRealStonePartitions()
        { yield return CaptureActualCounter(1); }
        [UnityTest] public IEnumerator ActualLinebreakerMovingCounterPunchLargeUsesRealStonePartitions()
        { yield return CaptureActualCounter(2); }
        private IEnumerator CaptureActualCounter(int pass)
        {
            // Separate Unity tests provide fresh authored spawn, terrain and pooled
            // matter per size; earlier movement/fragments cannot obstruct the next.
            string folder="BuildReports/HardPolish/StoneCounter/Production/size-"+pass;
            Directory.CreateDirectory(folder);
            var flow=_scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FrontendFlowController>(true)).Single();
            var actor=_actors.Single(a=>a.Presentation.transform.IsChildOf(flow.MatchController.PlayerTransform));
            var input=flow.MatchController.PlayerTransform.GetComponentsInChildren<MagicInputController>(true).Single();
            var body=input.GetComponent<Rigidbody>(); var motor=input.GetComponent<PlanetMotor>();
            var pool=input.EarthExecutor.FragmentPool;
            var guard=input.GetComponent<EarthStoneCounterGuard>()??input.gameObject.AddComponent<EarthStoneCounterGuard>();
            guard.Configure(body,motor,pool.DebrisPool,input.GetComponent<ActiveRagdollPuppet>());
            var presenter=input.GetComponent<EarthStoneCounterPresenter>()??input.gameObject.AddComponent<EarthStoneCounterPresenter>();
            presenter.Configure(guard,actor.Presentation.GetComponent<EarthCharacterPoseController>());
            var camera=_scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CelestialSystemBehaviour>(true)).Single().TargetCamera;
            var framing=camera.gameObject.AddComponent<FireVisualCaptureCamera>();
            var hand=actor.Presentation.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.RightHand);
            var report=new CounterCapture();var resolution=new ProductionCaptureResolution();
            Action<EarthStoneCounterImpact> recordImpact=impact=>report.counterEvents.Add(new CounterEventCapture{
                sequence=impact.Sequence,sourceId=impact.Source.StableId,generation=impact.Source.Generation,
                tier=impact.Tier.ToString(),time=Time.time,mass=impact.SourceMass,radius=impact.Radius});
            guard.Countered+=recordImpact;
            float oldCapture=Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime=0; yield return resolution.WaitForRenderedSize(camera);
                float[] sizes={.25f,.7f,1.35f};
                {
                    actor.Input.Move=float2.zero;
                    yield return new WaitForSeconds(.25f);
                    Vector3 up=motor.LocalUp,forward=Vector3.ProjectOnPlane(actor.Presentation.transform.forward,up).normalized;
                    Vector3 center=body.worldCenterOfMass;
                    Vector3 right=Vector3.Cross(up,forward);
                    framing.Place(center+forward*5+right*3+up*.65f,center+forward*.4f);
                    guard.SetHeld(false,forward);
                    float mass=pool.ResolveNewStoneMass((4f/3f)*Mathf.PI*sizes[pass]*sizes[pass]*sizes[pass]);
                    var stone=pool.Acquire(null,center+forward*(3.1f+sizes[pass]),sizes[pass],mass);
                    Assert.That(stone,Is.Not.Null);
                    report.primarySourceId=stone.TargetHandle.StableId;report.primaryGeneration=stone.TargetHandle.Generation;
                    // Keep only the fixture's not-yet-launched stone stationary
                    // above the arena while finding a clear, real combat heading.
                    stone.Body.isKinematic=true;
                    stone.Body.position=center+up*20f;
                    Physics.SyncTransforms();
                    var shape=stone.GetComponent<Collider>();
                    // Rigidbody interpolation can leave Transform and cached
                    // Collider.bounds at different poses immediately after staging.
                    double shapeDeadline=Time.realtimeSinceStartupAsDouble+3d;
                    yield return _frame;
                    while(!CounterShapeReady(shape,stone.Body)&&Time.realtimeSinceStartupAsDouble<shapeDeadline)
                    {Physics.SyncTransforms();yield return _frame;}
                    Assert.That(CounterShapeReady(shape,stone.Body),Is.True,
                        "Incoming collider did not become ready: "+shape.bounds+" transform="+shape.transform.position+" body="+stone.Body.position);
                    report.initialPhysicsBounds=shape.bounds;report.initialShapePosition=shape.transform.position;
                    report.initialBodyPosition=stone.Body.position;
                    Vector3 heading=forward;bool found=false;var blockedHeadings=new List<string>();
                    foreach(float degrees in new[]{0f,45f,-45f,90f,-90f,135f,-135f,180f})
                    {
                        Vector3 candidate=Quaternion.AngleAxis(degrees,up)*forward;
                        bool movementClear=true;string reason="";
                        // The moving pass advances before launch. Choose a heading
                        // clear throughout that preparation segment, not just at rest.
                        foreach(float advance in pass==0?new[]{0f}:new[]{0f,.2f,.4f,.6f})
                        {
                            if(!TryCounterIngress(shape,body,center+candidate*advance,candidate,up,sizes[pass],out _,out reason))
                            {movementClear=false;reason="advance="+advance+": "+reason;break;}
                        }
                        if(movementClear){heading=candidate;found=true;break;}
                        blockedHeadings.Add(degrees+" degrees: "+reason);
                    }
                    Assert.That(found,Is.True,"No clear heading from actual support: "+string.Join("; ",blockedHeadings));
                    report.selectedHeading=heading;
                    double turnDeadline=Time.realtimeSinceStartupAsDouble+8d;
                    while(Mathf.Abs(Vector3.SignedAngle(motor.FacingForward,heading,motor.LocalUp))>3f &&
                        Time.realtimeSinceStartupAsDouble<turnDeadline)
                    {
                        float angle=Vector3.SignedAngle(motor.FacingForward,heading,motor.LocalUp);
                        actor.Input.Move=new float2(Mathf.Clamp(angle/60f,-.5f,.5f),0f);
                        yield return _frame;
                    }
                    actor.Input.Move=float2.zero;
                    Assert.That(Mathf.Abs(Vector3.SignedAngle(motor.FacingForward,heading,motor.LocalUp)),Is.LessThanOrEqualTo(3f),
                        "Actual tank-turn input failed to face the clear ingress heading.");
                    actor.Input.Move=pass==0?float2.zero:new float2(0,.25f);
                    yield return new WaitForSeconds(.25f);
                    Assert.That(motor.HasStableSupport,Is.True,"Counter actor lost its actual support while positioning.");
                    up=motor.LocalUp;forward=Vector3.ProjectOnPlane(motor.FacingForward,up).normalized;
                    center=body.worldCenterOfMass;right=Vector3.Cross(up,forward);
                    framing.Place(center+forward*5+right*3+up*.65f,center+forward*.4f);
                    Physics.SyncTransforms();
                    report.finalHeading=forward;report.finalPhysicsBounds=shape.bounds;
                    bool finalClear=TryCounterIngress(shape,body,center,forward,up,sizes[pass],out Vector3 ingress,out string clearance);
                    report.clearance=clearance;
                    Assert.That(finalClear,Is.True,"No unobstructed real incoming path; chosen="+heading+" actual="+forward+
                        " shapeBounds="+shape.bounds+": "+clearance);
                    report.ingress=ingress;report.clearance=clearance;
                    stone.Body.position=ingress;
                    stone.Body.isKinematic=false;
                    stone.Body.linearVelocity=-forward*10f;
                    Physics.SyncTransforms();
                    Vector3 firstHand=actor.Presentation.transform.InverseTransformPoint(hand.position),startRoot=body.position;
                    uint initial=guard.Sequence;
                    guard.SetHeld(true,forward);
                    float start=Time.time,nextFrame=0,maxHand=0;int image=0;
                    while(Time.time-start<1.2f)
                    {
                        yield return _frame;
                        float travel=Vector3.Distance(firstHand,actor.Presentation.transform.InverseTransformPoint(hand.position));
                        maxHand=Mathf.Max(maxHand,travel);
                        report.frames.Add(new CounterFrame{pass=pass,elapsed=Time.time-start,handTravel=travel,
                            sequence=guard.Sequence,presented=presenter.PresentedSequence,actorPosition=body.position,stonePosition=stone.Body.position,
                            stoneVelocity=stone.Body.linearVelocity,stoneActive=stone.gameObject.activeSelf,rejection=guard.LastRejection});
                        if(Time.time-start>=nextFrame&&image<24)
                        {ProductionCaptureResolution.SaveScreen(folder+"/pass-"+pass+"-"+(image++).ToString("D2")+".png");nextFrame+=.05f;}
                    }
                    Assert.That(guard.Sequence,Is.EqualTo(initial+1),guard.LastRejection);
                    Assert.That(presenter.PresentedSequence,Is.EqualTo(guard.Sequence));
                    Assert.That(stone.gameObject.activeSelf,Is.False,"Incoming stone was not partitioned/archived.");
                    Assert.That(maxHand,Is.GreaterThan(.04f),"No visible hand motion reached the saved actor.");
                    if(pass==1)Assert.That(Vector3.Distance(startRoot,body.position),Is.GreaterThan(.08f),"Counter stopped ordinary movement.");
                    guard.SetHeld(false,forward);
                    yield return new WaitForSeconds(.4f);
                }
            }
            finally
            {
                guard.Countered-=recordImpact;
                guard.SetHeld(false,Vector3.forward);actor.Input.Move=float2.zero;
                Time.captureDeltaTime=oldCapture;resolution.Dispose();UnityEngine.Object.Destroy(framing);
                File.WriteAllText(folder+"/CounterCapture.json",JsonUtility.ToJson(report,true));
            }
        }
        private static bool CounterShapeReady(Collider shape,Rigidbody body)
        {
            return shape is MeshCollider mesh && mesh.enabled && mesh.gameObject.activeInHierarchy && mesh.convex &&
                mesh.sharedMesh!=null && mesh.sharedMesh.vertexCount>3 && mesh.bounds.extents.sqrMagnitude>.0001f &&
                Vector3.Distance(shape.transform.position,body.position)<.01f;
        }
        private static Bounds CounterShapeBoundsAt(Collider shape,Vector3 position)
        {
            Assert.That(shape,Is.InstanceOf<MeshCollider>(),"Production fragment collision must remain its real convex mesh.");
            var mesh=(MeshCollider)shape;Assert.That(mesh.sharedMesh,Is.Not.Null);
            Bounds local=mesh.sharedMesh.bounds;Matrix4x4 matrix=shape.transform.localToWorldMatrix;
            Vector3 x=matrix.MultiplyVector(new Vector3(local.extents.x,0,0));
            Vector3 y=matrix.MultiplyVector(new Vector3(0,local.extents.y,0));
            Vector3 z=matrix.MultiplyVector(new Vector3(0,0,local.extents.z));
            Vector3 extent=new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),
                Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z));
            return new Bounds(position+matrix.MultiplyVector(local.center),extent*2f);
        }
        private static bool TryCounterIngress(Collider shape,Rigidbody defender,Vector3 center,Vector3 forward,
            Vector3 up,float radius,out Vector3 ingress,out string diagnostic)
        {
            var blocked=new List<string>();
            float near=Elemental.Simulation.Combat.EarthStoneCounterPolicy.Reach+radius+.3f;
            float far=3.1f+radius;
            // Test the real convex collider along the approach until the guard's
            // physical sweep can intercept it. Never remove cover or ignore it.
            foreach(float lift in new[]{0f,.25f,.5f,.75f})
            for(int attempt=0;attempt<5;attempt++)
            {
                float distance=Mathf.Lerp(far,near,attempt/4f);
                Vector3 start=center+forward*distance+up*lift;
                bool clear=true;
                for(int sample=0;sample<5&&clear;sample++)
                {
                    Vector3 position=Vector3.Lerp(start,center+forward*(near-.25f)+up*lift,sample/4f);
                    // Build the exact candidate pose's conservative AABB from
                    // collision geometry. Cached physics bounds may describe a
                    // different interpolated pose than the Transform this frame.
                    Bounds candidateBounds=CounterShapeBoundsAt(shape,position);
                    var candidates=Physics.OverlapBox(candidateBounds.center,candidateBounds.extents,
                        Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
                    foreach(var other in candidates)
                    {
                        if(other==shape||other.attachedRigidbody==defender||other.transform.IsChildOf(defender.transform))continue;
                        if(Physics.ComputePenetration(shape,position,shape.transform.rotation,other,other.transform.position,
                            other.transform.rotation,out _,out float depth)&&depth>.001f)
                        {
                            blocked.Add("lift="+lift+" distance="+distance+" sample="+sample+" blocker="+other.name+" penetration="+depth);
                            clear=false;break;
                        }
                    }
                }
                if(clear){ingress=start;diagnostic="Clear actual collider approach; prior rejections: "+string.Join("; ",blocked);return true;}
            }
            ingress=default;diagnostic=string.Join("; ",blocked);return false;
        }
    }
}
#endif
