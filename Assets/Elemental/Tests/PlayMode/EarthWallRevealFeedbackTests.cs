using Elemental.Simulation.Bending;
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
    public sealed class EarthWallRevealFeedbackTests
    {
        private Scene scene;
        [UnityTearDown] public IEnumerator Cleanup()
        {Time.timeScale=1;if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);}
        [UnityTest] public IEnumerator EmergenceAndFirstCrackEmitDenseDistributedFeedbackAndResetOnReuse()
        {
            const string path="Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);scene=SceneManager.GetSceneByPath(path);
            var gate=Find<EarthSceneReadinessGate>();float deadline=Time.realtimeSinceStartup+130;
            while(!gate.IsReady&&!gate.Failed&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(gate.IsReady,Is.True,gate.Status);yield return ProductionCombatTestFlow.BeginBotAfterReadiness(scene);
            Find<EarthMvpBotController>().enabled=false;
            var pool=Find<EarthWallPool>();var hub=Find<EarthMaterialFeedbackHub>();EarthWall wall=null;
            int emergeDust=0,emergeChips=0,crackDust=0,crackChips=0,crackFrames=0,lastCrackFrame=-1;
            float minimumX=float.PositiveInfinity,maximumX=float.NegativeInfinity;
            void Observe(EarthMaterialFeedbackCue cue)
            {
                if(wall==null||cue.SourceId!=wall.WallId)return;
                if(cue.Kind==EarthMaterialFeedbackKind.Emerge){emergeDust+=cue.DustCount;emergeChips+=cue.ChipCount;}
                if(cue.Kind!=EarthMaterialFeedbackKind.Fracture)return;
                crackDust+=cue.DustCount;crackChips+=cue.ChipCount;
                minimumX=Mathf.Min(minimumX,cue.Point.x);maximumX=Mathf.Max(maximumX,cue.Point.x);
                if(lastCrackFrame!=Time.frameCount){lastCrackFrame=Time.frameCount;crackFrames++;}
            }
            hub.Presented+=Observe;
            try
            {
                wall=pool.Acquire(new Vector3(-3,120,15),new Vector3(3,120,15),Vector3.zero,2.5f,.4f,supportNormal:Vector3.up);
                deadline=Time.realtimeSinceStartup+5;while(!wall.IsEmergenceComplete&&Time.realtimeSinceStartup<deadline)yield return null;
                yield return null;
                Assert.That(emergeDust,Is.GreaterThanOrEqualTo(500));Assert.That(emergeChips,Is.GreaterThanOrEqualTo(100));
                Assert.That(crackDust,Is.Zero,"Fresh rise cannot trigger crack feedback before interaction.");
                wall.RevealCracks();
                yield return new WaitForSeconds(.35f);
                Directory.CreateDirectory("BuildReports/WallRevealFeedback");
                File.WriteAllText("BuildReports/WallRevealFeedback/result.txt",$"emergeDust={emergeDust}; emergeChips={emergeChips}; crackDust={crackDust}; crackChips={crackChips}; crackFrames={crackFrames}; span={maximumX-minimumX}");
                Assert.That(crackDust,Is.GreaterThanOrEqualTo(250));Assert.That(crackChips,Is.GreaterThanOrEqualTo(50));
                Assert.That(crackFrames,Is.EqualTo(3));Assert.That(maximumX-minimumX,Is.GreaterThan(2f));
                Assert.That(wall.IsCollapsing,Is.False);
                int before=crackDust;wall.RevealCracks();wall.RevealCracks();yield return new WaitForSeconds(.25f);
                Assert.That(crackDust,Is.EqualTo(before),"Repeated calls must not restart the first-crack burst.");
                pool.ReleaseTransient(wall);
                wall=pool.Acquire(new Vector3(-3,120,15),new Vector3(3,120,15),Vector3.zero,2.5f,.4f,supportNormal:Vector3.up);
                deadline=Time.realtimeSinceStartup+5;while(!wall.IsEmergenceComplete&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(wall.HasRevealedCracks,Is.False);
                wall.RevealCracks();yield return new WaitForSeconds(.35f);
                Assert.That(crackDust,Is.GreaterThan(before+200),"A new pooled lifetime restores its first-crack feedback.");
            }
            finally{hub.Presented-=Observe;}
        }
        private T Find<T>()where T:Component
        {foreach(var root in scene.GetRootGameObjects()){var c=root.GetComponentInChildren<T>(true);if(c!=null)return c;}Assert.Fail(typeof(T).Name);return null;}
    }
}
