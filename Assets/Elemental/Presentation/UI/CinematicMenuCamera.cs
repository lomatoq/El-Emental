using Elemental.Simulation.Rendering;
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
        private static readonly Unity.Profiling.ProfilerMarker DepartureMarker = new("Elemental.MenuCamera.Departure");
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
        [SerializeField, Range(30f, 55f)] private float countdownFieldOfView = 42f;
        private struct Framing
        {
            public Vector3 Position, Center, Up;
            public Quaternion Rotation;
            public LensSettings Lens;
            public float Height, Distance;
        }
        private Framing _departureStart, _departureTarget;
        private bool _departureCaptured;
        public bool DepartureComplete { get; private set; }
        public float DepartureProgress { get; private set; }
        public static float DepartureSeconds(bool reducedMotion) => reducedMotion ? .18f : .85f;
        private Quaternion _dollyStartRotation;
        private LensSettings _dollyStartLens;
        private float _countdownDollyProgress;
        private Vector3 _dollyCenter, _dollyStartDirection;
        private float _dollyStartDistance, _subjectHeight;
        private CameraState _dollyEndState;
        private bool _dollyEndCaptured;
        private CinemachineBrain.LensModeOverrideSettings _previousLensModeOverride;
        private LensSettings _previousOutputLens;
        private bool _countdownFraming;
        private bool _resultsFraming;private Transform _resultsVisualRoot;
        private Framing _resultsStart;
        public bool OwnsResultsStage=>_active&&_resultsFraming;
        public float CountdownFocalLength => UnityEngine.Camera.FieldOfViewToFocalLength(menuCamera.Lens.FieldOfView, 24f);
        private bool _active, _chargeWasEnabled;
        private PrioritySettings _previousMenuPriority;
        private LensSettings _previousMenuLens;
        private bool _previousMenuActive;
        private float _previousPresentationClock = 1f;
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
        private static readonly int CameraFadeId = Shader.PropertyToID("_MenuOcclusionFade");
        private static readonly Unity.Profiling.ProfilerMarker OcclusionMarker = new("Elemental.MenuCamera.OcclusionFade");
        private MaterialPropertyBlock _cameraFadeBlock;
        private bool[] _smoothOccluder = System.Array.Empty<bool>();
        private float[] _cameraFade = System.Array.Empty<float>(), _cameraFadeTarget = System.Array.Empty<float>();
        private bool _cameraFadePending;
        public bool OwnsPresentation => _active;
        public Vector3 SubjectCenter { get; private set; }
        public void Configure(CinemachineCamera camera, CinemachineBrain cameraBrain, UnityEngine.Camera output,
            EarthChargeCameraLookdevV2 charge, EarthCinematicDepthOfFieldController dof,
            EarthAnimationDriver driver, PlanetMotor actorMotor, Transform subject, Transform opponent = null, CinemachineCamera gameplay = null, EarthCinemachineCameraController controller = null)
        { menuCamera = camera; brain = cameraBrain; outputCamera = output; chargeLook = charge; depthOfField = dof; animationDriver = driver; motor = actorMotor; actor = subject; countdownOpponent = opponent; gameplayCamera = gameplay; gameplayController = controller; _opponentMotor = opponent != null ? opponent.GetComponent<PlanetMotor>() : null; }
        public void Enter(bool reducedMotion, float transitionSeconds)
        {
            if (menuCamera == null || actor == null) return;
            _cameraFadeBlock ??= new MaterialPropertyBlock();
            _returning = false; _resultsFraming=false; _resultsVisualRoot=null;
            _countdownFraming = false; _departureCaptured = false; DepartureComplete = false;
            if (_active)
            {
                SetPresentationClock();
                if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, reducedMotion ? 0 : transitionSeconds);
                menuCamera.Priority = 1000; menuCamera.gameObject.SetActive(true);
                depthOfField?.SetPresentationWeight(1); Reframe(reducedMotion);
                animationDriver?.SetPresentationClockMultiplier(.45f); return;
            }
            _previousMenuPriority = menuCamera.Priority; _previousMenuLens = menuCamera.Lens;
            _previousMenuActive = menuCamera.gameObject.activeSelf;
            _previousPresentationClock = animationDriver != null ? animationDriver.PresentationClockMultiplier : 1f;
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
            RestoreCameraFadeImmediate();
            _sceneRenderers = renderers.ToArray();
            _smoothOccluder = new bool[_sceneRenderers.Length];
            _cameraFade = new float[_sceneRenderers.Length]; _cameraFadeTarget = new float[_sceneRenderers.Length];
            for (int i = 0; i < _sceneRenderers.Length; i++)
            {
                var renderer = _sceneRenderers[i];
                var material = renderer != null ? renderer.sharedMaterial : null;
                _smoothOccluder[i] = material != null && material.HasProperty(CameraFadeId);
                _cameraFade[i] = _cameraFadeTarget[i] = 1;
            }
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
            if (_departureCaptured || _resultsFraming) return;
            Framing frame = ComputeFraming(_countdownFraming, reducedMotion);
            ApplyFraming(in frame, reducedMotion);
            _width = Screen.width; _height = Screen.height;
            _framedActorPosition = actor.position; _framedActorRotation = actor.rotation; _hasFramedActor = true;
            menuCamera.PreviousStateIsValid = false;
        }
        private Framing ComputeFraming(bool countdown, bool reducedMotion)
        {
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
            float framingFov = countdown ? countdownFieldOfView : fieldOfView;
            float distance = height / (2f * Mathf.Tan(framingFov * Mathf.Deg2Rad * .5f) * (countdown ? Mathf.Min(countdownCharacterScreenHeight, .4f) : characterScreenHeight));
            Vector3 center = feet + up * height * .5f;
            Vector3 separation = Vector3.zero;
            if (countdown && countdownOpponent != null)
            {
                if (_opponentMotor == null) _opponentMotor = countdownOpponent.GetComponent<PlanetMotor>();
                Vector3 otherFeet = _opponentMotor != null ? _opponentMotor.SupportFeetPoint(up) : countdownOpponent.position;
                separation = otherFeet - feet;
                center += separation * .5f;
                Vector3 duelAxis = Vector3.ProjectOnPlane(separation, up);
                if (duelAxis.sqrMagnitude > .01f) facing = duelAxis.normalized;
            }
            Vector3 requestedFacing = Quaternion.AngleAxis(countdown ? countdownAzimuth : presentationAzimuth, up) * facing;
            facing = requestedFacing;
            Vector3 right = Vector3.Cross(up, -facing).normalized;
            float aspect = outputCamera != null ? outputCamera.aspect : 16f / 9f;
            if (countdown)
            {
                float halfWidth = Mathf.Abs(Vector3.Dot(separation, right)) * .5f + height * .3f;
                float depthMargin = Mathf.Abs(Vector3.Dot(separation, facing)) * .5f;
                distance = Mathf.Max(distance, halfWidth / (Mathf.Tan(framingFov * Mathf.Deg2Rad * .5f) * aspect * .8f) + depthMargin);
            }
            Vector3 position = center + facing * distance;
            if (countdown) position = center + (facing * Mathf.Cos(countdownElevation * Mathf.Deg2Rad) + up * Mathf.Sin(countdownElevation * Mathf.Deg2Rad)) * distance;
            Vector3 target = center - right * distance * Mathf.Tan(framingFov * Mathf.Deg2Rad * .5f) * aspect * .40f;
            if (countdown) target = center;
            Quaternion rotation = countdown ? Quaternion.LookRotation(target - position, up) :
                PortraitRotation(position, center, up, framingFov, aspect, portraitViewport, reducedMotion ? 0 : dutchAngle);
            var lens = menuCamera.Lens;
            lens.ModeOverride = LensSettings.OverrideModes.Perspective;
            lens.FieldOfView = framingFov; lens.Dutch = reducedMotion || countdown ? 0 : dutchAngle;
            lens.PhysicalProperties.FocusDistance = distance;
            return new Framing { Position = position, Rotation = rotation, Lens = lens,
                Center = center, Up = up, Height = height, Distance = distance };
        }
        private void ApplyFraming(in Framing frame, bool reducedMotion)
        {
            menuCamera.transform.SetPositionAndRotation(frame.Position, frame.Rotation);
            menuCamera.Lens = frame.Lens; SubjectCenter = frame.Center; _subjectHeight = frame.Height;
            SuppressForegroundOccluders(frame.Position, frame.Center, frame.Up, frame.Height);
            depthOfField?.ApplyPolicy(!reducedMotion, frame.Distance, 5.6f, 50f);
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
            for (int i = 0; i < _cameraFadeTarget.Length; i++) _cameraFadeTarget[i] = 1;
            _cameraFadePending = true;
            Vector3 right = Vector3.Cross(up, center - position).normalized;
            for (int i = 0; i < _sceneRenderers.Length; i++)
            {
                var renderer = _sceneRenderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.transform.IsChildOf(actor) ||
                    (_resultsVisualRoot!=null&&renderer.transform.IsChildOf(_resultsVisualRoot)) ||
                    (_countdownFraming && countdownOpponent != null && renderer.transform.IsChildOf(countdownOpponent)) ||
                    renderer is not (MeshRenderer or SkinnedMeshRenderer)) continue;
                Bounds bounds = renderer.bounds;
                // A floor's broad AABB can intersect the portrait's lowest rays
                // even when the actual surface stays below the feet. Hiding that
                // whole mesh creates a hole in the result screen.
                if(IsBelowPortraitFeet(bounds,center,up,height))continue;
                if (Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) > height * 5f) continue;
                // Start fading before the lens enters the mesh bounds. Keep the
                // authored near plane; otherwise clipped triangles pop into view.
                float cameraClearance = Mathf.Max(1.25f, (outputCamera != null ? outputCamera.nearClipPlane : .1f) + .6f);
                bool occludes = _smoothOccluder[i] && (bounds.ClosestPoint(position) - position).sqrMagnitude < cameraClearance * cameraClearance;
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
                if (_smoothOccluder[i])
                {
                    _cameraFadeTarget[i] = occludes ? 0 : 1;
                    // Restore before the rendered gameplay handoff, not in one
                    // frame when the cinematic camera relinquishes ownership.
                    if (_countdownFraming && DepartureComplete)
                        _cameraFadeTarget[i] = Mathf.Max(_cameraFadeTarget[i], Mathf.InverseLerp(.72f, 1f, _countdownDollyProgress));
                }
                else if (occludes && !renderer.forceRenderingOff) { renderer.forceRenderingOff = true; _suppressed[i] = true; }
            }
        }
        private void LateUpdate()
        {
            if (!_cameraFadePending) return;
            using var marker = OcclusionMarker.Auto();
            bool pending = false;
            for (int i = 0; i < _sceneRenderers.Length; i++)
            {
                var renderer = _sceneRenderers[i];
                if (!_smoothOccluder[i] || renderer == null) continue;
                float target = _active ? _cameraFadeTarget[i] : 1;
                float next = MenuOcclusionFade.Step(_cameraFade[i], target, Time.unscaledDeltaTime);
                if (next != _cameraFade[i])
                {
                    // Preserve heat, stone tint and destruction's independent fade.
                    renderer.GetPropertyBlock(_cameraFadeBlock);
                    _cameraFadeBlock.SetFloat(CameraFadeId, next);
                    renderer.SetPropertyBlock(_cameraFadeBlock);
                    _cameraFade[i] = next;
                }
                pending |= next != target;
            }
            _cameraFadePending = pending;
        }
        private void RestoreCameraFadeImmediate()
        {
            if (_cameraFadeBlock == null) return;
            for (int i = 0; i < _sceneRenderers.Length; i++)
            {
                if (i >= _smoothOccluder.Length || !_smoothOccluder[i] || _sceneRenderers[i] == null) continue;
                _sceneRenderers[i].GetPropertyBlock(_cameraFadeBlock);
                _cameraFadeBlock.SetFloat(CameraFadeId, 1);
                _sceneRenderers[i].SetPropertyBlock(_cameraFadeBlock);
            }
            _cameraFadePending = false;
        }
        public static bool IsBelowPortraitFeet(Bounds bounds,Vector3 center,Vector3 up,float height)
        {
            up=up.sqrMagnitude>.001f?up.normalized:Vector3.up;
            float highest=Vector3.Dot(bounds.center,up)+Vector3.Dot(bounds.extents,new Vector3(Mathf.Abs(up.x),Mathf.Abs(up.y),Mathf.Abs(up.z)));
            return highest<=Vector3.Dot(center,up)-Mathf.Max(.1f,height)*.5f+.20f;
        }
        public void CaptureDepartureStart()
        {
            if (!_active || menuCamera == null || actor == null) return;
            // Final output already includes Cinemachine roll; clear lens Dutch to avoid applying it twice.
            LensSettings lens = outputCamera != null ? LensSettings.FromCamera(outputCamera) : menuCamera.Lens;
            Quaternion rotation = outputCamera != null ? outputCamera.transform.rotation : menuCamera.State.GetFinalOrientation();
            lens.Dutch = 0;
            _departureStart = new Framing {
                Position = outputCamera != null ? outputCamera.transform.position : menuCamera.State.GetFinalPosition(),
                Rotation = rotation, Lens = lens, Center = SubjectCenter,
                Up = motor != null ? motor.LocalUp : actor.up, Height = _subjectHeight
            };
            _departureStart.Distance = Vector3.Distance(_departureStart.Position, SubjectCenter);
            _departureCaptured = true; DepartureComplete = false; DepartureProgress = 0;
            // Reserve the rendered camera without changing the current DOF/accessibility policy.
            menuCamera.transform.SetPositionAndRotation(_departureStart.Position, _departureStart.Rotation);
            menuCamera.Lens = _departureStart.Lens;
        }
        public void BeginCountdown(bool reducedMotion, float blendSeconds)
        {
            if (!_departureCaptured) CaptureDepartureStart();
            _countdownFraming = true; _countdownDollyProgress = 0f;
            _departureTarget = ComputeFraming(true, reducedMotion);
            if (brain != null) brain.LensModeOverride.Enabled = true;
            _dollyEndCaptured = false;
            SetDepartureProgress(0, reducedMotion);
        }
        public void SetDepartureProgress(float progress, bool reducedMotion)
        {
            if (!_active || !_departureCaptured || !_countdownFraming) return;
            using var marker = DepartureMarker.Auto();
            DepartureProgress = Mathf.Clamp01(progress);
            float t = Mathf.SmoothStep(0, 1, DepartureProgress);
            Framing frame = _departureTarget;
            frame.Position = Vector3.Lerp(_departureStart.Position, _departureTarget.Position, t);
            frame.Rotation = Quaternion.Slerp(_departureStart.Rotation, _departureTarget.Rotation, t);
            frame.Lens = LensSettings.Lerp(_departureStart.Lens, _departureTarget.Lens, t);
            frame.Center = Vector3.Lerp(_departureStart.Center, _departureTarget.Center, t);
            frame.Distance = Vector3.Distance(frame.Position, frame.Center);
            ApplyFraming(in frame, reducedMotion);
            DepartureComplete = DepartureProgress >= 1;
            if (!DepartureComplete) return;
            _dollyCenter = _departureTarget.Center;
            Vector3 offset = _departureTarget.Position - _dollyCenter;
            _dollyStartDistance = offset.magnitude; _dollyStartDirection = offset.normalized;
            _dollyStartRotation = _departureTarget.Rotation; _dollyStartLens = _departureTarget.Lens;
        }
        public void SetCountdownDollyProgress(float progress, float alignmentProgress, bool reducedMotion)
        {
            if (!_active || !_countdownFraming || !DepartureComplete || menuCamera.Priority < 0) return;
            _countdownDollyProgress = Mathf.Clamp01(progress);
            if (gameplayCamera == null) return;
            if (!_dollyEndCaptured)
            {
                gameplayController?.SnapToTarget();
                gameplayCamera.PreviousStateIsValid = false;
                gameplayCamera.UpdateCameraState(motor != null ? motor.LocalUp : actor.up, -1f);
                _dollyEndState = gameplayCamera.State; _dollyEndCaptured = true;
            }
            Vector3 endOffset = _dollyEndState.GetFinalPosition() - _dollyCenter;
            float t = Mathf.SmoothStep(0f, 1f, _countdownDollyProgress);
            float align = Mathf.SmoothStep(0f, 1f, alignmentProgress);
            float distance = Mathf.Lerp(_dollyStartDistance, endOffset.magnitude, t);
            Vector3 direction = Vector3.Slerp(_dollyStartDirection, endOffset.normalized, align).normalized;
            Vector3 position = _dollyCenter + direction * distance;
            Vector3 up = motor != null ? motor.LocalUp : actor.up;
            Quaternion endRotation = _dollyEndState.GetFinalOrientation();
            Quaternion rotation = Quaternion.Slerp(_dollyStartRotation, endRotation, align);
            menuCamera.transform.SetPositionAndRotation(position, rotation);
            SuppressForegroundOccluders(position, _dollyCenter, up, _subjectHeight);
            var lens = LensSettings.Lerp(_dollyStartLens, _dollyEndState.Lens, t);
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
        public void BeginResultsStage(Transform visualRoot,Transform focusSubject,bool reducedMotion)
        {
            var start=new Framing{Position=outputCamera!=null?outputCamera.transform.position:menuCamera.transform.position,
                Rotation=outputCamera!=null?outputCamera.transform.rotation:menuCamera.transform.rotation,
                Lens=outputCamera!=null?LensSettings.FromCamera(outputCamera):menuCamera.Lens};
            Enter(reducedMotion,0);_resultsStart=start;_resultsFraming=true;_resultsVisualRoot=visualRoot;
            menuCamera.transform.SetPositionAndRotation(start.Position,start.Rotation);menuCamera.Lens=start.Lens;
            _departureCaptured=false;
            if(depthOfField!=null)depthOfField.ConfigureSubjects(focusSubject,focusSubject);
        }
        public void SetResultsStageFrame(Vector3 center,Vector3 up,Vector3 facing,float height,float progress,bool reducedMotion)
        {
            if(!OwnsResultsStage)return;
            const float fov=38;float distance=height/(2*Mathf.Tan(fov*Mathf.Deg2Rad*.5f)*.65f);
            Vector3 position=center+facing.normalized*distance+up*.08f;
            float aspect=outputCamera!=null?outputCamera.aspect:16f/9;
            Quaternion rotation=PortraitRotation(position,center,up,fov,aspect,new Vector2(.27f,.55f),0);
            LensSettings lens=_resultsStart.Lens;lens.ModeOverride=LensSettings.OverrideModes.Perspective;
            lens.FieldOfView=fov;lens.Dutch=0;lens.PhysicalProperties.FocusDistance=distance;
            float t=Mathf.Clamp01(progress);
            var frame=new Framing{Position=Vector3.Lerp(_resultsStart.Position,position,t),Rotation=Quaternion.Slerp(_resultsStart.Rotation,rotation,t),
                Lens=LensSettings.Lerp(_resultsStart.Lens,lens,t),Center=center,Up=up,Height=height,Distance=distance};
            ApplyFraming(frame,reducedMotion);
        }
        public void EndResultsStage(){if(OwnsResultsStage)FinishCombatTransition();}
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
            _active = false; _resultsFraming=false; _resultsVisualRoot=null; _returning = false; _departureCaptured = false; DepartureComplete = false; animationDriver?.SetPresentationClockMultiplier(_previousPresentationClock);
            RestoreOccluders(); _cameraFadePending = true;
            if (menuCamera != null) { menuCamera.Priority = _previousMenuPriority; menuCamera.Lens = _previousMenuLens; menuCamera.gameObject.SetActive(_previousMenuActive); }
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
        public bool NeedsReframe => _active && !_resultsFraming && !_returning && !_countdownFraming && !_departureCaptured && actor != null &&
            (!_hasFramedActor || _width != Screen.width || _height != Screen.height ||
             (actor.position - _framedActorPosition).sqrMagnitude > .0004f || Quaternion.Angle(actor.rotation, _framedActorRotation) > .5f);
        private void OnDisable() { FinishCombatTransition(); RestoreCameraFadeImmediate(); }
    }
}
