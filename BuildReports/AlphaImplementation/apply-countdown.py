from pathlib import Path
root=Path('.')
def edit(p,a,b):
 p=root/p;s=p.read_text(encoding='utf-8-sig');assert a in s,p;p.write_text(s.replace(a,b),encoding='utf-8')
edit(Path('Assets/Elemental/Presentation/UI/ElementalUITheme.cs'),'        public Font hudFont;','        [Header("In-game HUD font (all labels and numbers)")]\n        public Font hudFont;')
edit(Path('Assets/Elemental/Presentation/UI/ElementalUITheme.cs'),'        [Header("Type size relative to the authored layout")]','        [Header("Pre-match countdown")]\n        [Min(1f)] public float countdownSeconds = 4f;\n        [Min(.1f), Tooltip("Camera flies into gameplay during the last seconds of the countdown.")] public float countdownCameraBlendSeconds = 1.5f;\n        [Header("Type size relative to the authored layout")]')
edit(Path('Assets/Elemental/Authoring/Editor/AlphaFrontendSetup.cs'),'        [MenuItem("Elemental/UI/Install Alpha Frontend")]','        [MenuItem("Elemental/UI/Select In-Game HUD Theme")]\n        public static void SelectHudTheme()\n        { Selection.activeObject = AssetDatabase.LoadAssetAtPath<ElementalUITheme>(ThemePath); EditorGUIUtility.PingObject(Selection.activeObject); }\n\n        [MenuItem("Elemental/UI/Install Alpha Frontend")]')
p=Path('Assets/Elemental/Presentation/UI/FrontendMenuView.cs')
edit(p,'        private TMP_Text _status, _roomCode;','        private TMP_Text _status, _roomCode, _countdown;\n        private static readonly string[] CountdownDigits = { "", "1", "2", "3", "4" };\n        public string CountdownText => _countdown != null && _countdown.gameObject.activeSelf ? _countdown.text : "";')
edit(p,'            _buttons = GetComponentsInChildren<FrontendButton>(true);','            _countdown = Label(transform, "4", 160, _theme.text);\n            Stretch(_countdown.rectTransform, new Vector2(.35f, .30f), new Vector2(.65f, .70f));\n            _countdown.alignment = TextAlignmentOptions.Center;\n            var countdownGroup = _countdown.gameObject.AddComponent<CanvasGroup>();\n            countdownGroup.ignoreParentGroups = true; countdownGroup.blocksRaycasts = false;\n            _countdown.gameObject.SetActive(false);\n            _buttons = GetComponentsInChildren<FrontendButton>(true);')
edit(p,'        public void SetVisibility(float alpha, bool interactable)','        public void SetCountdown(int number)\n        {\n            if (_countdown == null) return;\n            _countdown.gameObject.SetActive(number > 0);\n            if (number > 0) _countdown.text = number < CountdownDigits.Length ? CountdownDigits[number] : number.ToString();\n        }\n        public void SetVisibility(float alpha, bool interactable)')
p=Path('Assets/Elemental/Presentation/UI/CinematicMenuCamera.cs')
edit(p,'        private bool _active, _chargeWasEnabled;','        [Header("Countdown framing")]\n        [SerializeField, Range(45, 120)] private float countdownAzimuth = 85f;\n        [SerializeField, Range(5, 35)] private float countdownElevation = 18f;\n        [SerializeField, Range(.3f, .65f)] private float countdownCharacterScreenHeight = .48f;\n        private bool _countdownFraming;\n        private bool _active, _chargeWasEnabled;')
edit(p,'            if (menuCamera == null || actor == null) return;','            if (menuCamera == null || actor == null) return;\n            _countdownFraming = false;')
edit(p,'* characterScreenHeight);','* (_countdownFraming ? countdownCharacterScreenHeight : characterScreenHeight));')
edit(p,'Quaternion.AngleAxis(presentationAzimuth, up)','Quaternion.AngleAxis(_countdownFraming ? countdownAzimuth : presentationAzimuth, up)')
edit(p,'            Vector3 position = center + facing * distance;','            Vector3 position = center + facing * distance;\n            if (_countdownFraming) position = center + (facing * Mathf.Cos(countdownElevation * Mathf.Deg2Rad) + up * Mathf.Sin(countdownElevation * Mathf.Deg2Rad)) * distance;')
edit(p,'            menuCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, up));','            if (_countdownFraming) target = center;\n            menuCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, up));')
edit(p,'lens.Dutch = reducedMotion ? 0 : dutchAngle;','lens.Dutch = reducedMotion || _countdownFraming ? 0 : dutchAngle;')
edit(p,'        public void BeginCombatTransition()','        public void BeginCountdown(bool reducedMotion, float blendSeconds)\n        {\n            _countdownFraming = true;\n            if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, blendSeconds);\n            Reframe(reducedMotion);\n        }\n        public void BeginCombatTransition()')
for p in [Path('Assets/Elemental/Presentation/UI/FrontendFlowController.cs'),Path('BuildReports/AlphaImplementation/Stage2Pending/FrontendPatch/Modified/FrontendFlowController.cs')]:
 edit(p,'        private float _transition;','        private float _transition;\n        private bool _countdownCameraReleased;')
 edit(p,'                float t = Mathf.Clamp01(_transition / theme.transitionSeconds); float smooth = t * t * (3 - 2 * t);\n                view.SetVisibility(1 - smooth, false); menuCamera.SetTransitionProgress(smooth);\n                if (t >= 1)', '''                float duration = Mathf.Max(1f, theme.countdownSeconds);
                float blendDuration = Mathf.Clamp(theme.countdownCameraBlendSeconds, .1f, duration);
                float remaining = Mathf.Max(0f, duration - _transition);
                view.SetCountdown(Mathf.CeilToInt(remaining));
                float menuFade = Mathf.Clamp01(_transition / theme.transitionSeconds);
                view.SetVisibility(1f - menuFade * menuFade * (3f - 2f * menuFade), false);
                if (!_countdownCameraReleased && remaining <= blendDuration)
                { _countdownCameraReleased = true; menuCamera.BeginCombatTransition(); }
                float t = Mathf.Clamp01(1f - remaining / blendDuration);
                menuCamera.SetTransitionProgress(t * t * (3f - 2f * t));
                if (remaining <= 0f)''')
 edit(p,'                    menuCamera.FinishCombatTransition(); State = FrontendState.Combat;','                    view.SetCountdown(0);\n                    menuCamera.FinishCombatTransition(); State = FrontendState.Combat;')
 s=(root/p).read_text(encoding='utf-8')
 s=s.replace('duel.RestartRound(); duel.SetRoundReady(true);','duel.SetRoundReady(true);')
 s=s.replace('            view.SetVisibility(1, false); menuCamera.BeginCombatTransition(); return true;','            duel.SetRoundReady(false); duel.RestartRound();\n            StartCountdownPresentation(); return true;')
 s=s.replace('        public void ShowMain(string message = null)','        private void StartCountdownPresentation()\n        {\n            _countdownCameraReleased = false; view.SetVisibility(1, false);\n            view.SetCountdown(Mathf.CeilToInt(theme.countdownSeconds));\n            menuCamera.BeginCountdown(Preferences.ReducedMotion, theme.countdownCameraBlendSeconds);\n        }\n        public void ShowMain(string message = null)')
 s=s.replace('            State = FrontendState.Main; menuCamera.Enter','            view.SetCountdown(0);\n            State = FrontendState.Main; menuCamera.Enter')
 s=s.replace('            menuCamera.BeginCombatTransition(); return true;','            StartCountdownPresentation(); return true;')
 (root/p).write_text(s,encoding='utf-8')
Path('BuildReports/AlphaImplementation/Stage2Pending/FrontendPatch/Original/FrontendFlowController.cs').write_text(Path('Assets/Elemental/Presentation/UI/FrontendFlowController.cs').read_text(encoding='utf-8'),encoding='utf-8')
