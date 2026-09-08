from pathlib import Path
p=Path('Assets/Elemental/Presentation/UI/CinematicMenuCamera.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('        private bool _countdownFraming;','''        [SerializeField, Range(50f, 250f), Tooltip("Full-frame starting focal length, millimetres.")] private float countdownStartFocalLength = 150f;
        private float _countdownDollyProgress;
        private CinemachineBrain.LensModeOverrideSettings _previousLensModeOverride;
        private LensSettings _previousOutputLens;
        private bool _countdownFraming;
        public float CountdownFocalLength => UnityEngine.Camera.FieldOfViewToFocalLength(menuCamera.Lens.FieldOfView, 24f);''')
s=s.replace('            _active = true;','            _active = true;\n            if (brain != null) _previousLensModeOverride = brain.LensModeOverride;\n            if (outputCamera != null) _previousOutputLens = LensSettings.FromCamera(outputCamera);')
s=s.replace('            float distance = height / (2f * Mathf.Tan(fieldOfView * Mathf.Deg2Rad * .5f)', '''            float framingFov = fieldOfView;
            if (_countdownFraming)
            {
                float normalFocalLength = UnityEngine.Camera.FieldOfViewToFocalLength(fieldOfView, 24f);
                float t = reducedMotion ? 1f : Mathf.SmoothStep(0f, 1f, _countdownDollyProgress);
                framingFov = UnityEngine.Camera.FocalLengthToFieldOfView(Mathf.Lerp(countdownStartFocalLength, normalFocalLength, t), 24f);
            }
            float distance = height / (2f * Mathf.Tan(framingFov * Mathf.Deg2Rad * .5f)''')
s=s.replace('Mathf.Tan(fieldOfView * Mathf.Deg2Rad * .5f)', 'Mathf.Tan(framingFov * Mathf.Deg2Rad * .5f)')
s=s.replace('            var lens = menuCamera.Lens; lens.ModeOverride = LensSettings.OverrideModes.Perspective;\n            lens.FieldOfView = fieldOfView;', '''            var lens = menuCamera.Lens;
            lens.ModeOverride = _countdownFraming ? LensSettings.OverrideModes.Physical : LensSettings.OverrideModes.Perspective;
            if (_countdownFraming)
            {
                lens.PhysicalProperties = _previousOutputLens.PhysicalProperties;
                lens.PhysicalProperties.SensorSize = new Vector2(36f, 24f);
                lens.PhysicalProperties.GateFit = UnityEngine.Camera.GateFitMode.Vertical;
                lens.PhysicalProperties.LensShift = Vector2.zero;
                lens.PhysicalProperties.FocusDistance = distance;
            }
            lens.FieldOfView = framingFov;''')
s=s.replace('            _countdownFraming = true;','            _countdownFraming = true; _countdownDollyProgress = 0f;\n            if (brain != null) brain.LensModeOverride.Enabled = true;')
s=s.replace('        public void BeginCombatTransition()', '''        public void SetCountdownDollyProgress(float progress, bool reducedMotion)
        {
            if (!_active || !_countdownFraming || menuCamera.Priority < 0) return;
            _countdownDollyProgress = Mathf.Clamp01(progress); Reframe(reducedMotion);
        }
        public void BeginCombatTransition()''')
s=s.replace('            if (brain != null) brain.DefaultBlend = _blend;','''            if (brain != null) { brain.DefaultBlend = _blend; brain.LensModeOverride = _previousLensModeOverride; }
            if (outputCamera != null)
            {
                outputCamera.usePhysicalProperties = _previousOutputLens.IsPhysicalCamera;
                outputCamera.sensorSize = _previousOutputLens.PhysicalProperties.SensorSize;
                outputCamera.gateFit = _previousOutputLens.PhysicalProperties.GateFit;
                outputCamera.lensShift = _previousOutputLens.PhysicalProperties.LensShift;
                outputCamera.focusDistance = _previousOutputLens.PhysicalProperties.FocusDistance;
                if (_previousOutputLens.IsPhysicalCamera)
                    outputCamera.focalLength = UnityEngine.Camera.FieldOfViewToFocalLength(outputCamera.fieldOfView, outputCamera.sensorSize.y);
            }''')
p.write_text(s,encoding='utf-8')
for p in [Path('Assets/Elemental/Presentation/UI/FrontendFlowController.cs'),Path('BuildReports/AlphaImplementation/Stage2Pending/FrontendPatch/Modified/FrontendFlowController.cs')]:
 s=p.read_text(encoding='utf-8-sig');s=s.replace('                if (!_countdownCameraReleased && remaining <= blendDuration)','                if (!_countdownCameraReleased)\n                    menuCamera.SetCountdownDollyProgress((duration - remaining) / Mathf.Max(.1f, duration - blendDuration), Preferences.ReducedMotion);\n                if (!_countdownCameraReleased && remaining <= blendDuration)');p.write_text(s,encoding='utf-8')
Path('BuildReports/AlphaImplementation/Stage2Pending/FrontendPatch/Original/FrontendFlowController.cs').write_text(Path('Assets/Elemental/Presentation/UI/FrontendFlowController.cs').read_text(encoding='utf-8'),encoding='utf-8')
