using System.Collections;
using System.IO;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class WallIntactPresentationProductionTests
    {
        private Scene _previous,_scene;
        [UnityTearDown] public IEnumerator Cleanup()
        { Time.timeScale=1; if(_previous.IsValid()&&_previous.isLoaded)SceneManager.SetActiveScene(_previous);
          if(_scene.IsValid()&&_scene.isLoaded)yield return SceneManager.UnloadSceneAsync(_scene); }

        [UnityTest] public IEnumerator NarrowProductionWallStartsSeamlessAndCracksWithoutBreakingItsStandingGraph()
        {
            _previous=SceneManager.GetActiveScene();const string path="Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);
            _scene=SceneManager.GetSceneByPath(path);SceneManager.SetActiveScene(_scene);
            var gate=Find<EarthSceneReadinessGate>();float deadline=Time.realtimeSinceStartup+130;
            while(!gate.IsReady&&!gate.Failed&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(gate.IsReady,Is.True,gate.Status);
            yield return ProductionCombatTestFlow.BeginBotAfterReadiness(_scene);
            var duel=Find<EarthMvpDuelController>();var bot=duel.BotTransform.GetComponent<EarthMvpBotController>();if(bot!=null)bot.enabled=false;
            var pool=Find<EarthWallPool>();var actor=duel.PlayerTransform;
            Vector3 up=actor.GetComponent<PlanetMotor>().LocalUp;
            Vector3 ahead=Vector3.ProjectOnPlane(actor.forward,up).normalized;
            Vector3 right=Vector3.Cross(up,ahead).normalized;
                        RaycastHit floor=default; bool found=false;
            for(int i=0;i<16&&!found;i++)
            {
                Vector3 offset=Quaternion.AngleAxis(i*22.5f,up)*ahead*4;
                if(!UnityEngine.Physics.Raycast(actor.position+offset+up*4,-up,out var hit,12f,~0,QueryTriggerInteraction.Ignore))continue;
                var structure=hit.collider.GetComponentInParent<EarthArenaStructure>();
                if(structure==null||structure.OrdinaryDamageEnabled)continue;
                floor=hit; found=true;
            }
            Assert.That(found,Is.True,"A clear exposed FloorBase is needed for the visual proof.");
            using var marker=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"Elemental.Earth.Wall.DimensionTopology",16);
            var timer=System.Diagnostics.Stopwatch.StartNew();
            var wall=pool.Acquire(floor.point-right*.6f,floor.point+right*.6f,Find<VoxelPlanetBehaviour>().transform.position,3f,.55f,supportNormal:floor.normal);
            timer.Stop(); double coldAcquireMs=timer.Elapsed.TotalMilliseconds;
            Assert.That(wall,Is.Not.Null);
            deadline=Time.realtimeSinceStartup+4;
            while(!wall.IsEmergenceComplete&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(wall.IsEmergenceComplete,Is.True);
            Assert.That(wall.HasRevealedCracks,Is.False);
            var filter=wall.VisualEmergenceRoot.GetComponent<MeshFilter>();
            Mesh fresh=filter.sharedMesh;int bonds=wall.RemainingBondCount;
            int pieces=wall.StructureRuntime.PieceCount;
            float mass=wall.EstimatedMass;
            Directory.CreateDirectory("BuildReports/WallIntact");
            yield return Capture("fresh");
            // A low accepted physical disturbance reveals the prepared assembly;
            // it is intentionally below the damage threshold for breaking bonds.
            wall.ApplyRockImpact(wall.transform.position,ahead,1f);
            Assert.That(wall.HasRevealedCracks,Is.True);
            Assert.That(filter.sharedMesh,Is.Not.SameAs(fresh));
            Assert.That(wall.IsCollapsing,Is.False);
            Assert.That(wall.RemainingBondCount,Is.EqualTo(bonds));
            Assert.That(wall.StructureRuntime.PieceCount,Is.EqualTo(pieces));
            Assert.That(wall.EstimatedMass,Is.EqualTo(mass));
            yield return Capture("cracked-standing");
            File.WriteAllText("BuildReports/WallIntact/result.txt",$"pieces={pieces}; bonds={bonds}; mass={mass}; coldAcquireMs={coldAcquireMs:F3}; fresh={fresh.name}; cracked={filter.sharedMesh.name}");
            Assert.That(pool.ReleaseTransient(wall),Is.True);
            var reused=pool.Acquire(floor.point-right*.6f,floor.point+right*.6f,Find<VoxelPlanetBehaviour>().transform.position,3f,.55f,supportNormal:floor.normal);
            Assert.That(reused.HasRevealedCracks,Is.False,"Reusing an identical cached graph resets the fresh appearance.");
        }
        private static IEnumerator Capture(string name)
        {yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes("BuildReports/WallIntact/"+name+".png",image.EncodeToPNG());Object.Destroy(image);}
        private T Find<T>()where T:Component
        {foreach(var root in _scene.GetRootGameObjects()){var c=root.GetComponentInChildren<T>(true);if(c!=null)return c;}Assert.Fail(typeof(T).Name);return null;}
    }
}
