using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Elemental.Input.Gestures;
using Elemental.Runtime.World;
using Elemental.Runtime.Physics;
using Elemental.Runtime.Characters;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    [DisallowMultipleComponent]
    public sealed class VisualQaCaptureBehaviour : MonoBehaviour
    {
        [SerializeField, Min(1)] private int settleFrames = 90;

        private IEnumerator Start()
        {
            if (!VisualQaCaptureRequest.TryParse(Environment.GetCommandLineArgs(), out VisualQaCaptureRequest request))
                yield break;

            for (int frame = 0; frame < settleFrames; frame++) yield return null;

            if (request.DemonstrateMagic)
            {
                yield return Demonstrate(request.Scenario);
                if (!_scenarioSucceeded)
                {
                    Debug.LogError($"[Elemental] Visual QA scenario failed: {request.Scenario}.");
                    Application.Quit(5);
                    yield break;
                }
            }

            string fullPath = Path.GetFullPath(request.OutputPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                Debug.LogError("[Elemental] Visual QA output directory could not be resolved.");
                Application.Quit(3);
                yield break;
            }

            Directory.CreateDirectory(directory);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(fullPath, 1);

            float deadline = Time.realtimeSinceStartup + 10f;
            while (!File.Exists(fullPath) && Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSecondsRealtime(0.5f);

            bool captured = File.Exists(fullPath) && new FileInfo(fullPath).Length > 0;
            VoxelPlanetBehaviour planet = FindAnyObjectByType<VoxelPlanetBehaviour>();
            if (planet != null)
                Debug.Log($"[Elemental] Voxel render queue peak: {planet.PeakRenderQueueMilliseconds:0.00} ms; pending: {planet.PendingRenderCount}.");
            if (captured) Debug.Log($"[Elemental] Visual QA captured: {fullPath}");
            else Debug.LogError($"[Elemental] Visual QA capture failed: {fullPath}");
            Application.Quit(captured ? 0 : 4);
        }

        private bool _scenarioSucceeded;

        private IEnumerator Demonstrate(VisualQaScenario scenario)
        {
            MagicInputController input = FindAnyObjectByType<MagicInputController>();
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            GameObject proxyObject = GameObject.Find("Planet Collision Proxy");
            Collider proxy = proxyObject != null ? proxyObject.GetComponent<Collider>() : null;
            if (input == null || camera == null || proxy == null) yield break;

            Physics.SyncTransforms();
            List<float2> surfaceLine = FindSurfaceLine(camera, proxy);
            if (surfaceLine == null) yield break;

            if (scenario == VisualQaScenario.Dawn || scenario == VisualQaScenario.Night)
            {
                CelestialSystemBehaviour celestial = FindAnyObjectByType<CelestialSystemBehaviour>();
                if (celestial == null) yield break;
                celestial.SetTimeOfDayForQa(scenario == VisualQaScenario.Dawn ? 0.015f : 0.72f);
                _scenarioSucceeded = true;
                for (int frame = 0; frame < 8; frame++) yield return null;
                yield break;
            }

            if (scenario == VisualQaScenario.Meteor)
            {
                MeteorShowerBehaviour shower = FindAnyObjectByType<MeteorShowerBehaviour>();
                PlanetMotor sceneMotor = FindAnyObjectByType<PlanetMotor>();
                if (shower == null || sceneMotor == null) yield break;
                Vector3 center = proxy.bounds.center;
                Vector3 target = proxy.ClosestPoint(
                    sceneMotor.transform.position + sceneMotor.FacingForward * 6f +
                    camera.transform.right * 3.5f);
                Vector3 up = (target - center).normalized;
                _scenarioSucceeded = shower.SpawnForQa(
                    target + up * 13f - sceneMotor.FacingForward * 2f,
                    -up * 31f + sceneMotor.FacingForward * 5f,
                    0.72f);
                yield return new WaitForSecondsRealtime(0.48f);
                yield break;
            }

            if (scenario == VisualQaScenario.Wall || scenario == VisualQaScenario.WallCollapse ||
                scenario == VisualQaScenario.WallDebris)
            {
                _scenarioSucceeded = input.SelectEarthAbility(Elemental.Simulation.Magic.EarthAbilityIds.LineWall) &&
                                     input.TryCommitScreenPath(surfaceLine, 0.8f);
                if (scenario == VisualQaScenario.WallDebris)
                {
                    yield return new WaitForSecondsRealtime(1.05f);
                    EarthWall wall = FindAnyObjectByType<EarthWall>();
                    if (wall == null || !wall.ApplyRockImpact(
                            wall.transform.position + (wall.transform.up * wall.Height * 0.15f),
                            camera.transform.forward,
                            6200f))
                    {
                        _scenarioSucceeded = false;
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(2.25f);
                    yield break;
                }
                yield return new WaitForSecondsRealtime(
                    scenario == VisualQaScenario.WallCollapse ? 4.48f : 1.05f);
                yield break;
            }

            if (scenario == VisualQaScenario.GravityWell)
            {
                EarthWallPool wallPool = FindAnyObjectByType<EarthWallPool>();
                MagicExecutor executor = input.EarthExecutor;
                PlanetMotor gripMotor = FindAnyObjectByType<PlanetMotor>();
                VoxelPlanetBehaviour voxel = FindAnyObjectByType<VoxelPlanetBehaviour>();
                if (wallPool == null || executor == null || gripMotor == null || voxel == null) yield break;
                Vector3 planetCenter = voxel.transform.position;
                Vector3 radial = (gripMotor.transform.position - planetCenter).normalized;
                Vector3 forward = Vector3.ProjectOnPlane(gripMotor.FacingForward, radial).normalized;
                Vector3 baseDirection = (radial + forward * (7f / voxel.Radius)).normalized;
                Vector3 side = Vector3.Cross(baseDirection, forward).normalized;
                Vector3 start = planetCenter +
                                (baseDirection - side * (1.7f / voxel.Radius)).normalized * voxel.Radius;
                Vector3 end = planetCenter +
                              (baseDirection + side * (1.7f / voxel.Radius)).normalized * voxel.Radius;
                EarthWall wall = wallPool.Acquire(start, end, planetCenter, 2.1f, 0.55f);
                _scenarioSucceeded = wall != null;
                yield return new WaitForSecondsRealtime(1.05f);
                Collider wallCollider = wall != null ? wall.GetComponent<Collider>() : null;
                if (wall == null || wallCollider == null || executor == null) yield break;
                Vector3 focus = wall.transform.position + wall.transform.up * 3.2f + side * 2f;
                _scenarioSucceeded = executor.TryBeginGravityWell(wallCollider, focus, wall.transform.up) &&
                                     wall.ApplyRockImpact(focus, camera.transform.forward, 6200f);
                // Fracture happens after the grip has remembered the whole structure.
                // Its source collider is disabled, so only the latched-source path can register these pieces.
                yield return new WaitForSecondsRealtime(1.2f);
                _scenarioSucceeded &= executor.IsGravityWellActive && executor.GravityWellCapturedCount >= 8;
                var sourceTargets = new IEarthPhysicalTarget[48];
                int sourceCount = wall.CopyActiveTargetsNonAlloc(sourceTargets);
                Debug.Log($"[Elemental] Gravity QA wall={wall.WallId}, fractured={wall.IsCollapsing}, " +
                          $"active={wall.ActiveFracturePieceCount}, source={sourceCount}, " +
                          $"captured={executor.GravityWellCapturedCount}/{executor.GravityWellMaximumCapturedTargets}, " +
                          $"executorActive={executor.isActiveAndEnabled}.");
                yield break;
            }

            if (scenario == VisualQaScenario.Platform)
            {
                float2 start = surfaceLine[0];
                float2 end = surfaceLine[1];
                float heightOffset = Mathf.Min(54f, Screen.height * 0.07f);
                var outline = new List<float2>(5)
                {
                    start,
                    start + new float2(0f, heightOffset),
                    end + new float2(0f, heightOffset),
                    end,
                    math.lerp(start, end, 0.5f) - new float2(0f, heightOffset * 0.25f)
                };
                _scenarioSucceeded = input.SelectEarthAbility(Elemental.Simulation.Magic.EarthAbilityIds.RaisePlatform) &&
                                     input.TryCommitScreenPath(outline, 1.1f);
                yield return new WaitForSecondsRealtime(0.35f);
                yield break;
            }

            if (scenario == VisualQaScenario.PillarWave)
            {
                EarthPillarWavePool pool = FindAnyObjectByType<EarthPillarWavePool>();
                PlanetMotor motor = FindAnyObjectByType<PlanetMotor>();
                Rigidbody body = motor != null ? motor.GetComponent<Rigidbody>() : null;
                if (pool != null && motor != null && body != null)
                {
                    Vector3 up = motor.LocalUp.sqrMagnitude > 0.5f ? motor.LocalUp.normalized : motor.transform.up;
                    int count = pool.Launch(
                        body.worldCenterOfMass - (up * 1.25f),
                        up,
                        motor.FacingForward,
                        0.45f,
                        1f,
                        body);
                    _scenarioSucceeded = count >= 40;
                }
                // Capture the travelling state: the near rows have already sunk while
                // the distinct outer crest is reaching full height.
                yield return new WaitForSecondsRealtime(1.35f);
                yield break;
            }

            if (scenario == VisualQaScenario.LandingCushion)
            {
                Material earthMaterial = FindEarthMaterial();
                PlanetMotor sceneMotor = FindAnyObjectByType<PlanetMotor>();
                Rigidbody sceneBody = sceneMotor != null ? sceneMotor.GetComponent<Rigidbody>() : null;
                if (sceneMotor == null || sceneBody == null) yield break;
                GameObject actor = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                actor.name = "Visual QA Falling Body";
                actor.GetComponent<MeshRenderer>().enabled = false;
                Vector3 center = proxy.bounds.center;
                Vector3 target = sceneBody.worldCenterOfMass + (sceneMotor.FacingForward * 4f);
                Vector3 surface = proxy.ClosestPoint(target);
                Vector3 up = (surface - center).normalized;
                actor.transform.position = surface + (up * 6f);
                Rigidbody body = actor.AddComponent<Rigidbody>();
                body.useGravity = false;
                body.linearVelocity = -up * 12f;
                PlanetMotor motor = actor.AddComponent<PlanetMotor>();
                motor.BeginExternalLaunch(90);
                motor.enabled = false;
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "Visual QA Cushion";
                visual.GetComponent<MeshRenderer>().sharedMaterial = earthMaterial;
                Destroy(visual.GetComponent<Collider>());
                EarthLandingCushion cushion = actor.AddComponent<EarthLandingCushion>();
                cushion.Configure(body, motor, null, proxy, null, visual.transform);
                _scenarioSucceeded = cushion.BeginHold();
                yield return new WaitForSecondsRealtime(0.36f);
                _scenarioSucceeded &= cushion.LastPrediction.Valid;
                yield break;
            }

            float2 pullStart = surfaceLine[0];
            var pullStroke = new List<float2>(2)
            {
                pullStart,
                pullStart + new float2(4f, 74f)
            };
            if (!input.SelectEarthAbility(Elemental.Simulation.Magic.EarthAbilityIds.PullRock)) yield break;
            if (scenario == VisualQaScenario.PullPreview)
            {
                _scenarioSucceeded = input.TryPreviewScreenPath(pullStroke, 0.8f);
                for (int frame = 0; frame < 3; frame++) yield return null;
                yield break;
            }

            if (!input.TryCommitScreenPath(pullStroke, 0.8f)) yield break;
            for (int frame = 0; frame < 30; frame++) yield return null;
            if (scenario == VisualQaScenario.PullHeld || scenario == VisualQaScenario.MageCast)
            {
                _scenarioSucceeded = true;
                yield break;
            }

            if (scenario != VisualQaScenario.Throw) yield break;
            if (!input.SelectEarthAbility(Elemental.Simulation.Magic.EarthAbilityIds.FlickThrow)) yield break;
            var flickStroke = new List<float2>(2)
            {
                new float2(Screen.width * 0.44f, Screen.height * 0.48f),
                new float2(Screen.width * 0.62f, Screen.height * 0.55f)
            };
            input.TryPreviewScreenPath(flickStroke, 0.18f);
            _scenarioSucceeded = input.TryCommitScreenPath(flickStroke, 0.18f);
            for (int frame = 0; frame < 4; frame++) yield return null;
        }

        private static Material FindEarthMaterial()
        {
            MeshRenderer[] renderers = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include);
            foreach (MeshRenderer renderer in renderers)
            {
                if (renderer == null || renderer.sharedMaterial == null) continue;
                if (renderer.name == "Earth Landing Cushion Preview")
                    return renderer.sharedMaterial;
            }

            foreach (MeshRenderer renderer in renderers)
            {
                Material material = renderer != null ? renderer.sharedMaterial : null;
                if (material != null && material.shader != null && material.shader.isSupported)
                    return material;
            }

            return null;
        }

        private static List<float2> FindSurfaceLine(UnityEngine.Camera camera, Collider proxy)
        {
            int width = Mathf.Max(320, Screen.width);
            int height = Mathf.Max(200, Screen.height);
            var candidates = new List<List<float2>>(24);
            for (int y = 16; y < height - 16; y += 8)
            {
                int first = -1;
                int last = -1;
                for (int x = 16; x < width - 16; x += 8)
                {
                    if (!proxy.Raycast(camera.ScreenPointToRay(new Vector2(x, y)), out _, 200f)) continue;
                    if (first < 0) first = x;
                    last = x;
                }

                if (last - first < 80) continue;
                int inset = Mathf.Max(16, (last - first) / 5);
                candidates.Add(new List<float2>(2)
                {
                    new float2(first + inset, y),
                    new float2(last - inset, y)
                });
            }

            if (candidates.Count == 0) return null;
            int chosen = Mathf.Clamp(Mathf.RoundToInt((candidates.Count - 1) * 0.62f), 0, candidates.Count - 1);
            return candidates[chosen];
        }
    }
}
