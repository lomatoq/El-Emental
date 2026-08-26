using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Characters;
using Unity.Cinemachine;
using UnityEngine;

namespace Elemental.Presentation.Camera
{
    /// <summary>
    /// Adapts Cinemachine's third-person rig to a spherical world.  The world-up frame
    /// follows the motor while the aim pivot owns pitch, so looking down never tilts the
    /// horizon or feeds camera motion back into gameplay physics.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class EarthCinemachineCameraController : MonoBehaviour
    {
        private const int ControlledMagicLayer = 2;
        private const float ChestSightlineRadius = 0.26f;
        private const float HeadSightlineRadius = 0.22f;
        [SerializeField] private UnityEngine.Camera controlledCamera;
        [SerializeField] private CinemachineBrain brain;
        [SerializeField] private CinemachineCamera virtualCamera;
        [SerializeField] private CinemachineThirdPersonFollow thirdPersonFollow;
        [SerializeField] private PlanetCameraRig legacyRig;
        [SerializeField] private EarthCameraDirector director;
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private EarthArmorController armor;
        [SerializeField] private VoxelPlanetBehaviour voxelPlanet;
        [SerializeField] private EarthCinemachineSphericalClearance sphericalClearance;
        [SerializeField] private Transform player;
        [SerializeField] private Transform worldUpFrame;
        [SerializeField] private Transform aimPivot;
        [Header("Composition")]
        [SerializeField, Range(5f, 35f)] private float neutralPitch = 16f;
        [SerializeField, Range(0f, 20f)] private float pointerPitchRange = 10f;
        [SerializeField, Min(0.01f)] private float headingDamping = 0.12f;
        [SerializeField, Min(0.01f)] private float pitchDamping = 0.1f;
        [SerializeField, Min(0f)] private float shoulderHeight = 0.62f;
        [SerializeField, Min(0f)] private float minimumArmLength = 0.72f;
        [SerializeField, Min(0f)] private float maximumArmLength = 1.55f;

        private float _smoothedPitch;
        private float _pitchVelocity;
        private Vector3 _smoothedForward;
        private bool _initialized;
        private readonly EarthArmorPiece[] _armorPieces =
            new EarthArmorPiece[EarthArmorProfile.MaximumPieceCount];

        public bool IsLive => brain != null && virtualCamera != null && virtualCamera.isActiveAndEnabled;
        public float AimPitch => _smoothedPitch;
        public Transform WorldUpFrame => worldUpFrame;
        public Transform AimPivot => aimPivot;
        public CinemachineCamera VirtualCamera => virtualCamera;
        public bool IgnoresControlledMagic => thirdPersonFollow != null &&
            (thirdPersonFollow.AvoidObstacles.CollisionFilter.value & (1 << ControlledMagicLayer)) == 0;
        public int HiddenArmorPieceCount { get; private set; }
        public bool HasSphericalClearance => sphericalClearance != null && sphericalClearance.isActiveAndEnabled;

        public void Configure(
            UnityEngine.Camera configuredCamera,
            CinemachineBrain configuredBrain,
            CinemachineCamera configuredVirtualCamera,
            CinemachineThirdPersonFollow configuredFollow,
            PlanetCameraRig configuredLegacyRig,
            EarthCameraDirector configuredDirector,
            PlanetMotor configuredMotor,
            Transform configuredPlayer,
            Transform configuredWorldUpFrame,
            Transform configuredAimPivot)
        {
            controlledCamera = configuredCamera;
            brain = configuredBrain;
            virtualCamera = configuredVirtualCamera;
            thirdPersonFollow = configuredFollow;
            legacyRig = configuredLegacyRig;
            director = configuredDirector;
            motor = configuredMotor;
            player = configuredPlayer;
            armor = configuredPlayer != null ? configuredPlayer.GetComponent<EarthArmorController>() : null;
            worldUpFrame = configuredWorldUpFrame;
            aimPivot = configuredAimPivot;
            EnsureAudioListener();
            PrepareRig();
            SnapToTarget();
        }

        private void Awake()
        {
            if (controlledCamera == null) controlledCamera = GetComponentInParent<UnityEngine.Camera>();
            if (controlledCamera == null) controlledCamera = UnityEngine.Camera.main;
            if (controlledCamera == null) controlledCamera = FindAnyObjectByType<UnityEngine.Camera>();
            if (brain == null && controlledCamera != null) brain = controlledCamera.GetComponent<CinemachineBrain>();
            if (legacyRig == null && controlledCamera != null) legacyRig = controlledCamera.GetComponent<PlanetCameraRig>();
            if (thirdPersonFollow == null && virtualCamera != null)
                thirdPersonFollow = virtualCamera.GetComponent<CinemachineThirdPersonFollow>();
            if (armor == null && player != null) armor = player.GetComponent<EarthArmorController>();
            EnsureAudioListener();
            PrepareRig();
        }

        private void OnEnable()
        {
            UnityEngine.Camera.onPreCull -= HandleCameraPreCull;
            UnityEngine.Camera.onPreCull += HandleCameraPreCull;
            EnsureAudioListener();
            PrepareRig();
            if (legacyRig != null) legacyRig.SetExternalDriverActive(true);
        }

        private void OnDisable()
        {
            UnityEngine.Camera.onPreCull -= HandleCameraPreCull;
            ClearArmorVisibility();
            if (legacyRig != null) legacyRig.SetExternalDriverActive(false);
        }

        private void LateUpdate()
        {
            if (motor == null || player == null || worldUpFrame == null || aimPivot == null ||
                virtualCamera == null || thirdPersonFollow == null)
                return;

            Vector3 up = motor.LocalUp;
            if (!IsFinite(up) || up.sqrMagnitude < 0.5f) return;
            up.Normalize();
            Vector3 desiredForward = Vector3.ProjectOnPlane(motor.FacingForward, up);
            if (desiredForward.sqrMagnitude < 0.01f)
                desiredForward = Vector3.ProjectOnPlane(player.forward, up);
            if (desiredForward.sqrMagnitude < 0.01f)
                desiredForward = Vector3.Cross(up, Vector3.right);
            desiredForward.Normalize();

            if (!_initialized || _smoothedForward.sqrMagnitude < 0.5f)
            {
                _smoothedForward = desiredForward;
                _smoothedPitch = ResolveTargetPitch();
                _initialized = true;
            }
            float headingBlend = 1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.01f, headingDamping));
            _smoothedForward = Vector3.Slerp(_smoothedForward, desiredForward, headingBlend);
            _smoothedForward = Vector3.ProjectOnPlane(_smoothedForward, up).normalized;
            if (_smoothedForward.sqrMagnitude < 0.5f) _smoothedForward = desiredForward;
            _smoothedPitch = Mathf.SmoothDampAngle(
                _smoothedPitch,
                ResolveTargetPitch(),
                ref _pitchVelocity,
                pitchDamping,
                140f,
                Time.unscaledDeltaTime);

            worldUpFrame.SetPositionAndRotation(
                player.position + up * 1.08f,
                Quaternion.LookRotation(_smoothedForward, up));
            aimPivot.localPosition = Vector3.zero;
            // Positive local-X pitch turns the target forward vector toward -localUp
            // in this rig, so profile values remain intuitive: positive looks down.
            aimPivot.localRotation = Quaternion.Euler(_smoothedPitch, 0f, 0f);
            ApplyStateComposition();
            ApplyArmorSafeObstacleFilter();
            legacyRig?.SyncExternalFrame(
                up,
                _smoothedForward,
                player.position + up * 1.05f + _smoothedForward * 4.5f);
        }

        public void SnapToTarget()
        {
            if (motor == null || player == null || worldUpFrame == null || aimPivot == null) return;
            Vector3 up = motor.LocalUp.sqrMagnitude > 0.5f ? motor.LocalUp.normalized : player.up;
            Vector3 forward = Vector3.ProjectOnPlane(motor.FacingForward, up);
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.ProjectOnPlane(player.forward, up);
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.Cross(up, Vector3.right);
            _smoothedForward = forward.normalized;
            _smoothedPitch = ResolveTargetPitch();
            _pitchVelocity = 0f;
            _initialized = true;
            worldUpFrame.SetPositionAndRotation(
                player.position + up * 1.08f,
                Quaternion.LookRotation(_smoothedForward, up));
            aimPivot.localPosition = Vector3.zero;
            aimPivot.localRotation = Quaternion.Euler(_smoothedPitch, 0f, 0f);
            legacyRig?.SyncExternalFrame(
                up,
                _smoothedForward,
                player.position + up * 1.05f + _smoothedForward * 4.5f);
            virtualCamera?.CancelDamping();
        }

        private void PrepareRig()
        {
            if (armor == null && player != null) armor = player.GetComponent<EarthArmorController>();
            if (voxelPlanet == null) voxelPlanet = FindAnyObjectByType<VoxelPlanetBehaviour>();
            if (brain != null && worldUpFrame != null)
            {
                brain.WorldUpOverride = worldUpFrame;
                brain.UpdateMethod = CinemachineBrain.UpdateMethods.SmartUpdate;
                brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
                brain.DefaultBlend = new CinemachineBlendDefinition(
                    CinemachineBlendDefinition.Styles.EaseInOut, 0.18f);
            }
            if (virtualCamera != null && aimPivot != null)
            {
                if (sphericalClearance == null)
                    sphericalClearance = virtualCamera.GetComponent<EarthCinemachineSphericalClearance>();
                if (sphericalClearance == null && Application.isPlaying)
                    sphericalClearance = virtualCamera.gameObject.AddComponent<EarthCinemachineSphericalClearance>();
                sphericalClearance?.Configure(voxelPlanet, player);
                // The scene is also exercised additively by PlayMode tests and editor
                // tools.  Give the gameplay camera an explicit ownership priority so
                // a stale/default CinemachineCamera from another loaded scene cannot
                // become live and invert the spherical-world composition.
                virtualCamera.Priority = 100;
                virtualCamera.Prioritize();
                virtualCamera.Target = new CameraTarget
                {
                    TrackingTarget = aimPivot,
                    LookAtTarget = null,
                    CustomLookAtTarget = false
                };
                EarthCameraState initialState = director != null
                    ? director.State
                    : EarthCameraState.Explore;
                EarthCameraStateProfile initialProfile = EarthCameraStateProfile.Default(initialState);
                if (director != null && director.Profile != null)
                    director.Profile.TryGet(initialState, out initialProfile);
                LensSettings initialLens = virtualCamera.Lens;
                initialLens.FieldOfView = initialProfile.FieldOfView;
                virtualCamera.Lens = initialLens;
            }
            if (thirdPersonFollow != null)
            {
                thirdPersonFollow.Damping = new Vector3(0.12f, 0.16f, 0.1f);
                thirdPersonFollow.ShoulderOffset = new Vector3(0.72f, shoulderHeight, 0f);
                thirdPersonFollow.VerticalArmLength = 0.9f;
                thirdPersonFollow.CameraDistance = 6.95f;
                thirdPersonFollow.CameraSide = 1f;
                thirdPersonFollow.AvoidObstacles.Enabled = true;
                // Layer 2 is reserved for player-controlled magic formations. Those
                // stones may protect the hero physically, but must never make the
                // camera solve inward through the visible character.
                ApplyArmorSafeObstacleFilter();
                thirdPersonFollow.AvoidObstacles.IgnoreTag = "Player";
                thirdPersonFollow.AvoidObstacles.CameraRadius = 0.28f;
                thirdPersonFollow.AvoidObstacles.DampingIntoCollision = 0.02f;
                thirdPersonFollow.AvoidObstacles.DampingFromCollision = 0.32f;
            }
            if (legacyRig != null) legacyRig.SetExternalDriverActive(true);
        }

        private void EnsureAudioListener()
        {
            if (controlledCamera == null) return;
            AudioListener listener = controlledCamera.GetComponent<AudioListener>();
            if (listener == null) listener = controlledCamera.gameObject.AddComponent<AudioListener>();

            AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
            for (int index = 0; index < listeners.Length; index++)
            {
                AudioListener candidate = listeners[index];
                if (candidate != null && candidate != listener) candidate.enabled = false;
            }
            listener.enabled = true;
        }

        private void ApplyArmorSafeObstacleFilter()
        {
            if (thirdPersonFollow == null) return;
            int collisionMask = thirdPersonFollow.AvoidObstacles.CollisionFilter.value;
            collisionMask &= ~(1 << ControlledMagicLayer);
            thirdPersonFollow.AvoidObstacles.CollisionFilter = collisionMask;
        }

        private void HandleCameraPreCull(UnityEngine.Camera renderingCamera)
        {
            if (renderingCamera != controlledCamera) return;
            RefreshArmorVisibilityNow();
        }

        /// <summary>
        /// Uses the final rendered camera pose to keep the avatar readable through a
        /// compact shell, dome, orbit and released debris. Only rendering is affected;
        /// armor colliders, mass and targetability remain authoritative.
        /// </summary>
        public void RefreshArmorVisibilityNow()
        {
            HiddenArmorPieceCount = 0;
            if (armor == null && player != null) armor = player.GetComponent<EarthArmorController>();
            if (armor == null || controlledCamera == null || player == null) return;

            int count = armor.CopyActivePiecesNonAlloc(_armorPieces);
            Vector3 up = motor != null && motor.LocalUp.sqrMagnitude > 0.5f
                ? motor.LocalUp.normalized
                : player.up;
            Vector3 cameraPosition = controlledCamera.transform.position;
            Vector3 chestFocus = player.position + up * 0.92f;
            Vector3 headFocus = player.position + up * 1.58f;
            float nearClearance = Mathf.Max(0.32f, controlledCamera.nearClipPlane + 0.18f);
            float nearClearanceSquared = nearClearance * nearClearance;

            for (int index = 0; index < count; index++)
            {
                EarthArmorPiece piece = _armorPieces[index];
                Renderer renderer = piece != null ? piece.VisualRenderer : null;
                if (piece == null || renderer == null || !piece.gameObject.activeInHierarchy)
                    continue;

                Bounds bounds = renderer.bounds;
                float plateRadius = Mathf.Clamp(bounds.extents.magnitude * 0.54f, 0.08f, 0.72f);
                bool wasSuppressed = piece.CameraSuppressed;
                bool suppress = bounds.SqrDistance(cameraPosition) <= nearClearanceSquared ||
                    EarthCameraArmorVisibilitySolver.ShouldSuppress(
                        cameraPosition, chestFocus, bounds.center, plateRadius,
                        ChestSightlineRadius, wasSuppressed) ||
                    EarthCameraArmorVisibilitySolver.ShouldSuppress(
                        cameraPosition, headFocus, bounds.center, plateRadius,
                        HeadSightlineRadius, wasSuppressed);
                piece.SetCameraSuppressed(suppress);
                if (suppress) HiddenArmorPieceCount++;
            }
        }

        private void ClearArmorVisibility()
        {
            HiddenArmorPieceCount = 0;
            if (armor == null) return;
            int count = armor.CopyActivePiecesNonAlloc(_armorPieces);
            for (int index = 0; index < count; index++)
                _armorPieces[index]?.SetCameraSuppressed(false);
        }

        private void ApplyStateComposition()
        {
            EarthCameraState state = director != null ? director.State : EarthCameraState.Explore;
            EarthCameraStateProfile stateProfile = EarthCameraStateProfile.Default(state);
            if (director != null && director.Profile != null)
                director.Profile.TryGet(state, out stateProfile);
            bool armorActive = armor != null && armor.IsActive;
            float desiredDistance = EarthCameraArmorVisibilitySolver.ResolveCameraDistance(
                stateProfile.Distance,
                armorActive,
                armorActive ? armor.Phase01 : 0f);
            thirdPersonFollow.CameraDistance = Mathf.Lerp(
                thirdPersonFollow.CameraDistance,
                desiredDistance,
                1f - Mathf.Exp(-(armorActive ? 12f : 8f) * Time.unscaledDeltaTime));
            float arm = Mathf.Clamp(stateProfile.Height - 3.05f, minimumArmLength, maximumArmLength);
            thirdPersonFollow.VerticalArmLength = Mathf.Lerp(
                thirdPersonFollow.VerticalArmLength,
                arm,
                1f - Mathf.Exp(-7f * Time.unscaledDeltaTime));
            Vector3 shoulder = thirdPersonFollow.ShoulderOffset;
            shoulder.x = Mathf.Lerp(shoulder.x, Mathf.Abs(stateProfile.ShoulderOffset),
                1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
            shoulder.y = shoulderHeight + (armorActive ? 0.10f : 0f);
            shoulder.z = 0f;
            thirdPersonFollow.ShoulderOffset = shoulder;
            thirdPersonFollow.CameraSide = director != null ? director.ShoulderSign : 1f;
            thirdPersonFollow.Damping = new Vector3(
                Mathf.Clamp(stateProfile.PositionDamping * 1.25f, 0.06f, 0.22f),
                Mathf.Clamp(stateProfile.PositionDamping * 1.55f, 0.08f, 0.28f),
                Mathf.Clamp(stateProfile.PositionDamping, 0.05f, 0.2f));
            if (virtualCamera != null)
            {
                LensSettings lens = virtualCamera.Lens;
                lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, stateProfile.FieldOfView,
                    1f - Mathf.Exp(-7f * Time.unscaledDeltaTime));
                virtualCamera.Lens = lens;
            }
        }

        private float ResolveTargetPitch()
        {
            float verticalBias = director != null ? director.LastPointerIntent.VerticalBias : 0f;
            float influence = director != null ? director.LastPointerInfluence : 0f;
            EarthCameraState state = director != null ? director.State : EarthCameraState.Explore;
            float stateOffset = state switch
            {
                EarthCameraState.DrawStructure => 3f,
                EarthCameraState.Airborne => 2f,
                EarthCameraState.HoldMass => 1.5f,
                _ => 0f
            };
            float pointerOffset = -verticalBias * pointerPitchRange * Mathf.Clamp01(influence);
            return Mathf.Clamp(neutralPitch + stateOffset + pointerOffset, 7f, 30f);
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
