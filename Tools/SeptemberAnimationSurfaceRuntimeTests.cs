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
            }
            finally
            {
                Time.captureDeltaTime=oldCapture; Time.fixedDeltaTime=oldFixed;
                Application.targetFrameRate=oldFps; QualitySettings.vSyncCount=oldVsync;
                Directory.CreateDirectory("BuildReports/SeptemberAnimation");
                File.WriteAllText("BuildReports/SeptemberAnimation/ActualSurfaceControlledSteps.json",JsonUtility.ToJson(report,true));
            }
        }

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
            report.runs.Add(run);
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
                float until=Time.time+4f;
                actor.Input.Move=new float2(0f,.48f);
                while(Time.time<until)
                {
                    yield return _frame;
                    double wall=Time.realtimeSinceStartupAsDouble;
                    float z=track.transform.InverseTransformPoint(body.position).z;
                    var row=new SurfaceFrame { actor=run.actor,requestedStepHz=requestedHz,frame=Time.frameCount,
                        delta=Time.deltaTime,wallDelta=(float)(wall-previousWall),trackZ=z,
                        contactFrame=feet.LastContactEvaluationFrame,leftWeight=feet.LeftFootIkWeight,
                        rightWeight=feet.RightFootIkWeight,leftLocked=feet.LeftFootLocked,rightLocked=feet.RightFootLocked };
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
                    if(z>=1.4f) break;
                }
                actor.Input.Move=float2.zero;
                yield return new WaitForSeconds(.6f);
                Assert.That(run.sawHump&&run.sawPit&&run.sawSlope,Is.True,"The real motor did not cross the complete irregular track.");
                Assert.That(run.plantedSamples,Is.GreaterThan(8),"No meaningful final planted-foot measurements.");
                Assert.That(run.swingSamples,Is.GreaterThan(4),"Walking never released the authored swing foot.");
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
            bool locked=left?feet.LeftFootLocked:feet.RightFootLocked;
            EarthFootContactReason reason=left?feet.LeftReason:feet.RightReason;
            bool onTrack=surface.Raycast(new Ray(actual+up*.8f,-up),out RaycastHit hit,1.6f);
            Assert.That(onTrack,Is.True,"Final ankle is outside the actual terrain fixture.");
            float gap=Vector3.Dot(actual-target,normal);
            float drift=Vector3.ProjectOnPlane(actual-target,normal).magnitude;
            float clearance=Vector3.Dot(actual-hit.point,up);
            if(left) { row.leftGap=gap;row.leftDrift=drift;row.leftClearance=clearance; }
            else { row.rightGap=gap;row.rightDrift=drift;row.rightClearance=clearance; }
            if(locked&&weight>.9f&&reason==EarthFootContactReason.Stance)
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

        private static float RestClearance(Vector3 ankle,MeshCollider surface,Vector3 up)
        {
            Assert.That(surface.Raycast(new Ray(ankle+up*.8f,-up),out RaycastHit hit,1.6f),Is.True);
            return Vector3.Dot(ankle-hit.point,up);
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
            public string actor;public int requestedStepHz,frames,plantedSamples,swingSamples;
            public double observedDeltaSum,wallDeltaSum;
            public float maxDrift,maxAbsoluteGap,highestSwingClearance,leftRestClearance,rightRestClearance;
            public bool sawHump,sawPit,sawSlope,completed;
        }
        [Serializable] private sealed class SurfaceFrame
        {
            public string actor;public int requestedStepHz,frame,contactFrame;
            public float delta,wallDelta,trackZ,leftWeight,rightWeight,leftGap,rightGap,leftDrift,rightDrift,leftClearance,rightClearance;
            public bool leftLocked,rightLocked;
        }
    }
}
