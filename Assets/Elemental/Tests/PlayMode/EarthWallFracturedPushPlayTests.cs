using System.Collections;
using System.Reflection;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
    public sealed class EarthWallFracturedPushPlayTests
    {
        private Scene _scene;
        [UnityTearDown] public IEnumerator Cleanup()
        {Time.timeScale=1;if(_scene.IsValid()&&_scene.isLoaded)yield return SceneManager.UnloadSceneAsync(_scene);}
        [UnityTest] public IEnumerator CohesiveWallCancelsAndAcceptsRepeatedShovesWithoutHealing()
        {
            const string path="Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);_scene=SceneManager.GetSceneByPath(path);
            var gate=Find<EarthSceneReadinessGate>();double deadline=Time.realtimeSinceStartupAsDouble+125;
            while(!gate.IsReady&&!gate.Failed&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(gate.IsReady,Is.True,gate.Status);
            yield return ProductionCombatTestFlow.BeginBotAfterReadiness(_scene);Find<EarthMvpBotController>().enabled=false;
            var pool=Find<EarthWallPool>();
            var wall=pool.Acquire(new Vector3(-3,120,15),new Vector3(3,120,15),Vector3.zero,2,.4f,0xAAF081,Vector3.up);
            Assert.That(wall,Is.Not.Null);deadline=Time.realtimeSinceStartupAsDouble+5;
            while(!wall.IsEmergenceComplete&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(wall.IsEmergenceComplete,Is.True);
            // Enter the production fractured representation without inventing an impact
            // that might accidentally destroy the very connected fixture being tested.
            typeof(EarthWall).GetMethod("BeginCohesiveFracture",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(wall,null);
            var bodies=(Rigidbody[])typeof(EarthWall).GetField("_pieceBodies",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(wall);
            var bonds=(bool[])typeof(EarthWall).GetField("_bondBroken",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(wall);
            int count=wall.RemainingBondCount;float mass=wall.Body.mass;var start=new Vector3[bodies.Length];
            for(int i=0;i<bodies.Length;i++)start[i]=bodies[i].position;
            Assert.That(wall.TryBeginHeldPush(Vector3.forward),Is.True);
            for(int frame=0;frame<5;frame++)yield return new WaitForFixedUpdate();
            for(int i=0;i<bodies.Length;i++)Assert.That(Vector3.Distance(start[i],bodies[i].position),Is.LessThan(.002f));
            wall.CancelHeldPush();Assert.That(wall.RemainingBondCount,Is.EqualTo(count));
            Assert.That(wall.IsCollapsing,Is.True);Assert.That(wall.Body.mass,Is.EqualTo(mass));
            Assert.That(wall.Body.isKinematic,Is.True);
            for(int trial=0;trial<2;trial++)
            {
                Assert.That(wall.TryBeginHeldPush(Vector3.forward),Is.True,"The same existing cohesive component must remain pushable.");
                yield return new WaitForFixedUpdate();
                var before=(bool[])bonds.Clone();
                Assert.That(wall.ReleaseHeldPush(),Is.True);Assert.That(wall.ReleaseHeldPush(),Is.False);
                Assert.That(wall.IsCollapsing,Is.True);Assert.That(wall.Body.isKinematic,Is.True);
                Assert.That(wall.Body.mass,Is.EqualTo(mass),"Retired shell cannot reacquire child mass.");
                for(int i=0;i<bonds.Length;i++)if(before[i])Assert.That(bonds[i],Is.True,"Push cannot heal a broken bond.");
                Assert.That(wall.RemainingBondCount,Is.GreaterThan(0),"Push must preserve interior cohesion.");
                yield return new WaitForFixedUpdate();
                float forwardMomentum=0;
                foreach(var body in bodies)if(body!=null&&!body.isKinematic)forwardMomentum+=Vector3.Dot(body.linearVelocity,Vector3.forward)*body.mass;
                Assert.That(forwardMomentum,Is.GreaterThan(1f),"Impulse belongs to the real children.");
            }
            pool.ReleaseTransient(wall);
        }
        private T Find<T>() where T:Component
        {foreach(var root in _scene.GetRootGameObjects()){var result=root.GetComponentInChildren<T>(true);if(result!=null)return result;}throw new System.InvalidOperationException(typeof(T).Name);}
    }
}
