using System.Collections;
using System.Linq;
using Elemental.Input.Gestures;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Elemental.Input.Actions;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthArenaGravityTargetRuntimeTests
    {
        [UnityTest]
        public IEnumerator ProtectedArenaFloorCannotStealScreenPointGrabOfReleasedCell() => VerifyArenaGrip(false);

        [UnityTest]
        public IEnumerator MiddleMousePressHoldReleaseAndRepressReachArenaGravityGrip() => VerifyArenaGrip(true);

        [UnityTest]
        public IEnumerator MiddleMouseAfterArmorReleaseReachesGravityInsteadOfInactiveArmor() => VerifyArenaGrip(true,true);

        private IEnumerator VerifyArenaGrip(bool middleMouse, bool afterArmor = false)
        {
            const string path = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            var scene = SceneManager.GetSceneByPath(path);
            var fixture = new GameObject("Arena gravity screen-point fixture");
            AsyncOperation unload = null;
            Mouse mouse = null;
            Keyboard keyboard = null;
            try
            {
                var roots = scene.GetRootGameObjects();
                foreach (var bot in roots.SelectMany(r=>r.GetComponentsInChildren<EarthMvpBotController>())) bot.enabled=false;
                yield return null;
                var arenas=roots.SelectMany(r=>r.GetComponentsInChildren<EarthArenaStructure>()).ToArray();
                var floor=arenas.First(a=>!a.OrdinaryDamageEnabled && !a.Repairable);
                var column=arenas.First(a=>a.name.Contains("Column") && a.OrdinaryDamageEnabled);
                Assert.That(column.TryPluckCell(column.transform.position,out var target),Is.True);
                Assert.That(target,Is.TypeOf<EarthArenaPiece>());
                var shape=target.Body.GetComponent<Collider>();
                var floorShape=floor.GetComponent<Collider>();
                // Keep the shipping collision geometry and capability flags. Move
                // this picking fixture away from combat to isolate the floor hit.
                floor.transform.position+=new Vector3(0,1000,0)-floorShape.bounds.center;
                Physics.SyncTransforms();
                var center=floorShape.bounds.center;
                float end=floorShape.bounds.extents.z+shape.bounds.extents.magnitude+4f;
                target.Body.position=center+Vector3.forward*end;
                target.Body.linearVelocity=Vector3.zero;
                target.Body.angularVelocity=Vector3.zero;
                target.Body.GetComponent<GravityBody>().enabled=false;
                // The deliberately intervening floor exercises ray filtering.
                // Pulling the remote cell into the shipping hold-distance range
                // must not shatter it against this artificial picking obstacle.
                Physics.IgnoreCollision(shape,floorShape,true);
                int expectedCaptured = afterArmor ? 3 : 1;
                if(afterArmor)
                {
                    Collider previous = shape;
                    for(int index = 0; index < 2; index++)
                    {
                        Assert.That(column.TryPluckCell(column.transform.position,out var neighbour),Is.True);
                        neighbour.Body.position = target.Body.position + Vector3.right * (index == 0 ? 3f : -3f);
                        neighbour.Body.linearVelocity = Vector3.zero;
                        neighbour.Body.angularVelocity = Vector3.zero;
                        neighbour.Body.GetComponent<GravityBody>().enabled = false;
                        var neighbourShape = neighbour.Body.GetComponent<Collider>();
                        Physics.IgnoreCollision(neighbourShape,floorShape,true);
                        Physics.IgnoreCollision(neighbourShape,shape,true);
                        Physics.IgnoreCollision(neighbourShape,previous,true);
                        previous = neighbourShape;
                    }
                }
                var camera=fixture.AddComponent<Camera>();camera.enabled=false;
                camera.transform.position=center-Vector3.forward*(floorShape.bounds.extents.z+10f);
                camera.transform.rotation=Quaternion.identity;
                var executor=fixture.AddComponent<MagicExecutor>();
                var input=roots.SelectMany(r=>r.GetComponentsInChildren<MagicInputController>()).First();
                input.enabled=middleMouse;
                input.Configure(input.GetComponent<UnityEngine.InputSystem.PlayerInput>(),camera,executor,null,null);
                if (middleMouse)
                {
                    var player=input.GetComponent<PlayerInput>();
                    mouse=InputSystem.AddDevice<Mouse>("Arena middle button test");
                    keyboard=InputSystem.AddDevice<Keyboard>("Arena middle button test keyboard");
                    // Live editor mouse/keyboard events must not steal this test's
                    // paired input devices while the user works in the editor.
                    player.neverAutoSwitchControlSchemes=true;
                    player.ActivateInput();
                    player.SwitchCurrentControlScheme("Keyboard&Mouse",keyboard,mouse);
                    player.currentActionMap.Enable();
                }
                Physics.SyncTransforms();
                Vector3 pixel=camera.WorldToScreenPoint(shape.bounds.center);
                var ray=camera.ScreenPointToRay(pixel);
                Assert.That(floorShape.Raycast(ray,out _,200f),Is.True,"The protected floor must intersect this ray.");
                Assert.That(shape.Raycast(ray,out _,200f),Is.True,"The released cell must also intersect this ray.");
                if (afterArmor)
                {
                    QueueMiddle(mouse,pixel,false);yield return null;yield return null;
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift));
                    QueueMiddle(mouse,pixel,true);yield return null;yield return null;
                    Assert.That(input.IsArmorActive,Is.True,"Start genuine Shift+MMB armor first.");
                    yield return new WaitForSeconds(.3f);
                    QueueMiddle(mouse,pixel,false);
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                    yield return null;yield return null;
                    yield return new WaitForSeconds(.5f);
                    Assert.That(input.IsArmorActive,Is.False,"Armor has ended before the plain MMB press.");
                }
                long bytesBefore=System.GC.GetAllocatedBytesForCurrentThread();
                long start=System.Diagnostics.Stopwatch.GetTimestamp();
                bool began;
                if (middleMouse)
                {
                    QueueMiddle(mouse,pixel,false);yield return null;yield return null;
                    QueueMiddle(mouse,pixel,true);yield return null;yield return null;
                    Assert.That(input.GetComponent<EarthInputAdapter>().BendFieldHeld,Is.True);
                    Assert.That(input.GetComponent<EarthActionRouterBehaviour>().AllowsGravity,Is.True);
                    began=executor.IsGravityWellActive;
                }
                else began=input.TryBeginGravityWellAtScreenPoint(new float2(pixel.x,pixel.y));
                double ms=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000d/System.Diagnostics.Stopwatch.Frequency;
                long allocated=System.GC.GetAllocatedBytesForCurrentThread()-bytesBefore;
                Assert.That(began,Is.True);
                Assert.That(executor.GravityWellCapturedCount,Is.GreaterThanOrEqualTo(expectedCaptured),"Capture the loose arena group without admitting the protected floor.");
                var before=target.Body.position;
                if (middleMouse)
                {
                    QueueMiddle(mouse,pixel+Vector3.up*60f,true);
                }
                else executor.UpdateGravityWell(before+Vector3.up*2f,Vector3.up);
                yield return new WaitForSeconds(.4f);
                if(middleMouse)
                    Assert.That(Vector2.Distance(input.GetComponent<EarthInputAdapter>().PointerPixels,
                        (Vector2)(pixel+Vector3.up*60f)),Is.LessThan(.1f),"Synthetic pointer must remain paired.");
                Assert.That(target.IsEarthTargetValid,Is.True,"The selected cell must stay released while held.");
                Assert.That(target.Body.position.y,Is.GreaterThan(before.y+.3f),
                    $"Cell {target.Body.name}: position={target.Body.position}, velocity={target.Body.linearVelocity}, active={executor.IsGravityWellActive}, captured={executor.GravityWellCapturedCount}");
                if(middleMouse)
                {
                    Assert.That(executor.IsGravityWellActive,Is.True,"Holding MMB must keep the session alive.");
                    QueueMiddle(mouse,pixel,false);yield return null;yield return null;
                    Assert.That(executor.IsGravityWellActive,Is.False,"MMB release must end the grip.");
                }
                else input.EndGravityWell();
                // Repeated presses must work after release, not just once per cell.
                for (int i=0;i<3;i++)
                {
                    Physics.SyncTransforms();pixel=camera.WorldToScreenPoint(shape.bounds.center);
                    if(middleMouse)
                    {
                        QueueMiddle(mouse,pixel,true);yield return null;yield return null;
                        Assert.That(executor.IsGravityWellActive,Is.True);
                        Assert.That(executor.GravityWellCapturedCount,Is.GreaterThanOrEqualTo(expectedCaptured));
                        QueueMiddle(mouse,pixel,false);yield return null;yield return null;
                        Assert.That(executor.IsGravityWellActive,Is.False);
                    }
                    else
                    {
                        Assert.That(input.TryBeginGravityWellAtScreenPoint(new float2(pixel.x,pixel.y)),Is.True);
                        Assert.That(executor.GravityWellCapturedCount,Is.GreaterThanOrEqualTo(1));input.EndGravityWell();
                    }
                }
                // Empty initial capture remains valid for an intact structure's
                // circle gesture, and must still disassemble and repair it.
                var intact=arenas.First(a=>a!=column && a.name.Contains("Column") && !a.IsFractured);
                var intactShape=intact.GetComponent<Collider>();
                Assert.That(executor.TryBeginGravityWell(intactShape,intactShape.bounds.center+Vector3.up,Vector3.up,true),Is.True);
                executor.SetGravityStructureGesture(Elemental.Simulation.Bending.EarthGravityStructureIntent.Disassemble,.55f);
                Assert.That(intact.ReleasedPieceCount,Is.GreaterThanOrEqualTo(2));
                Assert.That(executor.GravityWellCapturedCount,Is.GreaterThanOrEqualTo(2));
                executor.SetGravityStructureGesture(Elemental.Simulation.Bending.EarthGravityStructureIntent.Repair,1f);
                Assert.That(intact.IsFractured,Is.False);
                Assert.That(intact.ReleasedPieceCount,Is.Zero);
                executor.CancelGravityWell();
                Debug.Log(middleMouse
                    ? "[ArenaGravity] MMB Input System → router → field: four presses, hold, pointer move, lift and releases passed. After armor="+afterArmor+", stones="+expectedCaptured
                    : $"[ArenaGravity] Shipping floor + released cell: four screen-point grabs and lift passed; first press {ms:F4} ms / {allocated} bytes including first-use acquisition feedback.");
            }
            finally
            {
                if(mouse!=null)InputSystem.RemoveDevice(mouse);
                if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
                Object.Destroy(fixture);unload=SceneManager.UnloadSceneAsync(scene);
            }
            if(unload!=null)yield return unload;
        }

        private static void QueueMiddle(Mouse mouse,Vector3 pixel,bool held)
        {
            var state=new MouseState {position=new Vector2(pixel.x,pixel.y)};
            state.WithButton(MouseButton.Middle,held);
            InputSystem.QueueStateEvent(mouse,state);
        }

        [Test]
        public void NonTargetColliderCannotStartAnEmptySuccessfulGrip()
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var executor=go.AddComponent<MagicExecutor>();
                Assert.That(executor.TryBeginGravityWell(go.GetComponent<Collider>(),Vector3.up,Vector3.up,true),Is.False);
                Assert.That(executor.IsGravityWellActive,Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
