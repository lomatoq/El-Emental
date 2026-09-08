using System.Collections;
using System.Reflection;
using Elemental.Input.Actions;
using Elemental.Presentation.Camera;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
    public sealed class EarthWallChargeFeedbackPlayTests
    {
        private Scene _scene;
        [UnityTearDown] public IEnumerator Cleanup()
        {Time.timeScale=1;if(_scene.IsValid()&&_scene.isLoaded)yield return SceneManager.UnloadSceneAsync(_scene);}
        [UnityTest] public IEnumerator WallChargeFeedsExistingCameraEnvelopeAndClearsOnCancelAndRelease()
        {
            const string path="Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);_scene=SceneManager.GetSceneByPath(path);
            var gate=Find<EarthSceneReadinessGate>();double deadline=Time.realtimeSinceStartupAsDouble+125;
            while(!gate.IsReady&&!gate.Failed&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(gate.IsReady,Is.True,gate.Status);
            yield return ProductionCombatTestFlow.BeginBotAfterReadiness(_scene);Find<EarthMvpBotController>().enabled=false;
            var director=Find<EarthCameraDirector>();var look=director.GetComponent<EarthChargeCameraLookdevV2>();
            var router=director.Player.GetComponent<EarthActionRouterBehaviour>();
            Assert.That(look,Is.Not.Null);look.BindDirector(director);
            var pool=Find<EarthWallPool>();
            var wall=pool.Acquire(new Vector3(-3,120,15),new Vector3(3,120,15),Vector3.zero,2,.4f,0xAAF091,Vector3.up);
            Assert.That(wall,Is.Not.Null);deadline=Time.realtimeSinceStartupAsDouble+5;
            while(!wall.IsEmergenceComplete&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(wall.IsEmergenceComplete,Is.True);
            // Presentation adapter seam: actual charge state, explicit directed actor,
            // no gesture simulation here (covered by keyboard production tests).
            var owner=typeof(EarthActionRouterBehaviour).GetField("_heldPushWall",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.That(wall.TryBeginHeldPush(Vector3.forward),Is.True);owner.SetValue(router,wall);
            yield return new WaitForSeconds(1.4f);
            Assert.That(look.SampleChargeSources().WallPush,Is.GreaterThan(.95f));
            Assert.That(look.Charge01,Is.GreaterThan(.9f));
            Assert.That(look.ChargeFeedback.ChromaticAberration,Is.GreaterThan(.2f));
            Assert.That(look.ChargeFeedback.FieldOfViewDelta,Is.GreaterThan(8f));
            Assert.That(look.ChargeVignetteIntensity,Is.GreaterThan(.25f));
            System.IO.Directory.CreateDirectory("BuildReports/WallPushFeedback");
            ScreenCapture.CaptureScreenshot("BuildReports/WallPushFeedback/charge.png");
            yield return null;yield return null;
            wall.CancelHeldPush();
            Assert.That(look.SampleChargeSources().WallPush,Is.Zero,"Stale router reference cannot sustain a cancelled charge.");
            yield return new WaitForSeconds(1.5f);
            Assert.That(look.Charge01,Is.LessThan(.001f));
            Assert.That(wall.TryBeginHeldPush(Vector3.forward),Is.True);owner.SetValue(router,wall);
            yield return new WaitForSeconds(.6f);
            Assert.That(look.SampleChargeSources().WallPush,Is.GreaterThan(.2f));
            Assert.That(wall.ReleaseHeldPush(),Is.True);
            Assert.That(look.SampleChargeSources().WallPush,Is.Zero,"Physical coasting is not charging.");
            yield return new WaitForSeconds(1.5f);
            Assert.That(look.Charge01,Is.LessThan(.001f));
            Assert.That(look.ChargeFeedback.FieldOfViewDelta,Is.LessThan(.001f));
            owner.SetValue(router,null);pool.ReleaseTransient(wall);
        }
        private T Find<T>() where T:Component
        {foreach(var root in _scene.GetRootGameObjects()){var result=root.GetComponentInChildren<T>(true);if(result!=null)return result;}throw new System.InvalidOperationException(typeof(T).Name);}
    }
}
