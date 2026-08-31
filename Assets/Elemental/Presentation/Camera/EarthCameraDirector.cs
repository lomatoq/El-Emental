using System.Collections.Generic;
using Elemental.Input.Actions;
using Elemental.Input.Gestures;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Matter;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Presentation.Camera
{
    [DisallowMultipleComponent]
    public sealed class EarthCameraDirector : MonoBehaviour
    {
        private static readonly ProfilerMarker DirectMarker =
            new ProfilerMarker("Elemental.Earth.Camera.Direct");
        [SerializeField] private PlanetCameraRig rig;
        [SerializeField] private UnityEngine.Camera controlledCamera;
        [SerializeField] private Transform player;
        [SerializeField] private Rigidbody playerBody;
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private MagicInputController input;
        [SerializeField] private EarthInputAdapter inputAdapter;
        [SerializeField] private MagicExecutor executor;
        [SerializeField] private ActiveRagdollPuppet puppet;
        [SerializeField] private EarthCameraProfile profile;

        private EarthCameraState _state;
        private EarthCameraState _candidate;
        private float _candidateSince;
        private float _impactUntil;
        private float _recoveryUntil;
        private Vector3 _constructFocus;
        private bool _hasConstructFocus;
        private float _shoulderSign = 1f;
        private bool _subscribed;
        private Vector2 _smoothedPointerViewport = new Vector2(0.5f, 0.5f);
        private Vector2 _pointerVelocity;
        private EarthCameraRequest _activeRequest;
        private float _requestUntil;

        public EarthCameraState State => _state;
        public Transform Player => player;
        public float ShoulderSign => _shoulderSign;
        public EarthCameraProfile Profile => profile;
        public Vector3 LastWeightedFocus { get; private set; }
        public EarthCameraPointerIntent LastPointerIntent { get; private set; }
        public float LastPointerInfluence { get; private set; }
        public EarthCameraRequest ActiveRequest => _activeRequest;

        public void SubmitRequest(in EarthCameraRequest request)
        {
            float now = Time.unscaledTime;
            if (!EarthCameraRequestSolver.ShouldReplace(
                    in _activeRequest, _requestUntil, in request, now)) return;
            _activeRequest = request;
            EarthCameraRequestResponse response = EarthCameraRequestSolver.Solve(in request);
            _requestUntil = now + response.HoldSeconds;
        }

        public void Configure(
            PlanetCameraRig configuredRig,
            UnityEngine.Camera configuredCamera,
            Transform configuredPlayer,
            Rigidbody configuredBody,
            PlanetMotor configuredMotor,
            MagicInputController configuredInput,
            EarthInputAdapter configuredAdapter,
            MagicExecutor configuredExecutor,
            ActiveRagdollPuppet configuredPuppet,
            EarthCameraProfile configuredProfile)
        {
            if (_subscribed) Unsubscribe();
            rig = configuredRig;
            controlledCamera = configuredCamera;
            player = configuredPlayer;
            playerBody = configuredBody;
            motor = configuredMotor;
            input = configuredInput;
            inputAdapter = configuredAdapter;
            executor = configuredExecutor;
            puppet = configuredPuppet;
            profile = configuredProfile;
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void Update()
        {
            if (rig == null || player == null || motor == null) return;
            using var marker = DirectMarker.Auto();
            _shoulderSign = EarthCameraShoulderSolver.Resolve(
                _shoulderSign,
                inputAdapter != null && inputAdapter.ShoulderSwapPressed);
            EarthCameraContext context = BuildContext();
            EarthCameraState desired = EarthCameraStateResolver.Resolve(in context);
            UpdateState(desired);
            EarthCameraStateProfile stateProfile = ResolveProfile(_state);
            EarthCameraStateProfile explore = ResolveProfile(EarthCameraState.Explore);

            Vector3 playerChest = player.position + motor.LocalUp * 1.1f;
            Vector2 viewport = inputAdapter != null ? inputAdapter.PointerViewport01 : new Vector2(0.5f, 0.5f);
            LastPointerInfluence = EarthCameraPointerInfluenceSolver.Resolve(_state);
            Vector2 pointerTarget = Vector2.Lerp(
                new Vector2(0.5f, 0.5f),
                viewport,
                LastPointerInfluence);
            _smoothedPointerViewport = Vector2.SmoothDamp(
                _smoothedPointerViewport,
                pointerTarget,
                ref _pointerVelocity,
                Mathf.Lerp(0.065f, 0.14f, profile != null ? profile.CameraLag : 1f),
                5f,
                Time.unscaledDeltaTime);
            Vector2 deadZone = profile != null ? profile.PointerDeadZoneHalfExtents : new Vector2(0.2f, 0.18f);
            LastPointerIntent = EarthCameraPointerIntentSolver.Solve(
                new float2(_smoothedPointerViewport.x, _smoothedPointerViewport.y),
                new float2(deadZone.x, deadZone.y),
                profile != null ? profile.PointerNearGroundDistance : 4.4f,
                profile != null ? profile.PointerFarGroundDistance : 11.4f,
                profile != null ? profile.PointerLowerAimElevation : -0.65f,
                profile != null ? profile.PointerUpperAimElevation : 2.35f);
            Vector3 localForward = Vector3.ProjectOnPlane(motor.FacingForward, motor.LocalUp).normalized;
            if (localForward.sqrMagnitude < 0.5f) localForward = Vector3.ProjectOnPlane(player.forward, motor.LocalUp).normalized;
            Vector3 localRight = Vector3.Cross(motor.LocalUp, localForward).normalized;
            Vector3 pointerFocus = playerChest +
                                   localForward * LastPointerIntent.GroundFocusDistance +
                                   localRight * LastPointerIntent.HorizontalBias *
                                   (profile != null ? profile.PointerHorizontalFocusMeters : 2.15f) +
                                   motor.LocalUp * LastPointerIntent.AimElevation;
            Vector3 commandAim = input != null && input.BendTargetPosition.sqrMagnitude > 0.01f
                ? input.BendTargetPosition
                : pointerFocus;
            Vector3 aim = input != null && input.CurrentBendPhase != BendPhase.Idle
                ? Vector3.Lerp(pointerFocus, commandAim, 0.74f)
                : pointerFocus;
            Rigidbody held = executor != null ? executor.HeldBody : null;
            Vector3 heldPoint = held != null ? held.worldCenterOfMass : playerChest;
            Vector3 construct = _hasConstructFocus ? _constructFocus : aim;
            var focusInput = new EarthCameraFocusInput(
                ToFloat3(playerChest), ToFloat3(aim), ToFloat3(heldPoint), ToFloat3(construct),
                stateProfile.PlayerFocusWeight,
                stateProfile.AimFocusWeight,
                held != null ? stateProfile.HeldFocusWeight : 0f,
                _hasConstructFocus ? stateProfile.ConstructFocusWeight : 0f);
            LastWeightedFocus = ToVector3(EarthCameraFocusSolver.Solve(
                in focusInput, profile != null ? profile.MaximumFocusDistance : 7.5f));

            EarthCameraRequestResponse cameraRequest = default;
            bool hasRequest = _activeRequest.IsValid && Time.unscaledTime < _requestUntil;
            if (hasRequest)
            {
                cameraRequest = EarthCameraRequestSolver.Solve(in _activeRequest);
                Vector3 requestAxis = ToVector3(_activeRequest.ActionAxis);
                Vector3 requestFocus = ToVector3(_activeRequest.ActionBounds.Center) +
                                       requestAxis * cameraRequest.LookAhead;
                float requestLimit = profile != null ? profile.MaximumFocusDistance : 7.5f;
                Vector3 requestOffset = requestFocus - playerChest;
                if (requestOffset.magnitude > requestLimit)
                    requestFocus = playerChest + requestOffset.normalized * requestLimit;
                LastWeightedFocus = Vector3.Lerp(
                    LastWeightedFocus, requestFocus, cameraRequest.FocusWeight);
            }
            else if (_activeRequest.IsValid)
            {
                _activeRequest = default;
            }

            float shake = profile != null ? profile.ShakeIntensity : 1f;
            float lag = profile != null ? profile.CameraLag : 1f;
            float fovMotion = profile != null ? profile.FieldOfViewMotion : 1f;
            float targetFov = Mathf.Lerp(explore.FieldOfView, stateProfile.FieldOfView, fovMotion);
            float shoulder = stateProfile.ShoulderOffset * _shoulderSign;
            if (_state == EarthCameraState.DrawStructure && _hasConstructFocus)
            {
                float side = Vector3.Dot(_constructFocus - playerChest, transform.right);
                if (Mathf.Abs(side) > 0.15f) shoulder = -Mathf.Sign(side) * Mathf.Abs(shoulder);
            }
            float pointerDistance = Mathf.Max(0f, LastPointerIntent.VerticalBias) * 0.65f +
                                    Mathf.Abs(LastPointerIntent.HorizontalBias) * 0.18f;
            float pointerHeight = Mathf.Max(0f, -LastPointerIntent.VerticalBias) * 0.9f +
                                  Mathf.Max(0f, LastPointerIntent.VerticalBias) * 0.32f;
            rig.ConfigureMotionLimits(
                profile != null ? profile.MaximumFocusSpeed : 18f,
                profile != null ? profile.SpringResetDistance : 8f);
            rig.SetDirectorFrame(
                stateProfile.Distance + pointerDistance + (hasRequest ? cameraRequest.DistanceDelta : 0f),
                stateProfile.Height + pointerHeight + (hasRequest ? cameraRequest.VerticalBias : 0f),
                shoulder,
                targetFov + (hasRequest ? cameraRequest.FieldOfViewDelta : 0f),
                LastWeightedFocus,
                Mathf.Lerp(0.025f, stateProfile.PositionDamping, lag),
                Mathf.Lerp(0.025f, stateProfile.RotationDamping, lag),
                stateProfile.OcclusionRadius,
                stateProfile.ImpulseGain,
                stateProfile.MaximumRoll,
                shake,
                profile != null ? profile.PullInSpeed : 24f,
                profile != null ? profile.ReleaseSpeed : 4.5f,
                profile != null ? profile.OcclusionReleaseDelay : 0.12f,
                held != null ? held.transform : null);
        }

        private EarthCameraContext BuildContext()
        {
            bool bending = input != null && input.CurrentBendPhase != BendPhase.Idle &&
                           input.CurrentBendPhase != BendPhase.Cancelled;
            bool drawing = bending && (input.SelectedAbility == EarthAbilityIds.LineWall ||
                                       input.SelectedAbility == EarthAbilityIds.RaisePlatform);
            bool holding = executor != null && executor.HeldBody != null;
            float massEffort = holding ? 1f - Mathf.Exp(-executor.HeldBody.mass / 200f) : 0f;
            float effort = Mathf.Max(massEffort, input != null ? Mathf.Max(input.BendAmount01, input.BendCharge01) : 0f);
            bool aiming = drawing || holding ||
                          (input != null && input.CurrentBendPhase == BendPhase.Acquiring);
            return new EarthCameraContext(
                aiming,
                bending || (executor != null && (executor.IsGravityWellActive || executor.IsVectorFieldActive)),
                drawing,
                holding,
                !motor.IsGrounded,
                Time.unscaledTime < _impactUntil,
                Time.unscaledTime < _recoveryUntil,
                effort);
        }

        private void UpdateState(EarthCameraState desired)
        {
            if (desired == _state)
            {
                _candidate = desired;
                _candidateSince = Time.unscaledTime;
                return;
            }
            if (desired != _candidate)
            {
                _candidate = desired;
                _candidateSince = Time.unscaledTime;
                return;
            }
            EarthCameraStateProfile next = ResolveProfile(desired);
            EarthCameraStateProfile current = ResolveProfile(_state);
            float wait = Mathf.Max(next.EnterHysteresis, current.ExitHysteresis);
            if (desired == EarthCameraState.Impact) wait = 0f;
            if (Time.unscaledTime - _candidateSince >= wait) _state = desired;
        }

        private EarthCameraStateProfile ResolveProfile(EarthCameraState state)
        {
            if (profile != null && profile.TryGet(state, out EarthCameraStateProfile value)) return value;
            return EarthCameraStateProfile.Default(state);
        }

        private void OnPreview(IReadOnlyList<Vector3> points)
        {
            if (points == null || points.Count == 0) { _hasConstructFocus = false; return; }
            _constructFocus = points[points.Count / 2];
            _hasConstructFocus = true;
        }

        private void OnPreviewCleared() => _hasConstructFocus = false;
        private void OnImpact(Vector3 _, float impulse)
        {
            _impactUntil = Mathf.Max(_impactUntil, Time.unscaledTime + Mathf.Lerp(0.08f, 0.28f,
                Mathf.Clamp01(impulse / 900f)));
        }
        private void OnWallRaised(WallRaisedEvent value)
        {
            float3 center = (value.Start + value.End) * 0.5f;
            float3 axis = value.End - value.Start;
            float3 extents = new float3(
                math.max(value.Thickness, math.length(axis) * 0.5f),
                value.Height * 0.5f,
                math.max(value.Thickness, 0.3f));
            var envelope = new EarthCameraEnvelope(center, extents);
            var request = new EarthCameraRequest(
                EarthCameraIntent.Structure, axis, in envelope,
                value.Height * math.length(axis), 0f, 0.8f, 0.2f,
                new EarthMatterId(value.WallId, 1), 120);
            SubmitRequest(in request);
        }

        private void OnFragmentLaunched(FragmentLaunchedEvent value)
        {
            var envelope = EarthCameraEnvelope.Point(value.Position, math.max(0.15f, value.Mass / 1200f));
            var request = new EarthCameraRequest(
                EarthCameraIntent.Projectile, value.Direction, in envelope,
                value.Mass * value.VelocityChange, 0f, 1f, 0.15f,
                new EarthMatterId(value.FragmentId, 1), 130);
            SubmitRequest(in request);
        }

        private void OnEarthImpact(EarthImpactEvent value)
        {
            var envelope = EarthCameraEnvelope.Point(value.Point, math.lerp(0.2f, 1.8f,
                1f - math.exp(-value.Impulse / 900f)));
            var request = new EarthCameraRequest(
                EarthCameraIntent.Impact, value.Normal, in envelope,
                value.Impulse, 0f, 1f, 0.35f,
                default, 180);
            SubmitRequest(in request);
        }

        private void OnEarthReturn(EarthReturnEvent value)
        {
            if (value.Stage != EarthReturnEventStage.Subsurface &&
                value.Stage != EarthReturnEventStage.Completed) return;
            var envelope = EarthCameraEnvelope.Point(value.Position,
                math.max(0.18f, math.pow(math.max(0.001f, value.Volume), 1f / 3f)));
            var request = new EarthCameraRequest(
                EarthCameraIntent.Return, -math.normalizesafe(value.Position), in envelope,
                value.Mass, 0.1f, value.Stage == EarthReturnEventStage.Completed ? 1f : 0.45f,
                value.Stage == EarthReturnEventStage.Completed ? 0.7f : 0.15f,
                new EarthMatterId(value.MatterId, value.Generation), 115);
            SubmitRequest(in request);
        }
        private void OnPhysicalState(CharacterPhysicalState state)
        {
            if (state.Mode == CharacterPhysicalMode.Recovery)
                _recoveryUntil = Mathf.Max(_recoveryUntil, Time.unscaledTime + 0.45f);
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            if (input != null)
            {
                input.PreviewChanged += OnPreview;
                input.PreviewCleared += OnPreviewCleared;
            }
            if (puppet != null)
            {
                puppet.ImpactObserved += OnImpact;
                puppet.StateChanged += OnPhysicalState;
            }
            if (executor != null)
            {
                executor.Events.WallRaised += OnWallRaised;
                executor.Events.FragmentLaunched += OnFragmentLaunched;
                executor.Events.EarthImpactOccurred += OnEarthImpact;
                executor.Events.EarthReturnOccurred += OnEarthReturn;
            }
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (input != null)
            {
                input.PreviewChanged -= OnPreview;
                input.PreviewCleared -= OnPreviewCleared;
            }
            if (puppet != null)
            {
                puppet.ImpactObserved -= OnImpact;
                puppet.StateChanged -= OnPhysicalState;
            }
            if (executor != null)
            {
                executor.Events.WallRaised -= OnWallRaised;
                executor.Events.FragmentLaunched -= OnFragmentLaunched;
                executor.Events.EarthImpactOccurred -= OnEarthImpact;
                executor.Events.EarthReturnOccurred -= OnEarthReturn;
            }
            _subscribed = false;
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
