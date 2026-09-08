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
using UnityEngine.UIElements;
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
            Assert.That(Time.timeScale,Is.GreaterThan(0));Assert.That(menu.OwnsPresentation,Is.True);
            yield return AssertLivingMenu(duel);
            flow.OpenSettings();yield return AssertLivingMenu(duel);flow.Back();
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Capture("Main");AssertFramed("Main",duel,output,menu,brain);
            Assert.That(flow.BeginBot(),Is.True);
            deadline=Time.realtimeSinceStartupAsDouble+20;
            while(flow.State!=FrontendState.Combat&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(flow.State,Is.EqualTo(FrontendState.Combat));
            // Let UI Toolkit lay out the newly visible combat HUD before sampling screen coordinates.
            yield return null;yield return null;
            var hud=Find<EarthDuelHud>();
            var pauseButton=hud.GetComponent<UIDocument>().rootVisualElement.Q<Button>("pause-match");
            Assert.That(pauseButton.resolvedStyle.transformOrigin.x,Is.EqualTo(pauseButton.layout.width*.5f).Within(.1f));
            Assert.That(pauseButton.resolvedStyle.transformOrigin.y,Is.EqualTo(pauseButton.layout.height*.5f).Within(.1f));
            Vector2 pauseCenter=pauseButton.worldBound.center;
            var held=typeof(EarthDuelHud).GetField("_pauseHeld",BindingFlags.NonPublic|BindingFlags.Instance);
            held.SetValue(hud,true);yield return null;yield return null;
            Assert.That(Vector2.Distance(pauseButton.worldBound.center,pauseCenter),Is.LessThan(.2f),"Pause press must scale around the icon center.");
            held.SetValue(hud,false);
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
            yield return AssertLivingMenu(duel);
        }
        private IEnumerator AssertLivingMenu(EarthMvpDuelController duel)
        {
            var sky=Find<Elemental.Presentation.Rendering.CelestialSystemBehaviour>();
            var animator=duel.PlayerTransform.GetComponentInChildren<EarthAnimationDriver>(true);
            float time=Time.time,phase=sky.Snapshot.TimeOfDay01,round=duel.RoundRemainingSeconds;
            var pose=animator.GetCurrentAnimatorStateInfo(0);
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(Time.time-time,Is.GreaterThan(.15f),"The main-menu world clock must advance.");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(phase*360,sky.Snapshot.TimeOfDay01*360)),Is.GreaterThan(.0001f),"The menu day/night cycle must advance.");
            var nextPose=animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(nextPose.fullPathHash!=pose.fullPathHash||Mathf.Abs(nextPose.normalizedTime-pose.normalizedTime)>.0001f,Is.True,"The menu character must keep animating.");
            Assert.That(duel.CombatAllowed,Is.False);
            Assert.That(duel.RoundRemainingSeconds,Is.EqualTo(round).Within(.001f),"Live scenery must not start the match clock.");
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
