using System.Collections;
using System.Linq;
using Elemental.Presentation.UI;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using Elemental.Simulation.Combat;
using Elemental.Simulation.Time;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
namespace Elemental.Tests.PlayMode
{
    public sealed partial class MatchStageProductionTests
    {
        private Scene scene,previous;
        private ProductionCaptureResolution captureResolution;
        private FrontendFlowController preferenceOwner;
        private bool priorReducedMotion;
        [UnitySetUp] public IEnumerator SetCaptureSize(){captureResolution=new ProductionCaptureResolution();yield return null;}
        private const string Path="Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private T Find<T>() where T:Component=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).Single();
        [UnityTearDown] public IEnumerator Cleanup()
        {if(preferenceOwner!=null){var p=preferenceOwner.Preferences;p.Set(p.MasterVolume,p.UIVolume,p.Sensitivity,priorReducedMotion);preferenceOwner=null;}captureResolution?.Dispose();captureResolution=null;if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);}
        [UnityTest] public IEnumerator ResultFramingUsesStandingProxyWhenLiveHeadHasCollapsed()
        {
            previous=SceneManager.GetActiveScene();Assert.That(SceneManager.GetSceneByPath(Path).isLoaded,Is.False);
            yield return SceneManager.LoadSceneAsync(Path,LoadSceneMode.Additive);scene=SceneManager.GetSceneByPath(Path);SceneManager.SetActiveScene(scene);
            var flow=Find<FrontendFlowController>();var gate=Find<EarthSceneReadinessGate>();var stage=Find<MatchPresentationStage>();
            var duel=flow.MatchController;var hud=Find<EarthDuelHud>();
            double deadline=Time.realtimeSinceStartupAsDouble+145;
            while((!gate.IsReady||flow.State==FrontendState.Loading||!stage.Ready)&&stage.Failure==null&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(stage.Failure,Is.Null);Assert.That(stage.Ready,Is.True);
            yield return captureResolution.WaitForRenderedSize(Find<Elemental.Presentation.Rendering.CelestialSystemBehaviour>().TargetCamera);
            hud.SetLocalPerspective(EarthDuelFighterId.Player,true);
            var animator=duel.PlayerTransform.GetComponentInChildren<Animator>();
            var motor=duel.PlayerTransform.GetComponent<PlanetMotor>();
            Transform head=animator.GetBoneTransform(HumanBodyBones.Head);Vector3 original=head.localPosition;
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var begin=typeof(MatchPresentationStage).GetMethod("BeginResult",flags);
            var height=typeof(MatchPresentationStage).GetField("stageHeight",flags);
            try
            {
                begin.Invoke(stage,null);float standing=(float)height.GetValue(stage);
                Assert.That(standing,Is.GreaterThan(1),"Fixture must cache a full standing rig.");
                // Reproduce the final-death framing input without advancing or re-baking its standing proxy.
                head.position=motor.SupportFeetPoint(motor.LocalUp)+motor.LocalUp*.05f;
                begin.Invoke(stage,null);float collapsed=(float)height.GetValue(stage);
                Assert.That(collapsed,Is.EqualTo(standing).Within(.001f),"A collapsed live head must not shrink the cached standing camera subject.");
            }
            finally{head.localPosition=original;stage.enabled=false;}
        }
        [UnityTest] public IEnumerator StableReplicaIdsAndRepeatedResultMainDisableLeases()
        {
            previous=SceneManager.GetActiveScene();Assert.That(SceneManager.GetSceneByPath(Path).isLoaded,Is.False);
            yield return SceneManager.LoadSceneAsync(Path,LoadSceneMode.Additive);scene=SceneManager.GetSceneByPath(Path);SceneManager.SetActiveScene(scene);
            var flow=Find<FrontendFlowController>();var gate=Find<EarthSceneReadinessGate>();var stage=Find<MatchPresentationStage>();
            var duel=flow.MatchController;var hud=Find<EarthDuelHud>();var camera=Find<CinematicMenuCamera>();
            double deadline=Time.realtimeSinceStartupAsDouble+145;
            while((!gate.IsReady||flow.State==FrontendState.Loading||!stage.Ready)&&stage.Failure==null&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(stage.Failure,Is.Null);Assert.That(stage.Ready,Is.True);
            yield return captureResolution.WaitForRenderedSize(Find<Elemental.Presentation.Rendering.CelestialSystemBehaviour>().TargetCamera);
            Assert.That(stage.ResultsActive,Is.False);Assert.That(stage.ActiveLightCount,Is.Zero);
            preferenceOwner=flow;priorReducedMotion=flow.Preferences.ReducedMotion;
            flow.Preferences.Set(flow.Preferences.MasterVolume,flow.Preferences.UIVolume,flow.Preferences.Sensitivity,false);flow.ApplyPreferences();
            int pooled=stage.GetComponentsInChildren<Transform>(true).Length;
            Assert.That(stage.GetComponentsInChildren<Collider>(true),Is.Empty);
            Assert.That(stage.GetComponentsInChildren<Rigidbody>(true),Is.Empty);
            Assert.That(stage.GetComponentsInChildren<Animator>(true),Is.Empty);
            var skins=stage.GetComponentsInChildren<SkinnedMeshRenderer>(true);Assert.That(skins.Length,Is.GreaterThan(0));
            foreach(var skin in skins)foreach(var bone in skin.bones)if(bone!=null)Assert.That(bone.IsChildOf(stage.transform),Is.True);
            for(int cycle=0;cycle<6;cycle++)
            {
                var sky=Find<Elemental.Presentation.Rendering.CelestialSystemBehaviour>(); sky.SetTimeOfDayForQa(cycle<3?.25f:.75f); sky.EvaluatePresentationForQa();
                if(cycle==0||cycle==3){yield return new WaitForEndOfFrame();Capture("Main-"+(cycle<3?"day":"night"));}
                Assert.That(flow.BeginBot(),Is.True);deadline=Time.realtimeSinceStartupAsDouble+20;
                while(flow.State==FrontendState.Starting&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                Assert.That(flow.State,Is.EqualTo(FrontendState.Combat));
                var local=cycle%3==1?EarthDuelFighterId.Bot:EarthDuelFighterId.Player;hud.SetLocalPerspective(local,true);
                duel.ConfigureOnlineAuthority(false); // Feed authoritative replica scores without waiting the full round duration.
                uint before=stage.ResultGeneration;
                Assert.That(duel.ApplyReplicaMatch(100,100,4,cycle%3==2?4:2,0,true),Is.True);
                yield return new WaitForEndOfFrame();
                var resultRoot=hud.GetComponent<UIDocument>().rootVisualElement.Q("round-result");
                var retry=resultRoot.Q<Button>("restart-round");var back=resultRoot.Q<Button>("reference-result-menu");
                Assert.That(resultRoot.enabledInHierarchy,Is.False,"Invisible result controls must not receive input during reveal.");
                using(var submit=NavigationSubmitEvent.GetPooled())retry.SendEvent(submit);
                using(var submit=NavigationSubmitEvent.GetPooled())back.SendEvent(submit);
                Assert.That(duel.IsRoundOver,Is.True);Assert.That(flow.State,Is.EqualTo(FrontendState.Combat));
                hud.SetLocalPerspective(local,false); // Host-owned Retry remains disabled beyond the cosmetic gate.
                deadline=Time.realtimeSinceStartupAsDouble+1.1;
                while(Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                Assert.That(resultRoot.enabledInHierarchy,Is.True);Assert.That(back.enabledInHierarchy,Is.True);
                Assert.That(retry.enabledInHierarchy,Is.False);Assert.That(retry.text,Is.EqualTo("WAITING FOR HOST"));
                using(var submit=NavigationSubmitEvent.GetPooled())retry.SendEvent(submit);
                Assert.That(duel.IsRoundOver,Is.True,"Reveal must not override online restart authority.");
                Assert.That(stage.ResultsActive,Is.True);Assert.That(stage.ResultGeneration,Is.EqualTo(before+1));
                Assert.That(stage.SubjectId,Is.EqualTo(local));Assert.That(stage.Outcome,Is.EqualTo(cycle%3==0?MatchStageOutcome.Victory:cycle%3==1?MatchStageOutcome.Defeat:MatchStageOutcome.Draw));
                Assert.That(stage.HasWinner,Is.EqualTo(cycle%3!=2));Assert.That(stage.ActiveLightCount,Is.EqualTo(2));Assert.That(stage.ActiveStoneCount,Is.EqualTo(12));
                Assert.That(camera.OwnsResultsStage,Is.True);
                Assert.That(stage.GetComponentsInChildren<Transform>(true).Length,Is.EqualTo(pooled));
                var source=(local==EarthDuelFighterId.Player?duel.PlayerTransform:duel.BotTransform).GetComponentsInChildren<Renderer>(true);
                Assert.That(source.Any(r=>r.forceRenderingOff),Is.True);
                float oldScale=Time.timeScale;
                try
                {
                    Time.timeScale=0;
                    yield return new WaitForSecondsRealtime(.2f);
                    Assert.That(stage.ResultsActive&&camera.OwnsResultsStage,Is.True,"Results presentation must survive paused simulation.");
                    yield return new WaitForEndOfFrame();
                    Capture("Result-"+stage.Outcome+"-"+(cycle<3?"day":"night"));
                }
                finally{Time.timeScale=oldScale;}
                stage.enabled=false;yield return null;Assert.That(stage.ActiveLightCount,Is.Zero);Assert.That(camera.OwnsResultsStage,Is.False);
                stage.enabled=true;yield return null;Assert.That(stage.ResultGeneration,Is.EqualTo(before+2));
                using(var down=PointerDownEvent.GetPooled(new Event{type=EventType.MouseDown,button=0,mousePosition=back.worldBound.center}))
                {down.target=back;back.SendEvent(down);}
                yield return null;
                using(var up=PointerUpEvent.GetPooled(new Event{type=EventType.MouseUp,button=0,mousePosition=back.worldBound.center}))
                {up.target=back;back.SendEvent(up);}
                yield return new WaitForSecondsRealtime(.2f);Assert.That(flow.State,Is.EqualTo(FrontendState.Main),"Revealed Main button must execute the production exit.");Assert.That(stage.ResultsActive,Is.False);Assert.That(stage.ActiveLightCount,Is.Zero);Assert.That(camera.OwnsResultsStage,Is.False);
                duel.ConfigureOnlineAuthority(true);
            }
        }
        private static void Capture(string name)
        {
            const string folder="BuildReports/HardPolish/G08/Production";
            System.IO.Directory.CreateDirectory(folder);
            ProductionCaptureResolution.SaveScreen(folder+"/"+name+".png");
        }
    }
}
