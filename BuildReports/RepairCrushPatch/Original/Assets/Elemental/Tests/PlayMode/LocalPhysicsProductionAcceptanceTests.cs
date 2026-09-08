using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using Elemental.Presentation.Animation;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Elemental.Tests.PlayMode
{
    public sealed class LocalPhysicsProductionAcceptanceTests
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private const string Folder = "BuildReports/LocalPhysicsAcceptance";
        private readonly WaitForEndOfFrame _frame = new();
        private readonly WaitForFixedUpdate _fixed = new();
        private Scene _scene;
        private EarthCharacterImpactTarget _target;
        private HumanoidLocalizedPhysicsResponse _physics;
        private PlanetMotor _motor;
        private EarthFragment _stone;
        private EarthTypedCombatProjectile _typed;
        private uint _sourceId = 0xACCE0100u;
        private EarthWorldResponseEvent _accepted;
        private int _acceptedEvents;
        private Vector3 _launchDirection;
        private GameObject _stoneObject, _captureObject, _probeObject, _regionalGround;
        private Vector3 _regionalStart;
        private Quaternion _regionalRotation;
        private string _firstSurface;
        private Material _stoneMaterial;
        private int _oldCaptureRate;

        [UnitySetUp]
        public IEnumerator Load()
        {
            Assert.That(SceneManager.GetSceneByPath(ScenePath).isLoaded, Is.False);
            _oldCaptureRate = Time.captureFramerate;
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneByPath(ScenePath);
            var gate = Find<EarthSceneReadinessGate>();
            Assert.That(gate, Is.Not.Null);
            double deadline = Time.realtimeSinceStartupAsDouble + 125d;
            while (!gate.IsReady && !gate.Failed && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(gate.IsReady, Is.True, gate.Status);
            yield return ProductionCombatTestFlow.BeginBotAfterReadiness(_scene);
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                foreach (var bot in root.GetComponentsInChildren<EarthMvpBotController>()) bot.enabled = false;
                foreach (var target in root.GetComponentsInChildren<EarthCharacterImpactTarget>())
                    if (target.FighterId == EarthDuelFighterId.Bot) _target = target;
            }
            Assert.That(_target, Is.Not.Null);
            _motor = _target.GetComponent<PlanetMotor>();
            _motor.ConfigureInputSource(_motor.gameObject.AddComponent<LocalPhysicsQaIdleInput>());
            _physics = _target.GetComponentInChildren<HumanoidRagdollRig>(true).LocalizedPhysics;
            Assert.That(_physics, Is.Not.Null);
            Assert.That(_physics.IsReady, Is.True);
            _target.WorldResponseRequested += OnResponse;
            PrewarmStone();
            Directory.CreateDirectory(Folder);
            yield return new WaitForSeconds(1.3f);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.captureFramerate = _oldCaptureRate;
            if (_target != null) _target.WorldResponseRequested -= OnResponse;
            if (_probeObject != null) Object.Destroy(_probeObject);
            if (_captureObject != null) Object.Destroy(_captureObject);
            if (_stoneObject != null) Object.Destroy(_stoneObject);
            if (_regionalGround != null) Object.Destroy(_regionalGround);
            if (_stoneMaterial != null) Object.Destroy(_stoneMaterial);
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
        }

        [UnityTest]
        public IEnumerator LocalPhysicsCpuAndAllocationWindowHasNinetySixNormalFrames()
        {
            var allocation = new LocalPhysicsAllocationWindow();
            _probeObject = new GameObject("Local physical response allocation brackets");
            _probeObject.AddComponent<LocalPhysicsAllocationBegin>().Window = allocation;
            _probeObject.AddComponent<LocalPhysicsAllocationEnd>().Window = allocation;
            // Warm the real collision, response and all callback/JIT paths before
            // recording. There is no PNG encoding, readback or file I/O below.
            ThrowAt(2);
            for (int frame = 0; frame < 64; frame++) { yield return _fixed; yield return _frame; }
            Assert.That(_acceptedEvents, Is.GreaterThan(0), "Warmup stone did not physically hit the production actor.");
            const int frames = 96;
            var stepNs = new long[frames];
            var poseNs = new long[frames];
            var frameGcBytes = new long[frames];
            int activeFrames = 0;
            using var step = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Elemental.Character.LocalPhysicsStep", 256);
            using var pose = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Elemental.Character.LocalPhysicsPose", 256);
            using var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 256);
            Assert.That(step.Valid && pose.Valid, Is.True, "Local physical profiler markers were not registered.");
            allocation.Measuring = true;
            for (int frame = 0; frame < frames; frame++)
            {
                if (frame % 24 == 0) ThrowAt(2);
                yield return _fixed;
                yield return _frame;
                stepNs[frame] = step.LastValue;
                poseNs[frame] = pose.LastValue;
                frameGcBytes[frame] = gc.Valid ? gc.LastValue : -1;
                if (_physics.HasActiveResponse) activeFrames++;
            }
            allocation.Measuring = false;
            step.Stop(); pose.Stop(); gc.Stop();
            var text = new StringBuilder();
            text.AppendLine("Production EarthCoreSlice; prewarmed actual thrown stones; 96 normal fixed/render frames; no PNG/readback/file I/O in measured window.");
            text.AppendLine($"activeResponseFrames={activeFrames}/{frames}, acceptedPhysicalHitEvents={_acceptedEvents}, captureFramerate={Time.captureFramerate}");
            text.AppendLine(Statistics("LocalPhysicsStep/all fighters CPU ns", stepNs));
            text.AppendLine(Statistics("LocalPhysicsPose/all fighters CPU ns", poseNs));
            text.AppendLine(Statistics("Whole frame GC bytes (includes editor, test runner and all systems; not attributed to local physics)", frameGcBytes));
            text.AppendLine($"Execution-order2499→2501 current-thread GC window: fixedSamples={allocation.FixedSamples}, fixedTotalBytes={allocation.FixedBytes}, fixedMaxBytes={allocation.FixedMaximum}, poseSamples={allocation.PoseSamples}, poseTotalBytes={allocation.PoseBytes}, poseMaxBytes={allocation.PoseMaximum}");
            text.AppendLine("The bracket contains all order2500 callbacks (currently only HumanoidLocalizedPhysicsResponse) plus Unity callback dispatch. It does not include unrelated earlier/later game systems.");
            File.WriteAllText(Path.Combine(Folder, "Performance.txt"), text.ToString());
            Debug.Log("[Local physics acceptance] " + text);
            Assert.That(activeFrames, Is.GreaterThan(48), "Profiling did not contain enough real active responses.");
            Assert.That(Maximum(stepNs), Is.GreaterThan(0));
            Assert.That(Maximum(poseNs), Is.GreaterThan(0));
            Assert.That(allocation.FixedSamples, Is.GreaterThanOrEqualTo(60));
            Assert.That(allocation.PoseSamples, Is.GreaterThanOrEqualTo(60));
            Assert.That(allocation.FixedBytes, Is.Zero, "Managed allocations inside local physical FixedUpdate window.");
            Assert.That(allocation.PoseBytes, Is.Zero, "Managed allocations inside local physical LateUpdate window.");
        }

        [UnityTest]
        public IEnumerator PhysicalHeadBothArmsAndBothLegsThrowsProduceRegionalFrameSequences()
        {
            Time.captureFramerate = 30;
            PrepareRegionalGround();
            _captureObject = new GameObject("Production local hit evidence camera");
            Camera camera = _captureObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.fieldOfView = 34f;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 1000f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.065f, .09f, .13f);
            // Isolate existing skinned meshes and the real projectile for readable
            // regional footage. Physics layers and actor/collider poses stay intact.
            var presentation = _target.GetComponentInChildren<HumanoidCharacterPresentation>(true);
            Transform head = presentation.Animator.GetBoneTransform(HumanBodyBones.Head);
            Assert.That(_physics.Bone(2), Is.SameAs(head), "Head region must use the visible Animator's canonical Head.");
            Assert.That(_physics.Bone(3), Is.SameAs(presentation.Animator.GetBoneTransform(HumanBodyBones.LeftUpperArm)));
            Assert.That(_physics.Bone(5), Is.SameAs(presentation.Animator.GetBoneTransform(HumanBodyBones.RightUpperArm)));
            Assert.That(_physics.Bone(8), Is.SameAs(presentation.Animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg)));
            Assert.That(_physics.Bone(10), Is.SameAs(presentation.Animator.GetBoneTransform(HumanBodyBones.RightLowerLeg)));
            foreach (SkinnedMeshRenderer renderer in presentation.Animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (IsCharacterSkin(renderer, presentation.Animator)) renderer.gameObject.layer = 22;
            camera.cullingMask = 1 << 22;
            var target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(960, 540, TextureFormat.RGB24, false);
            target.Create();
            camera.targetTexture = target;
            RenderTexture oldActive = RenderTexture.active;
            var evidence = new StringBuilder("sequence,frame,seconds,source_id,events,selected_region,angle_deg,peak_displacement_m,stone_x,stone_y,stone_z,aim_bone_x,aim_bone_y,aim_bone_z,contact_x,contact_y,contact_z,incoming_x,incoming_y,incoming_z,response,first_surface\n");
            int[] regions = { 2, 3, 5, 8, 10 };
            string[] names = { "Head", "LeftArm", "RightArm", "LeftLeg", "RightLeg" };
            try
            {
                for (int sequence = 0; sequence < regions.Length; sequence++)
                {
                    string folder = Path.Combine(Folder, names[sequence]);
                    Directory.CreateDirectory(folder);
                    _stoneObject.SetActive(false);
                    ResetRegionalActor();
                    yield return new WaitForSeconds(.8f);
                    Assert.That(_motor.IsGrounded, Is.True, "Regional actor must stand on real physical support before launch.");
                    Bounds visible = VisibleBounds(presentation);
                    Vector3 toward = (_target.transform.forward * 4.1f + _target.transform.right * 2.1f).normalized *
                        Mathf.Max(1f, visible.size.y / (2f * Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad)) * 1.8f);
                    camera.transform.SetPositionAndRotation(visible.center + toward,
                        Quaternion.LookRotation(-toward, _motor.LocalUp));
                    int before = _physics.AcceptedImpactCount;
                    int eventsBefore = _acceptedEvents;
                    float peak = 0f;
                    for (int frame = 0; frame < 48; frame++)
                    {
                        if (frame == 5) ThrowAt(regions[sequence]);
                        yield return _frame;
                        AssertReadableCharacterFraming(camera, VisibleBounds(presentation));
                        camera.Render();
                        RenderTexture.active = target;
                        readback.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                        readback.Apply();
                        File.WriteAllBytes(Path.Combine(folder, $"frame-{frame:D3}.png"), readback.EncodeToPNG());
                        peak = Mathf.Max(peak, _physics.CurrentMaximumAngle);
                        Vector3 p = _stone.Body.position;
                        Vector3 bone = _physics.Bone(regions[sequence]).position;
                        evidence.AppendFormat(CultureInfo.InvariantCulture,
                            "{0},{1},{2:F4},{3},{4},{5},{6:F4},{7:F5},{8:F4},{9:F4},{10:F4},{11:F4},{12:F4},{13:F4},{14:F4},{15:F4},{16:F4},{17:F4},{18:F4},{19:F4},{20},{21}\n",
                            names[sequence], frame, frame / 30f, _sourceId, _acceptedEvents - eventsBefore,
                            _physics.LastHitRegion, _physics.CurrentMaximumAngle, _physics.PeakDisplacementMeters, p.x, p.y, p.z,
                            bone.x, bone.y, bone.z, _accepted.Point.x, _accepted.Point.y, _accepted.Point.z,
                            _accepted.Direction.x, _accepted.Direction.y, _accepted.Direction.z, _accepted.Response, _firstSurface);
                    }
                    File.WriteAllText(Path.Combine(Folder, "RegionalFrames.csv"), evidence.ToString());
                    Assert.That(_acceptedEvents - eventsBefore, Is.EqualTo(1), names[sequence] + ": physical hit must emit one canonical response.");
                    Assert.That(_physics.AcceptedImpactCount - before, Is.EqualTo(1),
                        $"{names[sequence]}: response={_accepted.Response}, first surface={_firstSurface}");
                    Assert.That(Vector3.Dot((Vector3)_accepted.Direction, _launchDirection), Is.GreaterThan(.97f),
                        names[sequence] + ": the response direction must retain the actual incoming throw, not its post-contact bounce.");
                    int selected = _physics.LastHitRegion;
                    Assert.That((int)_accepted.HitRegion, Is.EqualTo(selected), "Accepted hit fact must carry the bone region used by the physical response.");
                    int firstRegion = regions[sequence] >= 7 ? regions[sequence] - 1 : regions[sequence];
                    Assert.That(selected == firstRegion || (sequence != 0 && selected == firstRegion + 1),
                        Is.True, $"{names[sequence]} throw selected {selected}; actual contact={_accepted.Point}.");
                    Assert.That(peak, Is.GreaterThan(.5f), names[sequence] + ": no visible physical reaction.");
                    Assert.That(_physics.HasActiveResponse, Is.False, names[sequence] + ": no recovery.");
                }
                File.WriteAllText(Path.Combine(Folder, "RegionalFrames.txt"),
                    "Five production-character sequences (head, both arms, both legs), 960x540 at30fps,48frames each. Frames0–4 before release; launchframe5. Actual EarthFragment + EarthTypedCombatProjectile + Rigidbody/Collider collision; no direct ApplyImpact/ApplyHit or bone pose writes. Production actor stands on a test-only physical platform above arena obstacles and is reset before each throw. Launch sphere sweep verifies no intervening ground/obstacle. Isolated camera culling and fixed full-renderer framing show the complete body. CSV records source IDs, accepted events, selected region, response severity, first contact surface and physical angle. Encode frames at30fps to preserve real simulation timing.");
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = oldActive;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(readback);
            }
        }

        private void PrewarmStone()
        {
            _stoneObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _stoneObject.name = "Physical regional QA stone";
            SceneManager.MoveGameObjectToScene(_stoneObject, _scene);
            Rigidbody body = _stoneObject.AddComponent<Rigidbody>();
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearDamping = 0f;
            _stone = _stoneObject.AddComponent<EarthFragment>();
            _stone.SurfaceImpactAccepted += impact => { if (_firstSurface == null) _firstSurface = impact.Surface != null ? impact.Surface.name.Replace(',', '_') : "none"; };
            _typed = _stoneObject.AddComponent<EarthTypedCombatProjectile>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            _stoneMaterial = new Material(shader);
            _stoneMaterial.SetColor("_BaseColor", new Color(.34f, .26f, .16f));
            _stoneObject.GetComponent<Renderer>().sharedMaterial = _stoneMaterial;
            _stone.Initialize(_sourceId, null, _target.transform.position + _motor.LocalUp * 4f, .13f, 40f);
            _stoneObject.SetActive(false);
        }

        private void ThrowAt(int region)
        {
            Vector3 aim = _physics.Bone(region).position;
            Vector3 outward = region == 2 ? _target.transform.forward :
                region == 3 || region == 4 || region == 7 || region == 8 ? -_target.transform.right : _target.transform.right;
            Vector3 start = aim + outward * 1.6f;
            if (_regionalGround != null)
            {
                // Verify the real flight corridor rather than disabling terrain collisions.
                foreach (RaycastHit hit in UnityEngine.Physics.SphereCastAll(start, .15f, -outward, 1.6f, ~0, QueryTriggerInteraction.Ignore))
                    Assert.That(hit.collider.transform.IsChildOf(_target.transform) || hit.collider.gameObject == _stoneObject,
                        Is.True, $"Region {region} launch blocked by {hit.collider.name} at {hit.point}.");
            }
            _firstSurface = null;
            _sourceId++;
            _stone.Initialize(_sourceId, null, start, .13f, 40f);
            _stoneObject.layer = 22; // Test camera visibility only; layer22 keeps the project's ordinary collision matrix.
            _typed.Arm(_stone, EarthCharacterImpactSourceKind.LooseStone);
            _launchDirection = -outward.normalized;
            _stone.LaunchProjectile(-outward, 10f, null);
            UnityEngine.Physics.SyncTransforms();
        }

        private void PrepareRegionalGround()
        {
            _regionalRotation = _target.transform.rotation;
            _regionalGround = new GameObject("Regional QA physical ground above arena");
            SceneManager.MoveGameObjectToScene(_regionalGround, _scene);
            for (int layer = 0; layer < 32; layer++)
                if ((_motor.GroundMask.value & (1 << layer)) != 0 &&
                    !UnityEngine.Physics.GetIgnoreLayerCollision(layer, _target.gameObject.layer))
                { _regionalGround.layer = layer; break; }
            _regionalGround.transform.SetPositionAndRotation(_target.transform.position + _motor.LocalUp * 12f, _regionalRotation);
            _regionalGround.AddComponent<BoxCollider>().size = new Vector3(20f, 1f, 20f);
            CapsuleCollider capsule = _target.GetComponent<CapsuleCollider>();
            Assert.That(capsule, Is.Not.Null);
            _regionalStart = _regionalGround.transform.position + _motor.LocalUp *
                (.5f + (capsule.height * .5f - capsule.center.y) * _target.transform.lossyScale.y + .025f);
        }

        private void ResetRegionalActor()
        {
            Rigidbody body = _target.GetComponent<Rigidbody>();
            body.position = _regionalStart;
            body.rotation = _regionalRotation;
            body.linearVelocity = body.angularVelocity = Vector3.zero;
            _target.transform.SetPositionAndRotation(_regionalStart, _regionalRotation);
            _motor.ResetAfterTeleport();
            _physics.ResetToAnimation();
            _accepted = default;
            _firstSurface = null;
            UnityEngine.Physics.SyncTransforms();
        }

        private static Bounds VisibleBounds(HumanoidCharacterPresentation presentation)
        {
            Bounds bounds = new(presentation.transform.position, Vector3.zero);
            bool first = true;
            foreach (SkinnedMeshRenderer renderer in presentation.Animator.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!IsCharacterSkin(renderer, presentation.Animator) || !renderer.enabled) continue;
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
            Assert.That(first, Is.False, "Production visual renderer bounds are required for full-body framing.");
            return bounds;
        }

        private static bool IsCharacterSkin(SkinnedMeshRenderer renderer, Animator animator) =>
            renderer.sharedMesh != null && renderer.rootBone != null &&
            renderer.rootBone.IsChildOf(animator.transform);

        private static void AssertReadableCharacterFraming(Camera camera, Bounds bounds)
        {
            Vector3 min = bounds.min, max = bounds.max;
            float bottom = 1f, top = 0f;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 viewport = camera.WorldToViewportPoint(new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z));
                Assert.That(viewport.z, Is.GreaterThan(camera.nearClipPlane));
                bottom = Mathf.Min(bottom, viewport.y);
                top = Mathf.Max(top, viewport.y);
            }
            Assert.That(top - bottom, Is.InRange(.4f, .8f), "Actual character skin must occupy 40–80% of the frame height.");
            Assert.That(bottom, Is.GreaterThan(.02f), "Character feet are clipped.");
            Assert.That(top, Is.LessThan(.98f), "Character head/accessories are clipped.");
        }

        private void OnResponse(EarthWorldResponseEvent response)
        {
            if (response.SourceStableId != _sourceId) return;
            _accepted = response;
            _acceptedEvents++;
        }

        private T Find<T>() where T : Component
        {
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            return null;
        }

        private static long Maximum(long[] samples)
        { long result = 0; foreach (long value in samples) result = Math.Max(result, value); return result; }
        private static string Statistics(string label, long[] samples)
        {
            Array.Sort(samples);
            double total = 0;
            foreach (long value in samples) total += value;
            return string.Format(CultureInfo.InvariantCulture, "{0}: n={1}, mean={2:F1}, p95={3}, max={4}",
                label, samples.Length, total / samples.Length, samples[Mathf.FloorToInt((samples.Length - 1) * .95f)], samples[^1]);
        }
    }

    public sealed class LocalPhysicsQaIdleInput : MonoBehaviour, IPlanetMotorInputSource
    {
        public PlanetMotorCommand SampleCommand(uint tick) => new(tick, float2.zero, false);
    }

    public sealed class LocalPhysicsAllocationWindow
    {
        public bool Measuring;
        public long FixedStart, PoseStart, FixedBytes, PoseBytes, FixedMaximum, PoseMaximum;
        public int FixedSamples, PoseSamples;
    }

    [DefaultExecutionOrder(2499)]
    public sealed class LocalPhysicsAllocationBegin : MonoBehaviour
    {
        [NonSerialized] public LocalPhysicsAllocationWindow Window;
        private void FixedUpdate() { if (Window != null && Window.Measuring) Window.FixedStart = GC.GetAllocatedBytesForCurrentThread(); }
        private void LateUpdate() { if (Window != null && Window.Measuring) Window.PoseStart = GC.GetAllocatedBytesForCurrentThread(); }
    }

    [DefaultExecutionOrder(2501)]
    public sealed class LocalPhysicsAllocationEnd : MonoBehaviour
    {
        [NonSerialized] public LocalPhysicsAllocationWindow Window;
        private void FixedUpdate()
        {
            if (Window == null || !Window.Measuring) return;
            long bytes = GC.GetAllocatedBytesForCurrentThread() - Window.FixedStart;
            Window.FixedBytes += bytes; Window.FixedMaximum = Math.Max(Window.FixedMaximum, bytes); Window.FixedSamples++;
        }
        private void LateUpdate()
        {
            if (Window == null || !Window.Measuring) return;
            long bytes = GC.GetAllocatedBytesForCurrentThread() - Window.PoseStart;
            Window.PoseBytes += bytes; Window.PoseMaximum = Math.Max(Window.PoseMaximum, bytes); Window.PoseSamples++;
        }
    }
}
