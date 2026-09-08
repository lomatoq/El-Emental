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
