using Elemental.Presentation.Animation;
using Elemental.Presentation.Camera;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using Unity.Cinemachine;
using UnityEngine;

namespace Elemental.Presentation.UI
{
    [DisallowMultipleComponent]
    public sealed class CinematicMenuCamera : MonoBehaviour
    {
        private static readonly Unity.Profiling.ProfilerMarker ReframeMarker = new("Elemental.MenuCamera.Reframe");
        [SerializeField] private CinemachineCamera menuCamera;
        [SerializeField] private CinemachineBrain brain;
        [SerializeField] private CinemachineCamera gameplayCamera;
        [SerializeField] private EarthCinemachineCameraController gameplayController;
        [SerializeField] private EarthChargeCameraLookdevV2 chargeLook;
        [SerializeField] private EarthCinematicDepthOfFieldController depthOfField;
        [SerializeField] private EarthAnimationDriver animationDriver;
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private Transform actor;
        [SerializeField] private Transform countdownOpponent;
        private PlanetMotor _opponentMotor;
        [SerializeField] private UnityEngine.Camera outputCamera;
        [Header("Menu framing")]
        [SerializeField, Range(-14, 14), Tooltip("Camera roll in degrees.")] private float dutchAngle = 10;
        [SerializeField, Range(30, 55)] private float fieldOfView = 38;
        [SerializeField, Range(.45f, .65f)] private float characterScreenHeight = .55f;
        [SerializeField, Range(-75, 75)] private float presentationAzimuth = 35f;
        [SerializeField, Tooltip("Character framing centre in normalized screen coordinates; used by Main and Pause.")]
        private Vector2 portraitViewport = new Vector2(.78f, .58f);
        [Header("Countdown framing")]
        [SerializeField, Range(45, 120)] private float countdownAzimuth = 85f;
        [SerializeField, Range(5, 35)] private float countdownElevation = 18f;
        [SerializeField, Range(.3f, .65f)] private float countdownCharacterScreenHeight = .48f;
        [SerializeField, Range(50f, 250f), Tooltip("Full-frame starting focal length, millimetres.")] private float countdownStartFocalLength = 150f;
        private float _countdownDollyProgress;
        private Vector3 _dollyCenter, _dollyStartDirection;
        private float _dollyStartDistance, _subjectHeight;
        private CameraState _dollyEndState;
        private bool _dollyEndCaptured;
        private CinemachineBrain.LensModeOverrideSettings _previousLensModeOverride;
        private LensSettings _previousOutputLens;
        private bool _countdownFraming;
        public float CountdownFocalLength => UnityEngine.Camera.FieldOfViewToFocalLength(menuCamera.Lens.FieldOfView, 24f);
        private bool _active, _chargeWasEnabled;
        private bool _previousIgnoreTimeScale, _returning;
        private CinemachineBrain.UpdateMethods _previousUpdateMethod;
        private CinemachineBrain.BrainUpdateMethods _previousBlendUpdateMethod;
        private float _returnStartedAt, _returnSeconds;
        private int _returnStartedFrame;
        private Vector3 _framedActorPosition;
        private Quaternion _framedActorRotation;
        private bool _hasFramedActor;
        private SkinnedMeshRenderer[] _actorRenderers = System.Array.Empty<SkinnedMeshRenderer>();
        private Transform _primary, _secondary;
        private CinemachineBlendDefinition _blend;
        private float _previousDofWeight = 1f;
        private int _width, _height;
        private Renderer[] _sceneRenderers = System.Array.Empty<Renderer>();
        private bool[] _rendererWasHidden = System.Array.Empty<bool>();
        private bool[] _suppressed = System.Array.Empty<bool>();
        public bool OwnsPresentation => _active;
        public Vector3 SubjectCenter { get; private set; }
        public void Configure(CinemachineCamera camera, CinemachineBrain cameraBrain, UnityEngine.Camera output,
            EarthChargeCameraLookdevV2 charge, EarthCinematicDepthOfFieldController dof,
            EarthAnimationDriver driver, PlanetMotor actorMotor, Transform subject, Transform opponent = null, CinemachineCamera gameplay = null, EarthCinemachineCameraController controller = null)
        { menuCamera = camera; brain = cameraBrain; outputCamera = output; chargeLook = charge; depthOfField = dof; animationDriver = driver; motor = actorMotor; actor = subject; countdownOpponent = opponent; gameplayCamera = gameplay; gameplayController = controller; _opponentMotor = opponent != null ? opponent.GetComponent<PlanetMotor>() : null; }
        public void Enter(bool reducedMotion, float transitionSeconds)
        {
            if (menuCamera == null || actor == null) return;
            _returning = false;
            _countdownFraming = false;
            if (_active)
            {
                SetPresentationClock();
                if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, reducedMotion ? 0 : transitionSeconds);
                menuCamera.Priority = 1000; menuCamera.gameObject.SetActive(true);
                depthOfField?.SetPresentationWeight(1); Reframe(reducedMotion);
                animationDriver?.SetPresentationClockMultiplier(.45f); return;
            }
            _active = true;
            if (brain != null)
            {
                _previousLensModeOverride = brain.LensModeOverride;
                _previousIgnoreTimeScale = brain.IgnoreTimeScale;
                _previousUpdateMethod = brain.UpdateMethod;
                _previousBlendUpdateMethod = brain.BlendUpdateMethod;
            }
            SetPresentationClock();
            var activeAnimator = animationDriver != null ? animationDriver.Animator : null;
            _actorRenderers = activeAnimator != null ? activeAnimator.GetComponentsInChildren<SkinnedMeshRenderer>(true) : System.Array.Empty<SkinnedMeshRenderer>();
            if (outputCamera != null) _previousOutputLens = LensSettings.FromCamera(outputCamera);
            var renderers = new System.Collections.Generic.List<Renderer>();
            foreach (var root in gameObject.scene.GetRootGameObjects()) renderers.AddRange(root.GetComponentsInChildren<Renderer>(true));
            _sceneRenderers = renderers.ToArray();
            _rendererWasHidden = new bool[_sceneRenderers.Length]; _suppressed = new bool[_sceneRenderers.Length];
            for (int i = 0; i < _sceneRenderers.Length; i++) _rendererWasHidden[i] = _sceneRenderers[i] != null && _sceneRenderers[i].forceRenderingOff;
            if (brain != null) { _blend = brain.DefaultBlend; brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, reducedMotion ? 0 : transitionSeconds); }
            // The lookdev bootstrap may add this component after scene authoring.
            if (chargeLook == null && outputCamera != null) chargeLook = outputCamera.GetComponent<EarthChargeCameraLookdevV2>();
            _chargeWasEnabled = chargeLook != null && chargeLook.enabled; if (chargeLook != null) chargeLook.enabled = false;
            if (depthOfField != null)
            {
                _primary = depthOfField.PrimarySubject; _secondary = depthOfField.SecondarySubject;
                _previousDofWeight = depthOfField.PresentationWeight;
                depthOfField.SetPresentationWeight(1); depthOfField.ConfigureSubjects(actor, actor);
            }
            menuCamera.Priority = 1000; menuCamera.gameObject.SetActive(true);
            Reframe(reducedMotion); animationDriver?.SetPresentationClockMultiplier(.45f);
        }
        public void Reframe(bool reducedMotion)
        {
            if (!_active || actor == null || menuCamera == null) return;
            using var marker = ReframeMarker.Auto();
            Vector3 up = motor != null && motor.LocalUp.sqrMagnitude > .5f ? motor.LocalUp.normalized : actor.up;
            Vector3 facing = Vector3.ProjectOnPlane(motor != null ? motor.FacingForward : actor.forward, up).normalized;
            if (facing.sqrMagnitude < .1f) facing = Vector3.ProjectOnPlane(actor.forward, up).normalized;
            Vector3 feet = motor != null ? motor.SupportFeetPoint(up) : actor.position;
            var animator = animationDriver != null ? animationDriver.Animator : null;
            Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            float height = head != null ? Mathf.Clamp(Vector3.Dot(head.position - feet, up) + .22f, 1.4f, 2.5f) : 1.85f;
            // The stylized helmet/plume extends well above the Humanoid head bone.
            // Frame the actual rendered silhouette, including those accessories.
            if (animator != null)
            {
                foreach (var renderer in _actorRenderers)
                {
                    if (renderer == null) continue;
                    Bounds bounds = renderer.bounds;
                    float projectedHalfHeight = Mathf.Abs(up.x) * bounds.extents.x + Mathf.Abs(up.y) * bounds.extents.y + Mathf.Abs(up.z) * bounds.extents.z;
                    height = Mathf.Max(height, Vector3.Dot(bounds.center - feet, up) + projectedHalfHeight);
                }
            }
            _subjectHeight = height;
            float framingFov = fieldOfView;
            if (_countdownFraming)
            {
                float normalFocalLength = UnityEngine.Camera.FieldOfViewToFocalLength(fieldOfView, 24f);
                float t = reducedMotion ? 1f : Mathf.SmoothStep(0f, 1f, _countdownDollyProgress);
                framingFov = UnityEngine.Camera.FocalLengthToFieldOfView(Mathf.Lerp(countdownStartFocalLength, normalFocalLength, t), 24f);
            }
            float distance = height / (2f * Mathf.Tan(framingFov * Mathf.Deg2Rad * .5f) * (_countdownFraming ? countdownCharacterScreenHeight : characterScreenHeight));
            Vector3 center = feet + up * height * .5f;
            Vector3 separation = Vector3.zero;
            if (_countdownFraming && countdownOpponent != null)
            {
                if (_opponentMotor == null) _opponentMotor = countdownOpponent.GetComponent<PlanetMotor>();
                Vector3 otherFeet = _opponentMotor != null ? _opponentMotor.SupportFeetPoint(up) : countdownOpponent.position;
                separation = otherFeet - feet;
                center += separation * .5f;
                Vector3 duelAxis = Vector3.ProjectOnPlane(separation, up);
                if (duelAxis.sqrMagnitude > .01f) facing = duelAxis.normalized;
            }
            SubjectCenter = center;
            Vector3 requestedFacing = Quaternion.AngleAxis(_countdownFraming ? countdownAzimuth : presentationAzimuth, up) * facing;
            facing = requestedFacing;
            Vector3 right = Vector3.Cross(up, -facing).normalized;
            float aspect = outputCamera != null ? outputCamera.aspect : 16f / 9f;
            if (_countdownFraming)
            {
                float halfWidth = Mathf.Abs(Vector3.Dot(separation, right)) * .5f + height * .3f;
                float depthMargin = Mathf.Abs(Vector3.Dot(separation, facing)) * .5f;
                distance = Mathf.Max(distance, halfWidth / (Mathf.Tan(framingFov * Mathf.Deg2Rad * .5f) * aspect * .8f) + depthMargin);
            }
            Vector3 position = center + facing * distance;
            if (_countdownFraming) position = center + (facing * Mathf.Cos(countdownElevation * Mathf.Deg2Rad) + up * Mathf.Sin(countdownElevation * Mathf.Deg2Rad)) * distance;
            Vector3 target = center - right * distance * Mathf.Tan(framingFov * Mathf.Deg2Rad * .5f) * aspect * .40f;
            if (_countdownFraming) target = center;
            Quaternion rotation = _countdownFraming ? Quaternion.LookRotation(target - position, up) :
                PortraitRotation(position, center, up, framingFov, aspect, portraitViewport, reducedMotion ? 0 : dutchAngle);
            menuCamera.transform.SetPositionAndRotation(position, rotation);
            SuppressForegroundOccluders(position, center, up, height);
            var lens = menuCamera.Lens;
            lens.ModeOverride = _countdownFraming ? LensSettings.OverrideModes.Physical : LensSettings.OverrideModes.Perspective;
            if (_countdownFraming)
            {
                lens.PhysicalProperties = _previousOutputLens.PhysicalProperties;
                lens.PhysicalProperties.SensorSize = new Vector2(36f, 24f);
                lens.PhysicalProperties.GateFit = UnityEngine.Camera.GateFitMode.Vertical;
                lens.PhysicalProperties.LensShift = Vector2.zero;
                lens.PhysicalProperties.FocusDistance = distance;
            }
            lens.FieldOfView = framingFov; lens.Dutch = reducedMotion || _countdownFraming ? 0 : dutchAngle; menuCamera.Lens = lens;
            _width = Screen.width; _height = Screen.height;
            _framedActorPosition = actor.position; _framedActorRotation = actor.rotation; _hasFramedActor = true;
            menuCamera.PreviousStateIsValid = false;
            depthOfField?.ApplyPolicy(!reducedMotion, distance, 5.6f, 50f);
        }
        public static Quaternion PortraitRotation(Vector3 position, Vector3 subject, Vector3 up,
            float verticalFov, float aspect, Vector2 viewport, float dutch)
        {
            float tangent = Mathf.Tan(verticalFov * Mathf.Deg2Rad * .5f);
            Vector3 screenRay = new Vector3((viewport.x * 2 - 1) * tangent * aspect,
                (viewport.y * 2 - 1) * tangent, 1);
            // Compensate the final Cinemachine roll so the requested on-screen
            // position stays the same in normal and Reduced Motion presentation.
            screenRay = Quaternion.AngleAxis(dutch, Vector3.forward) * screenRay;
            return Quaternion.LookRotation(subject - position, up) *
                Quaternion.FromToRotation(screenRay.normalized, Vector3.forward);
        }
        private void RestoreOccluders()
        {
            for (int i = 0; i < _sceneRenderers.Length; i++)
                if (_suppressed[i]) { if (_sceneRenderers[i] != null) _sceneRenderers[i].forceRenderingOff = _rendererWasHidden[i]; _suppressed[i] = false; }
        }
        private void SuppressForegroundOccluders(Vector3 position, Vector3 center, Vector3 up, float height)
        {
            RestoreOccluders();
            Vector3 right = Vector3.Cross(up, center - position).normalized;
            for (int i = 0; i < _sceneRenderers.Length; i++)
            {
                var renderer = _sceneRenderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.transform.IsChildOf(actor) ||
                    (_countdownFraming && countdownOpponent != null && renderer.transform.IsChildOf(countdownOpponent)) ||
                    renderer is not (MeshRenderer or SkinnedMeshRenderer)) continue;
                Bounds bounds = renderer.bounds;
                if (Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) > height * 5f) continue;
                bool occludes = false;
                int subjects = _countdownFraming && countdownOpponent != null ? 3 : 1;
                for (int subject = 0; subject < subjects && !occludes; subject++)
                for (int row = -1; row <= 1 && !occludes; row++)
                for (int column = -1; column <= 1 && !occludes; column++)
                {
                    Vector3 sampleCenter = center;
                    if (subject == 1) sampleCenter = (motor != null ? motor.SupportFeetPoint(up) : actor.position) + up * height * .5f;
                    if (subject == 2) sampleCenter = (_opponentMotor != null ? _opponentMotor.SupportFeetPoint(up) : countdownOpponent.position) + up * height * .5f;
                    Vector3 sample = sampleCenter + up * (row * height * .42f) + right * (column * height * .18f);
                    Vector3 ray = sample - position;
                    occludes = bounds.IntersectRay(new Ray(position, ray.normalized), out float hit) && hit < ray.magnitude - .2f;
                }
                if (occludes && !renderer.forceRenderingOff) { renderer.forceRenderingOff = true; _suppressed[i] = true; }
            }
        }
        public void BeginCountdown(bool reducedMotion, float blendSeconds)
        {
            _countdownFraming = true; _countdownDollyProgress = 0f;
            if (brain != null) brain.LensModeOverride.Enabled = true;
            if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, blendSeconds);
            Reframe(reducedMotion);
            _dollyCenter = SubjectCenter;
            Vector3 offset = menuCamera.transform.position - _dollyCenter;
            _dollyStartDistance = offset.magnitude; _dollyStartDirection = offset.normalized;
            _dollyEndCaptured = false;
        }
        public void SetCountdownDollyProgress(float progress, float alignmentProgress, bool reducedMotion)
        {
            if (!_active || !_countdownFraming || menuCamera.Priority < 0) return;
            _countdownDollyProgress = Mathf.Clamp01(progress);
            if (gameplayCamera == null) { Reframe(reducedMotion); return; }
            if (!_dollyEndCaptured)
            {
                gameplayController?.SnapToTarget();
                gameplayCamera.PreviousStateIsValid = false;
                gameplayCamera.UpdateCameraState(motor != null ? motor.LocalUp : actor.up, -1f);
                _dollyEndState = gameplayCamera.State; _dollyEndCaptured = true;
                _dollyStartDistance = Mathf.Max(_dollyStartDistance, Vector3.Distance(_dollyEndState.GetFinalPosition(), _dollyCenter) + 2f);
            }
            Vector3 endOffset = _dollyEndState.GetFinalPosition() - _dollyCenter;
            float t = reducedMotion ? 1f : _countdownDollyProgress;
            float align = reducedMotion ? 1f : Mathf.SmoothStep(0f, 1f, alignmentProgress);
            float distance = Mathf.Lerp(_dollyStartDistance, endOffset.magnitude, t);
            Vector3 direction = Vector3.Slerp(_dollyStartDirection, endOffset.normalized, align).normalized;
            Vector3 position = _dollyCenter + direction * distance;
            Vector3 up = motor != null ? motor.LocalUp : actor.up;
            Quaternion endRotation = _dollyEndState.GetFinalOrientation();
            Vector3 gameplayAim = _dollyEndState.GetFinalPosition() + endRotation * Vector3.forward * Mathf.Max(1f, endOffset.magnitude);
            Vector3 aim = Vector3.Lerp(_dollyCenter, gameplayAim, align);
            Quaternion rotation = Quaternion.LookRotation(aim - position, Vector3.Slerp(up, endRotation * Vector3.up, align));
            menuCamera.transform.SetPositionAndRotation(position, rotation);
            SuppressForegroundOccluders(position, _dollyCenter, up, _subjectHeight);
            float normalFocal = UnityEngine.Camera.FieldOfViewToFocalLength(_dollyEndState.Lens.FieldOfView, 24f);
            var lens = menuCamera.Lens;
            lens.FieldOfView = UnityEngine.Camera.FocalLengthToFieldOfView(Mathf.Lerp(countdownStartFocalLength, normalFocal, t), 24f);
            lens.PhysicalProperties.FocusDistance = distance; menuCamera.Lens = lens;
            depthOfField?.ApplyPolicy(!reducedMotion, distance, 5.6f, 50f);
        }
        public void BeginCombatTransition()
        { if (_active && menuCamera != null && (!_countdownFraming || gameplayCamera == null)) menuCamera.Priority = -1000; }
        private void SetPresentationClock()
        {
            if (brain == null) return;
            // Cinemachine 3's Smart/Fixed update can stop when the duel freezes.
            // Both virtual-camera evaluation and brain blending must use render time.
            brain.IgnoreTimeScale = true;
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
            brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
        }
        public void ReturnToGameplay(bool reducedMotion, float transitionSeconds)
        {
            if (!_active || menuCamera == null) return;
            _returning = true; _countdownFraming = false;
            _returnStartedAt = Time.unscaledTime; _returnStartedFrame = Time.frameCount;
            _returnSeconds = reducedMotion ? 0 : Mathf.Max(0, transitionSeconds);
            SetPresentationClock();
            if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, _returnSeconds);
            gameplayController?.SnapToTarget();
            menuCamera.Priority = -1000;
        }
        private void Update()
        {
            if (!_returning || !_active) return;
            float age = Time.unscaledTime - _returnStartedAt;
            SetTransitionProgress(_returnSeconds > 0 ? Mathf.Clamp01(age / _returnSeconds) : 1);
            // Let the brain see the priority change and render the completed blend
            // before returning its saved clock, lens and occlusion policies.
            if (Time.frameCount > _returnStartedFrame + 1 && age >= _returnSeconds && (brain == null || !brain.IsBlending))
                FinishCombatTransition();
        }
        public void SetTransitionProgress(float progress)
        {
            if (!_active) return;
            animationDriver?.SetPresentationClockMultiplier(Mathf.Lerp(.45f, 1f, progress));
            depthOfField?.SetPresentationWeight(1 - progress);
        }
        public void FinishCombatTransition()
        {
            if (!_active) return;
            _active = false; _returning = false; animationDriver?.SetPresentationClockMultiplier(1f);
            RestoreOccluders();
            if (menuCamera != null) { menuCamera.Priority = -1000; menuCamera.gameObject.SetActive(false); }
            if (brain != null)
            {
                brain.DefaultBlend = _blend; brain.LensModeOverride = _previousLensModeOverride;
                brain.IgnoreTimeScale = _previousIgnoreTimeScale;
                brain.UpdateMethod = _previousUpdateMethod; brain.BlendUpdateMethod = _previousBlendUpdateMethod;
            }
            if (outputCamera != null)
            {
                outputCamera.usePhysicalProperties = _previousOutputLens.IsPhysicalCamera;
                outputCamera.sensorSize = _previousOutputLens.PhysicalProperties.SensorSize;
                outputCamera.gateFit = _previousOutputLens.PhysicalProperties.GateFit;
                outputCamera.lensShift = _previousOutputLens.PhysicalProperties.LensShift;
                outputCamera.focusDistance = _previousOutputLens.PhysicalProperties.FocusDistance;
                if (_previousOutputLens.IsPhysicalCamera)
                    outputCamera.focalLength = UnityEngine.Camera.FieldOfViewToFocalLength(outputCamera.fieldOfView, outputCamera.sensorSize.y);
            }
            if (depthOfField != null)
            {
                depthOfField.ApplyPolicy(false, 8, 5.6f, 50);
                depthOfField.SetPresentationWeight(_previousDofWeight);
                depthOfField.ConfigureSubjects(_primary, _secondary);
            }
            if (chargeLook != null) chargeLook.enabled = _chargeWasEnabled;
        }
        public bool NeedsReframe => _active && !_returning && !_countdownFraming && actor != null &&
            (!_hasFramedActor || _width != Screen.width || _height != Screen.height ||
             (actor.position - _framedActorPosition).sqrMagnitude > .0004f || Quaternion.Angle(actor.rotation, _framedActorRotation) > .5f);
        private void OnDisable() => FinishCombatTransition();
    }
}
