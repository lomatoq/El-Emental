using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Elemental.Runtime.Characters;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Presentation.Rendering
{
    [DisallowMultipleComponent]
    public sealed class Gate1TransientCaptureBehaviour : MonoBehaviour
    {
        private const string ShippingScenePath =
            "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private const int EvidenceWidth = 1920;
        private const int EvidenceHeight = 1080;
        private const int SettleFrames = 45;
        private static readonly ProfilerMarker CaptureMarker =
            new ProfilerMarker("Elemental.Gate1Capture.Frame");

        private readonly List<Gate1CaptureFrameEvidence> _frames =
            new List<Gate1CaptureFrameEvidence>(6);
        private Gate1CaptureRequest _request;
        private Gate1CaptureManifest _manifest;
        private Gate1AnimationCaptureScope _animationScope;
        private Gate1DuelShadowCaptureScope _duelScope;
        private Gate1PhysicalAnimationCaptureScope _physicalScope;
        private Gate1LegacyAnimationStimulusScope _legacyAnimationStimulus;
        private Gate1BehaviourFreezeScope _freezeScope;
        private int _registryCountBefore;
        private int _graphCountBefore;
        private int _previousVSyncCount;
        private int _previousTargetFrameRate;
        private int _previousCaptureFrameRate;
        private bool _runtimeRestored;
        private bool _animationOwnershipRestored;
        private bool _physicalOwnershipRestored;
        private string _captureFailure = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForRequestedCapture()
        {
            if (!TryReadRequest(out Gate1CaptureRequest request)) return;
            if (FindAnyObjectByType<Gate1TransientCaptureBehaviour>() != null) return;
            var owner = new GameObject("Gate1 Transient A-B Capture")
            {
                hideFlags = HideFlags.DontSave
            };
            var behaviour = owner.AddComponent<Gate1TransientCaptureBehaviour>();
            behaviour._request = request;
        }

        private IEnumerator Start()
        {
            string outputDirectory = Path.GetFullPath(_request.OutputDirectory);
            Directory.CreateDirectory(outputDirectory);
            _request = new Gate1CaptureRequest(outputDirectory);
            _manifest = new Gate1CaptureManifest
            {
                unityVersion = Application.unityVersion,
                scenePath = SceneManager.GetActiveScene().path,
                outputDirectory = outputDirectory,
                startedUtc = DateTime.UtcNow.ToString("O")
            };
            _registryCountBefore = DuelShadowCasterRegistry.Shared.Count;
            _graphCountBefore = CountComponentsByTypeName(
                "Elemental.Presentation.Animation.EarthAnimationGraph");
            _previousVSyncCount = QualitySettings.vSyncCount;
            _previousTargetFrameRate = Application.targetFrameRate;
            _previousCaptureFrameRate = Time.captureFramerate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            Time.captureFramerate = 60;
            _freezeScope = Gate1BehaviourFreezeScope.Begin();

            try
            {
                if (_manifest.scenePath != ShippingScenePath)
                {
                    Complete(
                        false,
                        $"Gate1 capture requires clean shipping scene '{ShippingScenePath}', not '{_manifest.scenePath}'.");
                    yield break;
                }
                for (int frame = 0; frame < SettleFrames; frame++) yield return null;
                if (!TryResolveScene(
                        out UnityEngine.Camera camera,
                        out Light directionalLight,
                        out List<Component> presentations,
                        out HumanoidRagdollRig recoveryRig,
                        out Animator recoveryAnimator,
                        out string failure))
                {
                    Complete(false, failure);
                    yield break;
                }

                if (!Gate1LegacyAnimationStimulusScope.TryBegin(
                        presentations,
                        out _legacyAnimationStimulus,
                        out failure))
                {
                    Complete(false, failure);
                    yield break;
                }
                var animationLegacy = CreateFrame(
                    Gate1CaptureVariant.AnimationLegacy,
                    new Gate1CaptureFeatureState());
                animationLegacy.animationLegacyFrames = 1;
                animationLegacy.featurePathEvidenceCount = 1;
                yield return Capture(camera, animationLegacy);
                _frames.Add(animationLegacy);
                _legacyAnimationStimulus.Dispose();
                _legacyAnimationStimulus = null;
                yield return null;

                if (!Gate1AnimationCaptureScope.TryBegin(
                        presentations,
                        out _animationScope,
                        out failure))
                {
                    Complete(false, failure);
                    yield break;
                }
                for (int frame = 0; frame < 4; frame++) yield return null;
                if (!_animationScope.TryTriggerDeterministicInertialization(
                        out int transitionRequests,
                        out failure))
                {
                    Complete(false, failure);
                    yield break;
                }
                var animationFeature = CreateFrame(
                    Gate1CaptureVariant.AnimationInertialization,
                    new Gate1CaptureFeatureState
                    {
                        usePlayablesAnimationGraph = true,
                        usePoseInertialization = true
                    });
                for (int frame = 0; frame < 8; frame++)
                {
                    yield return null;
                    _animationScope.AccumulateEvidence(animationFeature);
                }
                animationFeature.animationTransitionRequests = Mathf.Max(
                    animationFeature.animationTransitionRequests,
                    transitionRequests);
                animationFeature.featurePathEvidenceCount =
                    animationFeature.animationGraphActiveFrames > 0 &&
                    animationFeature.animationTopologyValidFrames > 0 &&
                    animationFeature.animationTransitionRequests > 0
                        ? 1
                        : 0;
                yield return Capture(camera, animationFeature);
                _frames.Add(animationFeature);
                _animationScope.Dispose();
                _animationOwnershipRestored = _animationScope.RestoreSucceeded;
                _animationScope = null;
                for (int frame = 0; frame < 2; frame++) yield return null;

                if (DuelShadowCaptureOverride.IsActive ||
                    DuelShadowBoundsProvider.Active != null)
                {
                    Complete(false, "The duel-shadow baseline was not clean before transient wiring.");
                    yield break;
                }
                var duelLegacy = CreateFrame(
                    Gate1CaptureVariant.DuelNoShadows,
                    new Gate1CaptureFeatureState());
                yield return Capture(camera, duelLegacy);
                DuelShadowDiagnosticsSnapshot disabled = DuelShadowDiagnostics.Current;
                duelLegacy.duelDisabledFrames = !disabled.FeatureRequested ? 1 : 0;
                duelLegacy.featurePathEvidenceCount = duelLegacy.duelDisabledFrames;
                _frames.Add(duelLegacy);

                Renderer[] playerRenderers = presentations[0]
                    .GetComponentsInChildren<Renderer>(true);
                Renderer[] opponentRenderers = presentations[1]
                    .GetComponentsInChildren<Renderer>(true);
                if (!Gate1DuelShadowCaptureScope.TryBegin(
                        directionalLight,
                        presentations[0].transform,
                        presentations[1].transform,
                        playerRenderers,
                        opponentRenderers,
                        out _duelScope,
                        out failure))
                {
                    Complete(false, failure);
                    yield break;
                }
                var duelFeature = CreateFrame(
                    Gate1CaptureVariant.DuelShadowMap,
                    new Gate1CaptureFeatureState
                    {
                        useDuelShadowMap = true,
                        useDuelShadowDebugReceiver = true
                    });
                for (int frame = 0; frame < 6; frame++)
                {
                    yield return null;
                    DuelShadowDiagnosticsSnapshot snapshot = DuelShadowDiagnostics.Current;
                    if (snapshot.MapRendered) duelFeature.duelMapRenderedFrames++;
                    duelFeature.duelDrawnCasterCount = Mathf.Max(
                        duelFeature.duelDrawnCasterCount,
                        snapshot.DrawnCasterCount);
                }
                yield return Capture(camera, duelFeature);
                DuelShadowDiagnosticsSnapshot capturedShadow = DuelShadowDiagnostics.Current;
                if (capturedShadow.MapRendered) duelFeature.duelMapRenderedFrames++;
                duelFeature.duelDrawnCasterCount = Mathf.Max(
                    duelFeature.duelDrawnCasterCount,
                    capturedShadow.DrawnCasterCount);
                duelFeature.featurePathEvidenceCount =
                    duelFeature.duelMapRenderedFrames > 0 &&
                    duelFeature.duelDrawnCasterCount > 0
                        ? 1
                        : 0;
                _frames.Add(duelFeature);
                _duelScope.Dispose();
                _duelScope = null;
                for (int frame = 0; frame < 2; frame++) yield return null;

                if (!Gate1PhysicalAnimationCaptureScope.TryBegin(
                        recoveryRig,
                        recoveryAnimator,
                        out _physicalScope,
                        out failure))
                {
                    Complete(false, failure);
                    yield break;
                }
                if (!_physicalScope.TryConfigureLegacy(out failure))
                {
                    Complete(false, failure);
                    yield break;
                }
                recoveryRig.BeginRagdoll(Vector3.zero);
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                recoveryRig.RecoverToAnimated(
                    recoveryRig.transform.up,
                    recoveryRig.transform.forward,
                    false);
                yield return null;
                if (!_physicalScope.TryConfirmLegacyRecovery(out failure))
                {
                    Complete(false, failure);
                    yield break;
                }
                var recoveryLegacy = CreateFrame(
                    Gate1CaptureVariant.RecoveryLegacy,
                    new Gate1CaptureFeatureState());
                _physicalScope.AccumulateLegacyEvidence(recoveryLegacy);
                recoveryLegacy.featurePathEvidenceCount = recoveryLegacy.recoveryLegacyFrames;
                yield return Capture(camera, recoveryLegacy);
                _frames.Add(recoveryLegacy);
                recoveryRig.ResetToAnimated();
                yield return null;
                yield return null;

                if (!_physicalScope.TryConfigurePoseMatched(out failure))
                {
                    Complete(false, failure);
                    yield break;
                }
                recoveryRig.BeginRagdoll(Vector3.zero);
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                Vector3 recoveryUp = recoveryRig.transform.up;
                if (!_physicalScope.TryCapturePoseMatchedContinuityOrigin(
                        recoveryUp,
                        out failure))
                {
                    Complete(false, failure);
                    yield break;
                }
                recoveryRig.RecoverToAnimated(
                    recoveryUp,
                    recoveryRig.transform.forward,
                    false);
                yield return null;
                if (!_physicalScope.TryConfirmPoseMatchedRecovery(out failure))
                {
                    Complete(false, failure);
                    yield break;
                }
                var recoveryFeature = CreateFrame(
                    Gate1CaptureVariant.RecoveryPoseMatched,
                    new Gate1CaptureFeatureState
                    {
                        usePoseMatchedRecovery = true
                    });
                _physicalScope.AccumulatePoseMatchedEvidence(recoveryFeature);
                recoveryFeature.featurePathEvidenceCount =
                    recoveryFeature.recoveryPoseMatchedFrames > 0 &&
                    recoveryFeature.recoveryStateVerifiedFrames > 0 &&
                    recoveryFeature.recoveryClearanceSucceededFrames > 0 &&
                    recoveryFeature.recoveryIsolatedSamplerFrames > 0 &&
                    recoveryFeature.recoveryPelvisContinuityVerifiedFrames > 0
                        ? 1
                        : 0;
                yield return Capture(camera, recoveryFeature);
                _frames.Add(recoveryFeature);
                _physicalScope.Dispose();
                _physicalOwnershipRestored = _physicalScope.RestoreSucceeded;
                _physicalScope = null;
                yield return null;
                yield return null;

                RestoreRuntimeState();
                yield return null;
                Complete(true, string.Empty);
            }
            finally
            {
                RestoreRuntimeState();
            }
        }

        private IEnumerator Capture(
            UnityEngine.Camera camera,
            Gate1CaptureFrameEvidence evidence)
        {
            string path = Path.Combine(
                _request.OutputDirectory,
                Gate1CaptureRequest.FileNameFor(evidence.variant));
            RenderTexture target = RenderTexture.GetTemporary(
                EvidenceWidth,
                EvidenceHeight,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            var pixels = new Texture2D(
                EvidenceWidth,
                EvidenceHeight,
                TextureFormat.RGB24,
                false);
            try
            {
                bool rendered = false;
                try
                {
                    using (CaptureMarker.Auto())
                    {
                        camera.targetTexture = target;
                        target.Create();
                        camera.Render();
                    }
                    rendered = true;
                }
                catch (Exception exception)
                {
                    RecordCaptureFailure(evidence.variant, "render", exception);
                }
                if (rendered)
                {
                    yield return null;
                    try
                    {
                        RenderTexture.active = target;
                        pixels.ReadPixels(
                            new Rect(0f, 0f, EvidenceWidth, EvidenceHeight),
                            0,
                            0,
                            false);
                        pixels.Apply(false, false);
                        AccumulateImageMetrics(pixels, evidence);
                        byte[] png = pixels.EncodeToPNG();
                        File.WriteAllBytes(path, png);
                        evidence.outputPath = Path.GetFullPath(path);
                        evidence.imageByteCount = png.Length;
                        evidence.unityFrame = Time.frameCount;
                    }
                    catch (Exception exception)
                    {
                        RecordCaptureFailure(evidence.variant, "readback/write", exception);
                    }
                }
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                Destroy(pixels);
            }
        }

        private void RecordCaptureFailure(
            Gate1CaptureVariant variant,
            string phase,
            Exception exception)
        {
            if (!string.IsNullOrWhiteSpace(_captureFailure)) return;
            _captureFailure =
                $"{variant} {phase} failed: {exception.GetType().Name}: {exception.Message}";
        }

        private static void AccumulateImageMetrics(
            Texture2D image,
            Gate1CaptureFrameEvidence evidence)
        {
            Color32[] pixels = image.GetPixels32();
            float minimum = 1f;
            float maximum = 0f;
            int stride = Mathf.Max(1, pixels.Length / 4096);
            for (int index = 0; index < pixels.Length; index += stride)
            {
                Color32 color = pixels[index];
                float luminance =
                    (color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f) /
                    255f;
                minimum = Mathf.Min(minimum, luminance);
                maximum = Mathf.Max(maximum, luminance);
            }
            evidence.imageMinimumLuminance = minimum;
            evidence.imageMaximumLuminance = maximum;
            evidence.imageLuminanceRange = Mathf.Max(0f, maximum - minimum);
        }

        private Gate1CaptureFrameEvidence CreateFrame(
            Gate1CaptureVariant variant,
            Gate1CaptureFeatureState state)
        {
            return new Gate1CaptureFrameEvidence
            {
                variant = variant,
                featureState = state
            };
        }

        private void Complete(bool sequenceSucceeded, string sequenceFailure)
        {
            RestoreRuntimeState();
            _manifest.frames = _frames.ToArray();
            _manifest.restoration.runtimeOverrideRestored =
                !DuelShadowCaptureOverride.IsActive;
            _manifest.restoration.boundsProviderRestored =
                DuelShadowBoundsProvider.Active == null;
            _manifest.restoration.registryCountBefore = _registryCountBefore;
            _manifest.restoration.registryCountAfter =
                DuelShadowCasterRegistry.Shared.Count;
            _manifest.restoration.casterRegistryRestored =
                _manifest.restoration.registryCountAfter == _registryCountBefore;
            _manifest.restoration.animationGraphOwnershipRestored =
                _animationOwnershipRestored &&
                CountComponentsByTypeName(
                    "Elemental.Presentation.Animation.EarthAnimationGraph") ==
                _graphCountBefore;
            _manifest.restoration.physicalAnimationOwnershipRestored =
                _physicalOwnershipRestored;
            _manifest.restoration.transientComponentCountAfter =
                FindObjectsByType<Gate1CaptureOwnerMarker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length;

            bool evidenceValid = Gate1CaptureEvidenceValidator.TryValidate(
                _manifest,
                out string validationFailure);
            bool restorationValid =
                _manifest.restoration.runtimeOverrideRestored &&
                _manifest.restoration.boundsProviderRestored &&
                _manifest.restoration.casterRegistryRestored &&
                _manifest.restoration.animationGraphOwnershipRestored &&
                _manifest.restoration.physicalAnimationOwnershipRestored &&
                _manifest.restoration.transientComponentCountAfter == 0;
            bool captureSucceeded = string.IsNullOrWhiteSpace(_captureFailure);
            _manifest.success = sequenceSucceeded && captureSucceeded &&
                evidenceValid && restorationValid;
            _manifest.failure = !string.IsNullOrWhiteSpace(sequenceFailure)
                ? sequenceFailure
                : !captureSucceeded
                    ? _captureFailure
                : !evidenceValid
                    ? validationFailure
                    : !restorationValid
                        ? "One or more transient Gate1 owners were not restored."
                        : string.Empty;
            _manifest.complete = true;
            _manifest.completedUtc = DateTime.UtcNow.ToString("O");
            File.WriteAllText(
                _request.ManifestPath,
                JsonUtility.ToJson(_manifest, true));
            if (_manifest.success)
                Debug.Log($"[Elemental] Gate1 transient A/B capture completed: {_request.ManifestPath}");
            else
                Debug.LogError($"[Elemental] Gate1 transient A/B capture failed: {_manifest.failure}");
        }

        private void RestoreRuntimeState()
        {
            if (_runtimeRestored) return;
            if (_animationScope != null)
            {
                _animationScope.Dispose();
                _animationOwnershipRestored = _animationScope.RestoreSucceeded;
            }
            _animationScope = null;
            _legacyAnimationStimulus?.Dispose();
            _legacyAnimationStimulus = null;
            _duelScope?.Dispose();
            _duelScope = null;
            if (_physicalScope != null)
            {
                _physicalScope.Dispose();
                _physicalOwnershipRestored = _physicalScope.RestoreSucceeded;
            }
            _physicalScope = null;
            _freezeScope?.Dispose();
            _freezeScope = null;
            QualitySettings.vSyncCount = _previousVSyncCount;
            Application.targetFrameRate = _previousTargetFrameRate;
            Time.captureFramerate = _previousCaptureFrameRate;
            _runtimeRestored = true;
        }

        private static bool TryResolveScene(
            out UnityEngine.Camera camera,
            out Light directionalLight,
            out List<Component> presentations,
            out HumanoidRagdollRig recoveryRig,
            out Animator recoveryAnimator,
            out string failure)
        {
            camera = UnityEngine.Camera.main;
            if (camera == null)
            {
                UnityEngine.Camera[] cameras = FindObjectsByType<UnityEngine.Camera>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
                if (cameras.Length > 0) camera = cameras[0];
            }
            directionalLight = null;
            Light[] lights = FindObjectsByType<Light>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int index = 0; index < lights.Length; index++)
                if (lights[index].type == LightType.Directional && lights[index].enabled)
                {
                    directionalLight = lights[index];
                    break;
                }

            presentations = new List<Component>(2);
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int index = 0; index < behaviours.Length; index++)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour != null && behaviour.GetType().FullName ==
                    "Elemental.Presentation.Animation.HumanoidCharacterPresentation")
                    presentations.Add(behaviour);
            }
            presentations.Sort((left, right) => string.CompareOrdinal(
                HierarchyPath(left.transform),
                HierarchyPath(right.transform)));
            recoveryRig = null;
            recoveryAnimator = null;
            if (presentations.Count > 0)
            {
                Component presentation = presentations[0];
                recoveryRig = presentation.GetComponent<HumanoidRagdollRig>() ??
                    presentation.GetComponentInChildren<HumanoidRagdollRig>(true) ??
                    presentation.GetComponentInParent<HumanoidRagdollRig>(true);
                recoveryAnimator = Gate1Reflection.GetProperty(
                    presentation,
                    "Animator") as Animator;
            }

            if (camera == null || directionalLight == null ||
                presentations.Count < 2 || recoveryRig == null || recoveryAnimator == null)
            {
                failure =
                    "Gate1 requires one game camera, one directional light, two HumanoidCharacterPresentation owners and one visible HumanoidRagdollRig.";
                return false;
            }
            failure = string.Empty;
            return true;
        }

        private static int CountComponentsByTypeName(string fullName)
        {
            int count = 0;
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < behaviours.Length; index++)
                if (behaviours[index] != null &&
                    behaviours[index].GetType().FullName == fullName)
                    count++;
            return count;
        }

        private static string HierarchyPath(Transform transform)
        {
            string path = transform != null ? transform.name : string.Empty;
            while (transform != null && transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }

        private static bool TryReadRequest(out Gate1CaptureRequest request)
        {
            if (Gate1CaptureRequest.TryParse(
                    Environment.GetCommandLineArgs(),
                    out request))
                return true;
            string requestPath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "../Library/ElementalGate1Capture.request.json"));
            if (!File.Exists(requestPath)) return false;
            try
            {
                Gate1EditorRequestFile requestFile =
                    JsonUtility.FromJson<Gate1EditorRequestFile>(
                        File.ReadAllText(requestPath));
                if (requestFile == null ||
                    requestFile.expiresUtcTicks < DateTime.UtcNow.Ticks)
                {
                    Debug.LogError(
                        "[Elemental] Gate1 request file is stale; rerun the Editor capture command.");
                    File.Delete(requestPath);
                    return false;
                }
                string outputDirectory = requestFile.outputDirectory;
                if (string.IsNullOrWhiteSpace(outputDirectory)) return false;
                request = new Gate1CaptureRequest(Path.GetFullPath(outputDirectory));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[Elemental] Gate1 request file is invalid: {exception.Message}");
                return false;
            }
        }

        [Serializable]
        private sealed class Gate1EditorRequestFile
        {
            public string outputDirectory;
            public long expiresUtcTicks;
        }

        private sealed class Gate1BehaviourFreezeScope : IDisposable
        {
            private static readonly string[] FrozenTypeNames =
            {
                "Elemental.Runtime.Characters.EarthMvpBotController",
                "Elemental.Input.Gestures.MagicInputController"
            };

            private readonly List<Behaviour> _behaviours = new List<Behaviour>(4);

            public static Gate1BehaviourFreezeScope Begin()
            {
                var scope = new Gate1BehaviourFreezeScope();
                MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
                for (int index = 0; index < behaviours.Length; index++)
                {
                    MonoBehaviour behaviour = behaviours[index];
                    if (behaviour == null || !behaviour.enabled) continue;
                    string typeName = behaviour.GetType().FullName;
                    for (int typeIndex = 0; typeIndex < FrozenTypeNames.Length; typeIndex++)
                        if (typeName == FrozenTypeNames[typeIndex])
                        {
                            scope._behaviours.Add(behaviour);
                            behaviour.enabled = false;
                            break;
                        }
                }
                return scope;
            }

            public void Dispose()
            {
                for (int index = 0; index < _behaviours.Count; index++)
                    if (_behaviours[index] != null)
                        _behaviours[index].enabled = true;
                _behaviours.Clear();
            }
        }
    }
}
