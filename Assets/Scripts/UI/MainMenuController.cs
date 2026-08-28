using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PrehistoricSurvival.Core;

namespace PrehistoricSurvival.UI
{
    /// <summary>
    /// Title screen logic. Every button is wired here in code, so the menu works
    /// whether the scene was authored by the editor tool or built at runtime.
    /// If no canvas is found the whole menu is generated on the fly.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Buttons (auto-found by name when empty)")]
        public Button playButton;
        public Button continueButton;
        public Button settingsButton;
        public Button quitButton;

        [Header("Panels")]
        public GameObject settingsPanel;

        [Header("Options")]
        [Tooltip("Build the full menu UI at runtime if none is present in the scene.")]
        public bool buildUIIfMissing = true;

        private Canvas _canvas;

        private void Start()
        {
            UIFactory.EnsureEventSystem();

            // Only treat a canvas that actually holds buttons as an authored menu —
            // the loading-screen canvas (DontDestroyOnLoad) must not fool us.
            var existingButtons = FindObjectsOfType<Button>();
            _canvas = existingButtons.Length > 0 ? existingButtons[0].GetComponentInParent<Canvas>() : null;

            if (_canvas == null && buildUIIfMissing)
                BuildMenu();
            else
                WireExistingButtons();

            RefreshContinueState();
        }

        // ------------------------------------------------------------------
        // Wiring
        // ------------------------------------------------------------------
        private void WireExistingButtons()
        {
            if (playButton == null) playButton = FindButton("PlayButton");
            if (continueButton == null) continueButton = FindButton("LoadButton") ?? FindButton("ContinueButton");
            if (settingsButton == null) settingsButton = FindButton("SettingsButton");
            if (quitButton == null) quitButton = FindButton("QuitButton");

            Bind(playButton, OnPlay);
            Bind(continueButton, OnContinue);
            Bind(settingsButton, OnSettings);
            Bind(quitButton, OnQuit);

            if (settingsPanel == null)
            {
                var found = GameObject.Find("SettingsPanel");
                if (found != null) settingsPanel = found;
            }
            if (settingsPanel == null && _canvas != null)
                settingsPanel = BuildSettingsPanel(_canvas.transform).gameObject;
            if (settingsPanel != null) settingsPanel.SetActive(false);
        }

        private static Button FindButton(string name)
        {
            var go = GameObject.Find(name);
            return go != null ? go.GetComponent<Button>() : null;
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            // Scene-authored buttons already have a persistent hook — don't double-fire.
            if (button.onClick.GetPersistentEventCount() > 0) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        // ------------------------------------------------------------------
        // Actions
        // ------------------------------------------------------------------
        public void OnPlay()
        {
            SceneLoader.Instance.NewGame();
        }

        public void OnContinue()
        {
            if (!SaveSystem.HasSave())
            {
                Debug.Log("[MainMenu] No save found — starting a new game instead.");
                SceneLoader.Instance.NewGame();
                return;
            }
            SceneLoader.Instance.ContinueGame();
        }

        public void OnSettings()
        {
            if (settingsPanel != null) settingsPanel.SetActive(!settingsPanel.activeSelf);
        }

        public void OnQuit()
        {
            EditorOnlyQuitter.Quit();
        }

        private void RefreshContinueState()
        {
            if (continueButton == null) return;
            bool has = SaveSystem.HasSave();
            continueButton.interactable = has;
            var label = continueButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.color = has ? UIFactory.Parchment : new Color(0.55f, 0.52f, 0.45f);
        }

        // ------------------------------------------------------------------
        // Runtime menu construction
        // ------------------------------------------------------------------
        private void BuildMenu()
        {
            _canvas = UIFactory.Canvas("MenuCanvas", 100);

            var bg = UIFactory.Panel(_canvas.transform, "Background", new Color(0.12f, 0.09f, 0.06f));
            UIFactory.Stretch(bg);

            UIFactory.Text(_canvas.transform, "Title", "WORLD EVOLUTION SAGA", 78,
                new Vector2(0.5f, 0.82f), new Vector2(1600, 120), new Color(0.93f, 0.83f, 0.55f));
            UIFactory.Text(_canvas.transform, "Subtitle", "Old Stone Age — survive an entire planet", 32,
                new Vector2(0.5f, 0.74f), new Vector2(1400, 60), new Color(0.72f, 0.66f, 0.52f));

            playButton = UIFactory.Button(_canvas.transform, "PlayButton", "NEW GAME",
                new Vector2(0.5f, 0.55f), new Vector2(460, 96), OnPlay);
            continueButton = UIFactory.Button(_canvas.transform, "ContinueButton", "CONTINUE",
                new Vector2(0.5f, 0.43f), new Vector2(460, 96), OnContinue);
            settingsButton = UIFactory.Button(_canvas.transform, "SettingsButton", "SETTINGS",
                new Vector2(0.5f, 0.31f), new Vector2(460, 96), OnSettings);
            quitButton = UIFactory.Button(_canvas.transform, "QuitButton", "QUIT",
                new Vector2(0.5f, 0.19f), new Vector2(460, 96), OnQuit,
                new Color(0.30f, 0.13f, 0.10f, 0.92f));

            UIFactory.Text(_canvas.transform, "Version", Application.version, 22,
                new Vector2(0.93f, 0.05f), new Vector2(300, 40), new Color(0.5f, 0.47f, 0.4f));

            settingsPanel = BuildSettingsPanel(_canvas.transform).gameObject;
            settingsPanel.SetActive(false);
        }

        private RectTransform BuildSettingsPanel(Transform parent)
        {
            var panel = UIFactory.Panel(parent, "SettingsPanel", new Color(0.10f, 0.08f, 0.06f, 0.97f));
            UIFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(900, 720));

            UIFactory.Text(panel.transform, "Header", "SETTINGS", 46,
                new Vector2(0.5f, 0.9f), new Vector2(700, 70), UIFactory.Parchment);

            // Quality
            UIFactory.Text(panel.transform, "QualityLabel", "Graphics Quality", 28,
                new Vector2(0.5f, 0.75f), new Vector2(700, 40), Color.white);
            string[] qualities = { "Low", "Medium", "High" };
            for (int i = 0; i < qualities.Length; i++)
            {
                int index = i;
                UIFactory.Button(panel.transform, "Quality" + qualities[i], qualities[i],
                    new Vector2(0.25f + 0.25f * i, 0.66f), new Vector2(200, 70),
                    () => SetQuality(index), null, 26);
            }

            // World detail
            UIFactory.Text(panel.transform, "DetailLabel", "World Detail (vegetation density)", 28,
                new Vector2(0.5f, 0.54f), new Vector2(760, 40), Color.white);
            string[] detail = { "Sparse", "Normal", "Lush" };
            float[] density = { 0.5f, 1f, 1.6f };
            for (int i = 0; i < detail.Length; i++)
            {
                float value = density[i];
                UIFactory.Button(panel.transform, "Detail" + detail[i], detail[i],
                    new Vector2(0.25f + 0.25f * i, 0.45f), new Vector2(200, 70),
                    () => PlayerPrefs.SetFloat("prop_density", value), null, 26);
            }

            // Music / SFX volume
            UIFactory.Text(panel.transform, "VolumeLabel", "Master Volume", 28,
                new Vector2(0.5f, 0.33f), new Vector2(700, 40), Color.white);
            var slider = UIFactory.ProgressBar(panel.transform, "VolumeSlider",
                new Vector2(0.5f, 0.26f), new Vector2(620, 34), UIFactory.Ember);
            slider.interactable = true;
            slider.value = PlayerPrefs.GetFloat("master_volume", 1f);
            AudioListener.volume = slider.value;
            slider.onValueChanged.AddListener(v =>
            {
                AudioListener.volume = v;
                PlayerPrefs.SetFloat("master_volume", v);
            });

            UIFactory.Button(panel.transform, "CloseSettings", "CLOSE",
                new Vector2(0.5f, 0.1f), new Vector2(320, 80),
                () => panel.gameObject.SetActive(false));

            return panel.rectTransform;
        }

        private static void SetQuality(int level)
        {
            QualitySettings.SetQualityLevel(Mathf.Clamp(level, 0, QualitySettings.names.Length - 1), true);
            PlayerPrefs.SetInt("quality_level", level);
        }
    }
}
