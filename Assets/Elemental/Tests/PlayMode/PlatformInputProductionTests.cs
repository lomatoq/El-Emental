using System.Collections;
using System.IO;
using System.Reflection;
using Elemental.Input.Actions;
using Elemental.Input.Gestures;
using Elemental.Presentation.UI;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class PlatformInputProductionTests
    {
        private Scene scene,previous;
        private Mouse mouse;
        private static readonly BindingFlags PrivateStatic=BindingFlags.NonPublic|BindingFlags.Static;
        [UnityTearDown] public IEnumerator Restore()
        {
            if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);
            Time.timeScale=1;
            if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
        }
        [UnityTest] public IEnumerator LiveCombatMouseContourPreviewsThenCreatesPlatform()
        {
            previous=SceneManager.GetActiveScene();
            const string path="Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);
            scene=SceneManager.GetSceneByPath(path);SceneManager.SetActiveScene(scene);
            var gate=Find<EarthSceneReadinessGate>();var flow=Find<FrontendFlowController>();
            double deadline=Time.realtimeSinceStartupAsDouble+130;
            while(!gate.IsReady&&!gate.Failed&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(gate.IsReady,Is.True,gate.Status);
            while(flow.State==FrontendState.Loading&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(flow.BeginBot(),Is.True);
            deadline=Time.realtimeSinceStartupAsDouble+20;
            while(flow.State!=FrontendState.Combat&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(flow.State,Is.EqualTo(FrontendState.Combat));
            var duel=Find<EarthMvpDuelController>();
            var bot=duel.BotTransform.GetComponent<EarthMvpBotController>();if(bot!=null)bot.enabled=false;
            var input=duel.PlayerTransform.GetComponent<MagicInputController>();
            var adapter=input.GetComponent<EarthInputAdapter>();
            var router=input.GetComponent<EarthActionRouterBehaviour>();
            var executor=input.EarthExecutor;var preview=input.GetComponent<EarthPreviewPresenter>();
            Assert.That(adapter.GameplayInputSuppressed,Is.False);
            Assert.That(input.isActiveAndEnabled&&router.isActiveAndEnabled,Is.True);
            yield return new WaitForSeconds(.5f);
            var helper=typeof(EarthCoreVisualRuntimeTests);
            mouse=(Mouse)helper.GetMethod("CreateRoutedMouse",PrivateStatic).Invoke(null,new object[]{input.GetComponent<PlayerInput>(),"Platform production regression mouse"});
            Physics.SyncTransforms();
            Assert.That(TryFindSizedContour(input,executor.PlatformPool.Profile,out var contour),Is.True,"No visible closed ground contour within production platform area limits found.");
            string status="";input.StatusChanged+=value=>status=value;
            var queue=helper.GetMethod("QueuePrimaryMouseState",PrivateStatic);
            queue.Invoke(null,new object[]{mouse,contour[0],false});yield return null;yield return null;
            int before=executor.SuccessfulCommandCount,maxPreview=0;bool sawVisibleContour=false;
            queue.Invoke(null,new object[]{mouse,contour[0],true});yield return null;
            for(int segment=1;segment<contour.Length;segment++)
                for(int step=1;step<=4;step++)
                {
                    var point=math.lerp(contour[segment-1],contour[segment],step/4f);
                    queue.Invoke(null,new object[]{mouse,point,true});yield return null;
                    maxPreview=Mathf.Max(maxPreview,preview.PositionCount);
                    sawVisibleContour|=input.GetComponent<LineRenderer>().enabled&&preview.PositionCount>2;
                }
            string beforeRelease=$"preview={maxPreview}, phase={input.CurrentBendPhase}, ability={input.SelectedAbility}, owner={router.Owner}, status={status}";
            yield return Capture("contour");
            queue.Invoke(null,new object[]{mouse,contour[contour.Length-1],false});yield return null;yield return null;
            var worldPath=(System.Collections.Generic.List<float3>)typeof(MagicInputController).GetField("_worldPath",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(input);
            var geometry=EarthPlatformGeometrySolver.Build(worldPath,(float3)input.PlanetCenterWorld);
            string afterRelease=$"phase={input.CurrentBendPhase}, owner={router.Owner}, held={adapter.BendPrimaryHeld}, released={adapter.BendPrimaryReleased}, mouseHeld={mouse.leftButton.isPressed}, worldPoints={worldPath.Count}, area={geometry.Area}, minArea={executor.PlatformPool.Profile.MinimumArea}, maxArea={executor.PlatformPool.Profile.MaximumArea}";
            Directory.CreateDirectory("BuildReports/PlatformInput");
            File.WriteAllText("BuildReports/PlatformInput/routing.txt",beforeRelease+"\n"+afterRelease+"\ncommands="+(executor.SuccessfulCommandCount-before)+"; final="+status);
            Assert.That(maxPreview,Is.GreaterThan(2),beforeRelease);
            Assert.That(sawVisibleContour,Is.True,"Populated contour vertices must actually render.");
            Assert.That(input.SelectedAbility,Is.EqualTo(EarthAbilityIds.RaisePlatform),beforeRelease+"; final="+status);
            Assert.That(executor.SuccessfulCommandCount,Is.EqualTo(before+1),beforeRelease+"; "+afterRelease+"; final="+status);
            Assert.That(executor.PlatformPool.LastAcquired,Is.Not.Null);
            yield return new WaitForSeconds(.7f);yield return Capture("platform");
        }
        private static IEnumerator Capture(string name)
        {
            Directory.CreateDirectory("BuildReports/PlatformInput");yield return new WaitForEndOfFrame();
            var frame=ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes("BuildReports/PlatformInput/"+name+".png",frame.EncodeToPNG());Object.Destroy(frame);
        }
        private static bool TryFindSizedContour(MagicInputController input,EarthPlatformProfile profile,out float2[] contour)
        {
            contour=null;var camera=input.CastCamera;
            float width=Screen.width,height=Screen.height;
            var candidate=new float2[9];var world=new float3[9];
            for(int scale=1;scale<=3;scale++)
            {
                float halfW=Mathf.Max(28,width/44)*scale,halfH=Mathf.Max(20,height/44)*scale;
                for(float y=halfH+20;y<height-halfH-20;y+=24)
                for(float x=halfW+20;x<width-halfW-20;x+=24)
                {
                    candidate[0]=new float2(x-halfW,y-halfH);candidate[1]=new float2(x,y-halfH);
                    candidate[2]=new float2(x+halfW,y-halfH);candidate[3]=new float2(x+halfW,y);
                    candidate[4]=new float2(x+halfW,y+halfH);candidate[5]=new float2(x,y+halfH);
                    candidate[6]=new float2(x-halfW,y+halfH);candidate[7]=new float2(x-halfW,y);
                    candidate[8]=candidate[0];Collider surface=null;bool valid=true;
                    for(int i=0;i<candidate.Length;i++)
                    {
                        if(!Physics.Raycast(camera.ScreenPointToRay(new Vector2(candidate[i].x,candidate[i].y)),out var hit,200,~0,QueryTriggerInteraction.Ignore))
                        {valid=false;break;}
                        var provider=hit.collider.GetComponentInParent<EarthArenaSurfaceProvider>();
                        if(provider==null||hit.collider.name.IndexOf("FloorBase",System.StringComparison.OrdinalIgnoreCase)<0||surface!=null&&hit.collider!=surface)
                        {valid=false;break;}
                        surface=hit.collider;world[i]=(float3)hit.point;
                    }
                    if(!valid)continue;
                    var geometry=EarthPlatformGeometrySolver.Build(world,(float3)input.PlanetCenterWorld);
                    // Margin protects against moving-camera sampling and makes this a
                    // valid creation fixture instead of a tiny rejected footprint.
                    if(!geometry.IsValid||geometry.Area<profile.MinimumArea*2||geometry.Area>profile.MaximumArea*.7f)continue;
                    contour=candidate;return true;
                }
            }
            return false;
        }
        private T Find<T>() where T:Component
        {foreach(var root in scene.GetRootGameObjects()){var found=root.GetComponentInChildren<T>(true);if(found!=null)return found;}return null;}
    }
}
