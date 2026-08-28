using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PrehistoricSurvival.Core;
using PrehistoricSurvival.Crafting;
using PrehistoricSurvival.Environment;

namespace PrehistoricSurvival.UI
{
    /// <summary>Runtime mobile inventory, crafting, hotbar and contextual action interface.</summary>
    public class SurvivalInteractionUI : MonoBehaviour
    {
        public int hotbarSize = 8;
        private Canvas _canvas;
        private GameObject _inventoryPanel;
        private GameObject _craftingPanel;
        private Transform _inventoryGrid;
        private Transform _recipeList;
        private TextMeshProUGUI _weightText;
        private TextMeshProUGUI _actionLabel;
        private Button _actionButton;
        private InventorySystem _inventory;
        private CraftingSystem _crafting;
        private readonly List<GameObject> _dynamicRows = new List<GameObject>();

        public void Build(Canvas canvas)
        {
            if (canvas == null || _canvas != null) return;
            _canvas = canvas;
            _inventory = InventorySystem.Instance;
            _crafting = CraftingSystem.Instance;
            UIFactory.Button(canvas.transform, "InventoryButton", "BAG", new Vector2(0f, 0f), new Vector2(150, 76), ToggleInventory, UIFactory.Bark, 26)
                .GetComponent<RectTransform>().SetAnchored(new Vector2(0f, 0f), new Vector2(105, 65));
            UIFactory.Button(canvas.transform, "CraftingButton", "CRAFT", new Vector2(0f, 0f), new Vector2(150, 76), ToggleCrafting, UIFactory.Bark, 24)
                .GetComponent<RectTransform>().SetAnchored(new Vector2(0f, 0f), new Vector2(275, 65));
            UIFactory.Button(canvas.transform, "BuildButton", "BUILD", new Vector2(0f, 0f), new Vector2(150, 76), ToggleBuild, UIFactory.Bark, 24)
                .GetComponent<RectTransform>().SetAnchored(new Vector2(0f, 0f), new Vector2(445, 65));

            _actionButton = UIFactory.Button(canvas.transform, "ActionButton", "", new Vector2(1f, 0f), new Vector2(240, 110), PerformAction, UIFactory.Ember, 28);
            _actionButton.GetComponent<RectTransform>().SetAnchored(new Vector2(1f, 0f), new Vector2(-175, 120));
            _actionLabel = _actionButton.GetComponentInChildren<TextMeshProUGUI>();

            BuildInventoryPanel();
            BuildCraftingPanel();
            _inventory.OnInventoryChanged += RefreshInventory;
            RefreshInventory();
            RefreshAction();
        }

        private void Update()
        {
            if (_canvas == null) return;
            if (Input.GetKeyDown(KeyCode.I)) ToggleInventory();
            if (Input.GetKeyDown(KeyCode.C)) ToggleCrafting();
            RefreshAction();
        }

        private void BuildInventoryPanel()
        {
            var bg = UIFactory.Panel(_canvas.transform, "InventoryPanel", new Color(0.10f, 0.075f, 0.05f, 0.98f));
            _inventoryPanel = bg.gameObject;
            UIFactory.Anchor(bg.rectTransform, new Vector2(0.5f, 0.52f), new Vector2(930, 650));
            UIFactory.Text(bg.transform, "Header", "INVENTORY", 42, new Vector2(0.5f, 0.92f), new Vector2(800, 55), UIFactory.Parchment);
            _weightText = UIFactory.Text(bg.transform, "Weight", "", 22, new Vector2(0.5f, 0.84f), new Vector2(800, 36), new Color(0.78f, 0.72f, 0.62f));
            _inventoryGrid = UIFactory.Rect(bg.transform, "Grid");
            _inventoryGrid.gameObject.AddComponent<GridLayoutGroup>().cellSize = new Vector2(155, 112);
            var grid = _inventoryGrid.GetComponent<GridLayoutGroup>();
            grid.spacing = new Vector2(12, 12); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 5;
            UIFactory.Anchor(_inventoryGrid.GetComponent<RectTransform>(), new Vector2(0.5f, 0.47f), new Vector2(835, 500));
            UIFactory.Button(bg.transform, "Close", "CLOSE", new Vector2(0.5f, 0.08f), new Vector2(230, 60), ToggleInventory, UIFactory.Bark, 22);
            _inventoryPanel.SetActive(false);
        }

        private void BuildCraftingPanel()
        {
            var bg = UIFactory.Panel(_canvas.transform, "CraftingPanel", new Color(0.10f, 0.075f, 0.05f, 0.98f));
            _craftingPanel = bg.gameObject;
            UIFactory.Anchor(bg.rectTransform, new Vector2(0.5f, 0.52f), new Vector2(980, 680));
            UIFactory.Text(bg.transform, "Header", "CRAFTING", 42, new Vector2(0.5f, 0.92f), new Vector2(800, 55), UIFactory.Parchment);
            _recipeList = UIFactory.Rect(bg.transform, "RecipeList");
            UIFactory.Anchor(_recipeList.GetComponent<RectTransform>(), new Vector2(0.5f, 0.49f), new Vector2(860, 520));
            var layout = _recipeList.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            UIFactory.Button(bg.transform, "Close", "CLOSE", new Vector2(0.5f, 0.08f), new Vector2(230, 60), ToggleCrafting, UIFactory.Bark, 22);
            _craftingPanel.SetActive(false);
        }

        private void RefreshInventory()
        {
            if (_inventoryGrid == null || _inventory == null) return;
            foreach (Transform child in _inventoryGrid) Destroy(child.gameObject);
            for (int i = 0; i < _inventory.Slots.Count; i++)
            {
                var slot = _inventory.Slots[i];
                string label = slot.IsEmpty ? "EMPTY" : slot.item.displayName + "\n x" + slot.quantity;
                var button = UIFactory.Button(_inventoryGrid, "Slot" + i, label, new Vector2(.5f, .5f), new Vector2(155, 112), null, new Color(.20f, .14f, .09f, .95f), 18);
                var rt = button.GetComponent<RectTransform>(); rt.localScale = Vector3.one;
                if (!slot.IsEmpty && slot.item.icon != null)
                {
                    var image = rt.gameObject.AddComponent<Image>(); image.sprite = slot.item.icon; image.preserveAspect = true; image.color = Color.white; image.raycastTarget = false;
                    image.rectTransform.anchorMin = new Vector2(.08f,.20f); image.rectTransform.anchorMax = new Vector2(.32f,.82f); image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
                }
            }
            if (_weightText != null) _weightText.text = $"Carry weight: {_inventory.TotalWeight():0.0} / {(_inventory.maxCarryWeight <= 0 ? 999 : _inventory.maxCarryWeight):0.0} kg";
        }

        private void RefreshCrafting()
        {
            if (_recipeList == null) return;
            foreach (var row in _dynamicRows) if (row != null) Destroy(row);
            _dynamicRows.Clear();
            if (_crafting == null || _crafting.recipeDatabase == null) return;
            foreach (var recipe in _crafting.recipeDatabase.GetAllRecipes())
            {
                if (recipe == null) continue;
                var r = UIFactory.Button(_recipeList, recipe.recipeId, recipe.displayName, new Vector2(.5f,.5f), new Vector2(850, 70), () => _crafting.StartCraft(recipe), UIFactory.Bark, 24);
                r.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
                _dynamicRows.Add(r.gameObject);
            }
        }

        private void RefreshAction()
        {
            if (_actionButton == null) return;
            var player = GameObject.FindGameObjectWithTag("Player");
            var target = player == null ? null : FindObjectsByType<VegetationInteraction>(FindObjectsSortMode.None);
            VegetationInteraction nearest = null; float distance = 2.4f;
            if (player != null && target != null) foreach (var v in target)
            {
                float d = Vector2.Distance(player.transform.position, v.transform.position);
                if (d < distance && !v.IsDepleted) { distance = d; nearest = v; }
            }
            _actionButton.interactable = nearest != null;
            if (_actionLabel != null) _actionLabel.text = nearest == null ? "NO ACTION" : "HARVEST";
        }

        private void PerformAction()
        {
            var player = GameObject.FindGameObjectWithTag("Player"); if (player == null) return;
            VegetationInteraction nearest = null; float distance = 2.4f;
            foreach (var v in FindObjectsByType<VegetationInteraction>(FindObjectsSortMode.None)) { float d = Vector2.Distance(player.transform.position, v.transform.position); if (d < distance && !v.IsDepleted) { distance = d; nearest = v; } }
            if (nearest != null) nearest.Harvest();
        }

        private void ToggleInventory() { if (_inventoryPanel != null) { _inventoryPanel.SetActive(!_inventoryPanel.activeSelf); if (_inventoryPanel.activeSelf) RefreshInventory(); } }
        private void ToggleCrafting() { if (_craftingPanel != null) { _craftingPanel.SetActive(!_craftingPanel.activeSelf); if (_craftingPanel.activeSelf) RefreshCrafting(); } }
        private void ToggleBuild()
        {
            var builder = BuildingPlacementSystem.Instance;
            if (builder == null) return;
            if (builder.buildMode) builder.SelectNext();
            else builder.SetBuildMode(true);
            if (_actionLabel != null) _actionLabel.text = builder.SelectedName + " — TAP TO PLACE";
        }

        private void OnDestroy() { if (_inventory != null) _inventory.OnInventoryChanged -= RefreshInventory; }
    }
}
