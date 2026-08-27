using System;
using System.Collections.Generic;
using UnityEngine;
using PrehistoricSurvival.Core;

namespace PrehistoricSurvival.Environment
{
    [Serializable]
    public class BuildingDefinition
    {
        public string id;
        public string displayName;
        public Color color;
        public Vector2 size = Vector2.one;
        public string[] costItems;
        public int[] costAmounts;
    }

    /// <summary>Grid-snapped, validated, saveable mobile building placement.</summary>
    public class BuildingPlacementSystem : MonoBehaviour
    {
        public static BuildingPlacementSystem Instance { get; private set; }
        public float gridSize = 1f;
        public float placementRange = 8f;
        public List<BuildingDefinition> buildings = new List<BuildingDefinition>();
        public bool buildMode;
        public int selectedIndex;

        private GameObject _preview;
        private Sprite _solidSprite;
        private readonly List<GameObject> _placed = new List<GameObject>();

        public BuildingDefinition Selected => buildings.Count == 0 ? null : buildings[Mathf.Clamp(selectedIndex, 0, buildings.Count - 1)];
        public string SelectedName => Selected == null ? "BUILD" : Selected.displayName;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (buildings.Count == 0) CreateDefaultBuildings();
            _solidSprite = PrehistoricSurvival.World.ChunkManager.SolidSprite(Color.white);
            LoadPlacedBuildings();
        }

        private void Update()
        {
            if (!buildMode) return;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null || Camera.main == null) return;
            Vector3 point = Camera.main.ScreenToWorldPoint(Input.mousePosition); point.z = 0f;
            UpdatePreview(point, player.transform.position);
            if (Input.GetMouseButtonDown(0)) PlaceAt(point);
            if (Input.GetKeyDown(KeyCode.Escape)) SetBuildMode(false);
        }

        public void SetBuildMode(bool enabled)
        {
            buildMode = enabled;
            if (!enabled && _preview != null) { Destroy(_preview); _preview = null; }
        }

        public void SelectNext() { if (buildings.Count > 0) selectedIndex = (selectedIndex + 1) % buildings.Count; }

        public bool CanPlace(Vector3 position)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            var definition = Selected;
            if (player == null || definition == null || Vector2.Distance(player.transform.position, position) > placementRange) return false;
            var center = Snap(position);
            var hits = Physics2D.OverlapBoxAll(center, definition.size * .9f, 0f);
            foreach (var hit in hits) if (hit.GetComponent<BuildingMarker>() != null) return false;
            if (InventorySystem.Instance == null) return false;
            for (int i = 0; i < definition.costItems.Length; i++) if (!InventorySystem.Instance.HasItem(definition.costItems[i], definition.costAmounts[i])) return false;
            return true;
        }

        public bool PlaceAt(Vector3 position)
        {
            var definition = Selected; if (!CanPlace(position)) return false;
            for (int i = 0; i < definition.costItems.Length; i++) InventorySystem.Instance.RemoveItemById(definition.costItems[i], definition.costAmounts[i]);
            var go = new GameObject(definition.displayName);
            go.transform.position = Snap(position);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = _solidSprite; sr.color = definition.color; sr.sortingOrder = 2;
            go.transform.localScale = new Vector3(definition.size.x, definition.size.y, 1f);
            var collider = go.AddComponent<BoxCollider2D>(); collider.size = Vector2.one;
            go.AddComponent<BuildingMarker>().buildingId = definition.id;
            _placed.Add(go); SavePlacedBuildings();
            EventManager.TriggerEvent(GameEvents.BuildingPlaced, go);
            return true;
        }

        private Vector3 Snap(Vector3 position) => new Vector3(Mathf.Round(position.x / gridSize) * gridSize, Mathf.Round(position.y / gridSize) * gridSize, 0f);
        private void UpdatePreview(Vector3 position, Vector3 playerPosition)
        {
            if (_preview == null) { _preview = new GameObject("BuildingPreview"); var sr = _preview.AddComponent<SpriteRenderer>(); sr.sprite = _solidSprite; sr.sortingOrder = 10; }
            var definition = Selected; if (definition == null) return;
            _preview.transform.position = Snap(position); _preview.transform.localScale = new Vector3(definition.size.x, definition.size.y, 1f);
            _preview.GetComponent<SpriteRenderer>().color = CanPlace(position) ? new Color(0.3f, 1f, .35f, .55f) : new Color(1f, .2f, .15f, .55f);
        }

        private void CreateDefaultBuildings()
        {
            buildings.Add(new BuildingDefinition { id = "shelter", displayName = "SHELTER", color = new Color(.45f,.25f,.12f), size = new Vector2(3,2), costItems = new[] { "wood_log" }, costAmounts = new[] { 8 } });
            buildings.Add(new BuildingDefinition { id = "campfire", displayName = "CAMPFIRE", color = new Color(1f,.35f,.08f), size = new Vector2(1.5f,1.5f), costItems = new[] { "wood_log", "stone" }, costAmounts = new[] { 3, 3 } });
            buildings.Add(new BuildingDefinition { id = "storage", displayName = "STORAGE", color = new Color(.28f,.18f,.10f), size = new Vector2(2,1.5f), costItems = new[] { "wood_log" }, costAmounts = new[] { 5 } });
        }

        private void SavePlacedBuildings()
        {
            var data = new List<PlacedBuildingData>(); foreach (var go in _placed) if (go != null) data.Add(new PlacedBuildingData { id = go.GetComponent<BuildingMarker>().buildingId, position = go.transform.position });
            PlayerPrefs.SetString("placed_buildings", JsonUtility.ToJson(new BuildingSaveData { buildings = data })); PlayerPrefs.Save();
        }
        private void LoadPlacedBuildings()
        {
            var json = PlayerPrefs.GetString("placed_buildings", ""); if (string.IsNullOrEmpty(json)) return;
            var data = JsonUtility.FromJson<BuildingSaveData>(json); if (data == null || data.buildings == null) return;
            foreach (var item in data.buildings) { int index = buildings.FindIndex(b => b.id == item.id); if (index >= 0) { selectedIndex = index; PlaceAtWithoutCost(item.position); } }
        }
        private void PlaceAtWithoutCost(Vector3 position)
        {
            var d = Selected; var go = new GameObject(d.displayName); go.transform.position = position; var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = _solidSprite; sr.color = d.color; sr.sortingOrder = 2; go.transform.localScale = new Vector3(d.size.x,d.size.y,1); go.AddComponent<BoxCollider2D>(); go.AddComponent<BuildingMarker>().buildingId = d.id; _placed.Add(go);
        }
    }
    public class BuildingMarker : MonoBehaviour { public string buildingId; }
    [Serializable] public class PlacedBuildingData { public string id; public Vector3 position; }
    [Serializable] public class BuildingSaveData { public List<PlacedBuildingData> buildings; }
}
