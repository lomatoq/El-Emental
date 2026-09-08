using System.Collections;
using System.IO;
using System.Reflection;
using Elemental.Presentation.Animation;
using Elemental.Presentation.UI;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
    public sealed class MenuCameraProductionTests
    {
        private Scene scene,previous;
        private const string Folder="BuildReports/MenuCamera";
        [UnityTearDown] public IEnumerator Restore()
        {
            Time.timeScale=1;
            if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
        }
        [UnityTest] public IEnumerator MainRestoredMatchAndPauseFrameActualCharacterThroughBrain()
        {
            Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/framing.txt","");
            previous=SceneManager.GetActiveScene();const string path="Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);scene=SceneManager.GetSceneByPath(path);SceneManager.SetActiveScene(scene);
            var gate=Find<EarthSceneReadinessGate>();var flow=Find<FrontendFlowController>();
            double deadline=Time.realtimeSinceStartupAsDouble+130;
            while(!gate.IsReady&&!gate.Failed&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(gate.IsReady,Is.True,gate.Status);
            while(flow.State==FrontendState.Loading&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            var menu=Find<CinematicMenuCamera>();var duel=Find<EarthMvpDuelController>();
            var output=(Camera)typeof(CinematicMenuCamera).GetField("outputCamera",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(menu);
            var brain=output.GetComponent<CinemachineBrain>();
            yield return new WaitForSecondsRealtime(flow.PresentationTransitionSeconds+.15f);
            Assert.That(Time.timeScale,Is.Zero);Assert.That(menu.OwnsPresentation,Is.True);
            yield return Capture("Main");AssertFramed("Main",duel,output,menu,brain);
            Assert.That(flow.BeginBot(),Is.True);
            deadline=Time.realtimeSinceStartupAsDouble+20;
            while(flow.State!=FrontendState.Combat&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(flow.State,Is.EqualTo(FrontendState.Combat));
            bool combatIgnoreScale=brain.IgnoreTimeScale;var combatUpdate=brain.UpdateMethod;var combatBlendUpdate=brain.BlendUpdateMethod;
            flow.Pause();yield return new WaitForSecondsRealtime(flow.PresentationTransitionSeconds+.15f);
            Assert.That(Time.timeScale,Is.Zero);Assert.That(menu.OwnsPresentation,Is.True);
            yield return Capture("Pause");AssertFramed("Pause",duel,output,menu,brain);
            // Actor restore/teleport can finish after Enter took its first framing
            // sample. Exercise that invalidation while physics is genuinely frozen.
            duel.PlayerTransform.position+=duel.PlayerTransform.right*2f;Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(.3f);
            AssertFramed("Pause after actor warp",duel,output,menu,brain);
            flow.Resume();deadline=Time.realtimeSinceStartupAsDouble+flow.PresentationTransitionSeconds+1;
            while(menu.OwnsPresentation&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(menu.OwnsPresentation,Is.False);
            Assert.That(brain.IgnoreTimeScale,Is.EqualTo(combatIgnoreScale));
            Assert.That(brain.UpdateMethod,Is.EqualTo(combatUpdate));Assert.That(brain.BlendUpdateMethod,Is.EqualTo(combatBlendUpdate));
            flow.EndMatch();deadline=Time.realtimeSinceStartupAsDouble+30;
            while((flow.State!=FrontendState.Main||duel.ArenaResetInProgress)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(flow.State,Is.EqualTo(FrontendState.Main));
            yield return new WaitForSecondsRealtime(flow.PresentationTransitionSeconds+.15f);
            yield return Capture("Main-after-restore");AssertFramed("Main after restore",duel,output,menu,brain);
        }
        private static void AssertFramed(string phase,EarthMvpDuelController duel,Camera output,CinematicMenuCamera menu,CinemachineBrain brain)
        {
            var timing=System.Diagnostics.Stopwatch.StartNew();menu.Reframe(false);timing.Stop();
            File.AppendAllText(Folder+"/framing.txt",phase+" reframe CPU ms="+timing.Elapsed.TotalMilliseconds.ToString("F4")+"\n");
            var driver=duel.PlayerTransform.GetComponentInChildren<EarthAnimationDriver>(true);
            var animator=driver.Animator;
            Vector3 head=output.WorldToViewportPoint(animator.GetBoneTransform(HumanBodyBones.Head).position);
            Vector3 foot=output.WorldToViewportPoint(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position);
            Vector3 center=(head+foot)*.5f;
            Vector3 subject=output.WorldToViewportPoint(menu.SubjectCenter);
            File.AppendAllText(Folder+"/framing.txt",$"{phase}: center={center} subject={subject} head={head} foot={foot} scale={Time.timeScale} ignoreScale={brain.IgnoreTimeScale} update={brain.UpdateMethod} camera={output.transform.position}\n");
            Assert.That(menu.OwnsPresentation,Is.True);Assert.That(brain.IgnoreTimeScale,Is.True);
            Assert.That(center.z,Is.GreaterThan(0));Assert.That(center.x,Is.InRange(.54f,.92f),phase+" character must appear right of sidebar.");
            Assert.That(subject.z,Is.GreaterThan(0));Assert.That(subject.x,Is.EqualTo(.78f).Within(.025f));Assert.That(subject.y,Is.EqualTo(.58f).Within(.025f),"Keep the character lifted into the requested portrait area.");
            Assert.That(head.y,Is.InRange(.15f,.97f));Assert.That(foot.y,Is.InRange(.02f,.8f));
            Assert.That(Mathf.Abs(head.y-foot.y),Is.GreaterThan(.25f),phase+" must focus the actual character, not distant scenery.");
        }
        private static IEnumerator Capture(string name)
        {yield return new WaitForEndOfFrame();var frame=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Folder+"/"+name+".png",frame.EncodeToPNG());Object.Destroy(frame);}
        private T Find<T>()where T:Component
        {foreach(var root in scene.GetRootGameObjects()){var found=root.GetComponentInChildren<T>(true);if(found!=null)return found;}return null;}
    }
}
