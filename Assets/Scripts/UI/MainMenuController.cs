using UnityEngine;
using UnityEngine.UIElements;

namespace ElEmental.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private StyleSheet theme;

        private Button _playButton;
        private Button _settingsButton;
        private Button _quitButton;
        private Label _statusLabel;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;

            if (theme != null && !root.styleSheets.Contains(theme))
            {
                root.styleSheets.Add(theme);
            }

            _playButton = root.Q<Button>("play-button");
            _settingsButton = root.Q<Button>("settings-button");
            _quitButton = root.Q<Button>("quit-button");
            _statusLabel = root.Q<Label>("status-label");

            if (_playButton != null) _playButton.clicked += HandlePlay;
            if (_settingsButton != null) _settingsButton.clicked += HandleSettings;
            if (_quitButton != null) _quitButton.clicked += HandleQuit;
        }

        private void OnDisable()
        {
            if (_playButton != null) _playButton.clicked -= HandlePlay;
            if (_settingsButton != null) _settingsButton.clicked -= HandleSettings;
            if (_quitButton != null) _quitButton.clicked -= HandleQuit;
        }

        private void HandlePlay()
        {
            SetStatus("Игровой цикл подключается следующим модулем");
        }

        private void HandleSettings()
        {
            SetStatus("Настройки будут открываться здесь");
        }

        private static void HandleQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetStatus(string message)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = message;
            }
        }
    }
}

