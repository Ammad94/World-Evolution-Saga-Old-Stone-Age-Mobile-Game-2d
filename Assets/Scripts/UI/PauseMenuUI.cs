using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PrehistoricSurvival.Core;

namespace PrehistoricSurvival.UI
{
    /// <summary>
    /// In-game pause menu: Resume, Save, World Map, Main Menu, Quit.
    /// Opened with the on-screen ☰ button, Escape, or the Android back button.
    /// Builds its own UI so it works in any scene.
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        [Header("Options")]
        public bool buildUIIfMissing = true;

        private GameObject _panel;
        private TextMeshProUGUI _statusText;
        private bool _open;

        public bool IsOpen => _open;

        private void Start()
        {
            UIFactory.EnsureEventSystem();
            if (buildUIIfMissing && _panel == null) Build();
            Close();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) Toggle();
        }

        /// <summary>Open / close the pause menu.</summary>
        public void Toggle()
        {
            if (_open) Close();
            else Open();
        }

        public void Open()
        {
            _open = true;
            if (_panel != null) _panel.SetActive(true);
            if (GameManager.Instance != null) GameManager.Instance.PauseGame();
            else Time.timeScale = 0f;
        }

        public void Close()
        {
            _open = false;
            if (_panel != null) _panel.SetActive(false);
            if (GameManager.Instance != null) GameManager.Instance.ResumeGame();
            else Time.timeScale = 1f;
        }

        // ------------------------------------------------------------------
        private void Build()
        {
            var canvas = UIFactory.Canvas("PauseCanvas", 900);
            canvas.transform.SetParent(transform, false);

            var panel = UIFactory.Panel(canvas.transform, "PausePanel", new Color(0.05f, 0.04f, 0.03f, 0.92f));
            UIFactory.Stretch(panel);
            _panel = panel.gameObject;

            UIFactory.Text(_panel.transform, "Header", "PAUSED", 68,
                new Vector2(0.5f, 0.82f), new Vector2(900, 100), UIFactory.Parchment);

            UIFactory.Button(_panel.transform, "ResumeButton", "RESUME",
                new Vector2(0.5f, 0.63f), new Vector2(460, 92), Close);

            UIFactory.Button(_panel.transform, "MapButton", "WORLD MAP",
                new Vector2(0.5f, 0.52f), new Vector2(460, 92), OpenMap);

            UIFactory.Button(_panel.transform, "SaveButton", "SAVE GAME",
                new Vector2(0.5f, 0.41f), new Vector2(460, 92), SaveGame);

            UIFactory.Button(_panel.transform, "MenuButton", "MAIN MENU",
                new Vector2(0.5f, 0.30f), new Vector2(460, 92), ToMainMenu);

            UIFactory.Button(_panel.transform, "QuitButton", "QUIT",
                new Vector2(0.5f, 0.19f), new Vector2(460, 92), Quit,
                new Color(0.30f, 0.13f, 0.10f, 0.95f));

            _statusText = UIFactory.Text(_panel.transform, "Status", "", 26,
                new Vector2(0.5f, 0.10f), new Vector2(900, 40), new Color(0.7f, 0.66f, 0.55f));
        }

        private void OpenMap()
        {
            var map = FindObjectOfType<WorldMapUI>();
            if (map != null)
            {
                Close();
                map.Open();
            }
        }

        private void SaveGame()
        {
            if (SaveSystem.Instance != null)
            {
                SaveSystem.Instance.SaveGame();
                if (_statusText != null) _statusText.text = "Game saved.";
            }
            else if (_statusText != null)
            {
                _statusText.text = "No save system in scene.";
            }
        }

        private void ToMainMenu()
        {
            Time.timeScale = 1f;
            if (SaveSystem.Instance != null) SaveSystem.Instance.SaveGame();
            SceneLoader.Instance.ToMainMenu();
        }

        private static void Quit()
        {
            if (SaveSystem.Instance != null) SaveSystem.Instance.SaveGame();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
