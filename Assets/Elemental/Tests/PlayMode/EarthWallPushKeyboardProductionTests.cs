using System.Collections;
using System.IO;
using Elemental.Input.Actions;
using Elemental.Input.Gestures;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthWallPushKeyboardProductionTests
    {
        private Scene _previous, _scene;
        private Keyboard _keyboard;
        private Mouse _mouse;
        private EarthWall _wall;
        private GameObject _occluder;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
            if (_occluder != null) Object.Destroy(_occluder);
            Time.timeScale = 1;
            if (_previous.IsValid() && _previous.isLoaded) SceneManager.SetActiveScene(_previous);
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
        }

        [UnityTest] public IEnumerator PairedControlForceTargetsGroundWallOnceAndRespectsOcclusion()
        {
            _previous = SceneManager.GetActiveScene();
            const string path = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneByPath(path); SceneManager.SetActiveScene(_scene);
            var gate = Find<EarthSceneReadinessGate>();
            float deadline = Time.realtimeSinceStartup + 130;
            while (!gate.IsReady && !gate.Failed && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(gate.IsReady, Is.True, gate.Status);
            var duel = Find<EarthMvpDuelController>();
            var bot = duel.BotTransform.GetComponent<EarthMvpBotController>();
            if (bot != null) bot.enabled = false;
            yield return ProductionCombatTestFlow.BeginBotAfterReadiness(_scene);
            if (bot != null) bot.enabled = false;
            var player = duel.PlayerTransform;
            var input = player.GetComponent<MagicInputController>();
            var router = player.GetComponent<EarthActionRouterBehaviour>();
            var motor = player.GetComponent<PlanetMotor>();
            var camera = input.CastCamera;
            var playerInput = player.GetComponent<PlayerInput>();
            _keyboard = InputSystem.AddDevice<Keyboard>("Wall push keyboard");
            _mouse = InputSystem.AddDevice<Mouse>("Wall push mouse");
            playerInput.neverAutoSwitchControlSchemes = true; playerInput.ActivateInput();
            if (!playerInput.user.valid) { playerInput.enabled = false; playerInput.enabled = true; playerInput.ActivateInput(); }
            playerInput.SwitchCurrentControlScheme("Keyboard&Mouse", _keyboard, _mouse);
            playerInput.currentActionMap.Enable();
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(_mouse, new MouseState());
            yield return null; yield return null;
            deadline = Time.realtimeSinceStartup + 8;
            while (!motor.HasStableSupport && Time.realtimeSinceStartup < deadline) yield return new WaitForFixedUpdate();
            Assert.That(motor.HasStableSupport, Is.True);

            Vector3 up = motor.LocalUp.normalized;
            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, up).normalized;
            Vector3 point = default, side = default;
            bool found = false; Collider supportingFloor = null;
            // Only the nearest actual low floor or planet hit qualifies. A column
            // cap, pedestal or arch above the arena can never seed this fixture.
            for (int ring = 0; ring < 3 && !found; ring++)
                for (int i = 0; i < 12 && !found; i++)
                {
                    Vector3 direction = Quaternion.AngleAxis(i * 30f, up) * forward;
                    Vector3 candidate = player.position + direction * (4f + ring * 1.5f);
                    if (!TryFloor(candidate, up, out var center)) continue;
                    if (Mathf.Abs(Vector3.Dot(center.point - player.position, up)) > 2.5f) continue;
                    if(Vector3.ProjectOnPlane(center.point-duel.BotTransform.position,up).magnitude<3.2f)continue;
                    Vector3 right = Vector3.Cross(up, direction).normalized;
                    if (!TryFloor(center.point + right * 1.2f, up, out var a) ||
                        !TryFloor(center.point - right * 1.2f, up, out var b)) continue;
                    if (Mathf.Abs(Vector3.Dot(a.point - b.point, up)) > .15f) continue;
                    Vector3 viewport = camera.WorldToViewportPoint(center.point + up);
                    if (viewport.z <= 0 || viewport.x < .12f || viewport.x > .88f || viewport.y < .12f || viewport.y > .88f) continue;
                    if (UnityEngine.Physics.CheckBox(center.point + up * 1.1f, new Vector3(1.25f,.9f,.35f),
                        Quaternion.LookRotation(direction, up), UnityEngine.Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    bool dynamicNeighbour=false;
                    foreach(var neighbour in UnityEngine.Physics.OverlapBox(center.point+up,new Vector3(1.8f,1.15f,.8f),
                        Quaternion.LookRotation(direction,up),UnityEngine.Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                        if(neighbour.attachedRigidbody!=null&&!neighbour.attachedRigidbody.isKinematic){dynamicNeighbour=true;break;}
                    if(dynamicNeighbour)continue;
                    point = center.point; side = right; supportingFloor = center.collider; found = true;
                }
            Assert.That(found, Is.True, "A clear, visible, actual arena floor patch must be selected, never an arch top.");
            var pool = Find<EarthWallPool>();
            var planet = Find<VoxelPlanetBehaviour>();
            var supportStructure=supportingFloor.GetComponentInParent<EarthArenaStructure>();
            _wall = pool.Acquire(point-side*1.2f, point+side*1.2f, planet.transform.position, 2f, .4f, 0xAFF053, up,
                supportStructure!=null?supportStructure.StructureId:0);
            Assert.That(_wall, Is.Not.Null);
            string firstFracture=null;
            _wall.TargetsActivated+=_=>{if(firstFracture==null)firstFracture=System.Environment.StackTrace;};
            deadline = Time.realtimeSinceStartup + 5;
            while (!_wall.IsEmergenceComplete && Time.realtimeSinceStartup < deadline) yield return null;
            Directory.CreateDirectory("BuildReports/WallPushInput");
            var emergenceReport=new System.Text.StringBuilder();
            emergenceReport.AppendLine($"point={point};support={supportingFloor.name};supportRigidbody={supportingFloor.attachedRigidbody};supportId={(supportStructure!=null?supportStructure.StructureId:0)};fractured={_wall.IsCollapsing};emerged={_wall.IsEmergenceComplete};physicalImpacts={_wall.AcceptedPhysicalImpactCount};bodyKinematic={_wall.Body.isKinematic};router={router.Owner};reveal={_wall.HasRevealedCracks};firstFracture={firstFracture??string.Empty}");
            foreach(var nearby in UnityEngine.Physics.OverlapBox(point+up, new Vector3(1.5f,1.2f,.6f),_wall.transform.rotation,~0,QueryTriggerInteraction.Ignore))
                emergenceReport.AppendLine($"nearby={nearby.name};body={nearby.attachedRigidbody};position={nearby.transform.position};bounds={nearby.bounds}");
            File.WriteAllText("BuildReports/WallPushInput/emergence.txt",emergenceReport.ToString());
            ScreenCapture.CaptureScreenshot("BuildReports/WallPushInput/emergence.png");
            Assert.That(_wall.IsEmergenceComplete, Is.True);
            Assert.That(_wall.IsCollapsing, Is.False, "A newly emerged wall must remain intact before input is applied.");
            Assert.That(supportingFloor != null && supportingFloor.enabled, Is.True, "The verified floor remains physically present beneath the wall.");
            var hub = Find<EarthMaterialFeedbackHub>(); _wall.ConfigureMaterialFeedback(hub);
            int dust = 0, chips = 0;
            void Observe(EarthMaterialFeedbackCue cue)
            { if (cue.SourceId == _wall.WallId && cue.Kind == EarthMaterialFeedbackKind.Friction) { dust += cue.DustCount; chips += cue.ChipCount; } }
            hub.Presented += Observe;
            try
            {
                Vector3 target = _wall.GetComponent<Collider>().bounds.center;
                _occluder = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _occluder.name = "Wall push nearest occluder proof";
                // Keep the ray blocker close to the camera, clear of the caster's
                // capsule and target. A large cube midway can itself shove an actor.
                _occluder.transform.position = Vector3.Lerp(camera.transform.position, target, .2f);
                _occluder.transform.localScale = Vector3.one * .6f;
                UnityEngine.Physics.SyncTransforms();
                Aim(camera, true); InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.LeftCtrl));
                yield return null; yield return null;
                Assert.That(router.Owner, Is.EqualTo(EarthActionOwner.WallPush));
                Assert.That(router.LastWallPushAccepted, Is.False, "Nearest solid occluder must reject wall targeting.");
                uint attempts = router.WallPushBeginCount;
                Object.Destroy(_occluder); _occluder = null;
                for (int i=0;i<4;i++) { Aim(camera, true); yield return null; }
                Assert.That(router.WallPushBeginCount, Is.EqualTo(attempts));
                Assert.That(_wall.IsHeldPushActive, Is.False, "Removing an occluder cannot retry a spent held chord.");
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState()); Aim(camera, false);
                yield return null; yield return null;

                var occlusionReport=new System.Text.StringBuilder($"fractured={_wall.IsCollapsing};physicalImpacts={_wall.AcceptedPhysicalImpactCount};owner={router.Owner};firstFracture={firstFracture??string.Empty}\n");
                foreach(var neighbour in UnityEngine.Physics.OverlapBox(_wall.Body.position,new Vector3(2,1.4f,1),_wall.Body.rotation,~0,QueryTriggerInteraction.Ignore))
                    occlusionReport.AppendLine($"nearby={neighbour};body={neighbour.attachedRigidbody};bounds={neighbour.bounds}");
                File.WriteAllText("BuildReports/WallPushInput/occlusion.txt",occlusionReport.ToString());
                Assert.That(_wall.IsCollapsing, Is.False, "Occluded input must not damage the fresh whole-wall target. First fracture: "+firstFracture);
                // Both semantic controls enter together. Ordinary RMB deliberately
                // plucks a cell after .22s; that is a different physical target.
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.RightCtrl));
                Aim(camera,true); yield return null; yield return null;
                Directory.CreateDirectory("BuildReports/WallPushInput");
                File.WriteAllText("BuildReports/WallPushInput/acquisition.txt",
                    $"owner={router.Owner}; accepted={router.LastWallPushAccepted}; rejection={router.LastWallPushRejection}; hit={router.LastWallPushHit}; target={router.LastWallPushTarget}; expectedWall={_wall}; fractured={_wall.IsCollapsing}; emerged={_wall.IsEmergenceComplete}; colliderEnabled={_wall.GetComponent<Collider>().enabled}; wallPosition={_wall.transform.position}; wallBounds={_wall.GetComponent<Collider>().bounds}; pointer={player.GetComponent<EarthInputAdapter>().PointerPixels}; ray={router.LastWallPushRay}; attempts={router.WallPushBeginCount}");
                Assert.That(router.Owner, Is.EqualTo(EarthActionOwner.WallPush));
                Assert.That(router.LastWallPushAccepted, Is.True, "Concurrent Ctrl+RMB must push this visible intact wall: " + router.LastWallPushRejection);
                Assert.That(router.HeldPushWall, Is.SameAs(_wall));
                Assert.That(_wall.IsHeldPushActive, Is.True);
                Vector3 start = _wall.Body.position; float mass = _wall.Body.mass;
                Vector3 travel=_wall.HeldPushDirection;Vector3 previousPosition=start;
                float backwards=0,maxPenetration=0;
                Vector3 releasePosition=default;bool provedMovingRelease=false;
                var contactReport=new System.Text.StringBuilder();
                var trajectory=new System.Text.StringBuilder("step,forward,side,supportGap,penetration\n");
                attempts = router.WallPushBeginCount;
                using var marker = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Elemental.Earth.Wall.HeldPush", 128);
                long peakNs = 0;
                for (int i=0;i<20;i++)
                {
                    Aim(camera,i<3); yield return new WaitForFixedUpdate();
                    Vector3 step=_wall.Body.position-previousPosition;previousPosition=_wall.Body.position;
                    backwards=Mathf.Max(backwards,-Vector3.Dot(step,travel));
                    Vector3 delta=_wall.Body.position-start;
                    float along=Vector3.Dot(delta,travel),sideways=Vector3.ProjectOnPlane(delta-travel*along,up).magnitude;
                    if(UnityEngine.Physics.ComputePenetration(_wall.GetComponent<Collider>(),_wall.Body.position,_wall.Body.rotation,
                        supportingFloor,supportingFloor.transform.position,supportingFloor.transform.rotation,out _,out float penetration))
                        maxPenetration=Mathf.Max(maxPenetration,penetration);
                    trajectory.AppendLine($"{i},{along:F5},{sideways:F5},{_wall.HeldPushSupportGap:F5},{maxPenetration:F5}");
                    if(_wall.Body.linearVelocity.magnitude<.1f)
                    {
                        var wallCollider=_wall.GetComponent<Collider>();var bounds=wallCollider.bounds;
                        var candidates=UnityEngine.Physics.OverlapBox(bounds.center,bounds.extents+Vector3.one*.03f,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
                        contactReport.AppendLine($"frame={i};speed={_wall.Body.linearVelocity};nearby={candidates.Length}");
                        foreach(var candidate in candidates)
                        {
                            if(candidate==wallCollider||candidate.transform.IsChildOf(_wall.transform))continue;
                            bool overlap=UnityEngine.Physics.ComputePenetration(wallCollider,_wall.Body.position,_wall.Body.rotation,
                                candidate,candidate.transform.position,candidate.transform.rotation,out var normal,out float depth);
                            contactReport.AppendLine($"other={candidate};bounds={candidate.bounds};overlap={overlap};normal={normal};depth={depth}");
                        }
                    }
                    Vector3 heading=_wall.HeldPushDirection;
                    _wall.UpdateHeldPush(Vector3.Cross(up,heading));
                    Assert.That(Vector3.Dot(heading,_wall.HeldPushDirection),Is.GreaterThan(.999f),"Moving cursor sideways cannot steer a latched whole-wall push.");
                    if(i==2)
                    {
                        // Release RMB with Control retained; charge fires only now. Later the
                        // wall reaches existing rubble, where zero speed is correct.
                        releasePosition=_wall.Body.position;
                        float beforeRelease=_wall.Body.linearVelocity.magnitude;
                        InputSystem.QueueStateEvent(_keyboard,new KeyboardState(Key.RightCtrl));Aim(camera,false);
                        yield return null;
                        yield return new WaitForFixedUpdate();
                        File.WriteAllText("BuildReports/WallPushInput/release.txt",$"before={beforeRelease};after={_wall.Body.linearVelocity.magnitude};active={_wall.IsHeldPushActive};owner={router.Owner};position={_wall.Body.position}");
                        Assert.That(beforeRelease,Is.LessThan(.05f),"Charge keeps the intact wall still until RMB release.");
                        Assert.That(_wall.IsHeldPushActive,Is.False);
                        Assert.That(router.HeldPushWall,Is.Null);
                        Assert.That(_wall.Body.linearVelocity.magnitude,Is.GreaterThan(.1f),"Releasing RMB while Control remains held fires the charged wall.");
                    }
                    if(i==3)
                    {
                        Assert.That(Vector3.Dot(_wall.Body.position-releasePosition,travel),Is.GreaterThan(.005f),"Released wall must physically coast forward.");
                        provedMovingRelease=true;
                    }
                    if (marker.Valid) peakNs = System.Math.Max(peakNs, marker.LastValue);
                }
                File.WriteAllText("BuildReports/WallPushInput/trajectory.csv",trajectory.ToString());
                File.WriteAllText("BuildReports/WallPushInput/stall-contacts.txt",contactReport.ToString());
                ScreenCapture.CaptureScreenshot("BuildReports/WallPushInput/grounded-push.png");
                Vector3 totalDelta=_wall.Body.position-start;
                float forwardTravel=Vector3.Dot(totalDelta,travel);
                float sideTravel=Vector3.ProjectOnPlane(totalDelta-travel*forwardTravel,up).magnitude;
                Assert.That(forwardTravel,Is.GreaterThan(.2f),"Actual floor wall must advance along its fixed normal.");
                Assert.That(sideTravel,Is.LessThan(forwardTravel*.2f+.03f));
                Assert.That(backwards,Is.LessThan(.025f),"No repeated backward collision jitter.");
                Assert.That(maxPenetration,Is.LessThan(.04f),"Full collider must remain supported by real floor geometry without repeated embed correction.");
                Assert.That(Vector3.ProjectOnPlane(_wall.Body.position-start,up).magnitude, Is.GreaterThan(.2f));
                Assert.That(_wall.Body.mass, Is.EqualTo(mass).Within(.001f));
                Assert.That(router.WallPushBeginCount, Is.EqualTo(attempts));

                Assert.That(dust, Is.GreaterThan(0)); Assert.That(chips, Is.GreaterThan(0));
                ScreenCapture.CaptureScreenshot("BuildReports/WallPushInput/grounded-push.png");
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
                Aim(camera,false); yield return null; yield return null;
                Assert.That(_wall.IsHeldPushActive, Is.False);
                Assert.That(router.HeldPushWall, Is.Null);
                Assert.That(provedMovingRelease,Is.True,"The live route must prove coasting before a later physical obstacle stops the wall.");
                Directory.CreateDirectory("BuildReports/WallPushInput");
                File.WriteAllText("BuildReports/WallPushInput/grounded-routing.txt", $"support={point}; mass={mass}; dust={dust}; chips={chips}; attempts={attempts}; displacement={Vector3.Distance(start,_wall.Body.position)}; heldPushMarkerValid={marker.Valid}; peakHeldPushNs={peakNs}");
                // Prove ordinary RMB remains ordinary after the whole-wall result.
                // Aim into the upper viewport so this check cannot pluck its target.
                InputSystem.QueueStateEvent(_mouse, new MouseState {position=new Vector2(Screen.width*.5f, Screen.height*.95f)});
                yield return null; yield return null;
                InputSystem.QueueStateEvent(_mouse, new MouseState {position=new Vector2(Screen.width*.5f, Screen.height*.95f), buttons=2});
                deadline = Time.realtimeSinceStartup + .8f;
                while (router.Owner != EarthActionOwner.VectorField && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(router.Owner, Is.EqualTo(EarthActionOwner.VectorField), "Ordinary RMB retains its existing owner.");
                Assert.That(router.WallPushBeginCount, Is.EqualTo(attempts));
                InputSystem.QueueStateEvent(_mouse,new MouseState());yield return null;yield return null;
                // A physical obstacle must stop or fracture the moving wall, not be
                // bypassed by a grounding teleport / transform-only slide.
                Vector3 obstacleDirection=_wall.HeldPushDirection;
                Vector3 obstacleStart=_wall.Body.position;
                _occluder=GameObject.CreatePrimitive(PrimitiveType.Cube);_occluder.name="Wall push blocking obstacle";
                _occluder.transform.SetPositionAndRotation(obstacleStart+obstacleDirection*1.1f,Quaternion.LookRotation(obstacleDirection,up));
                _occluder.transform.localScale=new Vector3(Vector3.Distance(_wall.Start,_wall.End)+4,_wall.Height+2,.4f);
                UnityEngine.Physics.SyncTransforms();
                Assert.That(_wall.TryBeginHeldPush(obstacleDirection),Is.True);
                Assert.That(_wall.ReleaseHeldPush(),Is.True);
                for(int i=0;i<30&&!_wall.IsCollapsing;i++)yield return new WaitForFixedUpdate();
                Assert.That(_wall.IsCollapsing||Vector3.Dot(_wall.Body.position-obstacleStart,obstacleDirection)<1.35f,Is.True,
                    "A real BoxCollider barrier must physically stop or fracture the wall.");
                _wall.CancelHeldPush();
            }
            finally { hub.Presented -= Observe; }
        }
        private void Aim(Camera camera, bool held)
        {
            Vector3 screen = camera.WorldToScreenPoint(_wall.GetComponent<Collider>().bounds.center);
            InputSystem.QueueStateEvent(_mouse, new MouseState {position = new Vector2(screen.x,screen.y), buttons = held ? (ushort)2 : (ushort)0});
        }
        private static bool TryFloor(Vector3 origin, Vector3 up, out RaycastHit hit)
        {
            if (!UnityEngine.Physics.Raycast(origin + up * 3, -up, out hit, 8, UnityEngine.Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            var arena = hit.collider.GetComponentInParent<EarthArenaStructure>();
            return Vector3.Dot(hit.normal, up) > .8f && ((arena != null && !arena.OrdinaryDamageEnabled) || hit.collider.GetComponentInParent<VoxelPlanetBehaviour>() != null);
        }
        private T Find<T>() where T : Component
        { foreach (var root in _scene.GetRootGameObjects()) { var value = root.GetComponentInChildren<T>(true); if (value != null) return value; } throw new System.InvalidOperationException(typeof(T).Name); }
    }
}
