using System.Collections;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthWallHeldPushPlayTests
    {
        private Scene _scene;
        [UnityTearDown] public IEnumerator Cleanup()
        {Time.timeScale=1;if(_scene.IsValid()&&_scene.isLoaded)yield return SceneManager.UnloadSceneAsync(_scene);}
        [UnityTest] public IEnumerator AirborneWallKeepsMassAndMomentumWithoutGroundDebris()
        {
            const string path="Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            Assert.That(SceneManager.GetSceneByPath(path).isLoaded,Is.False);
            yield return SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);_scene=SceneManager.GetSceneByPath(path);
            var gate=Find<EarthSceneReadinessGate>();double deadline=Time.realtimeSinceStartupAsDouble+125;
            while(!gate.IsReady&&!gate.Failed&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(gate.IsReady,Is.True,gate.Status);
            yield return ProductionCombatTestFlow.BeginBotAfterReadiness(_scene);
            Find<EarthMvpBotController>().enabled=false;
            var pool=Find<EarthWallPool>();
            var wall=pool.Acquire(new Vector3(-3,120,15),new Vector3(3,120,15),Vector3.zero,2,.4f,0xAAF031,Vector3.up);
            Assert.That(wall,Is.Not.Null);
            deadline=Time.realtimeSinceStartupAsDouble+5;
            while(!wall.IsEmergenceComplete&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(wall.IsEmergenceComplete,Is.True);
            var hub=Find<EarthMaterialFeedbackHub>();wall.ConfigureMaterialFeedback(hub);
            int dust=0,chips=0;uint source=wall.WallId;
            void Observe(EarthMaterialFeedbackCue cue)
            {if(cue.SourceId==source&&(cue.Kind==EarthMaterialFeedbackKind.Friction||cue.Kind==EarthMaterialFeedbackKind.Impact)){dust+=cue.DustCount;chips+=cue.ChipCount;}}
            hub.Presented+=Observe;
            try
            {
                float mass=wall.EstimatedMass;Vector3 start=wall.Body.position;
                Assert.That(wall.TryBeginHeldPush(Vector3.forward),Is.True);
                Assert.That(wall.TryBeginHeldPush(Vector3.forward),Is.True,"Repeated held input must not create a new impulse.");
                for(int i=0;i<12;i++){wall.UpdateHeldPush(Vector3.forward);yield return new WaitForFixedUpdate();}
                Assert.That(Vector3.Distance(start,wall.Body.position),Is.LessThan(.001f),"Charging cannot slide the wall before release.");
                wall.CancelHeldPush();
                Assert.That(wall.Body.isKinematic,Is.True,"Cancelled charge restores the original stationary wall.");
                Assert.That(wall.TryBeginHeldPush(Vector3.forward),Is.True);
                for(int i=0;i<12;i++)yield return new WaitForFixedUpdate();
                Assert.That(wall.ReleaseHeldPush(),Is.True);
                Assert.That(wall.ReleaseHeldPush(),Is.False,"A released charge cannot fire twice.");
                for(int i=0;i<12;i++)yield return new WaitForFixedUpdate();
                Assert.That(wall.Body.mass,Is.EqualTo(mass).Within(.001f));
                Assert.That(Vector3.Distance(start,wall.Body.position),Is.GreaterThan(.2f));
                Assert.That(wall.IsCollapsing,Is.False);
                float speed=wall.Body.linearVelocity.magnitude;Assert.That(speed,Is.GreaterThan(.1f));
                wall.CancelHeldPush();Assert.That(wall.IsHeldPushActive,Is.False);
                Assert.That(wall.Body.linearVelocity.magnitude,Is.EqualTo(speed).Within(.001f),"Cancellation preserves momentum.");
                yield return new WaitForSeconds(.2f);
                Assert.That(dust,Is.Zero,"Airborne wall must not emit ground friction dust.");Assert.That(chips,Is.Zero);
            }
            finally{hub.Presented-=Observe;pool.ReleaseTransient(wall);}
        }
        [UnityTest] public IEnumerator HoldTravelsAtLeastTwiceTapOnSamePhysicalFloor()
        {
            const string path="Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);_scene=SceneManager.GetSceneByPath(path);
            var gate=Find<EarthSceneReadinessGate>();double deadline=Time.realtimeSinceStartupAsDouble+125;
            while(!gate.IsReady&&!gate.Failed&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(gate.IsReady,Is.True,gate.Status);
            yield return ProductionCombatTestFlow.BeginBotAfterReadiness(_scene);Find<EarthMvpBotController>().enabled=false;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Clear physical floor for tap versus hold comparison";
            SceneManager.MoveGameObjectToScene(floor,_scene);
            floor.transform.position=new Vector3(0,119.5f,30);floor.transform.localScale=new Vector3(60,1,160);
            UnityEngine.Physics.SyncTransforms();
            var pool=Find<EarthWallPool>();var distances=new float[2];var report=new System.Text.StringBuilder();float expectedMass=0;
            var launchDust=new int[2];var trailDust=new int[2];
            var feedback=Find<EarthMaterialFeedbackHub>();
            for(int trial=0;trial<2;trial++)
            {
                var wall=pool.Acquire(new Vector3(-3,120,0),new Vector3(3,120,0),Vector3.zero,2,.4f,0xAAF041u+(uint)trial,Vector3.up);
                Assert.That(wall,Is.Not.Null);deadline=Time.realtimeSinceStartupAsDouble+5;
                while(!wall.IsEmergenceComplete&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                Assert.That(wall.IsEmergenceComplete,Is.True);Assert.That(wall.IsCollapsing,Is.False);
                if(trial==0)expectedMass=wall.Body.mass;else Assert.That(wall.Body.mass,Is.EqualTo(expectedMass).Within(.001f));
                wall.ConfigureMaterialFeedback(feedback);
                // This fixture runs 120m above the arena: prioritize its subject like the real player nearby.
                var authoredProfile=(EarthEffectsTuningProfile)typeof(EarthMaterialFeedbackHub).GetField("effectsProfile",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(feedback);
                feedback.FlushPending();feedback.Configure(authoredProfile,wall.transform);
                void Observe(EarthMaterialFeedbackCue cue)
                {
                    if(cue.SourceId!=wall.WallId)return;
                    if(cue.Kind==EarthMaterialFeedbackKind.Impact)launchDust[trial]+=cue.DustCount;
                    if(cue.Kind==EarthMaterialFeedbackKind.Friction)trailDust[trial]+=cue.DustCount;
                }
                feedback.Presented+=Observe;
                var start=wall.Body.position;float driveSeconds=trial==0?.06f:1.5f;float elapsed=0;bool released=false;float peakSpeed=0;
                Assert.That(wall.TryBeginHeldPush(Vector3.forward),Is.True);
                for(int step=0;step<600;step++)
                {
                    yield return new WaitForFixedUpdate();elapsed+=Time.fixedDeltaTime;
                    peakSpeed=Mathf.Max(peakSpeed,wall.Body.linearVelocity.magnitude);
                    if(!released){Assert.That(Vector3.Distance(wall.Body.position,start),Is.LessThan(.001f),"Hold must charge in place.");if(elapsed>=driveSeconds){Assert.That(wall.ReleaseHeldPush(),Is.True);released=true;}}
                    Assert.That(wall.IsCollapsing,Is.False,"Clear-floor comparison must not break either wall.");
                    if(released&&elapsed>driveSeconds+.5f&&wall.Body.linearVelocity.magnitude<.15f)break;
                }
                yield return null;feedback.Presented-=Observe;
                report.AppendLine($"trial={trial};launchDust={launchDust[trial]};trailDust={trailDust[trial]}");
                distances[trial]=Vector3.Dot(wall.Body.position-start,Vector3.forward);
                report.AppendLine($"trial={trial};mass={wall.Body.mass};heldSeconds={driveSeconds};distance={distances[trial]};peakSpeed={peakSpeed};finalSpeed={wall.Body.linearVelocity.magnitude};gap={wall.HeldPushSupportGap}");
                Assert.That(wall.Body.linearVelocity.magnitude,Is.LessThan(.2f),"Finite drive must settle after release.");
                Assert.That(float.IsFinite(wall.HeldPushSupportGap),Is.True,"Both runs must remain on the physical floor.");
                for(int repeat=0;repeat<2;repeat++)
                {
                    var repeatStart=wall.Body.position;
                    Assert.That(wall.TryBeginHeldPush(Vector3.forward),Is.True,"Settled wall must accept another charge.");
                    yield return new WaitForFixedUpdate();
                    Assert.That(wall.ReleaseHeldPush(),Is.True);
                    for(int frame=0;frame<600;frame++)
                    {
                        yield return new WaitForFixedUpdate();
                        if(frame>25&&wall.Body.linearVelocity.magnitude<.15f)break;
                    }
                    float repeatDistance=Vector3.Dot(wall.Body.position-repeatStart,Vector3.forward);
                    report.AppendLine($"trial={trial};repeat={repeat};distance={repeatDistance}");
                    Assert.That(repeatDistance,Is.GreaterThan(4f),"Second/third ordinary shove must retain useful travel.");
                }
                pool.ReleaseTransient(wall);yield return null;yield return new WaitForFixedUpdate();
            }
            System.IO.Directory.CreateDirectory("BuildReports/WallPushInput");System.IO.File.WriteAllText("BuildReports/WallPushInput/tap-hold-range.txt",report.ToString());
            Assert.That(launchDust[0],Is.GreaterThan(0),"Tap needs an immediate grounded release burst.");
            Assert.That(launchDust[1],Is.GreaterThan(launchDust[0]*1.5f),"Charged launch must visibly exceed tap.");
            Assert.That(trailDust[1],Is.GreaterThan(trailDust[0]),"Charged motion needs more ground feedback.");
            Assert.That(distances[0],Is.GreaterThan(4f));
            Assert.That(distances[1],Is.GreaterThanOrEqualTo(distances[0]*2),report.ToString());
        }
        [UnityTest] public IEnumerator FullChargeStaysNearGroundAcrossShallowRamp()
        {
            const string path="Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);_scene=SceneManager.GetSceneByPath(path);
            var gate=Find<EarthSceneReadinessGate>();double deadline=Time.realtimeSinceStartupAsDouble+125;
            while(!gate.IsReady&&!gate.Failed&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(gate.IsReady,Is.True,gate.Status);
            yield return ProductionCombatTestFlow.BeginBotAfterReadiness(_scene);Find<EarthMvpBotController>().enabled=false;
            void Surface(string label,Vector3 position,Vector3 scale,float pitch)
            {
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name=label;
                SceneManager.MoveGameObjectToScene(floor,_scene);floor.transform.position=position;
                floor.transform.localScale=scale;floor.transform.rotation=Quaternion.Euler(pitch,0,0);
            }
            Surface("Ground",new Vector3(0,119.5f,20),new Vector3(40,1,100),0);
            Surface("Shallow stone ramp",new Vector3(0,120.28f,6),new Vector3(20,.1f,4),-8);
            Surface("Raised ground",new Vector3(0,120.3f,20),new Vector3(20,.6f,24),0);
            UnityEngine.Physics.SyncTransforms();
            var pool=Find<EarthWallPool>();
            var wall=pool.Acquire(new Vector3(-3,120,0),new Vector3(3,120,0),Vector3.zero,2,.4f,0xAAF049,Vector3.up);
            deadline=Time.realtimeSinceStartupAsDouble+5;
            while(!wall.IsEmergenceComplete&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(wall.TryBeginHeldPush(Vector3.forward),Is.True);
            for(int frame=0;frame<55;frame++)yield return new WaitForFixedUpdate();
            Assert.That(wall.ReleaseHeldPush(),Is.True);
            float peakGap=0;int lifted=0,samples=0;float start=wall.Body.position.z;
            var trace=new System.Text.StringBuilder();
            for(int frame=0;frame<250;frame++)
            {
                yield return new WaitForFixedUpdate();
                var bounds=wall.SurfaceCollider.bounds;
                var foot=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                if(UnityEngine.Physics.Raycast(foot+Vector3.up*.02f,Vector3.down,out var hit,2f,~0,QueryTriggerInteraction.Ignore))
                {
                    float gap=foot.y-hit.point.y;peakGap=Mathf.Max(peakGap,gap);samples++;
                    trace.AppendLine($"frame={frame};pos={wall.Body.position};v={wall.Body.linearVelocity};gap={gap};support={wall.HeldPushSupportGap}");
                    if(gap>.15f)lifted++;
                }
                Assert.That(wall.IsCollapsing,Is.False);
                if(frame>50&&wall.Body.linearVelocity.magnitude<.15f)break;
            }
            System.IO.Directory.CreateDirectory("BuildReports/WallPushInput");
            System.IO.File.WriteAllText("BuildReports/WallPushInput/charged-grounding-trajectory.txt",trace.ToString());
            System.IO.File.WriteAllText("BuildReports/WallPushInput/charged-grounding.txt",$"travel={wall.Body.position.z-start};peakGap={peakGap};liftedSamples={lifted};samples={samples}");
            Assert.That(wall.Body.position.z-start,Is.GreaterThan(5f),"Charged wall must cross onto the ramp.");
            Assert.That(samples,Is.GreaterThan(40));
            Assert.That(peakGap,Is.LessThan(.35f),"Shallow ground must not launch the wall high into the air.");
            Assert.That(lifted,Is.LessThan(samples*.2f),"Wall should remain within 15cm of support for at least 80% of the slide.");
            pool.ReleaseTransient(wall);
        }
        [UnityTest] public IEnumerator HeavyObstacleShiftsOnlyThreeLinkedDomainsByCentimeters()
        {
            const string path="Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);_scene=SceneManager.GetSceneByPath(path);
            var gate=Find<EarthSceneReadinessGate>();double deadline=Time.realtimeSinceStartupAsDouble+125;
            while(!gate.IsReady&&!gate.Failed&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(gate.IsReady,Is.True,gate.Status);
            yield return ProductionCombatTestFlow.BeginBotAfterReadiness(_scene);Find<EarthMvpBotController>().enabled=false;
            var pool=Find<EarthWallPool>();
            var wall=pool.Acquire(new Vector3(-3,120,0),new Vector3(3,120,0),Vector3.zero,2,.4f,0xAAF059,Vector3.up);
            deadline=Time.realtimeSinceStartupAsDouble+5;
            while(!wall.IsEmergenceComplete&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(obstacle,_scene);
            obstacle.name="Massive stationary arena obstacle";
            obstacle.transform.position=wall.SurfaceCollider.bounds.center+Vector3.forward*2;
            obstacle.transform.localScale=new Vector3(10,5,1);UnityEngine.Physics.SyncTransforms();
            float mass=wall.Body.mass;
            Assert.That(wall.TryBeginHeldPush(Vector3.forward),Is.True);
            for(int frame=0;frame<55;frame++)yield return new WaitForFixedUpdate();
            Assert.That(wall.ReleaseHeldPush(),Is.True);
            for(int frame=0;frame<80&&wall.HeavyContactChippedPieceCount==0;frame++)yield return new WaitForFixedUpdate();
            Assert.That(wall.HeavyContactChippedPieceCount,Is.InRange(2,3));
            yield return new WaitForSeconds(.3f);
            var bodies=(Rigidbody[])typeof(EarthWall).GetField("_pieceBodies",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(wall);
            float sum=0,maxOffset=0;int moved=0;
            for(int i=0;i<bodies.Length;i++)
            {
                var body=bodies[i];sum+=body.mass;
                var rest=wall.StructureRuntime.GetPieceDefinition(i).RestLocalPosition;
                float offset=Vector3.Distance(body.position,wall.transform.TransformPoint(new Vector3(rest.x,rest.y,rest.z)));
                maxOffset=Mathf.Max(maxOffset,offset);if(offset>.005f)moved++;
                Assert.That(body.isKinematic,Is.True,"Chipped cells remain linked and supported rather than launching.");
            }
            System.IO.File.WriteAllText("BuildReports/WallPushInput/heavy-contact-chips.txt",$"selected={wall.HeavyContactChippedPieceCount};moved={moved};maximumOffset={maxOffset};childMass={sum};originalMass={mass};bonds={wall.RemainingBondCount}");
            Assert.That(moved,Is.InRange(2,3));Assert.That(maxOffset,Is.InRange(.01f,.026f));
            Assert.That(sum,Is.EqualTo(mass).Within(.02f));Assert.That(wall.RemainingBondCount,Is.GreaterThan(0));
            pool.ReleaseTransient(wall);
        }
        private T Find<T>() where T:Component
        {foreach(var root in _scene.GetRootGameObjects()){var result=root.GetComponentInChildren<T>(true);if(result!=null)return result;}throw new System.InvalidOperationException(typeof(T).Name);}
    }
}
