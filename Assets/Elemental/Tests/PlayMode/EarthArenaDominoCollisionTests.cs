using System.Collections;
using System.Linq;
using Elemental.Runtime.Characters;
using Elemental.Presentation.UI;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
    public sealed class EarthArenaDominoCollisionTests
    {
        private Scene _scene, _previous;
        private float _previousTimeScale;

        [UnitySetUp] public IEnumerator EnterProductionCombat()
        {
            _previous=SceneManager.GetActiveScene();_previousTimeScale=Time.timeScale;
            const string path="Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            Assert.That(SceneManager.GetSceneByPath(path).isLoaded,Is.False,
                "Use the focused test launcher; this fixture owns its additive production scene.");
            var loading=SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);
            Assert.That(loading,Is.Not.Null);
            double deadline=Time.realtimeSinceStartupAsDouble+180d;
            while(!loading.isDone && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            _scene=SceneManager.GetSceneByPath(path);
            Assert.That(loading.isDone && _scene.IsValid() && _scene.isLoaded,Is.True,"Production scene loading timed out.");
            SceneManager.SetActiveScene(_scene);
            var gates=All<EarthSceneReadinessGate>();
            Assert.That(gates.Length,Is.GreaterThan(0),"The saved production scene must expose world readiness.");
            while(gates.Any(g=>!g.IsReady) && !gates.Any(g=>g.Failed) && Time.realtimeSinceStartupAsDouble<deadline)
                yield return null;
            Assert.That(gates.All(g=>g.IsReady),Is.True,
                "World readiness failed/timed out: "+string.Join("; ",gates.Select(g=>g.Status)));
            var flow=All<FrontendFlowController>().Single();
            while(!flow.IsWorldReady && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(flow.IsWorldReady,Is.True,"The frontend never accepted the ready physical world.");
            yield return ProductionCombatTestFlow.BeginBotAfterReadiness(_scene);
            Assert.That(flow.State,Is.EqualTo(FrontendState.Combat));
            Assert.That(flow.MatchController.CombatAllowed,Is.True);
            Assert.That(Time.timeScale,Is.GreaterThan(0f),"Combat did not resume the production physics clock.");
            // BeginBot owns control activation. Disable autonomous opponents only after that boundary.
            foreach(var bot in All<EarthMvpBotController>())bot.enabled=false;
            double tickingDeadline=Time.realtimeSinceStartupAsDouble+5d;
            float began=Time.fixedTime;
            while(Time.fixedTime<=began && Time.realtimeSinceStartupAsDouble<tickingDeadline)yield return null;
            Assert.That(Time.fixedTime,Is.GreaterThan(began),"Production combat entered without advancing PhysX.");
        }

        [UnityTearDown] public IEnumerator RestoreOwnedSceneAndClock()
        {
            try
            {
                if(_previous.IsValid()&&_previous.isLoaded)SceneManager.SetActiveScene(_previous);
                if(_scene.IsValid()&&_scene.isLoaded)
                {
                    var unload=SceneManager.UnloadSceneAsync(_scene);
                    double deadline=Time.realtimeSinceStartupAsDouble+30d;
                    while(unload!=null&&!unload.isDone&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                    Assert.That(unload==null||unload.isDone,Is.True,"Owned production scene failed to unload within30s.");
                }
            }
            finally{Time.timeScale=_previousTimeScale;_scene=default;}
        }
        private T[] All<T>() where T:Component =>
            _scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
        private static IEnumerator WaitForContact(EarthDominoCollisionWitness witness,int maximumTicks)
        {
            float start=Time.fixedTime,limit=maximumTicks*Time.fixedDeltaTime;
            double deadline=Time.realtimeSinceStartupAsDouble+5d;
            while(witness.Count==0 && Time.fixedTime-start<limit && Time.realtimeSinceStartupAsDouble<deadline)
            {
                Assert.That(Time.timeScale,Is.GreaterThan(0f),"The production clock stopped while awaiting a real collision.");
                yield return null;
            }
            Assert.That(witness.Count>0 || Time.fixedTime-start>=limit,Is.True,
                "PhysX stopped advancing before the bounded collision observation completed.");
        }
        private static IEnumerator WaitSimulatedSeconds(float seconds)
        {
            float start=Time.time;double deadline=Time.realtimeSinceStartupAsDouble+5d;
            while(Time.time-start<seconds && Time.realtimeSinceStartupAsDouble<deadline)
            {
                Assert.That(Time.timeScale,Is.GreaterThan(0f),"The production clock stopped between contact episodes.");
                yield return null;
            }
            Assert.That(Time.time-start,Is.GreaterThanOrEqualTo(seconds),"Contact cooldown did not advance within5s.");
        }

        [UnityTest] public IEnumerator NewColumnFragmentCannotDamageNeighbourUntilDeliberateRelease()
        {
                var columns=All<EarthArenaStructure>().Where(x=>x.OrdinaryDamageEnabled&&x.name.Contains("Column")).Take(2).ToArray();
                Assert.That(columns.Length,Is.EqualTo(2));var source=columns[0];var target=columns[1];
                Collider targetShape=target.GetComponent<Collider>();Vector3 normal=target.transform.right.normalized;
                Assert.That(targetShape.Raycast(new Ray(targetShape.bounds.center+normal*20,-normal),out RaycastHit hit,40),Is.True);normal=hit.normal;
                for(int deliberate=0;deliberate<2;deliberate++)
                {
                    source.RestoreArenaStructure();target.RestoreArenaStructure();
                    Assert.That(source.TryPluckCell(source.GetComponent<Collider>().bounds.center,out IEarthPhysicalTarget physical),Is.True);
                    var piece=(EarthArenaPiece)physical;var body=piece.Body;var shape=piece.GetComponent<Collider>();
                    var witness=piece.GetComponent<EarthDominoCollisionWitness>()??piece.gameObject.AddComponent<EarthDominoCollisionWitness>();witness.Count=0;
                    foreach(var other in All<Collider>())if(other!=shape&&other!=targetShape)Physics.IgnoreCollision(shape,other,true);
                    foreach(var other in source.GetComponentsInChildren<EarthArenaPiece>(true))
                        if(other!=piece&&other.IsEarthTargetValid){other.Body.isKinematic=true;other.Body.detectCollisions=false;}
                    if(deliberate!=0){piece.OnEarthMagicGrabbed(EarthMagicGripKind.Telekinesis);piece.OnEarthMagicReleased(EarthMagicGripKind.Telekinesis);}
                    float extent=Vector3.Dot(new Vector3(Mathf.Abs(normal.x),Mathf.Abs(normal.y),Mathf.Abs(normal.z)),shape.bounds.extents);
                    body.position=hit.point+normal*(extent+.08f);body.mass=20;body.linearVelocity=-normal*20;
                    body.angularVelocity=Vector3.zero;body.isKinematic=false;body.detectCollisions=true;Physics.SyncTransforms();
                    float began=Time.time;
                    yield return WaitForContact(witness,6);
                    Assert.That(witness.Count,Is.GreaterThan(0),"Must measure an actual new-cell collision.");
                    Assert.That(Time.time-began,Is.LessThan(.20f),"Contact must occur inside the activation guard.");
                    if(deliberate==0){Assert.That(target.AccumulatedImpactImpulse,Is.Zero);Assert.That(target.ReleasedPieceCount,Is.Zero);}
                    else Assert.That(target.ReleasedPieceCount,Is.GreaterThan(0),"Deliberate released heavy fragment rearms immediately.");
                    body.detectCollisions=false;body.isKinematic=true;
                }
        }
        [UnityTest] public IEnumerator TwoProductionColumns_RejectTinyAndGrazing_AccumulateMeaningful_AdmitHeavy()
        {
            GameObject projectile=null;
            try
            {
                var columns=All<EarthArenaStructure>().Where(x=>x.OrdinaryDamageEnabled&&x.name.Contains("Column")).ToArray();
                Assert.That(columns.Length,Is.GreaterThanOrEqualTo(2));
                var first=columns[0];var second=columns.Skip(1).OrderBy(x=>(x.transform.position-first.transform.position).sqrMagnitude).First();
                Collider intact=first.GetComponent<Collider>();Assert.That(intact,Is.Not.Null);
                var origin=intact.bounds.center+first.transform.right*20;
                Assert.That(intact.Raycast(new Ray(origin,(intact.bounds.center-origin).normalized),out RaycastHit hit,40),Is.True);
                Vector3 normal=hit.normal.normalized,tangent=Vector3.Cross(normal,first.transform.up).normalized;
                if(tangent.sqrMagnitude<.1f)tangent=Vector3.Cross(normal,Vector3.forward).normalized;
                projectile=GameObject.CreatePrimitive(PrimitiveType.Cube);projectile.name="Domino real contact projectile";
                projectile.transform.localScale=Vector3.one*.2f;
                var body=projectile.AddComponent<Rigidbody>();body.useGravity=false;
                var plate=projectile.AddComponent<EarthArmorPiece>();var shape=projectile.GetComponent<Collider>();
                var witness=projectile.AddComponent<EarthDominoCollisionWitness>();
                plate.Configure(null,1234567,body,shape,projectile.GetComponent<MeshFilter>().sharedMesh);
                foreach(var other in All<Collider>())
                {
                    var owner=other.GetComponentInParent<EarthArenaStructure>();
                    if(owner!=first&&owner!=second)Physics.IgnoreCollision(shape,other,true);
                }
                uint shot=0;
                IEnumerator Fire(float mass,Vector3 velocity,bool grazing=false)
                {
                    witness.Count=0;witness.Closing=0;
                    plate.Activate(++shot,hit.point+normal*(grazing?.105f:.7f),Quaternion.identity);
                    body.mass=mass;body.position=hit.point+normal*(grazing?.105f:.7f);
                    plate.Release(velocity,2,1);shape.enabled=true;body.detectCollisions=true;Physics.SyncTransforms();
                    yield return WaitForContact(witness,35);
                    Assert.That(witness.Count,Is.GreaterThan(0),"Fixture must make a real PhysX contact, never pass from a miss.");
                    if(!grazing)Assert.That(witness.Closing,Is.GreaterThan(.75f),"Real frontal contact must verify the production normal/relative-velocity sign.");
                    shape.enabled=false;body.detectCollisions=false;body.linearVelocity=Vector3.zero;
                    yield return WaitSimulatedSeconds(.36f);
                }
                first.RestoreArenaStructure();second.RestoreArenaStructure();
                for(int i=0;i<8;i++)yield return Fire(.1f,-normal*8);
                Assert.That(first.AccumulatedImpactImpulse,Is.Zero,"Tiny contacts must not accumulate forever.");
                Assert.That(first.ReleasedPieceCount+second.ReleasedPieceCount,Is.Zero);
                yield return Fire(10,-normal*.05f+tangent*12,true);
                Assert.That(witness.Closing,Is.LessThan(.75f),"This test must actually remain grazing at contact.");
                Assert.That(first.AccumulatedImpactImpulse,Is.Zero);Assert.That(first.ReleasedPieceCount+second.ReleasedPieceCount,Is.Zero);
                int meaningful=0;
                while(first.ReleasedPieceCount==0&&meaningful<12)
                {
                    yield return Fire(2,-normal*8);meaningful++;
                    if(meaningful==1)
                    {
                        Assert.That(first.ReleasedPieceCount,Is.Zero,"One modest hit should not remove a column.");
                        Assert.That(first.AccumulatedImpactImpulse,Is.GreaterThan(0),"Actual frontal collision must be admitted, not silently rejected by an incorrect contact sign.");
                    }
                }
                Assert.That(first.ReleasedPieceCount,Is.GreaterThan(0),"Independent meaningful contacts still accumulate.");
                first.RestoreArenaStructure();second.RestoreArenaStructure();Physics.SyncTransforms();
                yield return Fire(20,-normal*10);
                Assert.That(first.ReleasedPieceCount,Is.GreaterThan(0),"A deliberately heavy real contact must still fracture.");
            }
            finally
            {
                if(projectile!=null)Object.Destroy(projectile);
            }
        }
    }
    public sealed class EarthDominoCollisionWitness:MonoBehaviour
    {
        public int Count;public float Closing;
        private void OnCollisionEnter(Collision hit)
        {Count++;if(hit.contactCount>0)Closing=Mathf.Max(0,Vector3.Dot(hit.relativeVelocity,hit.GetContact(0).normal));}
    }
}
