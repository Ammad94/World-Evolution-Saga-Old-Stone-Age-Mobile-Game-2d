using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using PrehistoricSurvival.UI;

namespace PrehistoricSurvival.Core
{
    /// <summary>
    /// Handles all scene transitions with an async loading screen, and carries the
    /// "should we load a save?" flag from the main menu into the gameplay scene.
    /// Survives scene loads (DontDestroyOnLoad) and creates its own UI, so it works
    /// no matter which scene the game is started from.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public const string MainMenuScene = "MainMenu";
        public const string GameplayScene = "GameplayWorld";

        /// <summary>True when the gameplay scene should restore the last save.</summary>
        public static bool LoadSaveOnStart { get; set; }
        /// <summary>Seed requested for a new game (0 = use the world map default).</summary>
        public static int RequestedSeed { get; set; }

        private static SceneLoader _instance;
        public static SceneLoader Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("SceneLoader");
                    _instance = go.AddComponent<SceneLoader>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private CanvasGroup _group;
        private Slider _progressBar;
        private TextMeshProUGUI _statusText;
        private TextMeshProUGUI _tipText;
        private bool _busy;

        private static readonly string[] Tips =
        {
            "Drink from rivers and lakes before crossing the open steppe.",
            "Cooked meat restores far more than raw — build a campfire.",
            "Sabertooths hunt alone. Mammoths only charge when provoked.",
            "Carrying heavy logs slows you down. Drop them before you run.",
            "Torches keep the night — and the cold — at bay.",
            "Follow the coast: fish, shellfish and driftwood are always close.",
            "The world is a whole planet. Set a waypoint before you wander."
        };

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            BuildUI();
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>Start a brand-new game.</summary>
        public void NewGame(int seed = 0)
        {
            LoadSaveOnStart = false;
            RequestedSeed = seed;
            Load(GameplayScene);
        }

        /// <summary>Continue from the last save.</summary>
        public void ContinueGame()
        {
            LoadSaveOnStart = true;
            Load(GameplayScene);
        }

        /// <summary>Back to the title screen.</summary>
        public void ToMainMenu()
        {
            Time.timeScale = 1f;
            Load(MainMenuScene);
        }

        /// <summary>Load any scene by name with the loading screen.</summary>
        public void Load(string sceneName)
        {
            if (_busy) return;
            StartCoroutine(LoadRoutine(sceneName));
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            _busy = true;
            Time.timeScale = 1f;

            _tipText.text = Tips[Random.Range(0, Tips.Length)];
            yield return Fade(1f);

            AsyncOperation op = null;
            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                op = SceneManager.LoadSceneAsync(sceneName);
            }
            else
            {
                // Scene missing from Build Settings: fall back to a bootstrapped runtime scene
                // so the game still runs instead of dying with an error.
                Debug.LogWarning($"[SceneLoader] Scene '{sceneName}' is not in Build Settings. " +
                                 "Falling back to a runtime-generated scene.");
                var scene = SceneManager.CreateScene(sceneName + "_Runtime");
                SceneManager.SetActiveScene(scene);
                yield return null;
                GameBootstrap.BuildRuntimeScene(sceneName);
            }

            if (op != null)
            {
                op.allowSceneActivation = false;
                while (op.progress < 0.9f)
                {
                    _progressBar.value = Mathf.Clamp01(op.progress / 0.9f);
                    _statusText.text = $"Shaping the world…  {Mathf.RoundToInt(_progressBar.value * 100f)}%";
                    yield return null;
                }
                _progressBar.value = 1f;
                _statusText.text = "Entering the Stone Age…";
                op.allowSceneActivation = true;
                while (!op.isDone) yield return null;
            }

            // Give the streamer a couple of frames to build the first chunks.
            yield return null;
            yield return null;

            yield return Fade(0f);
            _busy = false;
        }

        private IEnumerator Fade(float targetAlpha)
        {
            _group.blocksRaycasts = targetAlpha > 0.5f;
            while (!Mathf.Approximately(_group.alpha, targetAlpha))
            {
                _group.alpha = Mathf.MoveTowards(_group.alpha, targetAlpha, Time.unscaledDeltaTime * 2.5f);
                yield return null;
            }
            _group.blocksRaycasts = targetAlpha > 0.5f;
        }

        // ------------------------------------------------------------------
        // UI
        // ------------------------------------------------------------------
        private void BuildUI()
        {
            var canvasGO = new GameObject("LoadingCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            _group = canvasGO.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;

            var bg = UIFactory.Panel(canvasGO.transform, "Background", new Color(0.06f, 0.05f, 0.04f, 1f));
            UIFactory.Stretch(bg);

            UIFactory.Text(canvasGO.transform, "Title", "WORLD EVOLUTION SAGA", 64,
                new Vector2(0.5f, 0.72f), new Vector2(1400, 100), new Color(0.92f, 0.82f, 0.55f));

            _statusText = UIFactory.Text(canvasGO.transform, "Status", "Loading…", 32,
                new Vector2(0.5f, 0.45f), new Vector2(1200, 60), Color.white);

            _tipText = UIFactory.Text(canvasGO.transform, "Tip", "", 26,
                new Vector2(0.5f, 0.28f), new Vector2(1400, 80), new Color(0.75f, 0.72f, 0.62f));

            _progressBar = UIFactory.ProgressBar(canvasGO.transform, "Progress",
                new Vector2(0.5f, 0.36f), new Vector2(900, 26), new Color(0.75f, 0.55f, 0.2f));
        }
    }
}
