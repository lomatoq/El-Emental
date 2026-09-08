from pathlib import Path
p=Path('Assets/Elemental/Tests/PlayMode/AlphaFrontendPlayTests.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('        private Canvas _canvas;','        private Canvas _canvas;\n        private UnityEngine.InputSystem.Mouse _pauseTestMouse;');s=s.replace('            RestoreGameView();','            if (_pauseTestMouse != null) UnityEngine.InputSystem.InputSystem.RemoveDevice(_pauseTestMouse);\n            RestoreGameView();');s=s.replace('            using (var submit = UnityEngine.UIElements.NavigationSubmitEvent.GetPooled()) pauseButton.SendEvent(submit);','''            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            _pauseTestMouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            Vector2 pausePosition = pauseButton.worldBound.center;
            Vector2 screenPoint = new Vector2(pausePosition.x, Screen.height - pausePosition.y);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_pauseTestMouse,
                new UnityEngine.InputSystem.LowLevel.MouseState { position = screenPoint });
            yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_pauseTestMouse,
                new UnityEngine.InputSystem.LowLevel.MouseState { position = screenPoint }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
            yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_pauseTestMouse,
                new UnityEngine.InputSystem.LowLevel.MouseState { position = screenPoint });
            yield return null;''');p.write_text(s,encoding='utf-8')
