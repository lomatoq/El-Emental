using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Elemental.Presentation.Animation;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    // The existing September fixture supplies readiness, real actors and teardown.
    // Change its declaration to partial when importing this staged test.
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        [UnityTest]
        public IEnumerator FinalHumanoidFeetTraverseRealPitHumpAndSlopeAtControlledThirtySixtyOneTwentySteps()
        {
            int oldVsync=QualitySettings.vSyncCount,oldFps=Application.targetFrameRate;
            float oldCapture=Time.captureDeltaTime,oldFixed=Time.fixedDeltaTime;
            var report=new SurfaceReport { utc=DateTime.UtcNow.ToString("O") };
            _activeSurfaceReport=report;
            _surfaceOldVsync=oldVsync;_surfaceOldFps=oldFps;
            _surfaceOldCapture=oldCapture;_surfaceOldFixed=oldFixed;_surfaceClockOwned=true;
            try
            {
                QualitySettings.vSyncCount=0;
                Time.fixedDeltaTime=1f/60f;
                foreach(int rate in new[] { 30,60,120 })
                {
                    Application.targetFrameRate=rate;
                    Time.captureDeltaTime=1f/rate;
                    foreach(Actor actor in _actors) yield return RunSurfaceTrack(actor,rate,report);
                }
                Assert.That(report.runs.Count,Is.EqualTo(_actors.Count*3));
                foreach(SurfaceRun run in report.runs)
                    Assert.That(run.completed,Is.True,
                        $"Incomplete irregular-surface matrix entry: {run.actor}/{run.requestedStepHz}.");
            }
            finally
            {
                Time.captureDeltaTime=oldCapture; Time.fixedDeltaTime=oldFixed;
                Application.targetFrameRate=oldFps; QualitySettings.vSyncCount=oldVsync;
                Directory.CreateDirectory("BuildReports/SeptemberAnimation");
                File.WriteAllText("BuildReports/SeptemberAnimation/ActualSurfaceControlledSteps.json",JsonUtility.ToJson(report,true));
                _surfaceClockOwned=false;
            }
        }

        [TearDown]
        public void PersistSurfaceFailureAndRestoreClock()
        {
            // A failed yielded child IEnumerator is not guaranteed to execute its
            // finally block. Keep global timing and evidence cleanup independent.
            if(_surfaceClockOwned)
            {
                Time.captureDeltaTime=_surfaceOldCapture;Time.fixedDeltaTime=_surfaceOldFixed;
                Application.targetFrameRate=_surfaceOldFps;QualitySettings.vSyncCount=_surfaceOldVsync;
                _surfaceClockOwned=false;
            }
            foreach(UnityEngine.Object fixture in _surfaceFixtureObjects)
                if(fixture!=null)UnityEngine.Object.DestroyImmediate(fixture);
            _surfaceFixtureObjects.Clear();
            if(_activeSurfaceReport==null)return;
            Directory.CreateDirectory("BuildReports/SeptemberAnimation");
            File.WriteAllText("BuildReports/SeptemberAnimation/ActualSurfaceControlledSteps.json",JsonUtility.ToJson(_activeSurfaceReport,true));
            _activeSurfaceReport=null;
        }

        private SurfaceReport _activeSurfaceReport;
        private bool _surfaceClockOwned;
        private int _surfaceOldVsync,_surfaceOldFps;
        private float _surfaceOldCapture,_surfaceOldFixed;
        private readonly List<UnityEngine.Object> _surfaceFixtureObjects=new();

        private IEnumerator RunSurfaceTrack(Actor actor,int requestedHz,SurfaceReport report)
        {
            PlanetMotor motor=actor.Presentation.GetComponentInParent<PlanetMotor>();
            EarthFootContactController feet=actor.Presentation.FootContactController;
            Rigidbody body=motor.Body;
            Vector3 originalPosition=body.position;
            Quaternion originalRotation=body.rotation;
            Vector3 up=motor.LocalUp.normalized;
            Vector3 forward=Vector3.ProjectOnPlane(motor.FacingForward,up).normalized;
            // Only fixture setup repositions the real actor. The entire measured
            // traverse uses normal motor input and unmodified Animator/EAMM/IK.
            Vector3 origin=motor.SupportFeetPoint(up)+up*8f;
            GameObject track=new GameObject("Actual collider foot acceptance track");
            SceneManager.MoveGameObjectToScene(track,_scene);
            track.transform.SetPositionAndRotation(origin,Quaternion.LookRotation(forward,up));
            for(int layer=0;layer<32;layer++)
                if((motor.GroundMask.value&(1<<layer))!=0) { track.layer=layer; break; }
            Mesh mesh=CreateSurfaceTrackMesh();
            MeshCollider collider=track.AddComponent<MeshCollider>(); collider.sharedMesh=mesh;
            var run=new SurfaceRun { actor=actor.Presentation.name,requestedStepHz=requestedHz };
            run.soleOffset=(float)typeof(EarthFootContactController).GetField("soleOffset",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(feet);
            report.runs.Add(run);
            _surfaceFixtureObjects.Add(track);_surfaceFixtureObjects.Add(mesh);
            try
            {
                actor.Input.Move=float2.zero;
                motor.CancelMantle();
                body.linearVelocity=Vector3.zero; body.angularVelocity=Vector3.zero;
                body.position=originalPosition+up*(8f+SurfaceHeight(0f,-2.2f))-forward*2.2f;
                feet.InvalidateBasePose();
                UnityEngine.Physics.SyncTransforms();
                yield return new WaitForSeconds(1.2f);
                Assert.That(motor.HasStableSupport,Is.True,"Actual track fixture was not grounded.");
                run.leftRestClearance=RestClearance(feet.LeftActualFootWorld,collider,motor.LocalUp);
                run.rightRestClearance=RestClearance(feet.RightActualFootWorld,collider,motor.LocalUp);
                double previousWall=Time.realtimeSinceStartupAsDouble;
                float until=Time.time+7f;
                bool approachingPit=false,stoppingInPit=false;
                actor.Input.Move=new float2(0f,.48f);
                while(Time.time<until)
                {
                    yield return _frame;
                    double wall=Time.realtimeSinceStartupAsDouble;
                    float z=track.transform.InverseTransformPoint(body.position).z;
                    var row=new SurfaceFrame { actor=run.actor,requestedStepHz=requestedHz,frame=Time.frameCount,
                        delta=Time.deltaTime,wallDelta=(float)(wall-previousWall),trackZ=z,
                        contactFrame=feet.LastContactEvaluationFrame,leftWeight=feet.LeftFootIkWeight,
                        rightWeight=feet.RightFootIkWeight,leftLocked=feet.LeftFootLocked,rightLocked=feet.RightFootLocked,
                        leftReason=(int)feet.LeftReason,rightReason=(int)feet.RightReason,
                        leftRawTargetLag=RawTargetLag(feet,true,up,run.soleOffset),
                        rightRawTargetLag=RawTargetLag(feet,false,up,run.soleOffset),
                        leftSupportKind=(int)feet.LeftSupportKind,rightSupportKind=(int)feet.RightSupportKind,
                        leftSupportId=feet.LeftSupportId,rightSupportId=feet.RightSupportId,
                        leftSupportCollider=SupportColliderName(feet,true),
                        rightSupportCollider=SupportColliderName(feet,false),
                        leftRawNormalUpDot=Vector3.Dot(feet.LeftRawContactNormalWorld,up),
                        rightRawNormalUpDot=Vector3.Dot(feet.RightRawContactNormalWorld,up),
                        pelvisOffset=feet.PelvisOffsetMeters,
                        leftPelvisRequest=feet.LeftPelvisRequestMeters,
                        rightPelvisRequest=feet.RightPelvisRequestMeters,
                        pelvisTarget=feet.PelvisTargetMeters };
                    if(run.hasPreviousTargets)
                    {
                        row.leftTargetStep=Vector3.Distance(feet.LeftTargetWorld,run.previousLeftTarget);
                        row.rightTargetStep=Vector3.Distance(feet.RightTargetWorld,run.previousRightTarget);
                    }
                    run.previousLeftTarget=feet.LeftTargetWorld;
                    run.previousRightTarget=feet.RightTargetWorld;
                    run.hasPreviousTargets=true;
                    row.leftRawTrackGap=RawTargetTrackGap(
                        feet,true,collider,up,run.soleOffset,out row.leftRawOnTrack);
                    row.rightRawTrackGap=RawTargetTrackGap(
                        feet,false,collider,up,run.soleOffset,out row.rightRawOnTrack);
                    previousWall=wall;
                    run.observedDeltaSum+=row.delta; run.wallDeltaSum+=row.wallDelta; run.frames++;
                    report.frames.Add(row);
                    Assert.That(row.contactFrame,Is.EqualTo(row.frame),"Contact capture used an earlier rendered frame.");
                    Assert.That(row.delta,Is.EqualTo(1f/requestedHz).Within(.0002f),"Controlled animation step was not applied.");
                    Assert.That(actor.Probe.Latest.headHeight,Is.GreaterThan(.25f));
                    EvaluateSurfaceFoot(feet,true,collider,motor.LocalUp,row,run);
                    EvaluateSurfaceFoot(feet,false,collider,motor.LocalUp,row,run);
                    run.sawHump|=z>-.95f&&z<-.40f;
                    run.sawPit|=z>-.20f&&z<.45f;
                    run.sawSlope|=z>.55f&&z<1.25f;
                    if(!approachingPit&&!run.pitStopCompleted&&z>=-1f)
                    { approachingPit=true;actor.Input.Move=new float2(0f,.12f); }
                    if(!stoppingInPit&&!run.pitStopCompleted&&z>=-.25f)
                    {
                        stoppingInPit=true;run.pitStopBegan=Time.time;
                        actor.Input.Move=float2.zero;
                    }
                    if(stoppingInPit&&Time.time-run.pitStopBegan>.55f)
                    {
                        EvaluatePitStop(feet,collider,motor.LocalUp,run);
                        actor.Input.Move=new float2(0f,.48f);
                        approachingPit=false;stoppingInPit=false;run.pitStopCompleted=true;
                    }
                    if(run.pitStopCompleted&&z>=1.4f) break;
                }
                actor.Input.Move=float2.zero;
                yield return new WaitForSeconds(.6f);
                Assert.That(run.sawHump&&run.sawPit&&run.sawSlope,Is.True,"The real motor did not cross the complete irregular track.");
                Assert.That(run.plantedSamples,Is.GreaterThan(8),"No meaningful final planted-foot measurements.");
                Assert.That(run.swingSamples,Is.GreaterThan(4),"Walking never released the authored swing foot.");
                Assert.That(run.pitContactSamples,Is.GreaterThan(0),"Neither final foot followed the actual asymmetric pit during the stopped stance.");
                Assert.That(run.highestSwingClearance,Is.GreaterThan(.025f),"Swing feet were flattened onto the support.");
                Assert.That(motor.HasStableSupport,Is.True,"Stop lost destination support.");
                run.completed=true;
            }
            finally
            {
                actor.Input.Move=float2.zero;
                body.position=originalPosition; body.rotation=originalRotation;
                body.linearVelocity=Vector3.zero; body.angularVelocity=Vector3.zero;
                feet.InvalidateBasePose();
                UnityEngine.Object.Destroy(track); UnityEngine.Object.Destroy(mesh);
                _surfaceFixtureObjects.Remove(track);_surfaceFixtureObjects.Remove(mesh);
                UnityEngine.Physics.SyncTransforms();
            }
            yield return null;
        }

        private static void EvaluateSurfaceFoot(EarthFootContactController feet,bool left,MeshCollider surface,
            Vector3 up,SurfaceFrame row,SurfaceRun run)
        {
            Vector3 actual=left?feet.LeftActualFootWorld:feet.RightActualFootWorld;
            Vector3 target=left?feet.LeftTargetWorld:feet.RightTargetWorld;
            Vector3 normal=left?feet.LeftFilteredContactNormalWorld:feet.RightFilteredContactNormalWorld;
            float weight=left?feet.LeftFootIkWeight:feet.RightFootIkWeight;
            EarthFootContactReason reason=left?feet.LeftReason:feet.RightReason;
            // A released authored swing can lift more than 0.8 m on the XBot
            // stride. This observation ray must still reach the fixture; it is
            // not the production foot probe and does not grant IK ownership.
            bool onTrack=surface.Raycast(new Ray(actual+up*1f,-up),out RaycastHit hit,3f);
            Assert.That(onTrack,Is.True,"Final ankle is outside the actual terrain fixture.");
            float gap=Vector3.Dot(actual-target,normal);
            float drift=Vector3.ProjectOnPlane(actual-target,normal).magnitude;
            float clearance=Vector3.Dot(actual-hit.point,up);
            if(left) { row.leftGap=gap;row.leftDrift=drift;row.leftClearance=clearance; }
            else { row.rightGap=gap;row.rightDrift=drift;row.rightClearance=clearance; }
            if(weight>.9f&&reason==EarthFootContactReason.Stance)
            {
                run.plantedSamples++;
                run.maxDrift=Mathf.Max(run.maxDrift,drift);
                run.maxAbsoluteGap=Mathf.Max(run.maxAbsoluteGap,Mathf.Abs(gap));
                Assert.That(EarthAnimationContactAcceptance.IsPlantedGapAccepted(gap),Is.True,
                    $"{run.actor}/{run.requestedStepHz}: {(left?"left":"right")} final planted gap={gap:F4}m.");
                Assert.That(drift,Is.LessThanOrEqualTo(EarthAnimationContactAcceptance.MaximumPlantedDriftMeters),
                    $"{run.actor}/{run.requestedStepHz}: final stance drift.");
            }
            if(reason==EarthFootContactReason.Swing)
            {
                run.swingSamples++;
                Assert.That(weight,Is.LessThanOrEqualTo(.001f),"Swing phase retained a planted IK goal.");
                // Compare ankle clearance above its measured grounded offset,
                // not the released goal (which follows the animated swing).
                run.highestSwingClearance=Mathf.Max(run.highestSwingClearance,
                    clearance-(left?run.leftRestClearance:run.rightRestClearance));
            }
        }

        private static void EvaluatePitStop(EarthFootContactController feet,MeshCollider surface,Vector3 up,SurfaceRun run)
        {
            foreach(bool left in new[] { true,false })
            {
                Vector3 actual=left?feet.LeftActualFootWorld:feet.RightActualFootWorld;
                float weight=left?feet.LeftFootIkWeight:feet.RightFootIkWeight;
                EarthFootContactReason reason=left?feet.LeftReason:feet.RightReason;
                if(weight<.9f||reason!=EarthFootContactReason.Stance)continue;
                if(!surface.Raycast(new Ray(actual+up*.8f,-up),out RaycastHit hit,1.6f))continue;
                Vector3 local=surface.transform.InverseTransformPoint(hit.point);
                float pitDepth=.13f*Mathf.Exp(-Mathf.Pow((local.z-.10f)/.30f,2f))*
                    Mathf.Exp(-Mathf.Pow((local.x+.14f)/.42f,2f));
                if(pitDepth<.035f)continue;
                float surfaceGap=Vector3.Dot(actual-hit.point,hit.normal.normalized)-run.soleOffset;
                Assert.That(EarthAnimationContactAcceptance.IsPlantedGapAccepted(surfaceGap),Is.True,
                    $"{run.actor}/{run.requestedStepHz}: stopped foot does not follow actual pit, gap={surfaceGap:F4}m.");
                run.pitContactSamples++;run.deepestPitSample=Mathf.Max(run.deepestPitSample,pitDepth);
            }
        }

        private static float RestClearance(Vector3 ankle,MeshCollider surface,Vector3 up)
        {
            Assert.That(surface.Raycast(new Ray(ankle+up*.8f,-up),out RaycastHit hit,1.6f),Is.True);
            return Vector3.Dot(ankle-hit.point,up);
        }

        private static float RawTargetLag(
            EarthFootContactController feet,bool left,Vector3 up,float soleOffset)
        {
            Vector3 rawPoint=left?feet.LeftRawContactPointWorld:feet.RightRawContactPointWorld;
            Vector3 rawNormal=left?feet.LeftRawContactNormalWorld:feet.RightRawContactNormalWorld;
            Vector3 filtered=left?feet.LeftTargetWorld:feet.RightTargetWorld;
            Vector3 rawTarget=rawPoint+rawNormal.normalized*soleOffset;
            return Vector3.Dot(filtered-rawTarget,up);
        }

        private static float RawTargetTrackGap(
            EarthFootContactController feet,
            bool left,
            MeshCollider surface,
            Vector3 up,
            float soleOffset,
            out bool onTrack)
        {
            Vector3 rawPoint=left?feet.LeftRawContactPointWorld:feet.RightRawContactPointWorld;
            Vector3 rawNormal=left?feet.LeftRawContactNormalWorld:feet.RightRawContactNormalWorld;
            Vector3 rawTarget=rawPoint+rawNormal.normalized*soleOffset;
            onTrack=surface.Raycast(new Ray(rawTarget+up*.8f,-up),out RaycastHit hit,1.6f);
            return onTrack
                ? Vector3.Dot(rawTarget-hit.point,hit.normal.normalized)-soleOffset
                : float.MaxValue;
        }

        private static string SupportColliderName(EarthFootContactController feet,bool left)
        {
            string fieldName=left?"_leftSupportCollider":"_rightSupportCollider";
            var field=typeof(EarthFootContactController).GetField(
                fieldName,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Collider support=field?.GetValue(feet) as Collider;
            return support!=null?support.name:"<none>";
        }

        private static float SurfaceHeight(float x,float z)
        {
            float cap=-(x*x+z*z)/40f;
            float hump=.11f*Mathf.Exp(-Mathf.Pow((z+.70f)/.36f,2f));
            float pit=-.13f*Mathf.Exp(-Mathf.Pow((z-.10f)/.30f,2f))*Mathf.Exp(-Mathf.Pow((x+.14f)/.42f,2f));
            float slope=.13f*Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.45f,1.25f,z));
            return cap+hump+pit+slope;
        }

        private static Mesh CreateSurfaceTrackMesh()
        {
            const int nx=40,nz=120;
            var vertices=new Vector3[(nx+1)*(nz+1)];var triangles=new int[nx*nz*6];
            for(int z=0;z<=nz;z++) for(int x=0;x<=nx;x++)
            {
                float px=Mathf.Lerp(-2f,2f,x/(float)nx),pz=Mathf.Lerp(-4f,4f,z/(float)nz);
                vertices[z*(nx+1)+x]=new Vector3(px,SurfaceHeight(px,pz),pz);
            }
            int t=0;
            for(int z=0;z<nz;z++) for(int x=0;x<nx;x++)
            {
                int a=z*(nx+1)+x,b=a+nx+1;
                triangles[t++]=a;triangles[t++]=b;triangles[t++]=a+1;
                triangles[t++]=a+1;triangles[t++]=b;triangles[t++]=b+1;
            }
            var mesh=new Mesh { name="Shallow spherical-cap hump pit slope contact fixture",vertices=vertices,triangles=triangles };
            mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        [Serializable] private sealed class SurfaceReport
        {
            public string utc;
            public string scope="Controlled animation timesteps 30/60/120 Hz with real Humanoid, motor and collider. Wall intervals are recorded separately; targetFrameRate is not proof of achieved render FPS.";
            public List<SurfaceRun> runs=new(); public List<SurfaceFrame> frames=new();
        }
        [Serializable] private sealed class SurfaceRun
        {
            public string actor;public int requestedStepHz,frames,plantedSamples,swingSamples,pitContactSamples;
            public double observedDeltaSum,wallDeltaSum;
            public float maxDrift,maxAbsoluteGap,highestSwingClearance,leftRestClearance,rightRestClearance,soleOffset,pitStopBegan,deepestPitSample;
            public bool sawHump,sawPit,sawSlope,pitStopCompleted,completed;
            [NonSerialized] public Vector3 previousLeftTarget,previousRightTarget;
            [NonSerialized] public bool hasPreviousTargets;
        }
        [Serializable] private sealed class SurfaceFrame
        {
            public string actor;public int requestedStepHz,frame,contactFrame;
            public float delta,wallDelta,trackZ,leftWeight,rightWeight,leftGap,rightGap,leftDrift,rightDrift,leftClearance,rightClearance;
            public float leftRawTargetLag,rightRawTargetLag;
            public float leftRawTrackGap,rightRawTrackGap,leftRawNormalUpDot,rightRawNormalUpDot;
            public float leftTargetStep,rightTargetStep,pelvisOffset,leftPelvisRequest,rightPelvisRequest,pelvisTarget;
            public int leftReason,rightReason,leftSupportKind,rightSupportKind;
            public uint leftSupportId,rightSupportId;
            public string leftSupportCollider,rightSupportCollider;
            public bool leftLocked,rightLocked,leftRawOnTrack,rightRawOnTrack;
        }
    }
}
