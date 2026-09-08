from pathlib import Path
p=Path('Assets/Elemental/Presentation/UI/CinematicMenuCamera.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('        [SerializeField] private CinemachineBrain brain;','        [SerializeField] private CinemachineBrain brain;\n        [SerializeField] private CinemachineCamera gameplayCamera;')
s=s.replace('        private float _countdownDollyProgress;','        private float _countdownDollyProgress;\n        private Vector3 _dollyCenter, _dollyStartDirection;\n        private float _dollyStartDistance;\n        private CameraState _dollyEndState;\n        private bool _dollyEndCaptured;')
s=s.replace('Transform subject, Transform opponent = null)','Transform subject, Transform opponent = null, CinemachineCamera gameplay = null)')
s=s.replace('countdownOpponent = opponent; _opponentMotor', 'countdownOpponent = opponent; gameplayCamera = gameplay; _opponentMotor')
s=s.replace('            Reframe(reducedMotion);\n        }\n        public void SetCountdownDollyProgress(float progress, bool reducedMotion)','''            Reframe(reducedMotion);
            _dollyCenter = SubjectCenter;
            Vector3 offset = menuCamera.transform.position - _dollyCenter;
            _dollyStartDistance = offset.magnitude; _dollyStartDirection = offset.normalized;
            _dollyEndCaptured = false;
        }
        public void SetCountdownDollyProgress(float progress, float alignmentProgress, bool reducedMotion)''')
a='''            if (!_active || !_countdownFraming || menuCamera.Priority < 0) return;
            _countdownDollyProgress = Mathf.Clamp01(progress); Reframe(reducedMotion);'''
b='''            if (!_active || !_countdownFraming || menuCamera.Priority < 0) return;
            _countdownDollyProgress = Mathf.Clamp01(progress);
            if (gameplayCamera == null) { Reframe(reducedMotion); return; }
            if (!_dollyEndCaptured)
            {
                _dollyEndState = gameplayCamera.State; _dollyEndCaptured = true;
                _dollyStartDistance = Mathf.Max(_dollyStartDistance, Vector3.Distance(_dollyEndState.GetFinalPosition(), _dollyCenter) + 2f);
            }
            Vector3 endOffset = _dollyEndState.GetFinalPosition() - _dollyCenter;
            float t = _countdownDollyProgress;
            float align = Mathf.SmoothStep(0f, 1f, alignmentProgress);
            float distance = Mathf.Lerp(_dollyStartDistance, endOffset.magnitude, t);
            Vector3 direction = Vector3.Slerp(_dollyStartDirection, endOffset.normalized, align).normalized;
            Vector3 position = _dollyCenter + direction * distance;
            Vector3 up = motor != null ? motor.LocalUp : actor.up;
            Quaternion rotation = Quaternion.Slerp(Quaternion.LookRotation(_dollyCenter - position, up), _dollyEndState.GetFinalOrientation(), align);
            menuCamera.transform.SetPositionAndRotation(position, rotation);
            float normalFocal = UnityEngine.Camera.FieldOfViewToFocalLength(_dollyEndState.Lens.FieldOfView, 24f);
            var lens = menuCamera.Lens;
            lens.FieldOfView = UnityEngine.Camera.FocalLengthToFieldOfView(Mathf.Lerp(countdownStartFocalLength, normalFocal, t), 24f);
            lens.PhysicalProperties.FocusDistance = distance; menuCamera.Lens = lens;
            depthOfField?.ApplyPolicy(!reducedMotion, distance, 5.6f, 50f);'''
assert a in s;s=s.replace(a,b)
s=s.replace('{ if (_active && menuCamera != null) menuCamera.Priority = -1000; }','{ if (_active && menuCamera != null && (!_countdownFraming || gameplayCamera == null)) menuCamera.Priority = -1000; }')
p.write_text(s,encoding='utf-8')
for p in [Path('Assets/Elemental/Presentation/UI/FrontendFlowController.cs'),Path('BuildReports/AlphaImplementation/Stage2Pending/FrontendPatch/Modified/FrontendFlowController.cs')]:
 s=p.read_text(encoding='utf-8-sig');s=s.replace('                if (!_countdownCameraReleased)\n                    menuCamera.SetCountdownDollyProgress((duration - remaining) / Mathf.Max(.1f, duration - blendDuration), Preferences.ReducedMotion);','                menuCamera.SetCountdownDollyProgress((duration - remaining) / duration,\n                    Mathf.Clamp01(1f - remaining / blendDuration), Preferences.ReducedMotion);');p.write_text(s,encoding='utf-8')
Path('BuildReports/AlphaImplementation/Stage2Pending/FrontendPatch/Original/FrontendFlowController.cs').write_text(Path('Assets/Elemental/Presentation/UI/FrontendFlowController.cs').read_text(encoding='utf-8'),encoding='utf-8')
p=Path('Assets/Elemental/Authoring/Editor/AlphaFrontendSetup.cs');s=p.read_text(encoding='utf-8-sig').replace('director.Player, duel.BotTransform);','director.Player, duel.BotTransform, cameraController.VirtualCamera);');p.write_text(s,encoding='utf-8')
p=Path('BuildReports/AlphaImplementation/Stage2Pending/Assets/Elemental/NetworkingStage2/EarthOnlinePresentationBridge.cs');s=p.read_text(encoding='utf-8-sig').replace(': actorOne.OriginalDofSecondary);',': actorOne.OriginalDofSecondary, view.GameplayCamera);');p.write_text(s,encoding='utf-8')
