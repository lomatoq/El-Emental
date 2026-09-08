from pathlib import Path
def edit(p,a,b):
 p=Path(p);s=p.read_text(encoding='utf-8-sig');assert a in s,p;p.write_text(s.replace(a,b),encoding='utf-8')
p='Assets/Elemental/Presentation/UI/FrontendMenuView.cs'
edit(p,'        private TMP_Text _status, _roomCode, _countdown;','        private TMP_Text _status, _roomCode, _countdown;\n        private CanvasGroup _countdownGroup;\n        private int _countdownNumber;\n        private float _countdownChangedAt;')
edit(p,'            var countdownGroup = _countdown.gameObject.AddComponent<CanvasGroup>();\n            countdownGroup.ignoreParentGroups = true; countdownGroup.blocksRaycasts = false;','            _countdownGroup = _countdown.gameObject.AddComponent<CanvasGroup>();\n            _countdownGroup.ignoreParentGroups = true; _countdownGroup.blocksRaycasts = false;')
edit(p,'            _countdown.gameObject.SetActive(number > 0);','            if (number != _countdownNumber) { _countdownNumber = number; _countdownChangedAt = Time.unscaledTime; }\n            _countdown.gameObject.SetActive(number > 0);')
edit(p,'        public void SetVisibility(float alpha, bool interactable)','        private void Update()\n        {\n            if (_countdownNumber <= 0 || _countdownGroup == null) return;\n            float age = Time.unscaledTime - _countdownChangedAt;\n            float pop = Mathf.Clamp01(age / .22f);\n            float scale = _flow.Preferences.ReducedMotion ? 1f : Mathf.Lerp(1.3f, 1f, 1f - Mathf.Pow(1f - pop, 3f));\n            _countdown.transform.localScale = Vector3.one * scale;\n            _countdownGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / .07f)) *\n                (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - .72f) / .28f)));\n        }\n        public void SetVisibility(float alpha, bool interactable)')
paths=['Assets/Elemental/Presentation/UI/EarthDuelHud.cs']
paths += [str(p) for p in Path('BuildReports/AlphaImplementation/Stage2Pending').glob('HudPatch/*/EarthDuelHud.cs')]
for p in paths:
 edit(p,'        private Button _restart;','        private Button _restart, _pause;\n        private System.Action _pauseRequested;\n        public void ConfigurePause(System.Action pause) => _pauseRequested = pause;')
 edit(p,'            _root = Box("duel-hud", "duel-hud"); document.Add(_root); _root.style.opacity = 0;','''            _root = Box("duel-hud", "duel-hud"); document.Add(_root); _root.style.opacity = 0;
            _pause = new Button(() => _pauseRequested?.Invoke()) { name = "pause-match", tooltip = "Pause (Esc)" };
            _pause.style.position = Position.Absolute; _pause.style.right = 24; _pause.style.top = 24;
            _pause.style.width = 44; _pause.style.height = 44;
            _pause.style.backgroundColor = new Color(.03f, .08f, .13f, .32f);
            _pause.style.borderTopWidth = _pause.style.borderBottomWidth = _pause.style.borderLeftWidth = _pause.style.borderRightWidth = 0;
            _pause.style.borderTopLeftRadius = _pause.style.borderTopRightRadius = _pause.style.borderBottomLeftRadius = _pause.style.borderBottomRightRadius = 8;
            _pause.style.flexDirection = FlexDirection.Row; _pause.style.alignItems = Align.Center; _pause.style.justifyContent = Justify.Center;
            for (int bar = 0; bar < 2; bar++)
            {
                var stroke = new VisualElement { pickingMode = PickingMode.Ignore };
                stroke.style.width = 4; stroke.style.height = 18; stroke.style.marginLeft = stroke.style.marginRight = 3;
                stroke.style.backgroundColor = new Color(1f, .957f, .867f, .72f); _pause.Add(stroke);
            }
            _root.Add(_pause);''')
for p in ['Assets/Elemental/Presentation/UI/FrontendFlowController.cs','BuildReports/AlphaImplementation/Stage2Pending/FrontendPatch/Modified/FrontendFlowController.cs']:
 edit(p,'            ApplyPreferences(); HideDebugOverlays();','            hud?.ConfigurePause(Pause);\n            ApplyPreferences(); HideDebugOverlays();')
Path('BuildReports/AlphaImplementation/Stage2Pending/FrontendPatch/Original/FrontendFlowController.cs').write_text(Path('Assets/Elemental/Presentation/UI/FrontendFlowController.cs').read_text(encoding='utf-8'),encoding='utf-8')
