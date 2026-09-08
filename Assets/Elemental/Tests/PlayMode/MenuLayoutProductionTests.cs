#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Elemental.Presentation.UI;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
namespace Elemental.Tests.PlayMode
{
    public sealed class MenuLayoutProductionTests
    {
        private const string Folder="BuildReports/MenuLayouts";
        private Scene scene,previous;
        private readonly StoneSkinPlayTests captureSize=new StoneSkinPlayTests();
        private Unity.Profiling.ProfilerRecorder layoutRecorder;
        [UnityTearDown] public IEnumerator Restore()
        {
            Time.timeScale=1;
            layoutRecorder.Dispose();
            typeof(StoneSkinPlayTests).GetMethod("RestoreGameView",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(captureSize,null);
            if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
        }
        [UnityTest] public IEnumerator ProductionMenusAndResultsUseEditableLayoutsAndOneButtonPlate()
        {
            Directory.CreateDirectory(Folder);
            typeof(StoneSkinPlayTests).GetMethod("BindCapture",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(captureSize,new object[]{new Vector2Int(1920,1080)});
            previous=SceneManager.GetActiveScene();
            const string path="Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);scene=SceneManager.GetSceneByPath(path);SceneManager.SetActiveScene(scene);
            var gate=Find<EarthSceneReadinessGate>();double deadline=Time.realtimeSinceStartupAsDouble+130;
            while(!gate.IsReady&&!gate.Failed&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(gate.IsReady,Is.True,gate.Status);
            var view=Find<FrontendMenuView>();var flow=Find<FrontendFlowController>();
            while(flow.State==FrontendState.Loading&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            var theme=AssetDatabase.LoadAssetAtPath<ElementalUITheme>("Assets/Elemental/Content/UI/Frontend/ElementalUITheme.asset");
            Assert.That(theme.menuPresentation.Get(MenuScreenId.Sidebar).elements.Count,Is.GreaterThan(20));
            layoutRecorder=Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Scripts,"Elemental.MenuLayout.Apply",64);
            long totalNs=0,peakNs=0;int samples=0;
            for(int frame=0;frame<64;frame++)
            {yield return null;long ns=layoutRecorder.LastValue;if(ns>0){totalNs+=ns;peakNs=System.Math.Max(peakNs,ns);samples++;}}
            Assert.That(samples,Is.GreaterThan(0));
            var music=flow.GetComponent<FrontendMusicDirector>();var feedback=Find<UIAudioFeedback>();
            Assert.That(theme.frontendAudio,Is.Not.Null);Assert.That(music,Is.Not.Null);
            Assert.That(theme.frontendAudio.mainMenu.name,Is.EqualTo("Main Menu"));
            Assert.That(theme.frontendAudio.game.name,Is.EqualTo("Sky Temple Gate"));
            Assert.That(theme.frontendAudio.panelMove.name,Is.EqualTo("Panel Move Sound"));
            Assert.That(Time.timeScale,Is.Zero);Assert.That(music.MenuGain,Is.GreaterThan(0));
            Assert.That(music.GetComponentsInChildren<AudioSource>().Any(source=>source.isPlaying),Is.True);
            Assert.That(feedback.PanelMovePlayCount,Is.GreaterThanOrEqualTo(1));
            File.WriteAllText(Folder+"/layout-cpu.txt","samples="+samples+"; meanMs="+(totalNs/(double)samples/1000000d)+"; peakMs="+(peakNs/1000000d)+"; scope=layout binding only, editor, not total UI rendering");
            foreach(var page in new[]{FrontendPage.Main,FrontendPage.Settings,FrontendPage.Host,FrontendPage.Join,FrontendPage.Pause})
            {
                view.Show(page);yield return new WaitForSecondsRealtime(.5f);yield return Capture(page.ToString());
            }
            view.Show(FrontendPage.Main);yield return new WaitForSecondsRealtime(.7f);
            var button=(RectTransform)view.transform.Find("Menu contents/Menu column/Main/PLAY VS BOT");
            var hostButton=(RectTransform)button.parent.Find("HOST GAME");
            var joinButton=(RectTransform)button.parent.Find("JOIN GAME");
            Assert.That(hostButton.anchoredPosition.x-button.anchoredPosition.x,Is.EqualTo(30f).Within(.01f),"Keep the authored sidebar staircase.");
            Assert.That(joinButton.anchoredPosition.x-hostButton.anchoredPosition.x,Is.EqualTo(30f).Within(.01f));
            var settings=theme.menuPresentation.Get(MenuScreenId.Main);var entry=settings.Find("PLAY VS BOT");
            Assert.That(entry,Is.Not.Null);var savedOffset=entry.offset;var savedScale=entry.scale;
            Vector2 original=button.anchoredPosition;
            try
            {
                entry.offset=savedOffset+new Vector2(17,9);entry.scale=new Vector2(1.08f,1.08f);settings.Revision++;
                yield return null;yield return null;
                Assert.That(button.anchoredPosition.x,Is.EqualTo(original.x+17).Within(.05f));
                Assert.That(button.localScale.x,Is.EqualTo(1.08f).Within(.001f));
                yield return Capture("Live-layout-edit");
            }
            finally{entry.offset=savedOffset;entry.scale=savedScale;settings.Revision++;}
            bool oldReduced=flow.Preferences.ReducedMotion;
            flow.Preferences.Set(flow.Preferences.MasterVolume,flow.Preferences.UIVolume,flow.Preferences.Sensitivity,false);
            int panelSoundsBeforeStart=feedback.PanelMovePlayCount;
            Assert.That(flow.BeginBot(),Is.True);
            var menuGroup=view.transform.Find("Menu contents").GetComponent<CanvasGroup>();
            var panel=(RectTransform)view.transform.Find("Menu contents/Menu column");
            float panelStart=panel.anchoredPosition.x;
            bool movingCaptured=false,veilCaptured=false,countdownCaptured=false;
            deadline=Time.realtimeSinceStartupAsDouble+15;
            try
            {
                while(flow.State==FrontendState.Starting&&Time.realtimeSinceStartupAsDouble<deadline)
                {
                    float age=(float)typeof(FrontendFlowController).GetField("_transition",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(flow);
                    if(age<view.StartIntroSeconds)Assert.That(view.CountdownText,Is.Empty,"Countdown must wait for sidebar departure and camera cover.");
                    if(age>.18f&&age<.4f)
                    {
                        Assert.That(menuGroup.alpha,Is.GreaterThan(.98f),"Departing sidebar must slide, not dissolve halfway.");
                        Assert.That(panel.anchoredPosition.x,Is.LessThan(panelStart-150));
                        if(!movingCaptured){movingCaptured=true;yield return Capture("Start-01-sidebar-departure");}
                    }
                    if(age>.6f&&!veilCaptured){veilCaptured=true;Assert.That(menuGroup.alpha,Is.LessThan(.01f));yield return Capture("Start-02-camera-cover");}
                    if(age>1.05f&&!countdownCaptured){countdownCaptured=true;Assert.That(view.CountdownText,Is.Not.Empty);yield return Capture("Start-03-countdown");}
                    yield return null;
                }
                Assert.That(movingCaptured&&veilCaptured&&countdownCaptured,Is.True,"Capture every phase of the real bot-start transition.");
                Assert.That(flow.State,Is.EqualTo(FrontendState.Combat));
            }
            finally{flow.Preferences.Set(flow.Preferences.MasterVolume,flow.Preferences.UIVolume,flow.Preferences.Sensitivity,oldReduced);}
            Assert.That(feedback.PanelMovePlayCount,Is.EqualTo(panelSoundsBeforeStart+1),"Only one sound for the complete sidebar departure.");
            Assert.That(music.GameGain,Is.GreaterThan(0));Assert.That(music.MenuGain,Is.Zero);
            flow.Pause();yield return new WaitForSecondsRealtime(.25f);Assert.That(Time.timeScale,Is.Zero);
            Assert.That(feedback.PanelMovePlayCount,Is.EqualTo(panelSoundsBeforeStart+2));
            flow.Resume();yield return new WaitForSecondsRealtime(.25f);Assert.That(Time.timeScale,Is.GreaterThan(0));
            Assert.That(feedback.PanelMovePlayCount,Is.EqualTo(panelSoundsBeforeStart+3));
            var duel=Find<EarthMvpDuelController>();var hud=Find<EarthDuelHud>();
            var root=hud.GetComponent<UIDocument>().rootVisualElement.Q("duel-hud");
            foreach(var outcome in new[]{"Victory","Defeat","Draw"})
            {
                duel.RestartRound();deadline=Time.realtimeSinceStartupAsDouble+30;
                while(duel.ArenaResetInProgress&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                duel.SetRoundReady(true);hud.SetLocalPerspective(EarthDuelFighterId.Player,true);yield return null;
                if(outcome!="Draw")duel.RequestKnockout(outcome=="Victory"?EarthDuelFighterId.Bot:EarthDuelFighterId.Player,RagdollHandoff.Uniform(Vector3.zero));
                yield return null;
                var match=(EarthDuelMatchState)typeof(EarthMvpDuelController).GetField("_match",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(duel);
                match.Step(Mathf.Max(0,match.RemainingSeconds-.03f));yield return new WaitForSecondsRealtime(.1f);yield return new WaitForSecondsRealtime(.8f);
                Assert.That(duel.IsRoundOver,Is.True);
                Assert.That(root.Q<Label>("reference-result-title").text,Is.EqualTo(outcome.ToUpperInvariant()));
                Assert.That(root.Q("reference-result-button-edge"),Is.Null,"No extra polygon plate may sit over the authored sprite.");
                var result=root.Q("round-result");
                Assert.That(result.IndexOf(root.Q("reference-result-atmosphere")),Is.LessThan(result.IndexOf(root.Q("restart-round"))),"The dimming layer must stay behind both action buttons.");
                Assert.That(root.Q("reference-result-halo").resolvedStyle.opacity,Is.GreaterThan(.1f));
                Assert.That(root.Q("result-positive-fringe").resolvedStyle.color.a,Is.GreaterThan(.2f));
                Assert.That(root.Q("restart-round").resolvedStyle.height,Is.EqualTo(88).Within(.2f));
                yield return Capture(outcome);
                if(outcome=="Victory")
                {
                    hud.SetReducedMotion(true);var p=theme.menuPresentation.Get(MenuScreenId.Victory);float saved=p.chromaticOpacity;
                    try{p.chromaticOpacity=0;yield return null;yield return Capture("Victory-no-aberration");}
                    finally{p.chromaticOpacity=saved;hud.SetReducedMotion(false);}
                }
            }
        }
        private T Find<T>() where T:Component=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).First();
        private static IEnumerator Capture(string name)
        {yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");yield return null;yield return null;}
    }
}
#endif
