using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using Elemental.Input.Actions;
using Elemental.Input.Gestures;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
    public sealed class RepairMouseProductionTests
    {
        private Scene scene,previous;private Mouse mouse;
        [UnityTearDown] public IEnumerator Cleanup()
        {if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);Time.timeScale=1;
         if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
         if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);}
        [UnityTest] public IEnumerator HeldMiddleMouseClockwiseRimCircleActuallyRebuildsDamagedWall()
        {
            previous=SceneManager.GetActiveScene();const string path="Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);scene=SceneManager.GetSceneByPath(path);SceneManager.SetActiveScene(scene);
            var gate=Find<EarthSceneReadinessGate>();float deadline=Time.realtimeSinceStartup+130;
            while(!gate.IsReady&&!gate.Failed&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(gate.IsReady,Is.True,gate.Status);yield return ProductionCombatTestFlow.BeginBotAfterReadiness(scene);
            var duel=Find<EarthMvpDuelController>();var bot=duel.BotTransform.GetComponent<EarthMvpBotController>();if(bot!=null)bot.enabled=false;
            var actor=duel.PlayerTransform;var input=actor.GetComponent<MagicInputController>();var executor=input.EarthExecutor;
            var camera=input.CastCamera;var up=actor.GetComponent<PlanetMotor>().LocalUp;
            RaycastHit floor=default;bool found=false;
            for(int y=5;y>=2&&!found;y--)for(int x=4;x<=6&&!found;x++)
            {
                if(!UnityEngine.Physics.Raycast(camera.ViewportPointToRay(new Vector3(x*.1f,y*.1f,0)),out var hit,25f,~0,QueryTriggerInteraction.Ignore))continue;
                var structure=hit.collider.GetComponentInParent<EarthArenaStructure>();
                if(structure==null||structure.OrdinaryDamageEnabled||Vector3.Distance(hit.point,actor.position)>12)continue;
                floor=hit;found=true;
            }
            Assert.That(found,Is.True,"Visible nearby authored floor is needed for actual pointer targeting.");
            Vector3 right=Vector3.ProjectOnPlane(camera.transform.right,up).normalized;
            var wall=Find<EarthWallPool>().Acquire(floor.point-right*.6f,floor.point+right*.6f,Find<VoxelPlanetBehaviour>().transform.position,2f,.55f,supportNormal:floor.normal);
            deadline=Time.realtimeSinceStartup+4;while(!wall.IsEmergenceComplete&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(wall.ApplyRockImpact(wall.transform.position,camera.transform.forward,6000f),Is.True);
            yield return new WaitForFixedUpdate();
            Vector2 start=default;found=false;
            for(int i=0;i<wall.StructureRuntime.PieceCount&&!found;i++)
            {
                var piece=wall.StructureRuntime.GetPieceRuntime(i);Vector3 p=camera.WorldToScreenPoint(piece.transform.position);
                if(p.z<=0||p.x<Screen.width*.2f||p.x>Screen.width*.8f||p.y<Screen.height*.2f||p.y>Screen.height*.8f)continue;
                if(!UnityEngine.Physics.Raycast(camera.ScreenPointToRay(p),out var hit,30f,~0,QueryTriggerInteraction.Ignore))continue;
                var wallPiece=hit.collider.GetComponent<EarthWallPiece>();
                if(wallPiece==null||wallPiece.Owner!=wall)continue;
                start=p;found=true;
            }
            Assert.That(found,Is.True,"Ray must hit a real damaged-wall piece before MMB.");
            mouse=(Mouse)typeof(EarthCoreVisualRuntimeTests).GetMethod("CreateRoutedMouse",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{actor.GetComponent<PlayerInput>(),"Repair production mouse"});
            int rebuilt=0;wall.Reassembly.StructureRebuilt+=_=>rebuilt++;
            string lastStatus="";input.StatusChanged+=value=>lastStatus=value;
            var trace=new StringBuilder();var router=actor.GetComponent<EarthActionRouterBehaviour>();var adapter=actor.GetComponent<EarthInputAdapter>();
            Directory.CreateDirectory("BuildReports/RepairMouse");
            Queue(start,false);yield return null;yield return null;
            // Target acquisition is intentionally diagnosed at the actual press,
            // after device pairing and its two settling frames.
            UnityEngine.Physics.Raycast(camera.ScreenPointToRay(start),out var pressHit,30f,~0,QueryTriggerInteraction.Ignore);
            object[] queryArgs={new Unity.Mathematics.float2(start.x,start.y),default(RaycastHit)};
            bool query=(bool)typeof(MagicInputController).GetMethod("TryFindGravityFocus",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(input,queryArgs);
            var selected=(RaycastHit)queryArgs[1];
            trace.AppendLine($"beforePress rawHit={pressHit.collider?.name}; query={query}; selected={selected.collider?.name}; wall={wall.WallId}; collapse={wall.IsCollapsing}; stun={actor.GetComponent<PlanetMotor>().IsImpactStunned}; start={start}; support={floor.collider.name}; supportBody={floor.collider.attachedRigidbody?.name}; supportKinematic={floor.collider.attachedRigidbody?.isKinematic}");
            Queue(start,true);
            for(int frame=0;frame<3;frame++)
            {
                yield return null;
                trace.AppendLine($"pressFrame={frame}; mouseHeld={mouse.middleButton.isPressed}; adapterHeld={adapter.BendFieldHeld}; owner={router.Owner}; allowsGravity={router.AllowsGravity}; pointer={adapter.PointerPixels}; active={executor.IsGravityWellActive}; source={executor.HasGravityStructureTarget}; captured={executor.GravityWellCapturedCount}; status={lastStatus}");
            }
            File.WriteAllText("BuildReports/RepairMouse/acquisition.txt",trace.ToString());
            Assert.That(executor.HasGravityStructureTarget,Is.True,trace.ToString());
            float radius=Screen.height*.09f;Vector2 center=start-Vector2.right*radius;
            for(int i=1;i<=72;i++)
            {
                float angle=-i*Mathf.PI*2/72;
                Queue(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius,true);
                yield return null;
                trace.AppendLine($"i={i}; direction={input.GravityGestureDirection}; phase={input.GravityGesturePhase01:F3}; intent={executor.GravityStructureIntent}; repair={executor.IsRepairActive}; rebuilt={rebuilt}");
            }
            Directory.CreateDirectory("BuildReports/RepairMouse");File.WriteAllText("BuildReports/RepairMouse/routing.txt",trace.ToString());
            Assert.That(input.GravityGestureDirection,Is.EqualTo(EarthCircularGestureDirection.Clockwise));
            Assert.That(input.GravityGesturePhase01,Is.GreaterThan(.99f));
            deadline=Time.realtimeSinceStartup+30;
            float nextTrace=0;
            string repairEvent="";
            wall.Reassembly.RepairInterrupted+=value=>repairEvent=value.ToString();
            wall.Reassembly.RepairRejected+=value=>repairEvent=value.ToString();
            while(rebuilt==0&&Time.realtimeSinceStartup<deadline)
            {
                yield return null;
                if(Time.realtimeSinceStartup<nextTrace)continue;
                nextTrace=Time.realtimeSinceStartup+.25f;
                var repair=wall.Reassembly;
                trace.AppendLine($"wait: active={repair.IsRepairing}; partial={repair.LastRepairWasPartial}; total={wall.StructureRuntime.PieceCount}; selected={repair.SelectedPieceCount}; target={repair.TargetPieceCount}; welded={repair.WeldedPieceCount}; current={repair.CurrentPieceIndex}; phase={repair.CurrentPiecePhase}; error={repair.CurrentPiecePositionError:F4}; speed={repair.CurrentPieceSpeed:F3}; angle={repair.CurrentPieceAngleErrorDegrees:F3}; retry={repair.CurrentPieceRetryCount}; event={repairEvent}");
                if(!repair.IsRepairing)
                {
                    File.WriteAllText("BuildReports/RepairMouse/completion.txt",repair.LastCompletionDiagnostic ?? "No completion snapshot");
                    for(int bond=0;bond<wall.StructureRuntime.BondCount;bond++)
                    {
                        var definition=wall.StructureRuntime.GetBondDefinition(bond);var state=wall.StructureRuntime.GetBondState(bond);
                        trace.AppendLine($"bond={bond}; a={definition.PieceA}; b={definition.PieceB}; flags={definition.Flags}; state={state.Phase}; damage={state.AccumulatedDamage}; runtimeReleased={wall.StructureRuntime.GetBondRuntime(bond).IsReleased}");
                    }
                }
                File.WriteAllText("BuildReports/RepairMouse/repair-wait.txt",trace.ToString());
            }
            Assert.That(rebuilt,Is.EqualTo(1),"Holding the real MMB circle must finish exact physical repair.");
            Assert.That(wall.IsCollapsing,Is.False);
            Queue(start,false);yield return null;yield return null;
            Assert.That(executor.IsGravityWellActive,Is.False);
        }
        private void Queue(Vector2 point,bool held)
        {var state=new MouseState{position=point};state.WithButton(MouseButton.Middle,held);InputSystem.QueueStateEvent(mouse,state);}
        private T Find<T>()where T:Component
        {foreach(var root in scene.GetRootGameObjects()){var c=root.GetComponentInChildren<T>(true);if(c!=null)return c;}Assert.Fail(typeof(T).Name);return null;}
    }
}
