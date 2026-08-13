using System.Collections.Generic;
using Elemental.Input.Actions;
using Elemental.Input.Gestures;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Magic;
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

        public EarthCameraState State => _state;
        public float ShoulderSign => _shoulderSign;
        public EarthCameraProfile Profile => profile;
        public Vector3 LastWeightedFocus { get; private set; }

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
            Vector3 aim = input != null && input.BendTargetPosition.sqrMagnitude > 0.01f
                ? input.BendTargetPosition
                : playerChest + motor.FacingForward * 4f;
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
            rig.SetDirectorFrame(
                stateProfile.Distance,
                stateProfile.Height,
                shoulder,
                targetFov,
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
            _subscribed = false;
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
