using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Elemental.Input.Actions;
using Elemental.Input.Gestures;
using Elemental.Presentation.UI;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
    public sealed class PillarRowInputProductionTests
    {
        private Scene scene,previous;private Mouse mouse;
        [UnityTearDown] public IEnumerator Restore()
        {if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);Time.timeScale=1;if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);}
        [UnityTest] public IEnumerator RealPairedMouseDrawCreatesPillarRowAndDoesNotBecomeSingleMouseMagic()
        {
            previous=SceneManager.GetActiveScene();const string path="Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);scene=SceneManager.GetSceneByPath(path);SceneManager.SetActiveScene(scene);
            var gate=Find<EarthSceneReadinessGate>();var flow=Find<FrontendFlowController>();double deadline=Time.realtimeSinceStartupAsDouble+130;
            while(!gate.IsReady&&!gate.Failed&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(gate.IsReady,Is.True,gate.Status);while(flow.State==FrontendState.Loading&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(flow.BeginBot(),Is.True);deadline=Time.realtimeSinceStartupAsDouble+20;
            while(flow.State!=FrontendState.Combat&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(flow.State,Is.EqualTo(FrontendState.Combat));
            var duel=Find<EarthMvpDuelController>();var bot=duel.BotTransform.GetComponent<EarthMvpBotController>();if(bot!=null)bot.enabled=false;
            var input=duel.PlayerTransform.GetComponent<MagicInputController>();var router=input.GetComponent<EarthActionRouterBehaviour>();var ability=input.GetComponent<EarthDualMouseAbilityController>();
            var pool=(EarthPillarWavePool)typeof(EarthDualMouseAbilityController).GetField("wavePool",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ability);
            Assert.That(pool,Is.Not.Null);
            mouse=(Mouse)typeof(EarthCoreVisualRuntimeTests).GetMethod("CreateRoutedMouse",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{input.GetComponent<PlayerInput>(),"Pillar row regression mouse"});
            object[] args={input,input.EarthExecutor.PlatformPool.Profile,null};
            Assert.That((bool)typeof(PlatformInputProductionTests).GetMethod("TryFindSizedContour",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args),Is.True);
            var contour=(float2[])args[2];var start=new Vector2(contour[0].x,contour[0].y);
            var end=start+new Vector2(Screen.width*.18f,Screen.height*.10f);
            int commands=input.EarthExecutor.SuccessfulCommandCount,available=pool.AvailableColumns,rejected=pool.RejectedBusyCasts;
            Queue(start,false,false);yield return null;yield return null;
            float began=Time.unscaledTime;Queue(start,true,true);yield return null;
            bool tracked=false;
            while(Time.unscaledTime-began<.4f)
            {Queue(Vector2.Lerp(start,end,Mathf.Clamp01((Time.unscaledTime-began)/.32f)),true,true);yield return null;tracked|=router.Owner==EarthActionOwner.DualMouseEarth;}
            Queue(end,false,true);yield return null;Queue(end,false,false);yield return null;yield return null;
            Directory.CreateDirectory("BuildReports/PillarRowInput");
            string evidence=$"tracked={tracked}; freeBefore={available}; freeAfter={pool.AvailableColumns}; rejectedBefore={rejected}; rejectedAfter={pool.RejectedBusyCasts}; owner={router.Owner}; singleCommands={input.EarthExecutor.SuccessfulCommandCount-commands}; phase={input.CurrentBendPhase}";
            File.WriteAllText("BuildReports/PillarRowInput/routing.txt",evidence);
            Assert.That(tracked,Is.True,evidence);
            Assert.That(available-pool.AvailableColumns,Is.GreaterThanOrEqualTo(3),evidence);
            Assert.That(input.EarthExecutor.SuccessfulCommandCount,Is.EqualTo(commands),"Paired row must not replay a wall/platform or extraction.");
            var columns=(List<EarthPillarWaveColumn>)typeof(EarthPillarWavePool).GetField("_columns",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(pool);
            var original=new Dictionary<EarthPillarWaveColumn,uint>();
            foreach(var column in columns)if(column.IsAnchoredAnimation)original.Add(column,column.CastGeneration);
            Assert.That(original.Count,Is.GreaterThanOrEqualTo(3));
            available=pool.AvailableColumns;began=Time.unscaledTime;
            Queue(start,true,true);yield return null;
            while(Time.unscaledTime-began<.25f)
            {Queue(Vector2.Lerp(start,end,Mathf.Clamp01((Time.unscaledTime-began)/.20f)),true,true);yield return null;}
            // Reverse release order on the repeated stroke: both orders must commit once.
            Queue(end,true,false);yield return null;
            int overlapping=0;foreach(var pair in original)if(pair.Key.IsAnchoredAnimation)overlapping++;
            Assert.That(overlapping,Is.GreaterThan(0),"The repeated stroke must overlap an active first row.");
            Queue(end,false,false);yield return null;yield return null;
            string repeated=$"\nrepeatFreeBefore={available}; repeatFreeAfter={pool.AvailableColumns}; overlapping={overlapping}; rejectedAfterRepeat={pool.RejectedBusyCasts}";
            File.AppendAllText("BuildReports/PillarRowInput/routing.txt",repeated);
            Assert.That(available-pool.AvailableColumns,Is.GreaterThanOrEqualTo(3),repeated);
            Assert.That(pool.RejectedBusyCasts,Is.EqualTo(rejected),repeated);
            foreach(var pair in original)Assert.That(pair.Key.CastGeneration,Is.EqualTo(pair.Value),"A repeat must never reschedule an existing column.");
            Assert.That(input.EarthExecutor.SuccessfulCommandCount,Is.EqualTo(commands));
            yield return new WaitForSeconds(.28f);yield return new WaitForEndOfFrame();var frame=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes("BuildReports/PillarRowInput/row.png",frame.EncodeToPNG());Object.Destroy(frame);
            // The contact pulse shares the real pool, but must not reset a live row.
            original.Clear();foreach(var column in columns)if(column.IsAnchoredAnimation)original.Add(column,column.CastGeneration);
            Assert.That(original.Count,Is.GreaterThan(0));
            var actor=duel.PlayerTransform;var origin=actor.position;var up=actor.up;
            int pulse=pool.LaunchLandingPulse(origin,up,actor.forward,1f,actor.GetComponent<Rigidbody>());
            Assert.That(pulse,Is.EqualTo(36));
            foreach(var pair in original)Assert.That(pair.Key.CastGeneration,Is.EqualTo(pair.Value),"Landing pulse must preserve the live row.");
            Assert.That(pool.LaunchCrest(origin,up,actor.forward,5,actor.GetComponent<Rigidbody>()),Is.EqualTo(5),"A row also remains available during the radial pulse.");
            while(pool.AvailableColumns>=36)Assert.That(pool.LaunchLandingPulse(origin,up,actor.forward,1f,null),Is.EqualTo(36));
            available=pool.AvailableColumns;
            original.Clear();foreach(var column in columns)if(column.IsAnchoredAnimation)original.Add(column,column.CastGeneration);
            Assert.That(pool.LaunchLandingPulse(origin,up,actor.forward,1f,null),Is.Zero,"Insufficient capacity rejects the complete pulse.");
            Assert.That(pool.AvailableColumns,Is.EqualTo(available));
            foreach(var pair in original)Assert.That(pair.Key.CastGeneration,Is.EqualTo(pair.Value),"Capacity rejection must not recycle any active column.");
        }
        private void Queue(Vector2 point,bool primary,bool force)
        {var state=new MouseState{position=point};state.WithButton(MouseButton.Left,primary);state.WithButton(MouseButton.Right,force);InputSystem.QueueStateEvent(mouse,state);}
        private T Find<T>()where T:Component
        {foreach(var root in scene.GetRootGameObjects()){var found=root.GetComponentInChildren<T>(true);if(found!=null)return found;}return null;}
    }
}
