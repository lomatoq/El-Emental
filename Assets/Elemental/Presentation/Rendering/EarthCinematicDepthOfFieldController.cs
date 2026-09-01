using Elemental.Simulation.Rendering;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    public enum EarthCinematicDepthOfFieldDebugView : byte
    {
        Off = 0,
        SignedCircleOfConfusion = 1,
        NearLayer = 2,
        FarLayer = 3,
        Coverage = 4
    }

    public enum EarthCinematicDepthOfFieldGateState : byte
    {
        Inactive = 0,
        Ready = 1,
        MissingSubject = 2,
        NonFiniteEvidence = 3,
        SubjectOutsideSharpEnvelope = 4
    }

    public static class EarthCinematicDepthOfFieldGate
    {
        public static EarthCinematicDepthOfFieldGateState Evaluate(
            bool requested,
            in EarthCinematicDepthOfFieldEvidence evidence)
        {
            if (!requested)
                return EarthCinematicDepthOfFieldGateState.Inactive;
            if (!evidence.IsFinite)
                return EarthCinematicDepthOfFieldGateState.NonFiniteEvidence;
            if (!evidence.HasBothSubjects)
                return EarthCinematicDepthOfFieldGateState.MissingSubject;
            return evidence.BothSubjectsSharp
                ? EarthCinematicDepthOfFieldGateState.Ready
                : EarthCinematicDepthOfFieldGateState.SubjectOutsideSharpEnvelope;
        }
    }

    public readonly struct EarthCinematicDepthOfFieldSettings
    {
        public EarthCinematicDepthOfFieldSettings(
            float sharpNearDistance,
            float sharpFarDistance,
            float nearTransition,
            float farTransition,
            float maxRadiusPixels,
            EarthCinematicDepthOfFieldDebugView debugView)
        {
            SharpNearDistance = sharpNearDistance;
            SharpFarDistance = sharpFarDistance;
            NearTransition = nearTransition;
            FarTransition = farTransition;
            MaxRadiusPixels = maxRadiusPixels;
            DebugView = debugView;
        }

        public float SharpNearDistance { get; }
        public float SharpFarDistance { get; }
        public float FocusDistance =>
            (SharpNearDistance + SharpFarDistance) * 0.5f;
        public float NearTransition { get; }
        public float FarTransition { get; }
        public float MaxRadiusPixels { get; }
        public EarthCinematicDepthOfFieldDebugView DebugView { get; }
    }

    public readonly struct EarthCinematicDepthOfFieldEvidence
    {
        private const float ContainmentTolerance = 0.001f;

        public EarthCinematicDepthOfFieldEvidence(
            Vector3 cameraPosition,
            Quaternion cameraRotation,
            bool primaryConfigured,
            bool secondaryConfigured,
            bool primarySampled,
            float primaryNearDistance,
            float primaryFarDistance,
            bool secondarySampled,
            float secondaryNearDistance,
            float secondaryFarDistance,
            float sharpNearDistance,
            float sharpFarDistance)
        {
            CameraPosition = cameraPosition;
            CameraRotation = cameraRotation;
            PrimaryConfigured = primaryConfigured;
            SecondaryConfigured = secondaryConfigured;
            PrimarySampled = primarySampled;
            PrimaryNearDistance = primaryNearDistance;
            PrimaryFarDistance = primaryFarDistance;
            SecondarySampled = secondarySampled;
            SecondaryNearDistance = secondaryNearDistance;
            SecondaryFarDistance = secondaryFarDistance;
            SharpNearDistance = sharpNearDistance;
            SharpFarDistance = sharpFarDistance;
        }

        public Vector3 CameraPosition { get; }
        public Quaternion CameraRotation { get; }
        public bool PrimaryConfigured { get; }
        public bool SecondaryConfigured { get; }
        public bool PrimarySampled { get; }
        public float PrimaryNearDistance { get; }
        public float PrimaryFarDistance { get; }
        public bool SecondarySampled { get; }
        public float SecondaryNearDistance { get; }
        public float SecondaryFarDistance { get; }
        public float SharpNearDistance { get; }
        public float SharpFarDistance { get; }
        public bool HasBothSubjects =>
            PrimaryConfigured && SecondaryConfigured &&
            PrimarySampled && SecondarySampled;
        public bool BothSubjectsSharp => HasBothSubjects &&
            Contains(PrimaryNearDistance, PrimaryFarDistance) &&
            Contains(SecondaryNearDistance, SecondaryFarDistance);
        public bool IsFinite =>
            IsFiniteVector(CameraPosition) && IsFiniteQuaternion(CameraRotation) &&
            float.IsFinite(SharpNearDistance) &&
            float.IsFinite(SharpFarDistance) &&
            SharpFarDistance >= SharpNearDistance &&
            (!PrimarySampled ||
             float.IsFinite(PrimaryNearDistance) &&
             float.IsFinite(PrimaryFarDistance) &&
             PrimaryFarDistance >= PrimaryNearDistance) &&
            (!SecondarySampled ||
             float.IsFinite(SecondaryNearDistance) &&
             float.IsFinite(SecondaryFarDistance) &&
             SecondaryFarDistance >= SecondaryNearDistance);

        private bool Contains(float subjectNear, float subjectFar)
        {
            return subjectNear >= SharpNearDistance - ContainmentTolerance &&
                subjectFar <= SharpFarDistance + ContainmentTolerance;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                float.IsFinite(value.y) &&
                float.IsFinite(value.z);
        }

        private static bool IsFiniteQuaternion(Quaternion value)
        {
            return float.IsFinite(value.x) &&
                float.IsFinite(value.y) &&
                float.IsFinite(value.z) &&
                float.IsFinite(value.w);
        }
    }

    /// <summary>
    /// Stable, camera-local owner for the custom depth-aware DOF pass. Gameplay
    /// policy is supplied by EarthChargeCameraLookdevV2. This component is the
    /// sole owner of the padded two-subject sharp envelope and refuses to render
    /// until the request has remained stable.
    /// </summary>
    [DefaultExecutionOrder(10200)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class EarthCinematicDepthOfFieldController : MonoBehaviour
    {
        private const float ActivationHoldSeconds = 0.08f;
        private static readonly ProfilerMarker EnvelopeMarker =
            new ProfilerMarker("Elemental.Rendering.DualSubjectDofEnvelope");
        [SerializeField, Min(0.05f)] private float nearTransition = 1.15f;
        [SerializeField, Min(0.1f)] private float farTransition = 5.5f;
        [SerializeField, Range(1f, 12f)] private float maxRadiusPixels = 7f;
        [SerializeField, Range(0.1f, 2f)] private float silhouettePadding = 0.85f;
        [SerializeField, Min(0.1f)] private float envelopeContractionSpeed = 4f;
        [SerializeField] private Transform primarySubject;
        [SerializeField] private Transform secondarySubject;
        [SerializeField] private EarthCinematicDepthOfFieldDebugView debugView;

        private bool _requested;
        private bool _runtimeActive;
        private bool _hasEnvelope;
        private float _requestedFor;
        private float _targetFocusDistance = 8f;
        private EarthCinematicDepthOfFieldEnvelope _targetEnvelope =
            new EarthCinematicDepthOfFieldEnvelope(8f, 8f);
        private EarthCinematicDepthOfFieldEnvelope _envelope =
            new EarthCinematicDepthOfFieldEnvelope(8f, 8f);
        private Renderer[] _primaryRenderers = new Renderer[0];
        private Renderer[] _secondaryRenderers = new Renderer[0];
        private float _lensRadiusScale = 1f;
        private bool _captureOverride;
        private EarthCinematicDepthOfFieldDebugView _captureDebugView;
        private bool _hasEnvelopeCameraPose;
        private Vector3 _envelopeCameraPosition;
        private Quaternion _envelopeCameraRotation;
        private EarthCinematicDepthOfFieldEvidence _evidence;
        private EarthCinematicDepthOfFieldGateState _lastRenderGateState;
        private EarthCinematicDepthOfFieldGateState _lastReportedGateState;
        private uint _rejectedRenderFrameCount;
        private int _lastRejectedUnityFrame = -1;

        public bool IsRuntimeActive => _runtimeActive;
        public float FocusDistance => _envelope.Midpoint;
        public float SharpNearDistance => _envelope.Near;
        public float SharpFarDistance => _envelope.Far;
        public float SilhouettePadding => silhouettePadding;
        public Transform PrimarySubject => primarySubject;
        public Transform SecondarySubject => secondarySubject;
        public bool HasCaptureOverride => _captureOverride;
        public EarthCinematicDepthOfFieldDebugView CaptureDebugView =>
            _captureDebugView;
        public EarthCinematicDepthOfFieldGateState LastRenderGateState =>
            _lastRenderGateState;
        public uint RejectedRenderFrameCount => _rejectedRenderFrameCount;

        public void ConfigureSubjects(
            Transform configuredPrimarySubject,
            Transform configuredSecondarySubject)
        {
            primarySubject = configuredPrimarySubject;
            secondarySubject = configuredSecondarySubject;
            _primaryRenderers = ResolveSubjectRenderers(primarySubject);
            _secondaryRenderers = ResolveSubjectRenderers(secondarySubject);
            RefreshEnvelope(0f, true);
        }

        public void ApplyPolicy(
            bool requested,
            float targetFocusDistance,
            float aperture,
            float focalLength)
        {
            _requested = requested;
            _targetFocusDistance = Mathf.Clamp(targetFocusDistance, 1.25f, 36f);
            _lensRadiusScale = Mathf.Clamp(
                (Mathf.Max(1f, focalLength) / 50f) *
                (5.6f / Mathf.Max(1f, aperture)),
                0.65f,
                1.35f);
            if (_hasEnvelope) return;
            RefreshEnvelope(0f, true);
        }

        public void SetCaptureOverride(
            bool active,
            EarthCinematicDepthOfFieldDebugView captureDebugView =
                EarthCinematicDepthOfFieldDebugView.Off)
        {
            _captureOverride = active;
            _captureDebugView = captureDebugView;
            if (active) RefreshEnvelope(0f, true);
        }

        public bool TryGetRenderSettings(
            out EarthCinematicDepthOfFieldSettings settings)
        {
            // Manual Camera.Render(), deterministic captures and late camera
            // owners may move the camera after this component's LateUpdate. The
            // subject depths are camera-local, so never submit a stale envelope
            // to the renderer feature for a different view pose.
            bool cameraPoseChanged = HasCameraPoseChangedSinceEnvelope();
            // Actors can be moved by late presentation owners or an explicit
            // Camera.Render() capture after this component's LateUpdate. A
            // zero-delta resample expands immediately when either silhouette
            // moves, while preserving contraction hysteresis and allocating no
            // managed storage.
            RefreshEnvelope(0f, cameraPoseChanged);

            bool active = _captureOverride ||
                          (Application.isPlaying && _runtimeActive);
            settings = new EarthCinematicDepthOfFieldSettings(
                Mathf.Clamp(_envelope.Near, 1.25f, 36f),
                Mathf.Clamp(_envelope.Far, _envelope.Near, 36f),
                Mathf.Max(0.05f, nearTransition),
                Mathf.Max(0.1f, farTransition),
                Mathf.Clamp(maxRadiusPixels * _lensRadiusScale, 1f, 12f),
                _captureOverride ? _captureDebugView : debugView);
            bool requested = active && isActiveAndEnabled;
            EarthCinematicDepthOfFieldGateState gateState =
                EarthCinematicDepthOfFieldGate.Evaluate(requested, in _evidence);
            PublishRenderGateState(gateState);
            return gateState == EarthCinematicDepthOfFieldGateState.Ready;
        }

        public bool TryGetSharpEnvelopeEvidence(
            out EarthCinematicDepthOfFieldEvidence evidence)
        {
            bool cameraPoseChanged = HasCameraPoseChangedSinceEnvelope();
            RefreshEnvelope(0f, cameraPoseChanged);
            evidence = _evidence;
            return evidence.IsFinite;
        }

        private void PublishRenderGateState(
            EarthCinematicDepthOfFieldGateState gateState)
        {
            _lastRenderGateState = gateState;
            if (gateState == EarthCinematicDepthOfFieldGateState.Ready ||
                gateState == EarthCinematicDepthOfFieldGateState.Inactive)
            {
                _lastReportedGateState = gateState;
                return;
            }

            int unityFrame = Time.frameCount;
            if (_lastRejectedUnityFrame != unityFrame)
            {
                _lastRejectedUnityFrame = unityFrame;
                if (_rejectedRenderFrameCount < uint.MaxValue)
                    _rejectedRenderFrameCount++;
            }
            if (_lastReportedGateState == gateState) return;
            _lastReportedGateState = gateState;
            // Shipping rendering is deliberately fail-closed: the renderer
            // skips DOF and the rejected-frame counter remains authoritative.
            // Scene loads, test teardown and camera-owner handoffs can expose a
            // transient incomplete subject set, though, and logging that as a
            // project error makes unrelated PlayMode gates fail even though no
            // invalid DOF frame was rendered. Explicit capture/debug sessions
            // retain the loud diagnostic used by the focused lifecycle test.
            if (!_captureOverride) return;
            switch (gateState)
            {
                case EarthCinematicDepthOfFieldGateState.MissingSubject:
                    Debug.LogError(
                        "Earth cinematic DOF rejected the frame because both fighter subjects " +
                        "were not configured and sampled. Rebind both duel presentations.",
                        this);
                    break;
                case EarthCinematicDepthOfFieldGateState.NonFiniteEvidence:
                    Debug.LogError(
                        "Earth cinematic DOF rejected non-finite camera or sharp-envelope evidence.",
                        this);
                    break;
                case EarthCinematicDepthOfFieldGateState.SubjectOutsideSharpEnvelope:
                    Debug.LogError(
                        "Earth cinematic DOF rejected the frame because a fighter silhouette " +
                        "fell outside the shared sharp envelope.",
                        this);
                    break;
            }
        }

        private void OnEnable()
        {
            _primaryRenderers = ResolveSubjectRenderers(primarySubject);
            _secondaryRenderers = ResolveSubjectRenderers(secondarySubject);
            RefreshEnvelope(0f, true);
        }

        private void LateUpdate()
        {
            float deltaTime = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            RefreshEnvelope(deltaTime, false);
            if (!_requested)
            {
                // Capability policy may cut the lens immediately (currently Web);
                // the focus value remains warm for a later native re-entry.
                _requestedFor = 0f;
                _runtimeActive = false;
                return;
            }

            _requestedFor += deltaTime;
            _runtimeActive = _requestedFor >= ActivationHoldSeconds;
        }

        private void RefreshEnvelope(float deltaTime, bool forceInitialize)
        {
            using (EnvelopeMarker.Auto())
            {
                _envelopeCameraPosition = transform.position;
                _envelopeCameraRotation = transform.rotation;
                _hasEnvelopeCameraPose = true;

                bool primaryValid = TryGetSubjectDepthRange(
                    primarySubject,
                    _primaryRenderers,
                    out float primaryNear,
                    out float primaryFar);
                bool secondaryValid = TryGetSubjectDepthRange(
                    secondarySubject,
                    _secondaryRenderers,
                    out float secondaryNear,
                    out float secondaryFar);
                float sampledPrimaryNear = primaryNear;
                float sampledPrimaryFar = primaryFar;
                float sampledSecondaryNear = secondaryNear;
                float sampledSecondaryFar = secondaryFar;
                if (!primaryValid && !secondaryValid)
                {
                    primaryNear = _targetFocusDistance;
                    primaryFar = _targetFocusDistance;
                    secondaryNear = _targetFocusDistance;
                    secondaryFar = _targetFocusDistance;
                }
                else if (!primaryValid)
                {
                    primaryNear = secondaryNear;
                    primaryFar = secondaryFar;
                }
                else if (!secondaryValid)
                {
                    secondaryNear = primaryNear;
                    secondaryFar = primaryFar;
                }

                _targetEnvelope = EarthCinematicDepthOfFieldSolver.ResolveSharpEnvelopeFromRanges(
                    primaryNear,
                    primaryFar,
                    secondaryNear,
                    secondaryFar,
                    Mathf.Max(0.1f, silhouettePadding),
                    1.25f,
                    36f);
                if (forceInitialize || !_hasEnvelope)
                {
                    _envelope = _targetEnvelope;
                    _hasEnvelope = true;
                }
                else
                {
                    _envelope = EarthCinematicDepthOfFieldSolver.StepSharpEnvelope(
                        in _envelope,
                        in _targetEnvelope,
                        Mathf.Max(0.1f, envelopeContractionSpeed),
                        Mathf.Max(0f, deltaTime));
                }

                _evidence = new EarthCinematicDepthOfFieldEvidence(
                    _envelopeCameraPosition,
                    _envelopeCameraRotation,
                    primarySubject != null,
                    secondarySubject != null,
                    primaryValid,
                    sampledPrimaryNear,
                    sampledPrimaryFar,
                    secondaryValid,
                    sampledSecondaryNear,
                    sampledSecondaryFar,
                    _envelope.Near,
                    _envelope.Far);
            }
        }

        private bool HasCameraPoseChangedSinceEnvelope()
        {
            if (!_hasEnvelopeCameraPose) return true;
            if ((transform.position - _envelopeCameraPosition).sqrMagnitude > 0.000001f)
                return true;
            return Quaternion.Angle(transform.rotation, _envelopeCameraRotation) > 0.01f;
        }

        private bool TryGetSubjectDepthRange(
            Transform subject,
            Renderer[] renderers,
            out float nearDepth,
            out float farDepth)
        {
            nearDepth = float.PositiveInfinity;
            farDepth = float.NegativeInfinity;
            bool hasBounds = false;
            Vector3 forward = transform.forward;
            if (renderers != null)
            {
                for (int index = 0; index < renderers.Length; index++)
                {
                    Renderer renderer = renderers[index];
                    if (renderer == null || !renderer.enabled ||
                        !renderer.gameObject.activeInHierarchy ||
                        (renderer is not SkinnedMeshRenderer &&
                         renderer is not MeshRenderer))
                        continue;
                    Bounds bounds = renderer.bounds;
                    float centerDepth = Vector3.Dot(
                        bounds.center - transform.position,
                        forward);
                    Vector3 extents = bounds.extents;
                    float depthRadius = Mathf.Abs(forward.x) * extents.x +
                                        Mathf.Abs(forward.y) * extents.y +
                                        Mathf.Abs(forward.z) * extents.z;
                    nearDepth = Mathf.Min(nearDepth, centerDepth - depthRadius);
                    farDepth = Mathf.Max(farDepth, centerDepth + depthRadius);
                    hasBounds = true;
                }
            }
            if (hasBounds && float.IsFinite(nearDepth) &&
                float.IsFinite(farDepth) && farDepth > 0.05f)
                return true;
            if (subject == null) return false;
            float fallbackDepth = Vector3.Dot(
                subject.position + subject.up * 0.8f - transform.position,
                forward);
            nearDepth = fallbackDepth;
            farDepth = fallbackDepth;
            return float.IsFinite(fallbackDepth) && fallbackDepth > 0.05f;
        }

        private static Renderer[] ResolveSubjectRenderers(Transform subject)
        {
            return subject != null
                ? subject.GetComponentsInChildren<Renderer>(true)
                : new Renderer[0];
        }

        private void OnDisable()
        {
            _runtimeActive = false;
            _requestedFor = 0f;
            _captureOverride = false;
            _hasEnvelopeCameraPose = false;
            _lastRenderGateState = EarthCinematicDepthOfFieldGateState.Inactive;
            _lastReportedGateState = EarthCinematicDepthOfFieldGateState.Inactive;
            _lastRejectedUnityFrame = -1;
        }
    }
}
