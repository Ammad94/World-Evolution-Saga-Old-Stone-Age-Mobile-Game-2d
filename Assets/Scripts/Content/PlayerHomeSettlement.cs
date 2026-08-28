using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using PrehistoricSurvival.Core;
using PrehistoricSurvival.Lighting;
using PrehistoricSurvival.Player;
using PrehistoricSurvival.Survival;
using PrehistoricSurvival.World;

namespace PrehistoricSurvival.Content
{
    /// <summary>
    /// Spawns and manages the player's starting Home Settlement / Cave Base.
    ///
    /// When starting a new game, the player spawns right outside their home cave / shelter,
    /// complete with:
    /// - A Home Cave / Rock Shelter with warmth & storm protection
    /// - A lit, crackling Campfire for cooking and nighttime warmth
    /// - A Crafting Workbench
    /// - Starter survival supplies (torch, flint knife, stone axe, water skin, food)
    /// - A "Home Cave" map waypoint so the compass always guides the player home
    /// - Surrounding trees, flint rocks, and stone clusters
    /// All structures and props are billboarded in the 2.5D perspective diorama view.
    /// </summary>
    public class PlayerHomeSettlement : MonoBehaviour
    {
        public static PlayerHomeSettlement Instance { get; private set; }

        [Header("Spawn Settings")]
        [Tooltip("True to automatically create the home cave settlement on new game.")]
        public bool spawnOnNewGame = true;

        [Header("References")]
        public Transform settlementRoot;
        public GameObject caveObject;
        public GameObject campfireObject;
        public GameObject workbenchObject;

        private const string SPRITE_PATH = "Assets/Sprites/";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Creates the player's home cave settlement at the specified world position.
        /// </summary>
        public static void CreateAt(Vector3 spawnPos)
        {
            var go = new GameObject("PlayerHomeSettlement");
            var settlement = go.AddComponent<PlayerHomeSettlement>();
            settlement.BuildSettlement(spawnPos);
        }

        public void BuildSettlement(Vector3 pos)
        {
            if (settlementRoot != null) return; // already created

            var root = new GameObject("HomeSettlement_Root");
            root.transform.position = pos;
            settlementRoot = root.transform;

            // 1. Home Cave / Shelter
            BuildHomeCave(pos + new Vector3(0f, 2.2f, 0f), root.transform);

            // 2. Lit Campfire
            BuildCampfire(pos + new Vector3(1.8f, -0.3f, 0f), root.transform);

            // 3. Crafting Workbench
            BuildWorkbench(pos + new Vector3(-2.0f, 0.2f, 0f), root.transform);

            // 4. Natural Rocks & Trees around the cave clearing
            BuildEnvironmentClearing(pos, root.transform);

            // 5. Starter Survival Items in Inventory
            GrantStarterSupplies();

            // 6. Set Home Waypoint on Map & Compass
            RegisterHomeWaypoint(pos);

            Debug.Log($"[PlayerHomeSettlement] Player Home Cave base built at {pos}");
        }

        private void BuildHomeCave(Vector3 pos, Transform parent)
        {
            var cave = new GameObject("HomeCave");
            cave.transform.position = pos;
            cave.transform.SetParent(parent, true);

            var sr = cave.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite("Terrain/Mountain/cave_entrance")
                     ?? LoadSprite("Structures/hut")
                     ?? LoadSprite("Structures/tent");
            sr.sortingOrder = ChunkManager.SortingOrderFor(pos.y);

            cave.transform.localScale = new Vector3(1.6f, 1.6f, 1f);

            // Trigger area for shelter warmth
            var col = cave.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(3.0f, 2.2f);
            col.offset = new Vector2(0f, -0.2f);

            cave.AddComponent<HomeCaveShelterTrigger>();

            // Auto-billboard to 2.5D perspective camera
            Fake3D.Ensure(cave);
            caveObject = cave;
        }

        private void BuildCampfire(Vector3 pos, Transform parent)
        {
            var fire = new GameObject("HomeCampfire");
            fire.transform.position = pos;
            fire.transform.SetParent(parent, true);

            var sr = fire.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite("Items/campfire") ?? LoadSprite("Structures/campfire");
            sr.sortingOrder = ChunkManager.SortingOrderFor(pos.y);
            fire.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

            var col = fire.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.8f;

            // Crafting station trigger
            fire.AddComponent<PrehistoricSurvival.Crafting.CraftingStationTrigger>();

            // Light & warmth
            var lightGO = new GameObject("FireLight");
            lightGO.transform.SetParent(fire.transform, false);
            var light2D = lightGO.AddComponent<Light2D>();
            light2D.lightType = Light2D.LightType.Point;
            light2D.color = new Color(1f, 0.68f, 0.28f);
            light2D.intensity = 1.3f;
            light2D.pointLightOuterRadius = 8.5f;
            light2D.pointLightInnerRadius = 1.5f;

            var torch = fire.AddComponent<TorchLight>();
            torch.type = TorchLight.LightType.Campfire;
            torch.baseIntensity = 1.3f;
            torch.baseRadius = 8.5f;
            torch.usesFuel = false; // Starter home fire is kept lit

            Fake3D.Ensure(fire);
            campfireObject = fire;
        }

        private void BuildWorkbench(Vector3 pos, Transform parent)
        {
            var bench = new GameObject("HomeWorkbench");
            bench.transform.position = pos;
            bench.transform.SetParent(parent, true);

            var sr = bench.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite("Structures/workbench") ?? LoadSprite("Items/workbench");
            sr.sortingOrder = ChunkManager.SortingOrderFor(pos.y);
            bench.transform.localScale = new Vector3(1.1f, 1.1f, 1f);

            var col = bench.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.5f, 1.2f);

            bench.AddComponent<PrehistoricSurvival.Crafting.CraftingStationTrigger>();

            Fake3D.Ensure(bench);
            workbenchObject = bench;
        }

        private void BuildEnvironmentClearing(Vector3 center, Transform parent)
        {
            // Flint rock node nearby
            SpawnPropNode("FlintOutcrop", "Vegetation/Rocks/flint_outcrop",
                center + new Vector3(3.2f, 1.4f, 0f), 1.2f, parent);

            // Stone cluster nearby
            SpawnPropNode("StoneCluster", "Vegetation/Rocks/stone_cluster",
                center + new Vector3(-3.2f, 1.6f, 0f), 1.1f, parent);

            // Flanking trees
            SpawnPropNode("HomePineLeft", "Vegetation/Trees/pine_tree",
                center + new Vector3(-3.8f, 3.2f, 0f), 1.4f, parent);
            SpawnPropNode("HomePineRight", "Vegetation/Trees/pine_tree",
                center + new Vector3(3.8f, 3.2f, 0f), 1.4f, parent);

            // Berry bush near entrance
            SpawnPropNode("BerryBush", "Vegetation/Bushes/berry_bush",
                center + new Vector3(-1.4f, -1.2f, 0f), 1.0f, parent);
        }

        private void SpawnPropNode(string name, string spritePath, Vector3 pos, float scale, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.transform.SetParent(parent, true);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite(spritePath);
            if (sr.sprite != null)
            {
                sr.sortingOrder = ChunkManager.SortingOrderFor(pos.y);
                go.transform.localScale = new Vector3(scale, scale, 1f);
                var col = go.AddComponent<BoxCollider2D>();
                col.size = new Vector2(1.2f, 1.2f);
                Fake3D.Ensure(go);
            }
            else
            {
                Destroy(go);
            }
        }

        private void GrantStarterSupplies()
        {
            var inv = InventorySystem.Instance;
            if (inv == null) return;

            // Starter survival gear: torch, flint shard, stone axe, cooked meat, water skin
            TryAddItem(inv, "torch", "Torch", 1);
            TryAddItem(inv, "stone_axe", "Stone Axe", 1);
            TryAddItem(inv, "flint_shard", "Flint Shard", 3);
            TryAddItem(inv, "cooked_meat", "Cooked Meat", 2);
            TryAddItem(inv, "water_skin", "Water Skin", 1);
            TryAddItem(inv, "berries", "Berries", 4);
        }

        private void TryAddItem(InventorySystem inv, string itemId, string displayName, int amount)
        {
            var lib = GameLibrary.Instance;
            ItemData data = null;
            if (lib != null && lib.allItems != null)
            {
                foreach (var item in lib.allItems)
                {
                    if (item != null && item.id == itemId) { data = item; break; }
                }
            }

            if (data == null)
            {
                data = new ItemData
                {
                    id = itemId,
                    displayName = displayName,
                    category = ItemCategory.Tool,
                    isStackable = true,
                    icon = LoadSprite("Items/" + itemId)
                };
            }

            inv.AddItem(data, amount);
        }

        private void RegisterHomeWaypoint(Vector3 pos)
        {
            var waypoints = WaypointManager.Instance;
            if (waypoints != null)
            {
                waypoints.AddWaypoint(pos, "Home Cave", WaypointType.Custom);
            }
        }

        private static Sprite LoadSprite(string path)
        {
            // Try Resources first
            var spr = Resources.Load<Sprite>("Sprites/" + path);
            if (spr != null) return spr;
            spr = Resources.Load<Sprite>(path);
            if (spr != null) return spr;

#if UNITY_EDITOR
            string full = SPRITE_PATH + path + ".png";
            spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(full);
            if (spr != null) return spr;
            full = SPRITE_PATH + path;
            spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(full);
            if (spr != null) return spr;
#endif
            return null;
        }
    }

    /// <summary>Provides shelter warmth & comfort when player is inside the home cave.</summary>
    public class HomeCaveShelterTrigger : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            var temp = other.GetComponent<TemperatureSystem>();
            if (temp != null) temp.shelterWarmth = 8f;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            var temp = other.GetComponent<TemperatureSystem>();
            if (temp != null) temp.shelterWarmth = 0f;
        }
    }
}
