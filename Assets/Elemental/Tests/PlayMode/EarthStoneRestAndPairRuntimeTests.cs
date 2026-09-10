#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Gravity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthStoneRestAndPairRuntimeTests
    {
        private readonly List<GameObject> _owned=new();
        private EarthPhysicsFeelProfile _feel;
        private Mesh _replacementMesh;
        private Mesh Mesh(string name)=>AssetDatabase.LoadAssetAtPath<Mesh>(
            "Assets/Elemental/Content/GraphicsV5/Physics/"+name+"_CenteredUnit.asset");
        private GameObject Own(string name){var result=new GameObject(name);_owned.Add(result);return result;}
        [TearDown] public void Cleanup()
        {foreach(var item in _owned)if(item!=null)Object.DestroyImmediate(item);_owned.Clear();if(_feel!=null)Object.DestroyImmediate(_feel);if(_replacementMesh!=null)Object.DestroyImmediate(_replacementMesh);}
        private EarthFragment Stone(string name,Vector3 position,uint id,float mass)
        {
            var go=Own(name);var body=go.AddComponent<Rigidbody>();body.useGravity=false;
            var shape=go.AddComponent<MeshCollider>();shape.sharedMesh=Mesh(name);Assert.That(shape.sharedMesh,Is.Not.Null,name);shape.convex=true;
            var fragment=go.AddComponent<EarthFragment>();fragment.Initialize(id,null,position,.5f,mass);
            go.transform.localScale=Vector3.one;go.transform.rotation=Quaternion.identity;
            body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            _feel??=ScriptableObject.CreateInstance<EarthPhysicsFeelProfile>();_feel.Apply(body,shape,EarthPhysicsBodyClass.HeavyBlock);
            return fragment;
        }
        [UnityTest]
        public IEnumerator ActualMeshDropPileSettlesAndChangedOrRemovedSupportWakesIt()
        {
            var gravity=Own("Stone rest gravity");gravity.SetActive(false);gravity.transform.position=new Vector3(0,-1000,0);
            var source=gravity.AddComponent<PointPlanetGravitySource>();source.Configure(new GravityFieldId(71),1000,14,1,2000);
            var world=gravity.AddComponent<GravityWorldBehaviour>();world.Configure(new[]{source});gravity.SetActive(true);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);_owned.Add(floor);
            floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(20,1,20);
            Object.DestroyImmediate(floor.GetComponent<BoxCollider>());
            var floorShape=floor.AddComponent<MeshCollider>();floorShape.sharedMesh=floor.GetComponent<MeshFilter>().sharedMesh;
            string[] names={"V5_Physics_Boulder_04","V5_Physics_Boulder_05","V5_Physics_Pebble_17"};
            var bodies=new Rigidbody[3];
            for(int i=0;i<3;i++)
            {
                var fragment=Stone(names[i],new Vector3(i*.08f,2+i*1.3f,0),(uint)(i+1),35+i*20);
                bodies[i]=fragment.Body;fragment.gameObject.AddComponent<GravityBody>().Configure(world,bodies[i]);
            }
            for(int i=0;i<600;i++)yield return new WaitForFixedUpdate();
            var positions=new Vector3[3];var rotations=new Quaternion[3];
            for(int i=0;i<3;i++){positions[i]=bodies[i].position;rotations[i]=bodies[i].rotation;}
            float maximumDrift=0,maximumAngle=0;int sleepingSamples=0;
            for(int frame=0;frame<120;frame++)
            {
                yield return new WaitForFixedUpdate();
                for(int i=0;i<3;i++)
                {maximumDrift=Mathf.Max(maximumDrift,Vector3.Distance(positions[i],bodies[i].position));
                 maximumAngle=Mathf.Max(maximumAngle,Quaternion.Angle(rotations[i],bodies[i].rotation));if(bodies[i].IsSleeping())sleepingSamples++;}
            }
            Directory.CreateDirectory("BuildReports/HardPolish/StoneRest");
            File.WriteAllText("BuildReports/HardPolish/StoneRest/pile.txt",
                $"Actual saved physics meshes; 600 settling ticks +120 observed ticks, dt={Time.fixedDeltaTime:R}\nMaximum drift={maximumDrift:R}m; angle={maximumAngle:R}deg; sleeping={sleepingSamples}/360\n");
            Assert.That(maximumDrift,Is.LessThan(.003f));Assert.That(maximumAngle,Is.LessThan(.3f));
            Assert.That(sleepingSamples,Is.GreaterThan(300));
            float minimumBefore=Mathf.Min(bodies[0].position.y,Mathf.Min(bodies[1].position.y,bodies[2].position.y));
            _replacementMesh=Object.Instantiate(floorShape.sharedMesh);
            var vertices=_replacementMesh.vertices;
            for(int i=0;i<vertices.Length;i++)vertices[i]+=Vector3.down*3;
            _replacementMesh.vertices=vertices;_replacementMesh.RecalculateBounds();floorShape.sharedMesh=_replacementMesh;
            for(int i=0;i<25;i++)yield return new WaitForFixedUpdate();
            float minimumAfter=Mathf.Min(bodies[0].position.y,Mathf.Min(bodies[1].position.y,bodies[2].position.y));
            Assert.That(minimumAfter,Is.LessThan(minimumBefore-.15f),"Replaced terrain mesh left the pile suspended asleep.");
            for(int i=0;i<360;i++)yield return new WaitForFixedUpdate();
            minimumBefore=Mathf.Min(bodies[0].position.y,Mathf.Min(bodies[1].position.y,bodies[2].position.y));
            floor.SetActive(false);
            for(int i=0;i<25;i++)yield return new WaitForFixedUpdate();
            minimumAfter=Mathf.Min(bodies[0].position.y,Mathf.Min(bodies[1].position.y,bodies[2].position.y));
            Assert.That(minimumAfter,Is.LessThan(minimumBefore-.15f),"Disabled support left the pile suspended asleep.");
        }
        [UnityTest]
        public IEnumerator OpposingFastActualConvexStonesBlockAndTemporaryIgnoreRemainsExplicit()
        {
            var a=Stone("V5_Physics_Boulder_04",new Vector3(-2,100,0),101,40);
            var b=Stone("V5_Physics_Boulder_05",new Vector3(2,100,0),102,65);
            var ca=a.GetComponent<Collider>();var cb=b.GetComponent<Collider>();
            var sa=a.GetComponent<EarthProjectileSweepGuard>()??a.gameObject.AddComponent<EarthProjectileSweepGuard>();sa.Configure(a,_feel);
            var sb=b.GetComponent<EarthProjectileSweepGuard>()??b.gameObject.AddComponent<EarthProjectileSweepGuard>();sb.Configure(b,_feel);
            Physics.IgnoreCollision(ca,cb,true);
            a.Body.linearVelocity=Vector3.right*120;b.Body.linearVelocity=Vector3.left*100;Physics.SyncTransforms();
            for(int i=0;i<3;i++)yield return new WaitForFixedUpdate();
            Assert.That(a.Body.position.x,Is.GreaterThan(b.Body.position.x),"Sweep bypassed an explicit temporary ignore.");
            Physics.IgnoreCollision(ca,cb,false);
            a.Body.position=new Vector3(-2,100,0);b.Body.position=new Vector3(2,100,0);
            a.Body.linearVelocity=Vector3.right*120;b.Body.linearVelocity=Vector3.left*100;
            sa.Arm();sb.Arm();Physics.SyncTransforms();
            bool closed=false;
            for(int i=0;i<8;i++)
            {
                yield return new WaitForFixedUpdate();
                if(a.Body.linearVelocity.x-b.Body.linearVelocity.x<1){closed=true;break;}
            }
            Assert.That(closed,Is.True,"Restored stone pair failed to exchange collision momentum.");
            Assert.That(a.Body.position.x,Is.LessThan(b.Body.position.x),"Fast convex stones crossed through each other.");
        }
        [UnityTest]
        public IEnumerator ReleasedArmorSiblingsRestorePhysicalCollisionAfterBoundedGrace()
        {
            var caster=Own("Armor sibling collision owner");caster.transform.position=Vector3.up*24;
            var casterBody=caster.AddComponent<Rigidbody>();casterBody.useGravity=false;casterBody.isKinematic=true;
            var armor=caster.AddComponent<EarthArmorController>();armor.Configure(casterBody,null,null,null);
            Assert.That(armor.Begin(),Is.True);
            for(int i=0;i<18;i++)yield return new WaitForFixedUpdate();
            var pieces=new EarthArmorPiece[EarthArmorProfile.DefaultPieceCount];
            Assert.That(armor.CopyActivePiecesNonAlloc(pieces),Is.GreaterThanOrEqualTo(2));
            var a=pieces[0];var b=pieces[1];
            a.Release(Vector3.zero,10,1);b.Release(Vector3.zero,10,1);
            a.Body.position=new Vector3(-1,200,0);b.Body.position=new Vector3(1,200,0);
            Assert.That(Physics.GetIgnoreCollision(a.PieceCollider,b.PieceCollider),Is.True);
            for(int i=0;i<12;i++){yield return new WaitForFixedUpdate();yield return null;}
            Assert.That(Physics.GetIgnoreCollision(a.PieceCollider,b.PieceCollider),Is.False,
                "Formation sibling ignore leaked into released stones.");
            // Recall requires surviving matter. A real high-speed plate collision below
            // deliberately retires both transient plates, so test recall before that collision.
            Assert.That(a.TryBeginRecall(),Is.True);
            Assert.That(Physics.GetIgnoreCollision(a.PieceCollider,b.PieceCollider),Is.True,
                "Explicit formation recall must restore its sibling exception.");
            a.Release(Vector3.zero,10,1);
            for(int i=0;i<12;i++){yield return new WaitForFixedUpdate();yield return null;}
            Assert.That(Physics.GetIgnoreCollision(a.PieceCollider,b.PieceCollider),Is.False,
                "A recalled and re-released plate must expire its new sibling grace too.");
            a.Body.position=new Vector3(-.6f,200,0);b.Body.position=new Vector3(.6f,200,0);
            a.Body.rotation=Quaternion.identity;b.Body.rotation=Quaternion.identity;
            a.Body.constraints=RigidbodyConstraints.FreezeRotation;b.Body.constraints=RigidbodyConstraints.FreezeRotation;
            a.Body.linearVelocity=Vector3.right*12;b.Body.linearVelocity=Vector3.left*12;
            Physics.SyncTransforms();bool collided=false;
            for(int i=0;i<12;i++)
            {yield return new WaitForFixedUpdate();if(a.Body.linearVelocity.x-b.Body.linearVelocity.x<1){collided=true;break;}}
            Assert.That(collided,Is.True,"Released physical armor stones passed through each other.");
        }
        [Test]
        public void RotatedAnisotropicMeshEnvelopeUsesLocalBoundsExactlyOnce()
        {
            var stone=Stone("V5_Physics_Boulder_04",new Vector3(7,80,4),44,40);
            stone.transform.localScale=new Vector3(.3f,1.8f,.7f);stone.transform.rotation=Quaternion.Euler(23,61,14);
            var collider=stone.GetComponent<MeshCollider>();
            EarthProjectileSweepGuard.ResolveSweepBox(collider,out var center,out var extents,out var rotation);
            var bounds=collider.sharedMesh.bounds;
            Assert.That(Vector3.Distance(center,stone.transform.TransformPoint(bounds.center)),Is.LessThan(.00001f));
            Assert.That(Vector3.Distance(extents,Vector3.Scale(bounds.extents,stone.transform.lossyScale)),Is.LessThan(.00001f));
            Assert.That(Quaternion.Angle(rotation,stone.transform.rotation),Is.LessThan(.001f));
        }
    }
}
#endif
