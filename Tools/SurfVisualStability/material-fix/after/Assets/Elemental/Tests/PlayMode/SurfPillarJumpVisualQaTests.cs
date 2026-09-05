using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Elemental.Input.Actions;
using Elemental.Presentation.Camera;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class SurfPillarJumpVisualQaTests
    {
        private const int Width = 960;
        private const int Height = 540;

        [Serializable]
        private sealed class CaptureReport
        {
            public bool passed;
            public string scene;
            public string surfFrame;
            public string breakFrame;
            public string airborneFrame;
            public int releasedStones;
            public int attachedStonesBeforeRelease;
            public int pillarEvents;
            public float charge01;
            public float pillarTiltDegrees;
            public float riderRiseMeters;
            public float riderForwardMeters;
            public float maximumObservedRiseUpSpeed;
            public bool surfFramed;
            public bool pillarFramed;
            public int breakStonesFramed;
            public bool airborneRiderFramed;
            public bool launchOriginFramed;
        }

        private readonly struct BehaviourState
        {
            public BehaviourState(Behaviour component)
            {
                Component = component;
                Enabled = component.enabled;
            }

            public Behaviour Component { get; }
            public bool Enabled { get; }
        }

        [UnityTest]
        public IEnumerator ProductionSideViewShowsChargedTiltedPillarLongJump()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            Scene scene = default;
            var suspended = new List<BehaviourState>();
            Keyboard keyboard = null;
            Mouse mouse = null;
            PlayerInput playerInput = null;
            bool previousNeverAutoSwitch = false;
            EarthPillarMobility pillar = null;
            Action<EarthPillarLaunchEvent> countPillar = null;
            GameObject qaRunway = null;
            Mesh qaRunwayMesh = null;

            try
            {
                yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
                scene = SceneManager.GetSceneByPath(scenePath);
                EarthSceneReadinessGate gate = FindInScene<EarthSceneReadinessGate>(scene);
                for (int frame = 0; frame < 2400 && gate != null && !gate.IsReady && !gate.Failed; frame++)
                    yield return null;
                Assert.That(gate, Is.Not.Null);
                Assert.That(gate.Failed, Is.False);
                Assert.That(gate.IsReady, Is.True);

                EarthSurfController surf = FindInScene<EarthSurfController>(scene);
                PlanetMotor motor = surf != null ? surf.GetComponent<PlanetMotor>() : null;
                Rigidbody body = motor != null ? motor.Body : null;
                pillar = motor != null ? motor.GetComponent<EarthPillarMobility>() : null;
                ActiveRagdollPuppet puppet = motor != null ? motor.GetComponent<ActiveRagdollPuppet>() : null;
                EarthActionRouterBehaviour router = motor != null
                    ? motor.GetComponent<EarthActionRouterBehaviour>()
                    : null;
                playerInput = motor != null ? motor.GetComponent<PlayerInput>() : null;
                Camera camera = FindProductionCamera(scene);
                for (int frame = 0; frame < 120 && motor != null && !motor.IsGrounded; frame++)
                    yield return new WaitForFixedUpdate();

                Assert.That(surf, Is.Not.Null);
                Assert.That(body, Is.Not.Null);
                Assert.That(pillar, Is.Not.Null);
                Assert.That(router, Is.Not.Null);
                Assert.That(playerInput, Is.Not.Null);
                Assert.That(camera, Is.Not.Null);

                EarthMvpBotController rival = FindInScene<EarthMvpBotController>(scene);
                // Disabling the brain is insufficient: the bot capsule and active
                // ragdoll remain in the launch lane and visibly deflect the rider.
                if (rival != null) rival.gameObject.SetActive(false);
                suspended = SuspendQaOwners(scene);
                StageOnCurvedRunway(
                    scene, motor, body,
                    out qaRunway, out qaRunwayMesh,
                    out Vector3 up, out Vector3 stagedForward);
                for (int frame = 0; frame < 18 && !motor.IsGrounded; frame++)
                    yield return new WaitForFixedUpdate();
                Assert.That(motor.IsGrounded, Is.True,
                    "The temporary additive QA runway did not become valid production ground support.");
                Vector3 facing = SurfPillarQaLane.FindClearDirection(
                    body,
                    motor,
                    stagedForward,
                    out float clearDistance);
                Assert.That(clearDistance, Is.GreaterThan(5.5f),
                    "The capture needs an unobstructed production lane through the arena ring.");
                motor.SetAimDirection(facing);
                Assert.That(facing.sqrMagnitude, Is.GreaterThan(0.5f));
                Vector3 surfStart = body.worldCenterOfMass;

                string directory = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "BuildReports/EnvironmentAnimationRescue/SurfPillarJumpVisualQa",
                    DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
                Directory.CreateDirectory(directory);
                string surfFrame = Path.Combine(directory, "01-Surf.png");
                string breakFrame = Path.Combine(directory, "02-PillarBreak.png");
                string airborneFrame = Path.Combine(directory, "03-Airborne.png");

                mouse = InputSystem.AddDevice<Mouse>("Surf Pillar Jump QA Mouse");
                keyboard = InputSystem.AddDevice<Keyboard>("Surf Pillar Jump QA Keyboard");
                previousNeverAutoSwitch = playerInput.neverAutoSwitchControlSchemes;
                playerInput.neverAutoSwitchControlSchemes = true;
                playerInput.ActivateInput();
                Assert.That(playerInput.user.valid, Is.True);
                playerInput.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
                Assert.That(playerInput.currentControlScheme, Is.EqualTo("Keyboard&Mouse"));
                playerInput.currentActionMap?.Enable();
                QueueMouse(mouse);
                QueueKeys(keyboard, Key.LeftShift, Key.W);
                for (int frame = 0; frame < 120 && !surf.IsActive; frame++) yield return null;
                Assert.That(surf.IsActive, Is.True,
                    "Physical Shift+W must enter surf through the shipping input map.");
                for (int frame = 0; frame < 14 && motor.MovingSurfaceId != surf.SurfaceId; frame++)
                    yield return new WaitForFixedUpdate();
                yield return null;

                var stones = new Vector3[EarthSurfCellGraph.CellCount];
                ComposeSideShot(camera, surfStart, body.worldCenterOfMass, up, facing, stones, 0, null);
                bool surfFramed = IsFramed(camera, surfStart, 0.05f) &&
                                  IsFramed(camera, body.worldCenterOfMass, 0.05f);
                Capture(camera, surfFrame);

                int pillarEvents = 0;
                countPillar = _ => pillarEvents++;
                pillar.PillarRaised += countPillar;
                QueueKeys(keyboard, Key.LeftShift, Key.W, Key.Space);
                yield return null;
                Assert.That(pillar.IsCharging, Is.True,
                    "Space press during surf must begin the existing pillar charge without launching.");
                Assert.That(router.SurfPillarJumpSequence, Is.Zero,
                    "The surf board must stay intact while Space remains held.");
                double releaseAt = Time.realtimeSinceStartupAsDouble + 0.72d;
                int chargeFrame = 0;
                while (Time.realtimeSinceStartupAsDouble < releaseAt)
                {
                    QueueKeys(keyboard, Key.LeftShift, Key.W, Key.Space);
                    yield return null;
                    Debug.Log(
                        $"[SurfPillarInputDiag] frame={chargeFrame++} owner={router.Owner} " +
                        $"route={router.Current.Owner}/{router.Current.Phase}/{router.Current.Intent} " +
                        $"active={surf.IsActive} charging={pillar.IsCharging} charge={pillar.Charge01:F3} " +
                        $"stable={motor.HasStableSupport} grounded={motor.IsGrounded} " +
                        $"support=0x{motor.MovingSurfaceId:X8} " +
                        $"command=({motor.LastCommand.Move.x:F3},{motor.LastCommand.Move.y:F3}) " +
                        $"ragdoll={(puppet != null ? puppet.CurrentState.Mode.ToString() : "none")}");
                }
                float charge01 = pillar.Charge01;
                Assert.That(charge01, Is.GreaterThan(0.35f));
                // The board moves around a curved planet while charging.
                // Measure tilt in the local gravity frame at release.
                up = motor.LocalUp.normalized;
                Vector3 travel = Vector3.ProjectOnPlane(surf.SurfaceVelocity, up);
                if (travel.sqrMagnitude < 0.25f) travel = facing;
                travel.Normalize();
                Vector3 launchStart = body.worldCenterOfMass;
                int attachedStonesBeforeRelease = 0;
                for (int cell = 0; cell < EarthSurfCellGraph.CellCount; cell++)
                    if ((surf.AttachedCellMask & (1 << cell)) != 0) attachedStonesBeforeRelease++;
                Assert.That(attachedStonesBeforeRelease, Is.GreaterThanOrEqualTo(8),
                    "The launch proof requires a substantial live board after its real terrain traversal.");

                QueueKeys(keyboard, Key.LeftShift, Key.W);
                yield return null;
                Assert.That(router.SurfPillarJumpSequence, Is.EqualTo(1u),
                    "Physical Space release must reach the shipping router exactly once.");
                Assert.That(pillarEvents, Is.EqualTo(1));
                Assert.That(pillar.IsCharging, Is.False);
                Vector3 launchAxis = pillar.LastLaunchDirection.normalized;
                float pillarTilt = Vector3.Angle(up, launchAxis);
                Assert.That(pillarTilt, Is.InRange(18f, 28.1f));
                Assert.That(Vector3.Dot(launchAxis, travel), Is.GreaterThan(0.25f));

                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                float upSpeed = Vector3.Dot(body.linearVelocity, up);
                yield return null;
                int releasedCount = surf.CopyReleasedStonePositionsNonAlloc(stones);
                GameObject pillarVisual = FindByName(scene, "Rising Earth Pillar");
                ComposeSideShot(
                    camera, launchStart, body.worldCenterOfMass, up, travel,
                    stones, releasedCount, pillarVisual);
                bool pillarFramed = IsPillarFramed(camera, pillarVisual, launchAxis);
                int breakStonesFramed = CountFramed(camera, stones, releasedCount, 0.025f);
                Capture(camera, breakFrame);

                // Rendering the break frame already consumes part of the rise.
                // Observe its actual completion instead of waiting a second full
                // rise interval and accidentally measuring the descending arc.
                int riseTicks = Mathf.CeilToInt(pillar.LastLaunch.RiseSeconds / Time.fixedDeltaTime) + 3;
                for (int frame = 0; frame < riseTicks && pillar.IsLaunchPending; frame++)
                {
                    yield return new WaitForFixedUpdate();
                    upSpeed = Mathf.Max(upSpeed, Vector3.Dot(body.linearVelocity, up));
                }
                Assert.That(pillar.IsLaunchPending, Is.False);
                float rise = Vector3.Dot(body.worldCenterOfMass - launchStart, up);
                float forwardTravel = Vector3.Dot(body.worldCenterOfMass - launchStart, travel);
                yield return null;
                ComposeSideShot(
                    camera, launchStart, body.worldCenterOfMass, up, travel,
                    stones, 0, pillarVisual);
                bool airborneRiderFramed = IsFramed(camera, body.worldCenterOfMass, 0.05f);
                bool launchOriginFramed = IsFramed(camera, launchStart - up * 0.6f, 0.05f);
                Capture(camera, airborneFrame);

                // A terrain seam may already have shed an outer stone during
                // charge. Every stone still on this finite board must be released;
                // the independent physical test also covers the intact 12/12 case.
                bool passed = pillarEvents == 1 && releasedCount >= attachedStonesBeforeRelease &&
                              !surf.IsActive &&
                              charge01 > 0.35f && pillarTilt >= 18f && forwardTravel > 0.35f &&
                              rise > 0.35f && upSpeed > 2.5f && surfFramed && pillarFramed &&
                              breakStonesFramed >= 8 && airborneRiderFramed && launchOriginFramed &&
                              FileSize(surfFrame) > 4096 && FileSize(breakFrame) > 4096 &&
                              FileSize(airborneFrame) > 4096;
                var report = new CaptureReport
                {
                    passed = passed,
                    scene = scenePath,
                    surfFrame = surfFrame,
                    breakFrame = breakFrame,
                    airborneFrame = airborneFrame,
                    releasedStones = releasedCount,
                    attachedStonesBeforeRelease = attachedStonesBeforeRelease,
                    pillarEvents = pillarEvents,
                    charge01 = charge01,
                    pillarTiltDegrees = pillarTilt,
                    riderRiseMeters = rise,
                    riderForwardMeters = forwardTravel,
                    maximumObservedRiseUpSpeed = upSpeed,
                    surfFramed = surfFramed,
                    pillarFramed = pillarFramed,
                    breakStonesFramed = breakStonesFramed,
                    airborneRiderFramed = airborneRiderFramed,
                    launchOriginFramed = launchOriginFramed
                };
                File.WriteAllText(
                    Path.Combine(directory, "CaptureReport.json"),
                    JsonUtility.ToJson(report, true));

                Assert.That(pillarVisual, Is.Not.Null);
                Assert.That(Vector3.Dot(pillarVisual.transform.up, launchAxis), Is.GreaterThan(0.98f),
                    "The visible column must lean along the same axis that launches the rider.");
                Assert.That(passed, Is.True,
                    $"Visual proof failed: charge={charge01:F2}, tilt={pillarTilt:F1}, " +
                    $"events={pillarEvents}, stones={releasedCount}/{breakStonesFramed} framed, " +
                    $"rise={rise:F2}, forward={forwardTravel:F2}, upSpeed={upSpeed:F2}, " +
                    $"report={directory}");

                QueueKeys(keyboard);
                QueueMouse(mouse);
                AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null) yield return unload;
            }
            finally
            {
                if (pillar != null && countPillar != null) pillar.PillarRaised -= countPillar;
                if (keyboard != null && keyboard.added)
                {
                    QueueKeys(keyboard);
                    InputSystem.RemoveDevice(keyboard);
                }
                if (mouse != null && mouse.added)
                {
                    QueueMouse(mouse);
                    InputSystem.RemoveDevice(mouse);
                }
                if (playerInput != null)
                    playerInput.neverAutoSwitchControlSchemes = previousNeverAutoSwitch;
                Restore(suspended);
                if (qaRunway != null) UnityEngine.Object.Destroy(qaRunway);
                if (qaRunwayMesh != null) UnityEngine.Object.Destroy(qaRunwayMesh);
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            }
        }

        private static void StageOnCurvedRunway(
            Scene scene,
            PlanetMotor motor,
            Rigidbody rider,
            out GameObject runway,
            out Mesh runwayMesh,
            out Vector3 riderUp,
            out Vector3 riderForward)
        {
            PointPlanetGravitySource planet = FindInScene<PointPlanetGravitySource>(scene);
            Assert.That(planet, Is.Not.Null, "Production point-planet gravity source is missing.");
            Vector3 center = planet.transform.position;
            Vector3 centerUp = (rider.worldCenterOfMass - center).normalized;
            Vector3 centerForward = Vector3.ProjectOnPlane(motor.FacingForward, centerUp).normalized;
            if (centerForward.sqrMagnitude < .5f)
                centerForward = Vector3.ProjectOnPlane(rider.transform.forward, centerUp).normalized;
            Vector3 centerRight = Vector3.Cross(centerUp, centerForward).normalized;
            float radius = planet.Radius + 8f;

            runway = new GameObject("QA Surf Curved Runway");
            SceneManager.MoveGameObjectToScene(runway, scene);
            runway.transform.position = center;
            runwayMesh = BuildCurvedRunwayMesh(radius, centerUp, centerRight, centerForward);
            runway.AddComponent<MeshFilter>().sharedMesh = runwayMesh;
            MeshRenderer renderer = runway.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = FindEarthMaterial(scene);
            runway.AddComponent<MeshCollider>().sharedMesh = runwayMesh;

            // Begin eight metres behind the runway centre. The remaining 24 m are
            // enough for real Shift+W assembly, charge and tilted release without an
            // arena seam generating unrelated SupportTransfer damage.
            Vector3 stagedDirection = (centerUp - centerForward * (8f / radius)).normalized;
            Vector3 surface = center + stagedDirection * radius;
            riderUp = stagedDirection;
            riderForward = Vector3.ProjectOnPlane(centerForward, riderUp).normalized;
            Vector3 desiredCenterOfMass = surface + riderUp * .94f;
            Quaternion desiredRotation = Quaternion.LookRotation(riderForward, riderUp);
            TeleportRiderRig(motor, rider, desiredCenterOfMass, desiredRotation);
            motor.SetAimDirection(riderForward);
            motor.SettleTangentialMotion();
            Physics.SyncTransforms();
        }

        private static Mesh BuildCurvedRunwayMesh(
            float radius,
            Vector3 up,
            Vector3 right,
            Vector3 forward)
        {
            const int xSegments = 16;
            const int zSegments = 48;
            const float width = 12f;
            const float length = 32f;
            var vertices = new Vector3[(xSegments + 1) * (zSegments + 1)];
            var triangles = new int[xSegments * zSegments * 6];
            int vertex = 0;
            for (int z = 0; z <= zSegments; z++)
            {
                float localZ = Mathf.Lerp(-length * .5f, length * .5f, z / (float)zSegments);
                for (int x = 0; x <= xSegments; x++)
                {
                    float localX = Mathf.Lerp(-width * .5f, width * .5f, x / (float)xSegments);
                    Vector3 direction = (up + right * (localX / radius) + forward * (localZ / radius)).normalized;
                    vertices[vertex++] = direction * radius;
                }
            }
            int triangle = 0;
            int row = xSegments + 1;
            for (int z = 0; z < zSegments; z++)
            for (int x = 0; x < xSegments; x++)
            {
                int a = z * row + x;
                int b = a + 1;
                int c = a + row;
                int d = c + 1;
                triangles[triangle++] = a; triangles[triangle++] = c; triangles[triangle++] = d;
                triangles[triangle++] = a; triangles[triangle++] = d; triangles[triangle++] = b;
            }
            var mesh = new Mesh { name = "QA Surf Curved Runway Mesh" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void TeleportRiderRig(
            PlanetMotor motor,
            Rigidbody rider,
            Vector3 desiredCenterOfMass,
            Quaternion desiredRotation)
        {
            Vector3 pivot = rider.worldCenterOfMass;
            Quaternion rotationDelta = desiredRotation * Quaternion.Inverse(rider.rotation);
            Vector3 translation = desiredCenterOfMass - pivot;
            Rigidbody[] bodies = motor.GetComponentsInChildren<Rigidbody>(true);
            for (int index = 0; index < bodies.Length; index++)
            {
                Rigidbody body = bodies[index];
                body.position = desiredCenterOfMass + rotationDelta * (body.position - pivot);
                body.rotation = rotationDelta * body.rotation;
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
            // Some rigs expose only the motor body through the root query.
            if (Array.IndexOf(bodies, rider) < 0)
            {
                rider.position += translation;
                rider.rotation = desiredRotation;
                rider.linearVelocity = Vector3.zero;
                rider.angularVelocity = Vector3.zero;
            }
        }

        private static Material FindEarthMaterial(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                // Use the exact material already bound to production arena/decor
                // geometry. A shader-name search can select the procedural terrain
                // shader, whose vertex contract does not match this ordinary mesh.
                if (renderer.GetComponentInParent<EarthArenaStructure>(true) == null)
                    continue;

                Material[] materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                {
                    Material material = materials[index];
                    if (material != null && string.Equals(
                            material.name, "RumbleArenaSandstone", StringComparison.Ordinal))
                        return material;
                }
            }

            throw new InvalidOperationException(
                "Surf visual QA requires the production arena/decor RumbleArenaSandstone material.");
        }

        private static List<BehaviourState> SuspendQaOwners(Scene scene)
        {
            var states = new List<BehaviourState>();
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Behaviour component in root.GetComponentsInChildren<Behaviour>(true))
            {
                string type = component.GetType().FullName;
                bool suspend = component is PlanetCameraRig ||
                               component is EarthCinemachineCameraController ||
                               component is EarthCameraDirector ||
                               component is EarthMvpBotController ||
                               type == "Unity.Cinemachine.CinemachineBrain" ||
                               type == "Cinemachine.CinemachineBrain" ||
                               type == "Unity.Cinemachine.CinemachineCamera" ||
                               type == "Cinemachine.CinemachineCamera";
                if (!suspend) continue;
                states.Add(new BehaviourState(component));
                component.enabled = false;
            }
            return states;
        }

        private static void Restore(List<BehaviourState> states)
        {
            for (int index = states.Count - 1; index >= 0; index--)
                if (states[index].Component != null)
                    states[index].Component.enabled = states[index].Enabled;
        }

        private static void ComposeSideShot(
            Camera camera,
            Vector3 origin,
            Vector3 rider,
            Vector3 up,
            Vector3 travel,
            Vector3[] stones,
            int stoneCount,
            GameObject pillar)
        {
            // This additive QA scene uses a known perspective projection. The
            // shipping physical camera's sensor gate otherwise crops this 16:9
            // render request after the geometric framing calculation.
            camera.usePhysicalProperties = false;
            camera.orthographic = false;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.aspect = Width / (float)Height;
            camera.ResetWorldToCameraMatrix();
            camera.ResetProjectionMatrix();
            up.Normalize();
            travel = Vector3.ProjectOnPlane(travel, up).normalized;
            Vector3 side = Vector3.Cross(up, travel).normalized;
            float originUp = Vector3.Dot(origin, up);
            float originTravel = Vector3.Dot(origin, travel);
            float minUp = originUp - 1.25f;
            float maxUp = originUp + 2.4f;
            float minTravel = originTravel - 3.25f;
            float maxTravel = originTravel + 3.25f;
            Include(rider - up, up, travel, ref minUp, ref maxUp, ref minTravel, ref maxTravel);
            Include(rider + up * 1.5f, up, travel, ref minUp, ref maxUp, ref minTravel, ref maxTravel);
            for (int index = 0; index < stoneCount; index++)
                if (Finite(stones[index]))
                    Include(stones[index], up, travel, ref minUp, ref maxUp, ref minTravel, ref maxTravel);
            IncludeRenderers(pillar, up, travel, ref minUp, ref maxUp, ref minTravel, ref maxTravel);

            Vector3 focus = origin +
                            up * ((minUp + maxUp) * 0.5f - originUp) +
                            travel * ((minTravel + maxTravel) * 0.5f - originTravel);
            const float fov = 54f;
            float tangent = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float halfHeight = (maxUp - minUp) * 0.5f + 0.65f;
            float halfWidth = (maxTravel - minTravel) * 0.5f + 0.75f;
            float distance = Mathf.Max(halfHeight / tangent, halfWidth / (tangent * (Width / (float)Height)));
            distance = Mathf.Clamp(distance * 1.08f, 7.5f, 18f);
            camera.transform.SetPositionAndRotation(
                focus + side * distance,
                Quaternion.LookRotation(-side, up));
            camera.fieldOfView = fov;
        }

        private static void Include(
            Vector3 point,
            Vector3 up,
            Vector3 travel,
            ref float minUp,
            ref float maxUp,
            ref float minTravel,
            ref float maxTravel)
        {
            float vertical = Vector3.Dot(point, up);
            float horizontal = Vector3.Dot(point, travel);
            minUp = Mathf.Min(minUp, vertical);
            maxUp = Mathf.Max(maxUp, vertical);
            minTravel = Mathf.Min(minTravel, horizontal);
            maxTravel = Mathf.Max(maxTravel, horizontal);
        }

        private static void IncludeRenderers(
            GameObject root,
            Vector3 up,
            Vector3 travel,
            ref float minUp,
            ref float maxUp,
            ref float minTravel,
            ref float maxTravel)
        {
            if (root == null || !root.activeInHierarchy) return;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(false))
            {
                Bounds bounds = renderer.bounds;
                Vector3 extents = bounds.extents;
                float upRadius = Mathf.Abs(up.x) * extents.x + Mathf.Abs(up.y) * extents.y +
                                 Mathf.Abs(up.z) * extents.z;
                float travelRadius = Mathf.Abs(travel.x) * extents.x + Mathf.Abs(travel.y) * extents.y +
                                     Mathf.Abs(travel.z) * extents.z;
                float centerUp = Vector3.Dot(bounds.center, up);
                float centerTravel = Vector3.Dot(bounds.center, travel);
                minUp = Mathf.Min(minUp, centerUp - upRadius);
                maxUp = Mathf.Max(maxUp, centerUp + upRadius);
                minTravel = Mathf.Min(minTravel, centerTravel - travelRadius);
                maxTravel = Mathf.Max(maxTravel, centerTravel + travelRadius);
            }
        }

        private static bool IsPillarFramed(Camera camera, GameObject pillar, Vector3 axis)
        {
            if (pillar == null || !pillar.activeInHierarchy) return false;
            Renderer[] renderers = pillar.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0) return false;
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            Vector3 extents = bounds.extents;
            float radius = Mathf.Abs(axis.x) * extents.x + Mathf.Abs(axis.y) * extents.y +
                           Mathf.Abs(axis.z) * extents.z;
            return IsFramed(camera, bounds.center, 0.035f) &&
                   IsFramed(camera, bounds.center - axis * radius, 0.025f) &&
                   IsFramed(camera, bounds.center + axis * radius, 0.025f);
        }

        private static int CountFramed(Camera camera, Vector3[] points, int count, float margin)
        {
            int framed = 0;
            for (int index = 0; index < count; index++)
                if (Finite(points[index]) && IsFramed(camera, points[index], margin)) framed++;
            return framed;
        }

        private static bool IsFramed(Camera camera, Vector3 point, float margin)
        {
            Vector3 viewport = camera.WorldToViewportPoint(point);
            return viewport.z > camera.nearClipPlane &&
                   viewport.x > margin && viewport.x < 1f - margin &&
                   viewport.y > margin && viewport.y < 1f - margin;
        }

        private static bool Finite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        private static void Capture(Camera camera, string path)
        {
            RenderTexture texture = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousActive = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                var request = new RenderPipeline.StandardRequest { destination = texture };
                if (!RenderPipeline.SupportsRenderRequest(camera, request))
                    throw new InvalidOperationException(
                        $"Active render pipeline cannot submit a standard request for '{camera.name}'.");
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = texture;
                pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                pixels.Apply(false, false);
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(texture);
                if (pixels != null) UnityEngine.Object.Destroy(pixels);
            }
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        private static GameObject FindByName(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child.gameObject;
            return null;
        }

        private static Camera FindProductionCamera(Scene scene)
        {
            Camera fallback = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Camera candidate in root.GetComponentsInChildren<Camera>(true))
            {
                fallback ??= candidate;
                if (candidate.CompareTag("MainCamera")) return candidate;
            }
            return fallback;
        }

        private static long FileSize(string path) => File.Exists(path) ? new FileInfo(path).Length : 0L;

        private static void QueueKeys(Keyboard keyboard, params Key[] keys)
        {
            if (keyboard != null && keyboard.added)
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        }

        private static void QueueMouse(Mouse mouse)
        {
            if (mouse != null && mouse.added)
                InputSystem.QueueStateEvent(mouse, new MouseState());
        }
    }
}
