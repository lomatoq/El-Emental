using System.Collections;
using Elemental.Input.Actions;
using Elemental.Input.Gestures;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Networking;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthWallRmbInputTests
    {
        [UnityTest]
        public IEnumerator AuthoritativeRemoteRmbTapLaunchesOneCellAtQuickStoneSpeed() => RunGesture(false);

        [UnityTest]
        public IEnumerator LateSecondMouseButtonDrawsPillarRowWithoutFiringWallCell() => RunGesture(true);

        [UnityTest]
        public IEnumerator FreshSimultaneousMouseDragDrawsPillarsOnArenaGround() => RunGesture(true, true);

        private static IEnumerator RunGesture(bool pillarChord, bool simultaneousChord = false)
        {
            var root = new GameObject("Remote RMB wall tap integration");
            root.SetActive(false);
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var quick = ScriptableObject.CreateInstance<EarthQuickCastProfile>();
            var waveProfile = pillarChord ? ScriptableObject.CreateInstance<EarthPillarWaveProfile>() : null;
            try
            {
                var map = actions.AddActionMap("Gameplay");
                foreach (string action in new[] { "Move", "JumpOrStomp", "BendPrimary", "BendForce",
                    "BendField", "BendModifier", "BendParameter", "Cancel", "ShoulderSwap", "Pointer" })
                    map.AddAction(action, InputActionType.Button);
                var cameraObject = new GameObject("RMB targeting camera");
                cameraObject.transform.SetParent(root.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.transform.position = new Vector3(0f, 65.9f, -7f);
                camera.transform.rotation = Quaternion.identity;
                if (simultaneousChord)
                {
                    var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    ground.name = "Paired mouse arena support";
                    ground.transform.SetParent(root.transform, false);
                    ground.transform.position = new Vector3(0f, 64.8f, 0f);
                    ground.transform.localScale = new Vector3(20f, .2f, 20f);
                    camera.transform.LookAt(new Vector3(0f, 64.9f, -2f));
                }
                var executor = root.AddComponent<MagicExecutor>();
                var pool = root.AddComponent<EarthWallPool>();
                pool.Configure(1, null, null);
                var caster = new GameObject("Remote earth caster");
                caster.transform.SetParent(root.transform, false);
                EarthPillarWavePool wavePool = null;
                Rigidbody casterBody = null;
                if (pillarChord)
                {
                    caster.transform.position = new Vector3(0f, 65f, -7f);
                    casterBody = caster.AddComponent<Rigidbody>();
                    casterBody.isKinematic = true;
                    casterBody.useGravity = false;
                    wavePool = root.AddComponent<EarthPillarWavePool>();
                    wavePool.Configure(64, null, null, root.transform, waveProfile);
                }
                var playerInput = caster.AddComponent<PlayerInput>();
                playerInput.actions = actions;
                playerInput.defaultActionMap = "Gameplay";
                var adapter = caster.AddComponent<EarthInputAdapter>();
                adapter.Configure(playerInput);
                adapter.ConfigureRemoteInput(true);
                var controller = caster.AddComponent<MagicInputController>();
                controller.Configure(playerInput, camera, executor, null, null);
                controller.ConfigureEarthFeatureProfiles(quick, null);
                root.SetActive(true);
                if (pillarChord)
                    caster.GetComponent<EarthDualMouseAbilityController>().Configure(executor, wavePool,
                        null, camera, casterBody);
                EarthWall wall = pool.Acquire(new Vector3(-3f, 65f, 0f), new Vector3(3f, 65f, 0f),
                    Vector3.zero, 2.5f, .35f, supportNormal: Vector3.up, foundationEmbed: .3f);
                float deadline = Time.realtimeSinceStartup + 3f;
                while (!wall.IsEmergenceComplete && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(wall.IsEmergenceComplete, Is.True);
                Physics.SyncTransforms();
                Vector3 wallPosition = wall.transform.position;
                int releaseCount = 0;
                EarthBodyReleasedEvent released = default;
                executor.Events.EarthBodyReleased += value => { releaseCount++; released = value; };
                uint sequence = 1;
                EarthSemanticInputFrame frame = new EarthSemanticInputFrame
                {
                    Sequence = sequence++, Tick = 1, PointerViewport = new float2(.5f),
                    Held = simultaneousChord ? EarthInputBits.Primary | EarthInputBits.Force : EarthInputBits.Force,
                    Pressed = simultaneousChord ? EarthInputBits.Primary | EarthInputBits.Force : EarthInputBits.Force,
                    CameraPosition = camera.transform.position, CameraRotation = camera.transform.rotation,
                    FieldOfView = 60f, Aspect = camera.aspect
                };
                Assert.That(adapter.EnqueueRemoteInput(in frame), Is.True);
                yield return null;
                if (pillarChord)
                {
                    // Force has already fallen back to its normal owner after 80 ms.
                    // Adding primary must hand off the still-uncommitted wall tap.
                    float firstHeldUntil = Time.unscaledTime + (simultaneousChord ? 0f : .12f);
                    frame.Pressed = EarthInputBits.None;
                    while (Time.unscaledTime < firstHeldUntil)
                    {
                        frame.Sequence = sequence++;
                        Assert.That(adapter.EnqueueRemoteInput(in frame), Is.True);
                        yield return null;
                    }
                    Assert.That(wall.IsCollapsing, Is.False);
                    frame.Sequence = sequence++;
                    frame.Held = EarthInputBits.Primary | EarthInputBits.Force;
                    frame.Pressed = simultaneousChord ? EarthInputBits.None : EarthInputBits.Primary;
                    Assert.That(adapter.EnqueueRemoteInput(in frame), Is.True);
                    yield return null;
                    frame.Pressed = EarthInputBits.None;
                    frame.PointerViewport = new float2(.5f, .67f);
                    float bothHeldUntil = Time.unscaledTime + .25f;
                    while (Time.unscaledTime < bothHeldUntil)
                    {
                        frame.Sequence = sequence++;
                        Assert.That(adapter.EnqueueRemoteInput(in frame), Is.True);
                        yield return null;
                    }
                    frame.Sequence = sequence++;
                    frame.Held = EarthInputBits.None;
                    frame.Released = EarthInputBits.Primary | EarthInputBits.Force;
                    Assert.That(adapter.EnqueueRemoteInput(in frame), Is.True);
                    deadline = Time.realtimeSinceStartup + 1f;
                    while (wavePool.AvailableColumns == 64 && Time.realtimeSinceStartup < deadline)
                    {
                        yield return null;
                        frame.Sequence = sequence++;
                        frame.Released = EarthInputBits.None;
                        Assert.That(adapter.EnqueueRemoteInput(in frame), Is.True);
                    }
                    Assert.That(wavePool.AvailableColumns, Is.EqualTo(59),
                        "Routed paired-button drag must schedule the actual five-column row.");
                    Assert.That(releaseCount, Is.Zero, "The superseded single RMB must not fire a wall cell.");
                    Assert.That(wall.IsCollapsing, Is.False);
                    Assert.That(executor.IsVectorFieldActive, Is.False);
                    EarthPillarWaveColumn first = null;
                    foreach (var column in root.GetComponentsInChildren<EarthPillarWaveColumn>())
                        if (first == null) first = column;
                    Assert.That(first, Is.Not.Null);
                    Vector3 firstPose = first.Body.position;
                    for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
                    Assert.That(first.gameObject.activeSelf, Is.True);
                    Assert.That(Vector3.Distance(first.Body.position, firstPose), Is.GreaterThan(.02f),
                        "The scheduled pillar row must advance its real rise trajectory.");
                    yield break;
                }
                frame.Sequence = sequence++;
                frame.Held = EarthInputBits.None;
                frame.Pressed = EarthInputBits.None;
                frame.Released = EarthInputBits.Force;
                Assert.That(adapter.EnqueueRemoteInput(in frame), Is.True);
                deadline = Time.realtimeSinceStartup + 1f;
                while (releaseCount == 0 && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                    frame.Sequence = sequence++;
                    frame.Released = EarthInputBits.None;
                    Assert.That(adapter.EnqueueRemoteInput(in frame), Is.True);
                }
                Assert.That(releaseCount, Is.EqualTo(1), "Actual routed remote RMB must release exactly one cell.");
                Assert.That(wall.IsCollapsing, Is.True);
                Assert.That(executor.IsVectorFieldActive, Is.False);
                Assert.That(Vector3.Distance(wall.transform.position, wallPosition), Is.LessThan(.0001f));
                Assert.That(math.length(released.Velocity), Is.EqualTo(
                    quick.Data.ResolveLaunchSpeed(quick.Data.ExtractionSeconds)).Within(.05f));
                EarthPieceRuntime launched = null;
                foreach (EarthPieceRuntime piece in root.GetComponentsInChildren<EarthPieceRuntime>())
                    if (piece.StableEarthId == released.BodyId) launched = piece;
                Assert.That(launched, Is.Not.Null);
                Assert.That(wall.IsPieceStructurallySupported(launched.PieceIndex), Is.False);
                Assert.That(launched.Body.isKinematic, Is.False);
                Assert.That(launched.Body.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode.ContinuousDynamic));
                Vector3 initial = launched.transform.position;
                for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();
                Assert.That(Vector3.Distance(launched.transform.position, initial), Is.GreaterThan(.25f),
                    "The detached cell must actually fly away, not only emit a launch event.");
                Assert.That(Vector3.Distance(wall.transform.position, wallPosition), Is.LessThan(.001f));
                Assert.That(releaseCount, Is.EqualTo(1));
                foreach (EarthPieceRuntime sibling in root.GetComponentsInChildren<EarthPieceRuntime>())
                {
                    if (sibling == launched) continue;
                    Assert.That(UnityEngine.Physics.GetIgnoreCollision(launched.GetComponent<Collider>(),
                        sibling.GetComponent<Collider>()), Is.False,
                        "Source-wall collision must return once the projectile has cleared the wall.");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(actions);
                Object.DestroyImmediate(quick);
                if (waveProfile != null) Object.DestroyImmediate(waveProfile);
            }
        }
    }
}
