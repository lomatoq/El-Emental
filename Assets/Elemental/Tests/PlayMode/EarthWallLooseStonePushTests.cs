using System.Collections;
using System.IO;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
    public sealed class EarthWallLooseStonePushTests
    {
        private Scene scene;
        [UnityTearDown] public IEnumerator Cleanup()
        {Time.timeScale=1;if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);}
        [UnityTest] public IEnumerator ChargedWallPlowsAdjacentLooseStonesButStillFracturesFromHeavyIncomingStone()
        {
            const string path="Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);scene=SceneManager.GetSceneByPath(path);
            var gate=Find<EarthSceneReadinessGate>();float deadline=Time.realtimeSinceStartup+130;
            while(!gate.IsReady&&!gate.Failed&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(gate.IsReady,Is.True,gate.Status);yield return ProductionCombatTestFlow.BeginBotAfterReadiness(scene);
            Find<EarthMvpBotController>().enabled=false;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(floor,scene);
            floor.name="Loose stone shove floor";floor.transform.position=new Vector3(0,119.5f,30);floor.transform.localScale=new Vector3(40,1,120);
            var wall=Find<EarthWallPool>().Acquire(new Vector3(-3,120,0),new Vector3(3,120,0),Vector3.zero,2,.4f,supportNormal:Vector3.up);
            deadline=Time.realtimeSinceStartup+5;while(!wall.IsEmergenceComplete&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(wall.IsCollapsing,Is.False);
            Assert.That(wall.TryBeginHeldPush(Vector3.forward),Is.True);
            for(int i=0;i<55;i++)yield return new WaitForFixedUpdate();
            Vector3 forward=wall.HeldPushDirection;
            Vector3 right=Vector3.Cross(Vector3.up,forward).normalized;
            Bounds wallBounds=wall.SurfaceCollider.bounds;
            float frontExtent=Vector3.Dot(wallBounds.extents,new Vector3(Mathf.Abs(forward.x),Mathf.Abs(forward.y),Mathf.Abs(forward.z)));
            Vector3 front=wallBounds.center+forward*(frontExtent+.4f+.01f);
            var a=Stone(front-right*.9f,wall.Body.mass*.04f,.8f);
            var b=Stone(front+right*.9f,wall.Body.mass*.04f,.8f);
            UnityEngine.Physics.SyncTransforms();Vector3 start=wall.Body.position,aStart=a.position,bStart=b.position;
            Directory.CreateDirectory("BuildReports/WallLooseStonePush");
            var report=new System.Text.StringBuilder();
            report.AppendLine($"wallBounds={wallBounds}; direction={forward}; stoneA={a.GetComponent<Collider>().bounds}; stoneB={b.GetComponent<Collider>().bounds}; wallMass={wall.Body.mass}; stoneMass={a.mass}");
            Assert.That(wall.ReleaseHeldPush(),Is.True);
            float peakStoneSpeed=0;
            for(int i=0;i<18;i++)
            {
                yield return new WaitForFixedUpdate();peakStoneSpeed=Mathf.Max(peakStoneSpeed,Vector3.Dot(a.linearVelocity,forward),Vector3.Dot(b.linearVelocity,forward));
                report.AppendLine($"frame={i}; contacts={wall.OutgoingLooseStoneContacts}; wallPos={wall.Body.position}; wallVelocity={wall.Body.linearVelocity}; stoneAPos={a.position}; stoneAVelocity={a.linearVelocity}; stoneBPos={b.position}; stoneBVelocity={b.linearVelocity}; physicalA={a.GetComponent<PhysicalImpactTarget>().ImpactCount}; physicalB={b.GetComponent<PhysicalImpactTarget>().ImpactCount}; fractured={wall.IsCollapsing}");
                File.WriteAllText("BuildReports/WallLooseStonePush/result.txt",report.ToString());
                Assert.That(wall.IsCollapsing,Is.False,"Outgoing contact with light loose stones must not be treated as an incoming hit on our wall.");
            }
            Assert.That(wall.OutgoingLooseStoneContacts,Is.GreaterThan(0));
            Assert.That(Vector3.Dot(wall.Body.position-start,forward),Is.GreaterThan(.5f));
            Assert.That(Mathf.Min(Vector3.Dot(a.position-aStart,forward),Vector3.Dot(b.position-bStart,forward)),Is.GreaterThan(.15f));
            Assert.That(peakStoneSpeed,Is.GreaterThan(1f));
            Assert.That(a.detectCollisions&&b.detectCollisions&&wall.GetComponent<Collider>().enabled,Is.True);
            var heavy=Stone(wall.SurfaceCollider.bounds.center-forward*2f,wall.Body.mass*.75f,.65f);
            heavy.linearVelocity=wall.Body.linearVelocity+forward*45f;
            deadline=Time.realtimeSinceStartup+2;
            while(!wall.IsCollapsing&&Time.realtimeSinceStartup<deadline)yield return new WaitForFixedUpdate();
            Assert.That(wall.IsCollapsing,Is.True,"The outgoing-contact rule must not grant immunity to a heavy incoming projectile.");
        }
        [UnityTest] public IEnumerator GroundedAnchoredDecorBreaksLocallyAndWallContinuesWithoutCollectiveLaunch()
        {
            const string path="Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);scene=SceneManager.GetSceneByPath(path);
            var gate=Find<EarthSceneReadinessGate>();float deadline=Time.realtimeSinceStartup+130;
            while(!gate.IsReady&&!gate.Failed&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(gate.IsReady,Is.True,gate.Status);yield return ProductionCombatTestFlow.BeginBotAfterReadiness(scene);
            Find<EarthMvpBotController>().enabled=false;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(floor,scene);
            floor.transform.position=new Vector3(0,119.5f,20);floor.transform.localScale=new Vector3(40,1,100);
            var wall=Find<EarthWallPool>().Acquire(new Vector3(-3,120,0),new Vector3(3,120,0),Vector3.zero,2,.4f,supportNormal:Vector3.up);
            deadline=Time.realtimeSinceStartup+5;while(!wall.IsEmergenceComplete&&Time.realtimeSinceStartup<deadline)yield return null;
            var stone=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(stone,scene);
            stone.name="Actual anchored destructible decor on floor";stone.transform.position=new Vector3(0,120.45f,1.2f);stone.transform.localScale=Vector3.one*.9f;
            var body=stone.AddComponent<Rigidbody>();body.useGravity=false;
            var rock=stone.AddComponent<EarthDestructibleDecorRock>();
            rock.Configure(0xDEC091,body,stone.GetComponent<Collider>(),null,Find<EarthRockDebrisPool>(),.45f,720,true);
            Assert.That(rock.IsAnchored,Is.True);UnityEngine.Physics.SyncTransforms();
            Assert.That(wall.TryBeginHeldPush(Vector3.forward),Is.True);
            for(int i=0;i<55;i++)yield return new WaitForFixedUpdate();
            float originalMass=wall.Body.mass;
            using var profile=Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Scripts,"Elemental.Earth.Wall.RigidSlide",128);long peakNs=0;
            Assert.That(wall.ReleaseHeldPush(),Is.True);
            float peakLift=0,travel=0,maxBondStretch=0;int carrierFrames=0,rigidBonds=0;var trace=new System.Text.StringBuilder();
            var bonds=(EarthWallBond[])typeof(EarthWall).GetField("_bonds",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(wall);
            var broken=(bool[])typeof(EarthWall).GetField("_bondBroken",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(wall);
            for(int i=0;i<110;i++)
            {
                yield return new WaitForFixedUpdate();
                if(profile.Valid)peakNs=System.Math.Max(peakNs,profile.LastValue);
                if(!wall.IsCollapsing)continue;
                var bodies=(Rigidbody[])typeof(EarthWall).GetField("_pieceBodies",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(wall);
                float mass=0;Vector3 center=Vector3.zero;
                foreach(var piece in bodies)if(!piece.isKinematic){mass+=piece.mass;center+=piece.worldCenterOfMass*piece.mass;}
                for(int b=0;b<bonds.Length;b++)
                {
                    var bond=bonds[b];if(broken[b]||bond.Foundation||!wall.IsRigidDomain(bond.PieceA)||!wall.IsRigidDomain(bond.PieceB))continue;
                    rigidBonds++;
                    var a=wall.StructureRuntime.GetPieceDefinition(bond.PieceA).RestLocalPosition;
                    var z=wall.StructureRuntime.GetPieceDefinition(bond.PieceB).RestLocalPosition;
                    float rest=wall.transform.TransformVector(new Vector3(a.x-z.x,a.y-z.y,a.z-z.z)).magnitude;
                    maxBondStretch=Mathf.Max(maxBondStretch,Mathf.Abs(Vector3.Distance(wall.RigidDomainPosition(bond.PieceA),wall.RigidDomainPosition(bond.PieceB))-rest));
                }
                center/=mass;travel=center.z;
                if(wall.HasRigidSlideCarrier)
                {
                    carrierFrames++;float foot=float.PositiveInfinity;
                    foreach(var shape in wall.RigidSlideBody.GetComponentsInChildren<Collider>())
                        if(shape.enabled&&shape.gameObject.activeInHierarchy&&shape.attachedRigidbody==wall.RigidSlideBody)foot=Mathf.Min(foot,shape.bounds.min.y);
                    peakLift=Mathf.Max(peakLift,foot-120);
                    trace.AppendLine($"frame={i};foot={foot};carrier={wall.RigidSlideBody.position};velocity={wall.RigidSlideBody.linearVelocity}");
                }
            }
            Directory.CreateDirectory("BuildReports/WallLooseStonePush");
            File.WriteAllText("BuildReports/WallLooseStonePush/anchored-decor-motion.txt",trace.ToString());
            File.WriteAllText("BuildReports/WallLooseStonePush/anchored-decor.txt",$"contacts={wall.PloughedDecorContacts};chips={wall.PloughDetachedDomains};rockShattered={rock.IsShattered};rockAnchored={rock.IsAnchored};travel={travel};peakLift={peakLift};maxBondStretch={maxBondStretch};carrierFrames={carrierFrames};rigidBonds={rigidBonds};rigidMarkerNs={peakNs}");
            Assert.That(wall.PloughedDecorContacts,Is.GreaterThan(0));
            Assert.That(wall.PloughDetachedDomains,Is.InRange(1,2));
            Assert.That(rock.IsShattered,Is.True);
            Assert.That(travel,Is.GreaterThan(2f),"The stone at 1.2m must not stop the wall immediately.");
            Assert.That(carrierFrames,Is.GreaterThan(70),"Remaining wall must stay a physical rigid island.");
            Assert.That(rigidBonds,Is.GreaterThan(100));
            Assert.That(maxBondStretch,Is.LessThan(.005f),"Surviving bonds must not stretch like a spring.");
            Assert.That(peakLift,Is.LessThan(.12f),"Cracked wall must not spring collectively out of the floor.");
            var physicalBodies=(Rigidbody[])typeof(EarthWall).GetField("_pieceBodies",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(wall);
            float physicalMass=0;foreach(var piece in physicalBodies)if(!piece.isKinematic)physicalMass+=piece.mass;
            Assert.That(physicalMass,Is.EqualTo(originalMass).Within(.02f),"Aggregate mass is represented by one dynamic carrier, plus detached chips.");
            float domainMass=0;foreach(var piece in physicalBodies)domainMass+=piece.GetComponent<EarthPieceRuntime>().EarthMass;
            Assert.That(domainMass,Is.EqualTo(originalMass).Within(.02f),"Domain targeting must not count the carrier aggregate twice.");
            Assert.That(wall.TryBeginHeldPush(Vector3.forward),Is.True);wall.CancelHeldPush();
            Assert.That(wall.HasRigidSlideCarrier,Is.True,"Cancelling another charge must restore the rigid moving representation.");
            float beforeRepeat=wall.RigidSlideBody.worldCenterOfMass.z;
            Assert.That(wall.TryBeginHeldPush(Vector3.forward),Is.True);
            yield return new WaitForFixedUpdate();Assert.That(wall.ReleaseHeldPush(),Is.True);
            for(int i=0;i<25;i++)yield return new WaitForFixedUpdate();
            Assert.That(wall.RigidSlideBody.worldCenterOfMass.z-beforeRepeat,Is.GreaterThan(.3f),"A damaged wall must accept a second useful shove.");
            var proxy=wall.RigidSlideBody.GetComponentInChildren<EarthRigidDomainTarget>();
            Assert.That(proxy,Is.Not.Null);
            var source=proxy.Source;Vector3 domainPosition=proxy.transform.position;
            source.OnEarthMagicGrabbed(EarthMagicGripKind.Telekinesis);
            Assert.That(wall.HasRigidSlideCarrier,Is.False);
            Assert.That(source.IsEarthTargetValid,Is.True);
            Assert.That(Vector3.Distance(source.Body.position,domainPosition),Is.LessThan(.02f));
            source.OnEarthMagicReleased(EarthMagicGripKind.Telekinesis);
            Find<EarthWallPool>().ReleaseTransient(wall);
        }
        private Rigidbody Stone(Vector3 position,float mass,float size)
        {
            var stone=GameObject.CreatePrimitive(PrimitiveType.Cube);stone.name="Loose physical stone";SceneManager.MoveGameObjectToScene(stone,scene);
            stone.transform.position=position;stone.transform.localScale=Vector3.one*size;
            var body=stone.AddComponent<Rigidbody>();body.mass=mass;body.useGravity=false;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            stone.AddComponent<PhysicalImpactTarget>().Configure(body);return body;
        }
        private T Find<T>()where T:Component
        {foreach(var root in scene.GetRootGameObjects()){var c=root.GetComponentInChildren<T>(true);if(c!=null)return c;}Assert.Fail(typeof(T).Name);return null;}
    }
}
