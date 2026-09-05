// Production visual evidence only. No scene save, profile edit, or asset mutation.
using System;
using System.Collections.Generic;
using System.IO;
using Elemental.Input.Actions;
using Elemental.Input.Gestures;
using Elemental.Presentation.Camera;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Camera = UnityEngine.Camera;
using Object = UnityEngine.Object;

namespace Elemental.Authoring.Editor
{
    /// <summary>
    /// Captures one immutable production-particle layout at day, dusk, and night.
    /// The helper runs only when explicitly invoked in ready Play Mode and restores
    /// camera, celestial phase, time scale, behaviours, transforms, and particles.
    /// </summary>
    public static class DustProductionVisualQa
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private const int SettleFrames = 10;
        private const int WriteFrames = 5;
        private static readonly Shot[] Shots =
        {
            new("Day", .25f),
            new("Dusk", .49f),
            new("Night", .75f)
        };
        private static readonly Dictionary<Behaviour, bool> SavedBehaviours = new();
        private static readonly List<ParticleSnapshot> ParticleSnapshots = new();
        private static readonly List<CaptureRecord> Captures = new();

        private static bool _running, _captureRequested;
        private static int _shotIndex, _renderFrames, _poseFrame, _captureFrame, _lastRenderedFrame;
        private static double _deadline;
        private static DateTime _captureRequestedUtc;
        private static Scene _scene;
        private static Camera _camera;
        private static CelestialSystemBehaviour _sky;
        private static Vector3 _savedCameraPosition, _captureCameraPosition, _captureTarget, _up, _impactPoint;
        private static Quaternion _savedCameraRotation, _captureCameraRotation;
        private static float _savedFov, _savedPhase, _savedTimeScale;
        private static string _folder;
        private static Report _report;

        private readonly struct Shot
        {
            public readonly string Name;
            public readonly float Phase;
            public Shot(string name, float phase) { Name = name; Phase = phase; }
        }

        [Serializable]
        public sealed class ParticleRecord
        {
            public string objectPath, material, shader, layoutHash;
            public int particles;
        }

        [Serializable]
        public sealed class CaptureRecord
        {
            public string name, path, utc, impactLayoutHash, fractureLayoutHash, moteLayoutHash;
            public float requestedPhase, actualPhase, sunIntensity, moonIntensity, nightFraction;
            public Color sunColor, moonColor, ambientSky, ambientEquator, ambientGround;
            public Vector3 cameraPosition, cameraEuler, impactWorld, impactViewport;
            public int impactParticles, fractureParticles, moteParticles, screenWidth, screenHeight;
            public long pngBytes;
        }

        [Serializable]
        public sealed class Report
        {
            public string utc, completedUtc, unityVersion, scene, status, error, scope;
            public bool productionMainCamera, sameParticleLayout, restored, sceneWasDirty;
            public float originalPhase, originalTimeScale;
            public Vector3 originalCameraPosition, captureCameraPosition, captureTarget, impactWorld;
            public string[] temporarilyDisabled;
            public ParticleRecord[] sources;
            public CaptureRecord[] captures;
        }

        private sealed class ParticleSnapshot
        {
            public ParticleSystem System;
            public ParticleSystem.Particle[] Particles;
            public int Count;
            public ParticleSystem.Particle[] EvidenceParticles;
            public int EvidenceCount;
            public Vector3 LocalPosition, LocalScale;
            public Quaternion LocalRotation;
            public uint RandomSeed;
            public bool AutoSeed, WasPlaying, WasPaused;
            public float Time;
            public string Path, Material, Shader, EvidenceHash;

            public static ParticleSnapshot Save(ParticleSystem system)
            {
                int count = system.particleCount;
                var particles = new ParticleSystem.Particle[Mathf.Max(1, count)];
                count = system.GetParticles(particles);
                ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
                Material material = renderer != null ? renderer.sharedMaterial : null;
                return new ParticleSnapshot
                {
                    System = system,
                    Particles = particles,
                    Count = count,
                    LocalPosition = system.transform.localPosition,
                    LocalRotation = system.transform.localRotation,
                    LocalScale = system.transform.localScale,
                    RandomSeed = system.randomSeed,
                    AutoSeed = system.useAutoRandomSeed,
                    WasPlaying = system.isPlaying,
                    WasPaused = system.isPaused,
                    Time = system.time,
                    Path = TransformPath(system.transform),
                    Material = material != null ? material.name : "<missing>",
                    Shader = material != null && material.shader != null ? material.shader.name : "<missing>"
                };
            }

            public void PoseAndEmit(Vector3 position, Quaternion rotation, int count, uint seed, float age)
            {
                if (System == null) throw new InvalidOperationException("Production particle system disappeared.");
                if (System.main.simulationSpace != ParticleSystemSimulationSpace.World)
                    throw new InvalidOperationException(Path + " must use World simulation for immutable QA layout.");
                System.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                System.useAutoRandomSeed = false;
                System.randomSeed = seed;
                System.transform.SetPositionAndRotation(position, rotation);
                System.Emit(count);
                var particles = new ParticleSystem.Particle[Mathf.Max(1, System.particleCount)];
                int emitted = System.GetParticles(particles);
                for (int i = 0; i < emitted; i++)
                {
                    float elapsed = Mathf.Min(age, particles[i].startLifetime * .45f);
                    particles[i].position += particles[i].velocity * elapsed;
                    particles[i].remainingLifetime = Mathf.Max(.05f, particles[i].startLifetime - elapsed);
                }
                System.SetParticles(particles, emitted);
                System.Pause(true);
                EvidenceParticles = new ParticleSystem.Particle[Mathf.Max(1, System.particleCount)];
                EvidenceCount = System.GetParticles(EvidenceParticles);
                if (EvidenceCount != emitted || emitted == 0)
                    throw new InvalidOperationException(Path + " failed to retain emitted evidence particles.");
                // Unity converts remaining lifetime through its native normalized
                // representation when SetParticles writes it (a measured 1-ULP
                // difference). Keep this immutable input, and baseline its actual
                // native output. Every later write must reproduce that exact output.
                System.SetParticles(EvidenceParticles, EvidenceCount);
                System.Pause(true);
                EvidenceHash = LayoutHash(System, out int settledCount);
                if (settledCount != EvidenceCount)
                    throw new InvalidOperationException(Path + " lost evidence particles during native round-trip.");
                ReapplyEvidence();
            }

            public void EmitAtCurrentTransform(int count, uint seed, float age)
            {
                PoseAndEmit(System.transform.position, System.transform.rotation, count, seed, age);
            }

            public void ReapplyEvidence()
            {
                if (System == null) throw new InvalidOperationException("Production particle system disappeared.");
                if (EvidenceParticles == null || EvidenceCount <= 0)
                    throw new InvalidOperationException(Path + " has no settled evidence snapshot.");
                System.SetParticles(EvidenceParticles, EvidenceCount);
                System.Pause(true);
                ValidateEvidence();
            }

            public void ValidateEvidence()
            {
                string current = LayoutHash(System, out int count);
                if (count != EvidenceCount || current != EvidenceHash)
                {
                    var actual = new ParticleSystem.Particle[Mathf.Max(1, count)];
                    System.GetParticles(actual);
                    string difference = $" count={count}/{EvidenceCount}";
                    for (int i = 0; i < Mathf.Min(count, EvidenceCount); i++)
                    {
                        var expected = EvidenceParticles[i];
                        var observed = actual[i];
                        if (expected.position == observed.position && expected.velocity == observed.velocity &&
                            expected.remainingLifetime == observed.remainingLifetime && expected.startSize == observed.startSize &&
                            expected.startColor.Equals(observed.startColor)) continue;
                        difference += $" index={i} position={expected.position.ToString("R")}/{observed.position.ToString("R")} " +
                            $"velocity={expected.velocity.ToString("R")}/{observed.velocity.ToString("R")} " +
                            $"life={expected.remainingLifetime:R}/{observed.remainingLifetime:R} size={expected.startSize:R}/{observed.startSize:R}";
                        break;
                    }
                    throw new InvalidOperationException(Path +
                        " particle layout differed from its settled evidence snapshot after SetParticles." + difference);
                }
            }

            public void Restore()
            {
                if (System == null) return;
                System.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                System.transform.localPosition = LocalPosition;
                System.transform.localRotation = LocalRotation;
                System.transform.localScale = LocalScale;
                System.useAutoRandomSeed = AutoSeed;
                System.randomSeed = RandomSeed;
                if (Time > 0f) System.Simulate(Time, true, true, true);
                System.SetParticles(Particles, Count);
                if (WasPaused) System.Pause(true);
                else if (WasPlaying) System.Play(true);
                else System.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            public ParticleRecord Record()
            {
                ReapplyEvidence();
                return new ParticleRecord
                {
                    objectPath = Path,
                    material = Material,
                    shader = Shader,
                    layoutHash = EvidenceHash,
                    particles = EvidenceCount
                };
            }
        }

        [MenuItem("Elemental/QA/Capture Production Dust Day Dusk Night")]
        public static void Run()
        {
            if (_running) throw new InvalidOperationException("Production dust capture is already running.");
            if (!Application.isPlaying || EditorApplication.isPaused)
                throw new InvalidOperationException("Enter unpaused Play Mode and wait for Earth readiness first.");
            _scene = SceneManager.GetActiveScene();
            if (_scene.path != ScenePath)
                throw new InvalidOperationException("EarthCoreSlice must be the active scene.");
            EarthSceneReadinessGate gate = FindUnique<EarthSceneReadinessGate>(_scene);
            if (!gate.IsReady || gate.Failed)
                throw new InvalidOperationException("Earth readiness gate has not passed.");
            _sky = FindUnique<CelestialSystemBehaviour>(_scene);
            _camera = Camera.main;
            if (_camera == null || !_camera.isActiveAndEnabled || _camera.gameObject.scene != _scene ||
                _camera.targetTexture != null || !_sky.HasRequiredBindings || _sky.TargetCamera != _camera)
                throw new InvalidOperationException("The live production Main Camera and celestial bindings are required.");

            Transform ring = FindNamedTransform(_scene, "Outer Stone Ring");
            Transform arch = null;
            foreach (Transform candidate in ring.GetComponentsInChildren<Transform>(true))
                if (candidate.name == "FRAME_outer_arch_02") arch = candidate;
            if (arch == null) throw new InvalidOperationException("Authored FRAME_outer_arch_02 was not found.");
            Bounds archBounds = VisibleMeshBounds(arch);
            Vector3 anchor = _sky.LightingAnchor.position;
            _up = _sky.LightingUp.normalized;
            if (_up.sqrMagnitude < .9f) throw new InvalidOperationException("Celestial lighting frame is not initialized.");
            Vector3 outward = Vector3.ProjectOnPlane(archBounds.center - anchor, _up).normalized;
            if (outward.sqrMagnitude < .9f)
                outward = Vector3.ProjectOnPlane(_camera.transform.forward, _up).normalized;
            Vector3 side = Vector3.Cross(_up, outward).normalized;
            float downwardExtent = Mathf.Abs(_up.x) * archBounds.extents.x +
                                   Mathf.Abs(_up.y) * archBounds.extents.y +
                                   Mathf.Abs(_up.z) * archBounds.extents.z;
            float outwardExtent = Mathf.Abs(outward.x) * archBounds.extents.x +
                                  Mathf.Abs(outward.y) * archBounds.extents.y +
                                  Mathf.Abs(outward.z) * archBounds.extents.z;
            // Probe the arena immediately in front of the column. Bounds-bottom
            // was below the curved arena surface in the first production capture
            // (impact viewport y=-0.314), so the dust itself was outside the frame.
            Vector3 surfaceProbe = archBounds.center - outward * (outwardExtent + .65f);
            _impactPoint = FindVisibleStaticSurface(
                surfaceProbe, _up, Mathf.Max(8f, downwardExtent * 2f + 4f)) + _up * .10f;
            // Stay close enough to read the production particles while retaining
            // the authored column as lighting/context behind them.
            _captureCameraPosition = _impactPoint - outward * 4.2f + side * .75f + _up * 1.8f;
            _captureTarget = _impactPoint + outward * .70f + _up * .78f;
            _captureCameraRotation = Quaternion.LookRotation(_captureTarget - _captureCameraPosition, _up);

            _savedCameraPosition = _camera.transform.position;
            _savedCameraRotation = _camera.transform.rotation;
            _savedFov = _camera.fieldOfView;
            _savedPhase = _sky.Snapshot.TimeOfDay01;
            _savedTimeScale = Time.timeScale;
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            _folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "BuildReports", "DustProductionVisualQa", stamp);
            Directory.CreateDirectory(_folder);
            _report = new Report
            {
                utc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                scene = _scene.path, status = "SettingUp", productionMainCamera = true,
                originalPhase = _savedPhase, originalTimeScale = _savedTimeScale,
                originalCameraPosition = _savedCameraPosition, captureCameraPosition = _captureCameraPosition,
                captureTarget = _captureTarget, impactWorld = _impactPoint,
                sceneWasDirty = _scene.isDirty,
                scope = "Actual saved EarthCoreSlice, production Main Camera/Game view, live production ParticleSystem " +
                    "instances and materials. One deterministic impact/fracture/mote layout is paused at a fixed age " +
                    "for all three celestial phases. No gameplay damage, asset/profile/light tuning or scene save."
            };

            SavedBehaviours.Clear(); ParticleSnapshots.Clear(); Captures.Clear();
            _running = true;
            try
            {
                Time.timeScale = 0f;
                _camera.transform.SetPositionAndRotation(_captureCameraPosition, _captureCameraRotation);
                _camera.fieldOfView = 45f;
                _sky.EvaluatePresentationForQa();

                ParticleSnapshot impact = ParticleSnapshot.Save(FindNamed<ParticleSystem>(_scene, "Material Contact Dust"));
                ParticleSnapshot fracture = ParticleSnapshot.Save(FindNamed<ParticleSystem>(_scene, "Arena Fracture Dust"));
                ParticleSnapshot motes = ParticleSnapshot.Save(FindNamed<ParticleSystem>(_scene, "Sunlit Air Motes"));
                ParticleSnapshots.Add(impact); ParticleSnapshots.Add(fracture); ParticleSnapshots.Add(motes);
                // Save live particle state before disabling its production
                // presenters: EarthMaterialFeedbackPresenter.OnDisable clears
                // its owned systems by design.
                SuspendOwners();
                Quaternion surfaceFrame = Quaternion.FromToRotation(Vector3.up, _up);
                Vector3 towardCamera = (_captureCameraPosition - _impactPoint).normalized;
                Vector3 impactCloud = _impactPoint + towardCamera * .18f + side * .72f;
                Vector3 fractureCloud = _impactPoint + towardCamera * .12f - side * .42f;
                ValidateFraming(impactCloud, fractureCloud);
                impact.PoseAndEmit(impactCloud,
                    surfaceFrame, 96, 0xD0571001u, .24f);
                fracture.PoseAndEmit(fractureCloud,
                    surfaceFrame, 180, 0xD0572002u, .31f);
                motes.EmitAtCurrentTransform(42, 0xD0573003u, .9f);
                _report.sources = new[] { impact.Record(), fracture.Record(), motes.Record() };

                Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                if (gameViewType == null) throw new InvalidOperationException("Unity Game view type was not available.");
                EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
                gameView.Show(); gameView.Focus();
                _shotIndex = 0; _renderFrames = 0; _poseFrame = 0; _lastRenderedFrame = -1;
                _captureRequested = false; _deadline = EditorApplication.timeSinceStartup + 120d;
                _report.status = "Capturing";
                EditorApplication.update += Tick;
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
                AssemblyReloadEvents.beforeAssemblyReload += Abort;
                RenderPipelineManager.beginCameraRendering += BeforeCameraRendering;
                HoldShot(); SaveReport();
            }
            catch (Exception exception)
            {
                Finish("Failed", exception.ToString());
                throw;
            }
        }

        [MenuItem("Elemental/QA/Capture Production Dust Day Dusk Night", true)]
        private static bool ValidateRun() => Application.isPlaying && !EditorApplication.isPaused && !_running;

        [MenuItem("Elemental/QA/Abort Production Dust Capture")]
        public static void Abort() => Finish("Aborted", "Capture was aborted before all three images completed.");

        private static void SuspendOwners()
        {
            var disabled = new List<string>();
            foreach (Behaviour component in Object.FindObjectsByType<Behaviour>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (component == null || component.gameObject.scene != _scene) continue;
                string type = component.GetType().FullName;
                bool owner = component is PlanetCameraRig || component is EarthCinemachineCameraController ||
                    component is EarthCameraDirector || component is EarthChargeCameraLookdev ||
                    component is EarthChargeCameraLookdevV2 || component is EarthMvpBotController ||
                    component is EarthMaterialFeedbackPresenter || component is EarthArenaFractureDustPresenter ||
                    component is VisualQaCaptureBehaviour || component is MagicInputController ||
                    component is PlanetInputReader || component is UIDocument ||
                    type == "Unity.Cinemachine.CinemachineBrain" ||
                    type == "Cinemachine.CinemachineBrain";
                if (!owner) continue;
                SavedBehaviours.Add(component, component.enabled);
                if (component.enabled) disabled.Add(type + " @ " + component.name);
                component.enabled = false;
            }
            _report.temporarilyDisabled = disabled.ToArray();
        }

        private static void BeforeCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!_running || camera != _camera) return;
            try
            {
                HoldShot();
                if (Time.frameCount != _lastRenderedFrame)
                {
                    _lastRenderedFrame = Time.frameCount;
                    _renderFrames++;
                }
            }
            catch (Exception exception) { Finish("Failed", exception.ToString()); }
        }

        private static void Tick()
        {
            if (!_running) return;
            try
            {
                if (!Application.isPlaying || _camera == null || _sky == null)
                    throw new InvalidOperationException("Play scene or production camera disappeared.");
                if (EditorApplication.timeSinceStartup > _deadline)
                    throw new TimeoutException("Dust capture exceeded 120 seconds; keep Game view visible and Play unpaused.");
                HoldShot();
                if (!_captureRequested && _renderFrames - _poseFrame >= SettleFrames)
                {
                    _captureRequestedUtc = DateTime.UtcNow;
                    ScreenCapture.CaptureScreenshot(Path.Combine(_folder, Shots[_shotIndex].Name + ".png"), 1);
                    _captureFrame = _renderFrames;
                    _captureRequested = true;
                }
                if (!_captureRequested || _renderFrames - _captureFrame < WriteFrames) return;
                string path = Path.Combine(_folder, Shots[_shotIndex].Name + ".png");
                var file = new FileInfo(path);
                if (!file.Exists || file.Length == 0 || file.LastWriteTimeUtc < _captureRequestedUtc) return;
                RecordCapture(file);
                _shotIndex++;
                if (_shotIndex == Shots.Length)
                {
                    Finish("Captured", null);
                    return;
                }
                _poseFrame = _renderFrames;
                _captureRequested = false;
                HoldShot(); SaveReport();
            }
            catch (Exception exception) { Finish("Failed", exception.ToString()); }
        }

        private static void HoldShot()
        {
            Shot shot = Shots[_shotIndex];
            _camera.transform.SetPositionAndRotation(_captureCameraPosition, _captureCameraRotation);
            _camera.fieldOfView = 45f;
            _sky.SetTimeOfDayForQa(shot.Phase);
            _sky.EvaluatePresentationForQa();
            float error = Mathf.Abs(Mathf.DeltaAngle(shot.Phase * 360f, _sky.Snapshot.TimeOfDay01 * 360f));
            if (error > .01f) throw new InvalidOperationException("Celestial QA phase failed to remain pinned.");
            for (int i = 0; i < ParticleSnapshots.Count; i++)
            {
                ParticleSnapshot snapshot = ParticleSnapshots[i];
                // A queued player update or an event emitter can touch a paused
                // system between editor callbacks. Reapply the one settled
                // production layout immediately before every actual camera render.
                snapshot.ReapplyEvidence();
            }
        }

        private static Vector3 FindVisibleStaticSurface(Vector3 probe, Vector3 up, float castDistance)
        {
            Vector3 origin = probe + up * (castDistance * .5f);
            RaycastHit[] hits = Physics.RaycastAll(
                origin, -up, castDistance, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (int index = 0; index < hits.Length; index++)
            {
                RaycastHit hit = hits[index];
                if (hit.collider == null || hit.collider.gameObject.scene != _scene ||
                    hit.rigidbody != null || Vector3.Dot(hit.normal, up) < .65f)
                    continue;
                return hit.point;
            }
            throw new InvalidOperationException(
                "No upward-facing static arena surface was found in front of FRAME_outer_arch_02.");
        }

        private static void ValidateFraming(Vector3 impactCloud, Vector3 fractureCloud)
        {
            ValidateViewport("surface impact", _impactPoint);
            ValidateViewport("material contact dust", impactCloud);
            ValidateViewport("fracture dust", fractureCloud);
            ValidateLineOfSight("material contact dust", impactCloud);
            ValidateLineOfSight("fracture dust", fractureCloud);
        }

        private static void ValidateViewport(string label, Vector3 point)
        {
            Vector3 viewport = _camera.WorldToViewportPoint(point);
            if (viewport.z <= _camera.nearClipPlane || viewport.x < .15f || viewport.x > .85f ||
                viewport.y < .15f || viewport.y > .85f)
                throw new InvalidOperationException(
                    $"{label} is not safely framed: viewport={viewport.ToString("R")}.");
        }

        private static void ValidateLineOfSight(string label, Vector3 point)
        {
            Vector3 displacement = point - _captureCameraPosition;
            float distance = displacement.magnitude;
            // Stop short of the particle origin so the supporting ground at the
            // endpoint is allowed while intervening arena/column geometry is not.
            float unobstructedDistance = Mathf.Max(0f, distance - .14f);
            if (unobstructedDistance > 0f && Physics.Raycast(
                    _captureCameraPosition, displacement / distance, out RaycastHit hit,
                    unobstructedDistance, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException(
                    $"{label} is occluded by {hit.collider.name} " +
                    $"at {hit.distance:F3}m before its {distance:F3}m origin.");
        }

        private static void RecordCapture(FileInfo file)
        {
            ParticleSnapshot impact = ParticleSnapshots[0];
            ParticleSnapshot fracture = ParticleSnapshots[1];
            ParticleSnapshot motes = ParticleSnapshots[2];
            impact.ValidateEvidence();
            fracture.ValidateEvidence();
            motes.ValidateEvidence();
            string impactHash = LayoutHash(impact.System, out int impactCount);
            string fractureHash = LayoutHash(fracture.System, out int fractureCount);
            string moteHash = LayoutHash(motes.System, out int moteCount);
            Light moon = _sky.MoonLight;
            Captures.Add(new CaptureRecord
            {
                name = Shots[_shotIndex].Name,
                path = file.FullName,
                utc = DateTime.UtcNow.ToString("O"),
                requestedPhase = Shots[_shotIndex].Phase,
                actualPhase = _sky.Snapshot.TimeOfDay01,
                sunIntensity = _sky.SunLight != null ? _sky.SunLight.intensity : 0f,
                sunColor = _sky.SunLight != null ? _sky.SunLight.color : Color.black,
                moonIntensity = moon != null && moon.enabled ? moon.intensity : 0f,
                moonColor = moon != null ? moon.color : Color.black,
                nightFraction = _sky.Snapshot.Night01,
                ambientSky = RenderSettings.ambientSkyColor,
                ambientEquator = RenderSettings.ambientEquatorColor,
                ambientGround = RenderSettings.ambientGroundColor,
                cameraPosition = _camera.transform.position,
                cameraEuler = _camera.transform.eulerAngles,
                impactWorld = _impactPoint,
                impactViewport = _camera.WorldToViewportPoint(_impactPoint),
                impactLayoutHash = impactHash,
                fractureLayoutHash = fractureHash,
                moteLayoutHash = moteHash,
                impactParticles = impactCount,
                fractureParticles = fractureCount,
                moteParticles = moteCount,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                pngBytes = file.Length
            });
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (_running && change == PlayModeStateChange.ExitingPlayMode)
                Finish("Aborted", "Play Mode exited during production dust capture.");
        }

        private static void Finish(string status, string error)
        {
            if (!_running) return;
            _running = false;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Abort;
            RenderPipelineManager.beginCameraRendering -= BeforeCameraRendering;
            bool restored = false;
            try
            {
                if (_camera != null)
                {
                    _camera.transform.SetPositionAndRotation(_savedCameraPosition, _savedCameraRotation);
                    _camera.fieldOfView = _savedFov;
                }
                if (_sky != null)
                {
                    _sky.SetTimeOfDayForQa(_savedPhase);
                    _sky.EvaluatePresentationForQa();
                }
                foreach (KeyValuePair<Behaviour, bool> state in SavedBehaviours)
                    if (state.Key != null) state.Key.enabled = state.Value;
                // Presenters can Play or clear their particle children from
                // OnEnable. Restore exact prior particles and playback last.
                for (int i = 0; i < ParticleSnapshots.Count; i++)
                    ParticleSnapshots[i].Restore();
                Time.timeScale = _savedTimeScale;
                restored = true;
            }
            catch (Exception restoreException)
            {
                status = "Failed";
                error = (error ?? string.Empty) + "\nRestore failed: " + restoreException;
            }
            finally
            {
                if (_report != null)
                {
                    _report.status = status;
                    _report.error = error;
                    _report.completedUtc = DateTime.UtcNow.ToString("O");
                    _report.restored = restored;
                    _report.sameParticleLayout = Captures.Count == Shots.Length && SameLayouts();
                    SaveReport();
                }
                SavedBehaviours.Clear(); ParticleSnapshots.Clear(); Captures.Clear();
            }
            Debug.Log("[DustProductionVisualQa] " + status + "; " +
                      Path.Combine(_folder, "CaptureReport.json"));
        }

        private static bool SameLayouts()
        {
            if (Captures.Count == 0) return false;
            CaptureRecord first = Captures[0];
            for (int i = 1; i < Captures.Count; i++)
                if (Captures[i].impactLayoutHash != first.impactLayoutHash ||
                    Captures[i].fractureLayoutHash != first.fractureLayoutHash ||
                    Captures[i].moteLayoutHash != first.moteLayoutHash) return false;
            return true;
        }

        private static void SaveReport()
        {
            if (_report == null || string.IsNullOrEmpty(_folder)) return;
            _report.captures = Captures.ToArray();
            File.WriteAllText(Path.Combine(_folder, "CaptureReport.json"), JsonUtility.ToJson(_report, true));
        }

        private static string LayoutHash(ParticleSystem system, out int count)
        {
            if (system == null) { count = 0; return "missing"; }
            var particles = new ParticleSystem.Particle[Mathf.Max(1, system.particleCount)];
            count = system.GetParticles(particles);
            return LayoutHash(particles, count);
        }

        private static string LayoutHash(ParticleSystem.Particle[] particles, int count)
        {
            ulong hash = 1469598103934665603UL;
            Hash(ref hash, count);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = particles[i].position;
                Vector3 v = particles[i].velocity;
                Hash(ref hash, BitConverter.SingleToInt32Bits(p.x));
                Hash(ref hash, BitConverter.SingleToInt32Bits(p.y));
                Hash(ref hash, BitConverter.SingleToInt32Bits(p.z));
                Hash(ref hash, BitConverter.SingleToInt32Bits(v.x));
                Hash(ref hash, BitConverter.SingleToInt32Bits(v.y));
                Hash(ref hash, BitConverter.SingleToInt32Bits(v.z));
                Hash(ref hash, BitConverter.SingleToInt32Bits(particles[i].remainingLifetime));
                Hash(ref hash, BitConverter.SingleToInt32Bits(particles[i].startSize));
                Color32 color = particles[i].startColor;
                Hash(ref hash, color.r | color.g << 8 | color.b << 16 | color.a << 24);
            }
            return hash.ToString("X16");
        }

        private static void Hash(ref ulong hash, int value)
        {
            unchecked { hash ^= (uint)value; hash *= 1099511628211UL; }
        }

        private static T FindUnique<T>(Scene scene) where T : Component
        {
            T found = null;
            foreach (T candidate in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.gameObject.scene != scene) continue;
                if (found != null) throw new InvalidOperationException("Expected one " + typeof(T).Name + ".");
                found = candidate;
            }
            return found != null ? found : throw new InvalidOperationException("Missing " + typeof(T).Name + ".");
        }

        private static T FindNamed<T>(Scene scene, string name) where T : Component
        {
            T found = null;
            foreach (T candidate in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.gameObject.scene != scene || candidate.name != name) continue;
                if (found != null) throw new InvalidOperationException("Ambiguous scene object " + name + ".");
                found = candidate;
            }
            return found != null ? found : throw new InvalidOperationException("Missing scene object " + name + ".");
        }

        private static Transform FindNamedTransform(Scene scene, string name) => FindNamed<Transform>(scene, name);

        private static Bounds VisibleMeshBounds(Transform root)
        {
            bool found = false;
            Bounds bounds = default;
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!renderer.enabled || renderer.GetComponent<MeshFilter>()?.sharedMesh == null) continue;
                if (found) bounds.Encapsulate(renderer.bounds);
                else { bounds = renderer.bounds; found = true; }
            }
            return found ? bounds : throw new InvalidOperationException("No visible mesh under " + root.name + ".");
        }

        private static string TransformPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }
    }
}
