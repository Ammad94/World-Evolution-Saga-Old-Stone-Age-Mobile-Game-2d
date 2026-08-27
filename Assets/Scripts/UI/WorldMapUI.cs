using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using PrehistoricSurvival.World;

namespace PrehistoricSurvival.UI
{
    /// <summary>
    /// Full-screen map of the entire planet.
    ///
    /// The whole earth is rendered into a texture once (all continents, oceans,
    /// deserts, jungles and ice caps), the player's position is drawn as a marker,
    /// and tapping anywhere drops a waypoint the compass will point to.
    /// </summary>
    public class WorldMapUI : MonoBehaviour, IPointerClickHandler
    {
        [Header("Rendering")]
        [Tooltip("Resolution of the planet overview texture.")]
        public int textureWidth = 1024;
        public int textureHeight = 512;

        [Header("Options")]
        public bool buildUIIfMissing = true;

        private GameObject _panel;
        private RawImage _mapImage;
        private RectTransform _mapRect;
        private RectTransform _playerMarker;
        private RectTransform _waypointMarker;
        private TextMeshProUGUI _infoText;
        private Texture2D _mapTexture;
        private Transform _player;
        private bool _open;

        public bool IsOpen => _open;

        private void Start()
        {
            UIFactory.EnsureEventSystem();
            if (buildUIIfMissing && _panel == null) Build();
            if (_panel != null) _panel.SetActive(false);
        }

        private void Update()
        {
            if (!_open) return;
            UpdateMarkers();
        }

        // ------------------------------------------------------------------
        public void Toggle()
        {
            if (_open) Close();
            else Open();
        }

        public void Open()
        {
            EnsureTexture();
            _open = true;
            if (_panel != null) _panel.SetActive(true);
            Time.timeScale = 0f;
            UpdateMarkers();
        }

        public void Close()
        {
            _open = false;
            if (_panel != null) _panel.SetActive(false);
            var gm = Core.GameManager.Instance;
            if (gm == null || !gm.IsPaused) Time.timeScale = 1f;
        }

        // ------------------------------------------------------------------
        private void EnsureTexture()
        {
            if (_mapTexture != null) return;
            var map = WorldMap.Instance != null ? WorldMap.Instance : WorldMap.EnsureExists();
            _mapTexture = map.GenerateOverviewTexture(textureWidth, textureHeight);
            if (_mapImage != null) _mapImage.texture = _mapTexture;
        }

        private void UpdateMarkers()
        {
            var map = WorldMap.Instance;
            if (map == null || _mapRect == null) return;

            if (_player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) _player = p.transform;
            }
            if (_player == null) return;

            int tx = map.WrapX(Mathf.FloorToInt(_player.position.x));
            int ty = map.ClampY(Mathf.FloorToInt(_player.position.y));
            Vector2 uv = new Vector2(tx / (float)map.worldWidth, ty / (float)map.worldHeight);

            if (_playerMarker != null)
            {
                _playerMarker.anchorMin = _playerMarker.anchorMax = uv;
                _playerMarker.anchoredPosition = Vector2.zero;
            }

            var wp = WaypointManager.Instance != null ? WaypointManager.Instance.ActiveWaypoint : null;
            if (_waypointMarker != null)
            {
                bool has = wp != null;
                _waypointMarker.gameObject.SetActive(has);
                if (has)
                {
                    Vector2 wuv = new Vector2(
                        map.WrapX(Mathf.FloorToInt(wp.position.x)) / (float)map.worldWidth,
                        map.ClampY(Mathf.FloorToInt(wp.position.y)) / (float)map.worldHeight);
                    _waypointMarker.anchorMin = _waypointMarker.anchorMax = wuv;
                    _waypointMarker.anchoredPosition = Vector2.zero;
                }
            }

            if (_infoText != null)
            {
                var sample = map.Sample(tx, ty);
                float distance = wp != null
                    ? Vector2.Distance(new Vector2(_player.position.x, _player.position.y),
                                       new Vector2(wp.position.x, wp.position.y))
                    : -1f;
                string wpLine = distance >= 0f
                    ? $"   |   Waypoint: {distance * map.kilometresPerTile:0.0} km"
                    : "   |   Tap the map to set a waypoint";
                _infoText.text = $"{map.DescribePosition(tx, ty)}   |   {WorldMap.BiomeName(sample.biome)}   |   " +
                                 $"{sample.temperature:0}°C{wpLine}";
            }
        }

        // ------------------------------------------------------------------
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_open || _mapRect == null) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _mapRect, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;

            Rect r = _mapRect.rect;
            float u = Mathf.Clamp01((local.x - r.xMin) / r.width);
            float v = Mathf.Clamp01((local.y - r.yMin) / r.height);

            var map = WorldMap.Instance;
            if (map == null) return;

            var target = new Vector3(u * map.worldWidth, v * map.worldHeight, 0f);
            if (WaypointManager.Instance != null)
                WaypointManager.Instance.AddWaypoint(target, map.GetRegionName((int)target.x, (int)target.y));

            UpdateMarkers();
        }

        // ------------------------------------------------------------------
        private void Build()
        {
            var canvas = UIFactory.Canvas("WorldMapCanvas", 800);
            canvas.transform.SetParent(transform, false);

            var panel = UIFactory.Panel(canvas.transform, "MapPanel", new Color(0.04f, 0.035f, 0.03f, 0.98f));
            UIFactory.Stretch(panel);
            _panel = panel.gameObject;

            UIFactory.Text(_panel.transform, "Header", "WORLD MAP", 48,
                new Vector2(0.5f, 0.94f), new Vector2(900, 70), UIFactory.Parchment);

            // Map image (2:1 aspect, like a real world map).
            var mapGO = UIFactory.Rect(_panel.transform, "Map");
            mapGO.anchorMin = new Vector2(0.5f, 0.5f);
            mapGO.anchorMax = new Vector2(0.5f, 0.5f);
            mapGO.sizeDelta = new Vector2(1600, 800);
            mapGO.anchoredPosition = new Vector2(0, 20);
            _mapRect = mapGO;

            _mapImage = mapGO.gameObject.AddComponent<RawImage>();
            _mapImage.raycastTarget = true;
            EnsureTexture();
            _mapImage.texture = _mapTexture;

            // Click handling on the map itself.
            var clickRelay = mapGO.gameObject.AddComponent<PointerClickRelay>();
            clickRelay.target = this;

            _playerMarker = MakeMarker(mapGO, "PlayerMarker", new Color(1f, 0.25f, 0.15f), 22);
            _waypointMarker = MakeMarker(mapGO, "WaypointMarker", new Color(1f, 0.85f, 0.2f), 18);
            _waypointMarker.gameObject.SetActive(false);

            _infoText = UIFactory.Text(_panel.transform, "Info", "", 26,
                new Vector2(0.5f, 0.10f), new Vector2(1700, 50), Color.white);

            UIFactory.Button(_panel.transform, "CloseMap", "CLOSE",
                new Vector2(0.5f, 0.045f), new Vector2(320, 74), Close, null, 28);
        }

        private static RectTransform MakeMarker(Transform parent, string name, Color color, float size)
        {
            var rt = UIFactory.Rect(parent, name);
            rt.sizeDelta = new Vector2(size, size);
            rt.pivot = new Vector2(0.5f, 0.5f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UIFactory.CircleSprite(64);
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }
    }

    /// <summary>Forwards clicks on the map image to the WorldMapUI.</summary>
    public class PointerClickRelay : MonoBehaviour, IPointerClickHandler
    {
        [System.NonSerialized] public WorldMapUI target;
        public void OnPointerClick(PointerEventData eventData)
        {
            if (target != null) target.OnPointerClick(eventData);
        }
    }
}
