using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TMPro;
using PrehistoricSurvival.AI;
using PrehistoricSurvival.Crafting;
using PrehistoricSurvival.Lighting;
using PrehistoricSurvival.Player;
using PrehistoricSurvival.Survival;
using PrehistoricSurvival.UI;
using PrehistoricSurvival.World;

namespace PrehistoricSurvival.Core
{
    /// <summary>
    /// Builds and wires a complete, playable game at runtime.
    ///
    /// Drop this single component into an empty scene (the editor setup tool does it
    /// for you) and it will create the planet, the streaming world, the player, the
    /// camera, every manager, the touch joystick and the full HUD — then place the
    /// player somewhere habitable or restore the last save.
    ///
    /// Anything that already exists in the scene is reused instead of duplicated, so
    /// hand-authored scenes keep working.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class GameBootstrap : MonoBehaviour
    {
        [Header("World")]
        [Tooltip("Seed for this planet. 0 = use the WorldMap default seed.")]
        public int worldSeed = 0;
        [Tooltip("Chunk load radius (3 = 224×224 tiles kept alive around the player).")]
        [Range(1, 6)] public int loadRadius = 3;
        [Tooltip("Vegetation / rock density multiplier.")]
        [Range(0.1f, 2f)] public float propDensity = 1f;

        [Header("Content")]
        public bool spawnAnimals = true;
        public bool createHUD = true;
        public bool createPauseMenu = true;
        public bool createWorldMap = true;

        [Header("Player")]
        [Tooltip("Optional explicit player prefab (otherwise taken from the GameLibrary).")]
        public GameObject playerPrefabOverride;

        private GameLibrary _lib;
        private Transform _player;
        private MobileJoystick _joystick;

        // ------------------------------------------------------------------
        private void Awake()
        {
            _lib = GameLibrary.Instance;
            BuildAll();
        }

        /// <summary>Create a runtime scene from nothing (used as a fallback by SceneLoader).</summary>
        public static void BuildRuntimeScene(string sceneName)
        {
            if (sceneName == SceneLoader.MainMenuScene)
            {
                var menuGO = new GameObject("MainMenu");
                menuGO.AddComponent<MainMenuController>();
                UIFactory.EnsureEventSystem();
                EnsureCamera2D(new Color(0.09f, 0.07f, 0.05f));
                return;
            }

            var go = new GameObject("GameBootstrap");
            go.AddComponent<GameBootstrap>();
        }

        // ------------------------------------------------------------------
        private void BuildAll()
        {
            var map = EnsureWorldMap();
            EnsureManagers();
            var camera = EnsureCamera2D(new Color(0.35f, 0.55f, 0.75f));
            EnsureComponent<PrehistoricSurvival.Player.CombatFeedback>("CombatFeedback");
            EnsureGlobalLight();
            EnsurePlayer(map);
            EnsureStreaming(map);
            if (spawnAnimals) EnsureComponent<AnimalSpawner>("AnimalSpawner");
            if (createHUD) BuildHUD();
            if (createPauseMenu) EnsureComponent<PauseMenuUI>("PauseMenu");
            if (createWorldMap) EnsureComponent<WorldMapUI>("WorldMapUI");

            // Camera locks onto the player after everything exists.
            var follow = camera.GetComponent<CameraFollow>();
            if (follow != null)
            {
                follow.AcquireTarget();
                follow.SnapToTarget();
            }

            // Restore a save if the menu asked for one.
            if (SceneLoader.LoadSaveOnStart && SaveSystem.Instance != null)
            {
                SaveSystem.Instance.LoadGame();
                SceneLoader.LoadSaveOnStart = false;
                if (ChunkManager.Instance != null) ChunkManager.Instance.ForceReloadAll();
                if (follow != null) follow.SnapToTarget();
            }

            if (ChunkManager.Instance != null) ChunkManager.Instance.ForceBuildAroundPlayer();

            Debug.Log("[GameBootstrap] World ready — planet seed " +
                      (WorldMap.Instance != null ? WorldMap.Instance.seed : 0));
        }

        // ------------------------------------------------------------------
        private WorldMap EnsureWorldMap()
        {
            var map = WorldMap.Instance;
            if (map == null) map = FindFirstObjectByType<WorldMap>();
            if (map == null)
            {
                var go = new GameObject("WorldMap");
                map = go.AddComponent<WorldMap>();
            }
            int seed = SceneLoader.RequestedSeed != 0 ? SceneLoader.RequestedSeed : worldSeed;
            if (seed != 0) map.seed = seed;
            map.Initialise();
            return map;
        }

        private void EnsureManagers()
        {
            if (GameManager.Instance == null && FindFirstObjectByType<GameManager>() == null)
            {
                var go = new GameObject("GameManager");
                go.AddComponent<GameManager>();
                go.AddComponent<SaveSystem>();
            }
            else if (FindFirstObjectByType<SaveSystem>() == null)
            {
                (GameManager.Instance != null ? GameManager.Instance.gameObject : new GameObject("SaveSystem"))
                    .AddComponent<SaveSystem>();
            }

            var audio = EnsureComponent<AudioManager>("AudioManager");
            if (audio.library == null && _lib != null) audio.library = _lib.audioLibrary;

            EnsureComponent<InventorySystem>("InventorySystem");
            EnsureComponent<CraftingSystem>("CraftingSystem");
            EnsureComponent<WaypointManager>("WaypointManager");
            EnsureComponent<BiomeManager>("BiomeManager");
            EnsureComponent<SeasonManager>("SeasonManager");
            EnsureComponent<WeatherController>("WeatherController");
            EnsureComponent<Survival.ParticleEffectsManager>("ParticleEffectsManager");
            EnsureComponent<Environment.BuildingPlacementSystem>("BuildingPlacementSystem");
            EnsureComponent<ShadowManager>("ShadowManager");
            EnsureComponent<AccessibilityAndPerformance>("AccessibilityAndPerformance");
            EnsureComponent<QuestManager>("QuestManager");
            EnsureComponent<TradingSystem>("TradingSystem");
            EnsureComponent<AnimalTrackingSystem>("AnimalTrackingSystem");
            EnsureComponent<HerdMigrationSystem>("HerdMigrationSystem");

            // --- AAA pass: art/audio/content systems ---
            Feedback.GameFeel.Ensure();
            Art.SpriteSheetFX.Ensure();
            Feedback.DamageNumber.Ensure();
            EnsureComponent<Audio.DynamicMusicDirector>("MusicDirector");
            EnsureComponent<Audio.BiomeAmbience>("BiomeAmbience");
            EnsureComponent<Audio.WeatherAudio>("WeatherAudio");
            EnsureComponent<Content.EraProgression>("EraProgression");
            EnsureComponent<Content.QuestSystem>("QuestSystem");
            EnsureComponent<Content.TribeCampSystem>("TribeCampSystem");
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null && playerGo.GetComponent<Art.PlayerActionAnimator>() == null)
                playerGo.AddComponent<Art.PlayerActionAnimator>();

            var crafting = FindFirstObjectByType<CraftingSystem>();
            if (crafting != null && crafting.recipeDatabase == null && _lib != null)
                crafting.recipeDatabase = _lib.recipeDatabase as RecipeDatabase;
        }

        private static T EnsureComponent<T>(string goName) where T : Component
        {
            var existing = FindFirstObjectByType<T>();
            if (existing != null) return existing;
            var go = new GameObject(goName);
            return go.AddComponent<T>();
        }

        private static Camera EnsureCamera2D(Color background)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 9f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.transform.rotation = Quaternion.identity;
            cam.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, -10f);

            if (cam.GetComponent<CameraFollow>() == null) cam.gameObject.AddComponent<CameraFollow>();
            return cam;
        }

        private void EnsureGlobalLight()
        {
            var existing = FindFirstObjectByType<Light2D>();
            Light2D global = null;
            if (existing != null && existing.lightType == Light2D.LightType.Global) global = existing;

            if (global == null)
            {
                var go = new GameObject("GlobalLight2D");
                global = go.AddComponent<Light2D>();
                global.lightType = Light2D.LightType.Global;
                global.color = new Color(1f, 0.96f, 0.88f);
                global.intensity = 1f;
            }

            var cycle = EnsureComponent<DayNightCycle>("DayNightCycle");
            if (cycle.globalLight == null) cycle.globalLight = global;
        }

        private void EnsurePlayer(WorldMap map)
        {
            var existing = GameObject.FindGameObjectWithTag("Player");
            GameObject player;

            if (existing != null)
            {
                player = existing;
            }
            else
            {
                GameObject prefab = playerPrefabOverride != null
                    ? playerPrefabOverride
                    : (_lib != null ? _lib.playerPrefab : null);

                player = prefab != null ? Instantiate(prefab) : CreatePlaceholderPlayer();
                player.name = "Player";
            }

            // Position: spawn on habitable land unless a save will move us.
            if (!SceneLoader.LoadSaveOnStart)
            {
                Vector2Int spawn = map.FindSpawnTile();
                player.transform.position = new Vector3(spawn.x + 0.5f, spawn.y + 0.5f, 0f);
            }

            _player = player.transform;
            if (player.GetComponent<Survival.TemperatureSystem>() == null)
                player.AddComponent<Survival.TemperatureSystem>();
            if (player.GetComponent<CombatEquipment>() == null)
                player.AddComponent<CombatEquipment>();
            if (player.GetComponent<PrehistoricSurvival.Player.DodgeSystem>() == null)
                player.AddComponent<PrehistoricSurvival.Player.DodgeSystem>();
        }

        private GameObject CreatePlaceholderPlayer()
        {
            var go = new GameObject("Player") { tag = "Player" };
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ChunkManager.SolidSprite(new Color(0.85f, 0.65f, 0.45f));
            sr.sortingOrder = 0;
            go.transform.localScale = new Vector3(1f, 1.8f, 1f);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            var col = go.AddComponent<CapsuleCollider2D>();
            col.size = new Vector2(0.8f, 1.4f);

            go.AddComponent<PlayerController>();
            go.AddComponent<SurvivalStats>();
            go.AddComponent<WeightCarrySystem>();
            go.AddComponent<Water.SwimmingSystem>();
            go.AddComponent<Traversal.ClimbingSystem>();
            go.AddComponent<Environment.MiningSystem>();
            go.AddComponent<ConsumableSystem>();
            go.AddComponent<WeaponSystem>();
            Debug.LogWarning("[GameBootstrap] No player prefab found in the GameLibrary — " +
                             "using a placeholder. Run PrehistoricSurvival → Setup Entire Project.");
            return go;
        }

        private void EnsureStreaming(WorldMap map)
        {
            var streamer = FindFirstObjectByType<ChunkManager>();
            if (streamer == null)
            {
                var go = new GameObject("ChunkManager");
                streamer = go.AddComponent<ChunkManager>();
            }
            streamer.worldMap = map;
            streamer.player = _player;
            streamer.loadRadius = loadRadius;
            streamer.propDensity = PlayerPrefs.GetFloat("prop_density", propDensity);
        }

        // ------------------------------------------------------------------
        // HUD
        // ------------------------------------------------------------------
        private void BuildHUD()
        {
            if (FindFirstObjectByType<SurvivalStatsHUD>() != null) { EnsureJoystick(null); return; }

            UIFactory.EnsureEventSystem();
            var canvas = UIFactory.Canvas("GameCanvas", 100);

            // --- Touch joystick (bottom-left half of the screen) ---
            EnsureJoystick(canvas);

            // --- Survival bars ---
            var hud = canvas.gameObject.AddComponent<SurvivalStatsHUD>();
            hud.healthBar = StatBar(canvas.transform, "HealthBar", "Health", 0, new Color(0.82f, 0.18f, 0.15f), out var healthFill);
            hud.hungerBar = StatBar(canvas.transform, "HungerBar", "Hunger", 1, new Color(0.85f, 0.55f, 0.15f), out var hungerFill);
            hud.thirstBar = StatBar(canvas.transform, "ThirstBar", "Thirst", 2, new Color(0.20f, 0.50f, 0.90f), out var thirstFill);
            hud.energyBar = StatBar(canvas.transform, "EnergyBar", "Energy", 3, new Color(0.90f, 0.85f, 0.20f), out var energyFill);
            hud.staminaBar = StatBar(canvas.transform, "StaminaBar", "Stamina", 4, new Color(0.25f, 0.78f, 0.30f), out var staminaFill);
            hud.healthFill = healthFill; hud.hungerFill = hungerFill; hud.thirstFill = thirstFill;
            hud.energyFill = energyFill; hud.staminaFill = staminaFill;

            // --- Clock / season ---
            var timePanel = UIFactory.Rect(canvas.transform, "TimePanel");
            timePanel.anchorMin = timePanel.anchorMax = new Vector2(1f, 1f);
            timePanel.pivot = new Vector2(1f, 1f);
            timePanel.anchoredPosition = new Vector2(-30, -25);
            timePanel.sizeDelta = new Vector2(320, 130);

            var ts = timePanel.gameObject.AddComponent<TimeSeasonHUD>();
            ts.timeText = UIFactory.Text(timePanel, "TimeText", "06:00", 40, new Vector2(0.5f, 0.78f), new Vector2(300, 50), Color.white);
            ts.dayText = UIFactory.Text(timePanel, "DayText", "Day 1", 28, new Vector2(0.5f, 0.48f), new Vector2(300, 40), new Color(0.85f, 0.82f, 0.72f));
            ts.seasonText = UIFactory.Text(timePanel, "SeasonText", "Spring", 28, new Vector2(0.5f, 0.20f), new Vector2(300, 40), new Color(0.85f, 0.82f, 0.72f));

            // --- Location / biome readout ---
            var locationText = UIFactory.Text(canvas.transform, "LocationText", "", 26,
                new Vector2(0.5f, 0.975f), new Vector2(1200, 44), new Color(0.92f, 0.88f, 0.75f));
            var readout = canvas.gameObject.AddComponent<LocationReadout>();
            readout.label = locationText;

            // --- Compass ---
            var compassGO = UIFactory.Rect(canvas.transform, "CompassHUD");
            UIFactory.Anchor(compassGO, new Vector2(0.5f, 0.90f), new Vector2(360, 70));
            var container = UIFactory.Panel(compassGO, "Container", new Color(0f, 0f, 0f, 0.45f));
            UIFactory.Stretch(container);
            var arrow = UIFactory.Rect(container.transform, "Arrow");
            UIFactory.Anchor(arrow, new Vector2(0.12f, 0.5f), new Vector2(40, 40));
            var arrowImg = arrow.gameObject.AddComponent<Image>();
            arrowImg.sprite = UIFactory.CircleSprite(64);
            arrowImg.color = new Color(1f, 0.85f, 0.25f);
            var distance = UIFactory.Text(container.transform, "DistanceText", "--- m", 28,
                new Vector2(0.58f, 0.5f), new Vector2(240, 50), Color.white);

            var compass = compassGO.gameObject.AddComponent<CompassHUD>();
            compass.arrow = arrow;
            compass.distanceText = distance;
            compass.compassContainer = container.gameObject;

            // --- Buttons: map + pause ---
            UIFactory.Button(canvas.transform, "MapButton", "MAP", new Vector2(0.5f, 0.5f),
                new Vector2(150, 90), OpenMap).GetComponent<RectTransform>()
                .SetAnchored(new Vector2(1f, 0f), new Vector2(-120, 110));

            // NOTE: keep the label ASCII — TMP's default LiberationSans SDF atlas has
            // no glyph for U+275A (❚), which logged "character not found" warnings.
            UIFactory.Button(canvas.transform, "PauseButton", "II", new Vector2(0.5f, 0.5f),
                new Vector2(110, 90), TogglePause).GetComponent<RectTransform>()
                .SetAnchored(new Vector2(1f, 1f), new Vector2(-390, -70));

            // --- Tooltip ---
            var tooltipRT = UIFactory.Rect(canvas.transform, "TooltipPanel");
            UIFactory.Anchor(tooltipRT, new Vector2(0.5f, 0.16f), new Vector2(520, 120));
            var tooltipBG = tooltipRT.gameObject.AddComponent<Image>();
            tooltipBG.color = new Color(0f, 0f, 0f, 0.78f);
            var tooltipText = UIFactory.Text(tooltipRT, "TooltipText", "", 24,
                new Vector2(0.5f, 0.5f), new Vector2(500, 110), Color.white);
            var tooltip = canvas.gameObject.AddComponent<TooltipUI>();
            tooltip.panel = tooltipRT;
            tooltip.text = tooltipText;
            tooltipRT.gameObject.SetActive(false);

            // Inventory, crafting and a single context-aware mobile action button.
            var interactionUI = canvas.gameObject.AddComponent<SurvivalInteractionUI>();
            interactionUI.Build(canvas);
            var onboarding = canvas.gameObject.AddComponent<OnboardingAndAccessibilityUI>();
            onboarding.Build(canvas);
        }

        private void EnsureJoystick(Canvas canvas)
        {
            if (FindFirstObjectByType<MobileJoystick>() != null) return;
            if (canvas == null)
            {
                foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if (c.name == "GameCanvas") { canvas = c; break; }
            }
            if (canvas == null) canvas = UIFactory.Canvas("GameCanvas", 100);

            // Full-screen invisible touch area so the stick can appear anywhere on the left.
            var areaRT = UIFactory.Rect(canvas.transform, "JoystickArea");
            UIFactory.Stretch(areaRT);
            areaRT.SetAsFirstSibling();
            var areaImg = areaRT.gameObject.AddComponent<Image>();
            areaImg.color = new Color(0f, 0f, 0f, 0f);

            var ringRT = UIFactory.Rect(areaRT, "Ring");
            ringRT.sizeDelta = new Vector2(300, 300);
            var ringImg = ringRT.gameObject.AddComponent<Image>();
            ringImg.sprite = UIFactory.CircleSprite(256, 18f);
            ringImg.color = new Color(1f, 1f, 1f, 0.35f);
            ringImg.raycastTarget = false;
            var group = ringRT.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            var knobRT = UIFactory.Rect(ringRT, "Knob");
            knobRT.sizeDelta = new Vector2(120, 120);
            var knobImg = knobRT.gameObject.AddComponent<Image>();
            knobImg.sprite = UIFactory.CircleSprite(128);
            knobImg.color = new Color(1f, 0.95f, 0.85f, 0.65f);
            knobImg.raycastTarget = false;

            _joystick = areaRT.gameObject.AddComponent<MobileJoystick>();
            _joystick.background = ringRT;
            _joystick.knob = knobRT;
            _joystick.visuals = group;
            _joystick.maxRadius = 150f;

            var controller = FindFirstObjectByType<PlayerController>();
            if (controller != null) controller.joystick = _joystick;
        }

        private Slider StatBar(Transform parent, string name, string label, int row, Color color, out Image fill)
        {
            var rt = UIFactory.Rect(parent, name);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(340, 34);
            rt.anchoredPosition = new Vector2(30, -25 - row * 44);

            var slider = UIFactory.ProgressBar(rt, "Bar", new Vector2(0.5f, 0.5f), new Vector2(340, 34), color);
            UIFactory.Stretch(slider.GetComponent<RectTransform>());

            UIFactory.Text(rt, "Label", label, 20, new Vector2(0.5f, 0.5f), new Vector2(320, 30),
                new Color(1f, 1f, 1f, 0.9f));

            fill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
            return slider;
        }

        private void OpenMap()
        {
            var map = FindFirstObjectByType<WorldMapUI>();
            if (map != null) map.Toggle();
        }

        private void TogglePause()
        {
            var pause = FindFirstObjectByType<PauseMenuUI>();
            if (pause != null) pause.Toggle();
        }
    }

    /// <summary>Shows latitude/longitude, region and biome at the top of the screen.</summary>
    public class LocationReadout : MonoBehaviour
    {
        public TextMeshProUGUI label;
        private Transform _player;
        private float _timer;

        private void Update()
        {
            if (label == null) return;
            _timer += Time.deltaTime;
            if (_timer < 0.4f) return;
            _timer = 0f;

            if (_player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p == null) return;
                _player = p.transform;
            }

            var map = WorldMap.Instance;
            if (map == null) return;

            int x = Mathf.FloorToInt(_player.position.x);
            int y = Mathf.FloorToInt(_player.position.y);
            var sample = map.Sample(x, y);
            label.text = $"{map.DescribePosition(x, y)}  ·  {WorldMap.BiomeName(sample.biome)}  ·  {sample.temperature:0}°C";
        }
    }

    /// <summary>Small RectTransform helper used by the bootstrapper.</summary>
    public static class RectTransformExtensions
    {
        public static void SetAnchored(this RectTransform rt, Vector2 anchor, Vector2 offset)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset;
        }
    }
}
