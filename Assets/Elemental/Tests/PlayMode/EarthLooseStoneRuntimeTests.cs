using System.Collections;
using System.IO;
using Elemental.Input.Gestures;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Gravity;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public class EarthLooseStoneRuntimeTests
    {
        [Test]
        public void CameraHiddenParentDoesNotMakeLooseStoneUntargetable()
        {
            var root = new GameObject("Hidden arena parent");
            try
            {
                var arena = root.AddComponent<EarthArenaStructure>(); arena.enabled = false;
                arena.SetCameraSuppressed(true);
                var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rock.transform.SetParent(root.transform);
                rock.AddComponent<Rigidbody>().useGravity = false;
                var fragment = rock.AddComponent<EarthFragment>();
                fragment.Initialize(81,null,new Vector3(20,10,0),.4f,3f);
                fragment.Body.Sleep();
                UnityEngine.Physics.SyncTransforms();
                var query = new EarthTargetQueryService();
                Assert.That(query.TryQuery(new Ray(rock.transform.position+Vector3.forward*3,Vector3.back),
                    5f,0f,null,null,EarthTargetCapabilities.Gravity,out var hit), Is.True,
                    "Camera suppression belongs to the structure, not its visible loose descendants.");
                Assert.That(hit.Target.PhysicalTarget, Is.SameAs(fragment));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator FallenStonesSleepStayStillAndWakeForGravityGrip()
        {
            var root = new GameObject("Loose stone rest fixture");
            var report = new RestReport();
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"Elemental.GravityBody.FixedTick",32);
            try
            {
                var sourceObject = new GameObject("Radial source"); sourceObject.transform.SetParent(root.transform);
                sourceObject.transform.position = Vector3.down*100;
                var source = sourceObject.AddComponent<PointPlanetGravitySource>();
                source.Configure(new GravityFieldId(1),100,11.5f,1,400);
                var world = root.AddComponent<GravityWorldBehaviour>();world.Configure(new[]{source});
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform);
                floor.transform.position = Vector3.down*.5f;floor.transform.localScale=new Vector3(20,1,20);
                var rocks = new Rigidbody[3];
                for(int i=0;i<rocks.Length;i++)
                {
                    var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.transform.SetParent(root.transform);
                    go.transform.position=new Vector3((i-1)*2f,1.2f+i*.6f,0);
                    go.transform.localScale=Vector3.one*(.3f+i*.25f);
                    var body=go.AddComponent<Rigidbody>();body.mass=2+i*8;body.useGravity=false;
                    go.AddComponent<PhysicalImpactTarget>().Configure(body);
                    go.AddComponent<GravityBody>().Configure(world,body);rocks[i]=body;
                }
                yield return new WaitForSeconds(6f);
                foreach(var body in rocks) if(body.IsSleeping())report.sleeping++;
                Assert.That(report.sleeping,Is.EqualTo(3),"Gravity must let settled bodies sleep rather than restarting their contact solver every tick.");
                Vector3[] positions={rocks[0].position,rocks[1].position,rocks[2].position};
                Quaternion[] rotations={rocks[0].rotation,rocks[1].rotation,rocks[2].rotation};
                for(int i=0;i<60;i++)
                {
                    yield return new WaitForFixedUpdate();
                    for(int j=0;j<3;j++)
                    {
                        report.maximumDrift=Mathf.Max(report.maximumDrift,Vector3.Distance(positions[j],rocks[j].position));
                        report.maximumRotationDrift=Mathf.Max(report.maximumRotationDrift,Quaternion.Angle(rotations[j],rocks[j].rotation));
                        Assert.That(rocks[j].IsSleeping(),Is.True);
                    }
                }
                Assert.That(report.maximumDrift,Is.LessThan(.0001f));
                Assert.That(report.maximumRotationDrift,Is.LessThan(.001f));
                var executor=root.AddComponent<MagicExecutor>();
                for(int i=0;i<3;i++)
                {
                    float before=rocks[i].position.y;
                    Assert.That(executor.TryBeginGravityWell(rocks[i].GetComponent<Collider>(),rocks[i].position+Vector3.up*2,Vector3.up,true),Is.True);
                    Assert.That(executor.GravityWellCapturedCount,Is.EqualTo(3),"MMB wakes and captures the nearby group, not just the aimed stone.");
                    yield return new WaitForSeconds(.7f);
                    Assert.That(rocks[i].position.y,Is.GreaterThan(before+.4f));
                    report.grabbed++;
                    executor.CancelGravityWell();
                }
                // Removing support must still wake a sleeping stone and let it fall.
                rocks[0].position=new Vector3(0,.15f,3);rocks[0].linearVelocity=Vector3.zero;rocks[0].angularVelocity=Vector3.zero;
                yield return new WaitForSeconds(2f);rocks[0].Sleep();
                float supportedY=rocks[0].position.y;floor.GetComponent<Collider>().enabled=false;
                yield return new WaitForSeconds(.5f);
                Assert.That(rocks[0].position.y,Is.LessThan(supportedY-.1f));
                if(recorder.Valid)report.gravityMilliseconds=recorder.LastValue/1000000d;
                report.passed=true;
            }
            finally
            {
                Directory.CreateDirectory("BuildReports/LooseStones");
                File.WriteAllText("BuildReports/LooseStones/Rest.json",JsonUtility.ToJson(report,true));
                Object.Destroy(root);
            }
            yield return null;
        }

        [System.Serializable] private class RestReport
        {
            public bool passed;
            public int sleeping,grabbed;
            public float maximumDrift,maximumRotationDrift;
            public double gravityMilliseconds;
        }
    }
}
