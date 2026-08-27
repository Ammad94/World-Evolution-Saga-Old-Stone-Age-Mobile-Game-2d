using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Tilemaps;
using TMPro;
using System.IO;
using System.Collections.Generic;
using PrehistoricSurvival.Core;
using PrehistoricSurvival.Player;
using PrehistoricSurvival.World;
using PrehistoricSurvival.Environment;
using PrehistoricSurvival.Survival;
using PrehistoricSurvival.AI;
using PrehistoricSurvival.Crafting;
using PrehistoricSurvival.Traversal;
using PrehistoricSurvival.Water;
using PrehistoricSurvival.Lighting;
using PrehistoricSurvival.UI;

namespace PrehistoricSurvival.Editor
{
    /// <summary>
    /// Automated project setup tool. Run from Unity menu:
    /// PrehistoricSurvival → Setup Project (creates all prefabs, scenes, and ScriptableObjects).
    /// </summary>
    public class ProjectSetup : EditorWindow
    {
        private const string PREFAB_PATH = "Assets/Prefabs/";
        private const string SO_PATH = "Assets/ScriptableObjects/";
        private const string SCENE_PATH = "Assets/Scenes/";
        private const string SPRITE_PATH = "Assets/Sprites/";

        [MenuItem("PrehistoricSurvival/Setup Entire Project")]
        public static void SetupEntireProject()
        {
            if (!EditorUtility.DisplayDialog("Setup Project",
                "This will create all prefabs, scenes, and ScriptableObjects.\n\nProceed?",
                "Yes", "Cancel"))
                return;

            EditorUtility.DisplayProgressBar("Setting up...", "Creating directories", 0.05f);
            CreateDirectories();

            EditorUtility.DisplayProgressBar("Setting up...", "Creating ScriptableObjects", 0.15f);
            CreateAllScriptableObjects();

            EditorUtility.DisplayProgressBar("Setting up...", "Creating prefabs", 0.30f);
            CreateAllPrefabs();

            EditorUtility.DisplayProgressBar("Setting up...", "Creating MainMenu scene", 0.60f);
            CreateMainMenuScene();

            EditorUtility.DisplayProgressBar("Setting up...", "Creating GameplayWorld scene", 0.75f);
            CreateGameplayWorldScene();

            EditorUtility.DisplayProgressBar("Setting up...", "Configuring tags and layers", 0.90f);
            ConfigureTagsAndLayers();

            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("✅ [ProjectSetup] Project setup complete! Open Assets/Scenes/GameplayWorld.unity to play.");
            EditorUtility.DisplayDialog("Setup Complete",
                "All prefabs, scenes, and ScriptableObjects created!\n\n" +
                "Open Assets/Scenes/GameplayWorld.unity to start playing.\n" +
                "Open Assets/Scenes/MainMenu.unity for the main menu.",
                "OK");
        }

        [MenuItem("PrehistoricSurvival/Create Prefabs Only")]
        public static void CreatePrefabsOnly()
        {
            CreateDirectories();
            CreateAllPrefabs();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("✅ [ProjectSetup] All prefabs created.");
        }

        [MenuItem("PrehistoricSurvival/Create Scenes Only")]
        public static void CreateScenesOnly()
        {
            CreateDirectories();
            CreateMainMenuScene();
            CreateGameplayWorldScene();
            AssetDatabase.SaveAssets();
            Debug.Log("✅ [ProjectSetup] All scenes created.");
        }

        // ==================================================================
        // DIRECTORY SETUP
        // ==================================================================
        private static void CreateDirectories()
        {
            string[] dirs = {
                "Assets/Prefabs/Player",
                "Assets/Prefabs/Animals",
                "Assets/Prefabs/Vegetation",
                "Assets/Prefabs/Terrain",
                "Assets/Prefabs/Items",
                "Assets/Prefabs/UI",
                "Assets/Prefabs/Structures",
                "Assets/ScriptableObjects/Items",
                "Assets/ScriptableObjects/Recipes",
                "Assets/Scenes",
                "Assets/Materials",
                "Assets/Tilemaps"
            };
            foreach (var d in dirs) EnsureFolder(d);
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace("\\", "/");
                string folderName = Path.GetFileName(path);
                if (!AssetDatabase.IsValidFolder(parent))
                    EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        // ==================================================================
        // HELPER: Load Sprite
        // ==================================================================
        private static Sprite LoadSprite(string relativePath)
        {
            string fullPath = SPRITE_PATH + relativePath;
            Sprite spr = AssetDatabase.LoadAssetAtPath<Sprite>(fullPath);
            if (spr == null)
                Debug.LogWarning($"[ProjectSetup] Sprite not found: {fullPath}");
            return spr;
        }

        private static GameObject CreatePrefab(GameObject obj, string path)
        {
            string fullPath = path + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(obj, fullPath);
            Object.DestroyImmediate(obj);
            return prefab;
        }

        // ==================================================================
        // SCRIPTABLE OBJECTS
        // ==================================================================
        private static void CreateAllScriptableObjects()
        {
            // --- ITEMS ---
            CreateItemSO("raw_meat", "Raw Meat", "Uncooked meat from hunted animals. Risk of food poisoning.",
                ItemCategory.Food, true, 20f, 0f, -5f, 0f, 1f, "Items/raw_meat");
            CreateItemSO("cooked_meat", "Cooked Meat", "Safely cooked meat. Restores hunger and health.",
                ItemCategory.Food, true, 50f, 0f, 10f, 5f, 1f, "Items/cooked_meat");
            CreateItemSO("wild_apple", "Wild Apple", "A small wild apple. Slightly tart.",
                ItemCategory.Food, true, 15f, 10f, 0f, 0f, 0.2f, "Items/wild_apple");
            CreateItemSO("berries", "Berries", "A handful of wild berries.",
                ItemCategory.Food, true, 8f, 5f, 0f, 0f, 0.1f, "Items/berries");
            CreateItemSO("wild_carrot", "Wild Carrot", "A dug-up wild carrot root.",
                ItemCategory.Food, true, 10f, 0f, 5f, 0f, 0.2f, "Items/wild_carrot");
            CreateItemSO("wood_log", "Wood Log", "A heavy log. Can be carried on shoulder.",
                ItemCategory.Resource, false, 0f, 0f, 0f, 0f, 25f, "Items/wood_log");
            CreateItemSO("stone", "Stone", "A rough stone. Useful for crafting.",
                ItemCategory.Resource, false, 0f, 0f, 0f, 0f, 5f, "Items/stone");
            CreateItemSO("animal_hide", "Animal Hide", "Tanned animal skin. Used for clothing.",
                ItemCategory.Resource, false, 0f, 0f, 0f, 0f, 3f, "Items/animal_hide");
            CreateItemSO("fiber", "Fiber", "Plant fibers for binding and weaving.",
                ItemCategory.Resource, false, 0f, 0f, 0f, 0f, 0.1f, "Items/fiber");
            CreateItemSO("stone_pickaxe", "Stone Pickaxe", "Crude pickaxe for mining stone.",
                ItemCategory.Tool, false, 0f, 0f, 0f, 0f, 2f, "Items/stone_pickaxe");
            CreateItemSO("stone_axe", "Stone Axe", "Crude axe for chopping trees.",
                ItemCategory.Tool, false, 0f, 0f, 0f, 0f, 2.5f, "Items/stone_axe");
            CreateItemSO("torch", "Torch", "Wooden torch with pitch. Provides light.",
                ItemCategory.Tool, false, 0f, 0f, 0f, 0f, 0.5f, "Items/torch");

            // --- RECIPES ---
            CreateRecipeSO("stone_pickaxe", "Stone Pickaxe", "Craft a crude pickaxe for mining.",
                new (string, int)[] { ("stone", 3), ("wood_log", 2), ("fiber", 1) },
                "stone_pickaxe", 1, 5f, "");
            CreateRecipeSO("stone_axe", "Stone Axe", "Craft a crude axe for chopping.",
                new (string, int)[] { ("stone", 3), ("wood_log", 2), ("fiber", 1) },
                "stone_axe", 1, 5f, "");
            CreateRecipeSO("torch", "Torch", "Craft a torch for light.",
                new (string, int)[] { ("wood_log", 1), ("fiber", 2) },
                "torch", 1, 3f, "");
            CreateRecipeSO("campfire", "Campfire", "Build a campfire for cooking and warmth.",
                new (string, int)[] { ("wood_log", 5), ("stone", 3) },
                "stone", 1, 10f, "");
            CreateRecipeSO("cooked_meat", "Cooked Meat", "Cook raw meat over a campfire.",
                new (string, int)[] { ("raw_meat", 1) },
                "cooked_meat", 1, 10f, "campfire");
            CreateRecipeSO("log_raft", "Log Raft", "Build a raft from logs.",
                new (string, int)[] { ("wood_log", 10), ("fiber", 5) },
                "wood_log", 1, 30f, "");
        }

        private static void CreateItemSO(string id, string displayName, string desc,
            ItemCategory category, bool isConsumable, float hunger, float thirst,
            float health, float energy, float weight, string spritePath)
        {
            string path = SO_PATH + "Items/" + id + ".asset";
            if (File.Exists(path)) return;

            var so = ScriptableObject.CreateInstance<ItemDataSO>();
            so.data = new ItemData
            {
                itemId = id,
                displayName = displayName,
                description = desc,
                category = category,
                isConsumable = isConsumable,
                hungerRestore = hunger,
                thirstRestore = thirst,
                healthRestore = health,
                energyRestore = energy,
                weight = weight,
                maxStack = 99,
                icon = LoadSprite(spritePath)
            };
            AssetDatabase.CreateAsset(so, path);
        }

        private static void CreateRecipeSO(string id, string displayName, string desc,
            (string itemId, int amount)[] ingredients, string outputId, int outputAmount,
            float craftTime, string station)
        {
            string path = SO_PATH + "Recipes/" + id + ".asset";
            // Recipes are stored in the database; create a standalone database if none exists
            string dbPath = SO_PATH + "Recipes/RecipeDatabase.asset";
            RecipeDatabase db = AssetDatabase.LoadAssetAtPath<RecipeDatabase>(dbPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<RecipeDatabase>();
                AssetDatabase.CreateAsset(db, dbPath);
            }

            var recipe = new Recipe
            {
                recipeId = id,
                displayName = displayName,
                description = desc,
                outputAmount = outputAmount,
                craftTime = craftTime,
                requiredStation = station,
                ingredients = new Ingredient[ingredients.Length]
            };

            for (int i = 0; i < ingredients.Length; i++)
            {
                recipe.ingredients[i] = new Ingredient
                {
                    itemId = ingredients[i].itemId,
                    amount = ingredients[i].amount
                };
            }

            // Link output item
            var outputSO = AssetDatabase.LoadAssetAtPath<ItemDataSO>(SO_PATH + "Items/" + outputId + ".asset");
            if (outputSO != null) recipe.outputItem = outputSO.data;

            db.recipes.Add(recipe);
            EditorUtility.SetDirty(db);
        }

        // ==================================================================
        // PREFABS
        // ==================================================================
        private static void CreateAllPrefabs()
        {
            CreatePlayerPrefab();
            CreateAnimalPrefabs();
            CreateVegetationPrefabs();
            CreateRockPrefabs();
            CreateItemPickupPrefabs();
            CreateStructurePrefabs();
            CreateUIPrefabs();
        }

        // --- PLAYER ---
        private static void CreatePlayerPrefab()
        {
            var player = new GameObject("Player");
            player.tag = "Player";
            player.layer = LayerMask.NameToLayer("Default");

            // SpriteRenderer
            var sr = player.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite("Player/South/player_south");
            sr.sortingOrder = 0;

            // Collider & Rigidbody
            var col = player.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.8f, 1.2f);
            col.offset = new Vector2(0f, -0.2f);
            var rb = player.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            // Water trigger (larger trigger for water detection)
            var waterTrigger = new GameObject("WaterTrigger");
            waterTrigger.transform.SetParent(player.transform);
            waterTrigger.transform.localPosition = Vector3.zero;
            var wtCol = waterTrigger.AddComponent<BoxCollider2D>();
            wtCol.isTrigger = true;
            wtCol.size = new Vector2(1f, 1f);

            // Player Controller
            var pc = player.AddComponent<PlayerController>();
            pc.spritesNorth = new[] { LoadSprite("Player/North/player_north") };
            pc.spritesNorthEast = new[] { LoadSprite("Player/NorthEast/player_northeast") };
            pc.spritesEast = new[] { LoadSprite("Player/East/player_east") };
            pc.spritesSouthEast = new[] { LoadSprite("Player/SouthEast/player_southeast") };
            pc.spritesSouth = new[] { LoadSprite("Player/South/player_south") };
            pc.spritesSouthWest = new[] { LoadSprite("Player/SouthWest/player_southwest") };
            pc.spritesWest = new[] { LoadSprite("Player/West/player_west") };
            pc.spritesNorthWest = new[] { LoadSprite("Player/NorthWest/player_northwest") };
            pc.baseSpeed = 5f;

            // Survival Stats
            player.AddComponent<SurvivalStats>();

            // Footprint System
            var fps = player.AddComponent<FootprintSystem>();

            // Weight Carry
            player.AddComponent<WeightCarrySystem>();

            // Swimming
            player.AddComponent<SwimmingSystem>();

            // Climbing
            player.AddComponent<ClimbingSystem>();

            // Mining
            player.AddComponent<MiningSystem>();

            // Root Digging
            player.AddComponent<RootDigging>();

            // Consumable
            player.AddComponent<ConsumableSystem>();

            // Weapon
            player.AddComponent<WeaponSystem>();

            // Shadow Caster 2D (for URP 2D shadows)
            player.AddComponent<UnityEngine.Rendering.Universal.ShadowCaster2D>();

            // AudioSource
            player.AddComponent<AudioSource>();

            CreatePrefab(player, PREFAB_PATH + "Player/Player");
            Debug.Log("[ProjectSetup] Player prefab created.");
        }

        // --- ANIMALS ---
        private static void CreateAnimalPrefabs()
        {
            CreateAnimalPrefab("Mammoth", "Woolly Mammoth", 200f, 25f, 2.5f, 5f, 15f, 2f, 30f,
                0.2f, AnimalAI.AggressionLevel.Neutral, "Animals/Mammoth/",
                new LootDropper.LootEntry[] {
                    new LootDropper.LootEntry { minAmount = 5, maxAmount = 10, dropChance = 1f },
                    new LootDropper.LootEntry { minAmount = 3, maxAmount = 5, dropChance = 1f }
                });

            CreateAnimalPrefab("Sabertooth", "Sabertooth Tiger", 150f, 30f, 3.5f, 7f, 12f, 2f, 25f,
                0.3f, AnimalAI.AggressionLevel.Aggressive, "Animals/Sabertooth/",
                new LootDropper.LootEntry[] {
                    new LootDropper.LootEntry { minAmount = 3, maxAmount = 6, dropChance = 1f },
                    new LootDropper.LootEntry { minAmount = 2, maxAmount = 3, dropChance = 1f }
                });

            CreateAnimalPrefab("CaveBear", "Cave Bear", 180f, 20f, 3f, 6f, 14f, 2.5f, 28f,
                0.25f, AnimalAI.AggressionLevel.Aggressive, "Animals/CaveBear/",
                new LootDropper.LootEntry[] {
                    new LootDropper.LootEntry { minAmount = 4, maxAmount = 8, dropChance = 1f },
                    new LootDropper.LootEntry { minAmount = 2, maxAmount = 4, dropChance = 1f }
                });

            CreateAnimalPrefab("Bison", "Steppe Bison", 160f, 15f, 3f, 5.5f, 16f, 2f, 30f,
                0.2f, AnimalAI.AggressionLevel.Neutral, "Animals/Bison/",
                new LootDropper.LootEntry[] {
                    new LootDropper.LootEntry { minAmount = 4, maxAmount = 7, dropChance = 1f },
                    new LootDropper.LootEntry { minAmount = 2, maxAmount = 3, dropChance = 1f }
                });
        }

        private static void CreateAnimalPrefab(string folder, string animalName,
            float health, float damage, float moveSpeed, float runSpeed,
            float detectionRange, float attackRange, float leashRange,
            float fleeThreshold, AnimalAI.AggressionLevel aggression,
            string spriteFolder, LootDropper.LootEntry[] lootEntries)
        {
            var go = new GameObject(animalName);
            go.tag = "Animal";
            go.layer = LayerMask.NameToLayer("Default");

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite(spriteFolder + folder.ToLower() + "_south");
            sr.sortingOrder = 0;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.5f, 1.5f);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            var ai = go.AddComponent<AnimalAI>();
            ai.animalName = animalName;
            ai.maxHealth = health;
            ai.damage = damage;
            ai.moveSpeed = moveSpeed;
            ai.runSpeed = runSpeed;
            ai.detectionRange = detectionRange;
            ai.attackRange = attackRange;
            ai.leashRange = leashRange;
            ai.fleeThreshold = fleeThreshold;
            ai.aggression = aggression;
            ai.spriteRenderer = sr;

            var dropper = go.AddComponent<LootDropper>();
            dropper.scatterRadius = 1.5f;
            // Assign loot items
            dropper.lootTable = new LootDropper.LootEntry[lootEntries.Length];
            var rawMeatSO = AssetDatabase.LoadAssetAtPath<ItemDataSO>(SO_PATH + "Items/raw_meat.asset");
            var hideSO = AssetDatabase.LoadAssetAtPath<ItemDataSO>(SO_PATH + "Items/animal_hide.asset");
            for (int i = 0; i < lootEntries.Length; i++)
            {
                dropper.lootTable[i] = new LootDropper.LootEntry
                {
                    item = i == 0 && rawMeatSO != null ? rawMeatSO.data : (hideSO != null ? hideSO.data : null),
                    minAmount = lootEntries[i].minAmount,
                    maxAmount = lootEntries[i].maxAmount,
                    dropChance = lootEntries[i].dropChance
                };
            }

            // Shadow caster
            go.AddComponent<UnityEngine.Rendering.Universal.ShadowCaster2D>();

            CreatePrefab(go, PREFAB_PATH + "Animals/" + folder);
            Debug.Log($"[ProjectSetup] {animalName} prefab created.");
        }

        // --- VEGETATION ---
        private static void CreateVegetationPrefabs()
        {
            // Trees
            CreateTreePrefab("PineTree", "Pine Tree", VegetationInteraction.VegetationType.TimberTree,
                "Vegetation/Trees/pine_tree", "wood_log", 4);
            CreateTreePrefab("OakTree", "Oak Tree", VegetationInteraction.VegetationType.TimberTree,
                "Vegetation/Trees/oak_tree", "wood_log", 5);
            CreateTreePrefab("AppleTree", "Wild Apple Tree", VegetationInteraction.VegetationType.FruitTree,
                "Vegetation/Trees/apple_tree", "wild_apple", 5);
            CreateTreePrefab("FigTree", "Wild Fig Tree", VegetationInteraction.VegetationType.FruitTree,
                "Vegetation/Trees/fig_tree", "wild_apple", 4);

            // Bushes
            CreateBushPrefab("BerryBush", "Wild Berry Bush", VegetationInteraction.VegetationType.BerryBush,
                "Vegetation/Bushes/berry_bush", "berries", 6);
            CreateBushPrefab("Vine", "Wild Grape Vine", VegetationInteraction.VegetationType.Vine,
                "Vegetation/Bushes/vine", "berries", 4);
        }

        private static void CreateTreePrefab(string name, string displayName,
            VegetationInteraction.VegetationType type, string spritePath,
            string dropItemId, int dropAmount)
        {
            var go = new GameObject(name);
            go.tag = "Tree";

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite(spritePath);
            sr.sortingOrder = Mathf.RoundToInt(-1f * 100);

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(2f, 2f);
            col.isTrigger = true;

            // Physics collider for blocking
            var physCol = new GameObject("PhysicsCollider");
            physCol.transform.SetParent(go.transform);
            physCol.transform.localPosition = new Vector3(0, -1.5f, 0);
            var pc = physCol.AddComponent<BoxCollider2D>();
            pc.size = new Vector2(0.8f, 0.8f);

            var veg = go.AddComponent<VegetationInteraction>();
            veg.type = type;
            veg.speciesName = displayName;
            veg.woodYield = dropAmount;
            veg.fruitYield = dropAmount;
            veg.regrowDays = 7;
            veg.harvestTime = type == VegetationInteraction.VegetationType.TimberTree ? 3f : 1f;

            // Link item drops
            var woodSO = AssetDatabase.LoadAssetAtPath<ItemDataSO>(SO_PATH + "Items/" + dropItemId + ".asset");
            if (woodSO != null)
            {
                if (type == VegetationInteraction.VegetationType.TimberTree)
                    veg.woodDrop = woodSO.data;
                else
                    veg.fruitDrop = woodSO.data;
            }

            go.AddComponent<UnityEngine.Rendering.Universal.ShadowCaster2D>();

            CreatePrefab(go, PREFAB_PATH + "Vegetation/" + name);
            Debug.Log($"[ProjectSetup] {displayName} prefab created.");
        }

        private static void CreateBushPrefab(string name, string displayName,
            VegetationInteraction.VegetationType type, string spritePath,
            string dropItemId, int dropAmount)
        {
            var go = new GameObject(name);
            go.tag = "Vegetation";

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite(spritePath);
            sr.sortingOrder = 0;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.5f, 1.5f);
            col.isTrigger = true;

            var veg = go.AddComponent<VegetationInteraction>();
            veg.type = type;
            veg.speciesName = displayName;
            veg.fruitYield = dropAmount;
            veg.regrowDays = 5;
            veg.harvestTime = 1f;

            var itemSO = AssetDatabase.LoadAssetAtPath<ItemDataSO>(SO_PATH + "Items/" + dropItemId + ".asset");
            if (itemSO != null) veg.fruitDrop = itemSO.data;

            CreatePrefab(go, PREFAB_PATH + "Vegetation/" + name);
            Debug.Log($"[ProjectSetup] {displayName} prefab created.");
        }

        // --- ROCKS ---
        private static void CreateRockPrefabs()
        {
            CreateRockPrefab("LargeRock", "Vegetation/Rocks/large_rock");
            CreateRockPrefab("StoneCluster", "Vegetation/Rocks/stone_cluster");
        }

        private static void CreateRockPrefab(string name, string spritePath)
        {
            var go = new GameObject(name);
            go.tag = "Rock";

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite(spritePath);
            sr.sortingOrder = 0;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.5f, 1.2f);

            go.AddComponent<UnityEngine.Rendering.Universal.ShadowCaster2D>();

            CreatePrefab(go, PREFAB_PATH + "Terrain/" + name);
            Debug.Log($"[ProjectSetup] {name} prefab created.");
        }

        // --- ITEM PICKUPS ---
        private static void CreateItemPickupPrefabs()
        {
            string[] items = {
                "raw_meat", "cooked_meat", "wild_apple", "berries", "wild_carrot",
                "wood_log", "stone", "animal_hide", "fiber",
                "stone_pickaxe", "stone_axe", "torch"
            };

            foreach (var itemId in items)
            {
                var go = new GameObject("Pickup_" + itemId);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = LoadSprite("Items/" + itemId);
                sr.sortingOrder = 5;

                var col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size = new Vector2(0.8f, 0.8f);

                go.AddComponent<WorldItemPickup>();

                CreatePrefab(go, PREFAB_PATH + "Items/Pickup_" + itemId);
            }
            Debug.Log("[ProjectSetup] All item pickup prefabs created.");
        }

        // --- STRUCTURES ---
        private static void CreateStructurePrefabs()
        {
            // Campfire
            var campfire = new GameObject("Campfire");
            var cfSR = campfire.AddComponent<SpriteRenderer>();
            cfSR.sprite = LoadSprite("Items/campfire");
            cfSR.sortingOrder = 0;
            var cfCol = campfire.AddComponent<BoxCollider2D>();
            cfCol.isTrigger = true;
            cfCol.size = new Vector2(2f, 2f);
            var cfLight = campfire.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            cfLight.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
            cfColor_light(cfLight);
            var cfTorch = campfire.AddComponent<TorchLight>();
            cfTorch.type = TorchLight.LightType.Campfire;
            cfTorch.baseIntensity = 1.2f;
            cfTorch.baseRadius = 8f;
            cfTorch.usesFuel = true;
            cfTorch.maxFuel = 1200f;
            // Add station tag script
            campfire.AddComponent<CraftingStationTrigger>();
            CreatePrefab(campfire, PREFAB_PATH + "Structures/Campfire");

            // Log Raft
            var raft = new GameObject("LogRaft");
            raft.layer = LayerMask.NameToLayer("Default");
            var rSR = raft.AddComponent<SpriteRenderer>();
            rSR.sprite = LoadSprite("Items/log_raft");
            rSR.sortingOrder = -1;
            var rRb = raft.AddComponent<Rigidbody2D>();
            rRb.gravityScale = 0f;
            rRb.freezeRotation = true;
            var rCol = raft.AddComponent<BoxCollider2D>();
            rCol.isTrigger = true;
            rCol.size = new Vector2(3f, 2f);
            raft.AddComponent<RaftController>();
            var mountPt = new GameObject("MountPoint");
            mountPt.transform.SetParent(raft.transform);
            mountPt.transform.localPosition = Vector3.zero;
            CreatePrefab(raft, PREFAB_PATH + "Structures/LogRaft");

            // Footprint
            var fp = new GameObject("Footprint");
            var fpSR = fp.AddComponent<SpriteRenderer>();
            fpSR.sprite = LoadSprite("Items/footprint");
            fpSR.sortingOrder = -10;
            CreatePrefab(fp, PREFAB_PATH + "Items/Footprint");

            Debug.Log("[ProjectSetup] Structure prefabs created.");
        }

        private static void cfColor_light(UnityEngine.Rendering.Universal.Light2D light)
        {
            light.color = new Color(1f, 0.8f, 0.4f);
            light.intensity = 1.2f;
            light.pointLightOuterRadius = 8f;
            light.pointLightInnerRadius = 2f;
        }

        // --- UI PREFABS ---
        private static void CreateUIPrefabs()
        {
            // Mobile Joystick
            var joyBG = new GameObject("MobileJoystick");
            joyBG.AddComponent<RectTransform>();
            var joyImg = joyBG.AddComponent<Image>();
            joyImg.color = new Color(1f, 1f, 1f, 0.2f);

            var knob = new GameObject("Knob");
            knob.transform.SetParent(joyBG.transform);
            knob.AddComponent<RectTransform>();
            var knobImg = knob.AddComponent<Image>();
            knobImg.color = new Color(1f, 1f, 1f, 0.5f);

            var joy = joyBG.AddComponent<MobileJoystick>();
            joy.knob = knob.GetComponent<RectTransform>();
            joy.background = joyBG.GetComponent<RectTransform>();

            CreatePrefab(joyBG, PREFAB_PATH + "UI/MobileJoystick");
            Debug.Log("[ProjectSetup] UI prefabs created.");
        }

        // ==================================================================
        // SCENES
        // ==================================================================
        private static void CreateMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Canvas
            var canvasGO = new GameObject("MenuCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            // EventSystem
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Background
            var bg = new GameObject("Background");
            bg.transform.SetParent(canvasGO.transform, false);
            var bgRT = bg.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.15f, 0.1f, 0.05f);

            // Title
            var title = new GameObject("Title");
            title.transform.SetParent(canvasGO.transform, false);
            var titleRT = title.AddComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.5f, 0.7f);
            titleRT.anchorMax = new Vector2(0.5f, 0.7f);
            titleRT.sizeDelta = new Vector2(800, 100);
            var titleText = title.AddComponent<TextMeshProUGUI>();
            titleText.text = "PREHISTORIC SURVIVAL";
            titleText.fontSize = 60;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(0.9f, 0.8f, 0.5f);

            // Play Button
            CreateMenuButton(canvasGO.transform, "PlayButton", "PLAY", new Vector2(0.5f, 0.5f),
                new Color(0.4f, 0.3f, 0.1f));

            // Load Button
            CreateMenuButton(canvasGO.transform, "LoadButton", "LOAD GAME", new Vector2(0.5f, 0.4f),
                new Color(0.4f, 0.3f, 0.1f));

            // Settings Button
            CreateMenuButton(canvasGO.transform, "SettingsButton", "SETTINGS", new Vector2(0.5f, 0.3f),
                new Color(0.3f, 0.25f, 0.1f));

            // Quit Button
            CreateMenuButton(canvasGO.transform, "QuitButton", "QUIT", new Vector2(0.5f, 0.2f),
                new Color(0.3f, 0.1f, 0.1f));

            // Camera background color
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = new Color(0.1f, 0.08f, 0.04f);

            EditorSceneManager.SaveScene(scene, SCENE_PATH + "MainMenu.unity");
            Debug.Log("[ProjectSetup] MainMenu scene created.");
        }

        private static void CreateMenuButton(Transform parent, string name, string label,
            Vector2 anchor, Color bgColor)
        {
            var btnGO = new GameObject(name);
            btnGO.transform.SetParent(parent, false);
            var rt = btnGO.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.sizeDelta = new Vector2(300, 60);

            var img = btnGO.AddComponent<Image>();
            img.color = bgColor;

            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = img;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(btnGO.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;

            var text = textGO.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 28;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.9f, 0.85f, 0.7f);
        }

        private static void CreateGameplayWorldScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Camera setup
            var cam = Camera.main;
            if (cam != null)
            {
                cam.backgroundColor = new Color(0.5f, 0.7f, 0.9f);
                cam.orthographic = true;
                cam.orthographicSize = 8f;
                cam.transform.position = new Vector3(0f, 10f, -10f);
                cam.transform.rotation = Quaternion.Euler(45f, 0f, 0f);

                // Camera Follow
                var camFollow = cam.gameObject.AddComponent<CameraFollow>();
                camFollow.offset = new Vector3(0f, 8f, -10f);
            }

            // ---- MANAGER OBJECTS ----

            // GameManager
            var gameMgr = new GameObject("GameManager");
            gameMgr.AddComponent<GameManager>();
            gameMgr.AddComponent<SaveSystem>();

            // WorldManager
            var worldMgr = new GameObject("WorldManager");
            worldMgr.AddComponent<ChunkManager>();
            worldMgr.AddComponent<BiomeManager>();
            worldMgr.AddComponent<WaypointManager>();
            worldMgr.AddComponent<ShadowManager>();

            // Season & Weather
            var seasonGO = new GameObject("SeasonManager");
            seasonGO.AddComponent<SeasonManager>();
            seasonGO.AddComponent<WeatherController>();

            // Day/Night
            var dayNight = new GameObject("DayNightCycle");
            dayNight.AddComponent<DayNightCycle>();

            // Crafting System (on player, but we add a global reference)
            var craftingGO = new GameObject("CraftingSystem");
            craftingGO.AddComponent<CraftingSystem>();

            // ---- PLAYER ----
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH + "Player/Player.prefab");
            if (playerPrefab != null)
            {
                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.transform.position = new Vector3(0f, 0f, 0f);
            }
            else
            {
                Debug.LogWarning("[ProjectSetup] Player prefab not found. Creating placeholder.");
                var player = new GameObject("Player");
                player.tag = "Player";
                player.transform.position = Vector3.zero;
                player.AddComponent<SpriteRenderer>();
                player.AddComponent<Rigidbody2D>().gravityScale = 0f;
                player.AddComponent<BoxCollider2D>();
            }

            // ---- GROUND TILEMAP ----
            var gridGO = new GameObject("WorldGrid");
            var grid = gridGO.AddComponent<Grid>();
            grid.cellSize = Vector3.one;

            var groundGO = new GameObject("GroundTilemap");
            groundGO.transform.SetParent(gridGO.transform);
            var tm = groundGO.AddComponent<Tilemap>();
            var tmr = groundGO.AddComponent<TilemapRenderer>();
            tmr.sortingOrder = -100;

            // Create basic tiles
            CreateBasicTiles(tm);

            // ---- WATER AREA ----
            var waterArea = new GameObject("WaterArea");
            waterArea.tag = "Water";
            waterArea.transform.position = new Vector3(30f, 0f, 20f);
            var waterCol = waterArea.AddComponent<BoxCollider2D>();
            waterCol.isTrigger = true;
            waterCol.size = new Vector2(20f, 15f);
            var waterSR = waterArea.AddComponent<SpriteRenderer>();
            waterSR.sprite = LoadSprite("Terrain/Water/calm_water_tile");
            waterSR.sortingOrder = -50;
            waterSR.color = new Color(0.3f, 0.5f, 0.8f, 0.7f);
            waterSR.drawMode = SpriteDrawMode.Tiled;
            waterSR.size = new Vector2(20f, 15f);

            // ---- CLIMBABLE CLIFF ----
            var cliff = new GameObject("ClimbableCliff");
            cliff.tag = "Climbable";
            cliff.transform.position = new Vector3(-20f, 0f, -10f);
            var cliffSR = cliff.AddComponent<SpriteRenderer>();
            cliffSR.sprite = LoadSprite("Terrain/Mountain/cliff_face");
            cliffSR.sortingOrder = 0;
            var cliffCol = cliff.AddComponent<BoxCollider2D>();
            cliffCol.isTrigger = true;
            cliffCol.size = new Vector2(3f, 6f);

            // ---- SCATTER SOME VEGETATION ----
            ScatterVegetation(scene);

            // ---- SCATTER SOME ANIMALS ----
            ScatterAnimals(scene);

            // ---- SCATTER ROCKS ----
            ScatterRocks(scene);

            // ---- MOUNTAIN ----
            var mountain = new GameObject("Mountain");
            mountain.transform.position = new Vector3(-30f, 0f, 30f);
            var mtnSR = mountain.AddComponent<SpriteRenderer>();
            mtnSR.sprite = LoadSprite("Terrain/Mountain/mountain_peak");
            mtnSR.sortingOrder = -50;

            // ---- CAVE ENTRANCE ----
            var cave = new GameObject("CaveEntrance");
            cave.transform.position = new Vector3(-28f, 0f, 28f);
            var caveSR = cave.AddComponent<SpriteRenderer>();
            caveSR.sprite = LoadSprite("Terrain/Mountain/cave_entrance");
            caveSR.sortingOrder = 0;
            cave.AddComponent<UnityEngine.Rendering.Universal.ShadowCaster2D>();

            // ---- UI CANVAS ----
            CreateGameplayUI(scene);

            // ---- GLOBAL LIGHT 2D ----
            var lightGO = new GameObject("GlobalLight2D");
            var globalLight = lightGO.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            globalLight.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;
            globalLight.color = new Color(1f, 0.95f, 0.85f);
            globalLight.intensity = 1f;

            // Link day/night cycle to global light
            var dnc = dayNight.GetComponent<DayNightCycle>();
            if (dnc != null) dnc.globalLight = globalLight;

            // EventSystem
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            EditorSceneManager.SaveScene(scene, SCENE_PATH + "GameplayWorld.unity");
            Debug.Log("[ProjectSetup] GameplayWorld scene created.");
        }

        private static void CreateBasicTiles(Tilemap tm)
        {
            // Create simple colored tiles programmatically
            string[] tileNames = { "GrassTile", "DirtTile", "SandTile", "SnowTile", "StoneTile", "MudTile" };
            Color[] tileColors = {
                new Color(0.3f, 0.6f, 0.2f),
                new Color(0.5f, 0.35f, 0.2f),
                new Color(0.85f, 0.75f, 0.5f),
                new Color(0.9f, 0.92f, 0.95f),
                new Color(0.5f, 0.5f, 0.5f),
                new Color(0.35f, 0.25f, 0.15f)
            };

            // Place a large ground of grass tiles
            for (int x = -50; x < 50; x++)
            {
                for (int z = -50; z < 50; z++)
                {
                    // Simple biome assignment based on position
                    int tileIndex = 0; // default grass
                    float dist = Mathf.Sqrt(x * x + z * z);
                    if (x > 20 && z > 10) tileIndex = 2; // sand near water
                    else if (x < -20 && z > 20) tileIndex = 3; // snow near mountain
                    else if (dist > 40) tileIndex = 1; // dirt at edges

                    // Create a simple tile at runtime (tiles will be replaced by proper tile assets)
                    // For now, just use the tilemap's built-in color
                }
            }
            Debug.Log("[ProjectSetup] Basic tilemap grid placed. Create tile assets in Unity Editor for full visuals.");
        }

        private static void ScatterVegetation(Scene scene)
        {
            System.Random rng = new System.Random(42);

            string[] treePrefabs = {
                PREFAB_PATH + "Vegetation/PineTree.prefab",
                PREFAB_PATH + "Vegetation/OakTree.prefab",
                PREFAB_PATH + "Vegetation/AppleTree.prefab",
                PREFAB_PATH + "Vegetation/FigTree.prefab"
            };
            string[] bushPrefabs = {
                PREFAB_PATH + "Vegetation/BerryBush.prefab",
                PREFAB_PATH + "Vegetation/Vine.prefab"
            };

            // Scatter trees
            for (int i = 0; i < 40; i++)
            {
                string path = treePrefabs[rng.Next(treePrefabs.Length)];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                float x = (float)(rng.NextDouble() * 80 - 40);
                float z = (float)(rng.NextDouble() * 80 - 40);
                // Avoid center (player spawn) and water area
                if (Mathf.Abs(x) < 5 && Mathf.Abs(z) < 5) continue;
                if (x > 20 && z > 10) continue;

                var tree = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                tree.transform.position = new Vector3(x, 0, z);
                tree.GetComponent<SpriteRenderer>().sortingOrder = Mathf.RoundToInt(-z * 100);
            }

            // Scatter bushes
            for (int i = 0; i < 20; i++)
            {
                string path = bushPrefabs[rng.Next(bushPrefabs.Length)];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                float x = (float)(rng.NextDouble() * 60 - 30);
                float z = (float)(rng.NextDouble() * 60 - 30);
                if (Mathf.Abs(x) < 5 && Mathf.Abs(z) < 5) continue;

                var bush = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                bush.transform.position = new Vector3(x, 0, z);
            }

            Debug.Log("[ProjectSetup] Vegetation scattered.");
        }

        private static void ScatterAnimals(Scene scene)
        {
            System.Random rng = new System.Random(123);

            string[] animalPrefabs = {
                PREFAB_PATH + "Animals/Mammoth.prefab",
                PREFAB_PATH + "Animals/Sabertooth.prefab",
                PREFAB_PATH + "Animals/CaveBear.prefab",
                PREFAB_PATH + "Animals/Bison.prefab"
            };

            for (int i = 0; i < 12; i++)
            {
                string path = animalPrefabs[rng.Next(animalPrefabs.Length)];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                float x = (float)(rng.NextDouble() * 70 - 35);
                float z = (float)(rng.NextDouble() * 70 - 35);
                if (Mathf.Abs(x) < 10 && Mathf.Abs(z) < 10) continue;

                var animal = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                animal.transform.position = new Vector3(x, 0, z);
            }

            Debug.Log("[ProjectSetup] Animals scattered.");
        }

        private static void ScatterRocks(Scene scene)
        {
            System.Random rng = new System.Random(789);

            string[] rockPrefabs = {
                PREFAB_PATH + "Terrain/LargeRock.prefab",
                PREFAB_PATH + "Terrain/StoneCluster.prefab"
            };

            for (int i = 0; i < 15; i++)
            {
                string path = rockPrefabs[rng.Next(rockPrefabs.Length)];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                float x = (float)(rng.NextDouble() * 60 - 30);
                float z = (float)(rng.NextDouble() * 60 - 30);
                if (Mathf.Abs(x) < 5 && Mathf.Abs(z) < 5) continue;

                var rock = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                rock.transform.position = new Vector3(x, 0, z);
            }

            Debug.Log("[ProjectSetup] Rocks scattered.");
        }

        private static void CreateGameplayUI(Scene scene)
        {
            var canvasGO = new GameObject("GameCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            // --- Survival Stat Bars (top-left) ---
            CreateStatBar(canvasGO.transform, "HealthBar", "Health", new Vector2(20, -20),
                new Color(0.8f, 0.1f, 0.1f), "UI/Icons/health_icon");
            CreateStatBar(canvasGO.transform, "HungerBar", "Hunger", new Vector2(20, -70),
                new Color(0.9f, 0.6f, 0.1f), "UI/Icons/hunger_icon");
            CreateStatBar(canvasGO.transform, "ThirstBar", "Thirst", new Vector2(20, -120),
                new Color(0.1f, 0.4f, 0.9f), "UI/Icons/thirst_icon");
            CreateStatBar(canvasGO.transform, "EnergyBar", "Energy", new Vector2(20, -170),
                new Color(0.9f, 0.9f, 0.1f), "UI/Icons/energy_icon");
            CreateStatBar(canvasGO.transform, "StaminaBar", "Stamina", new Vector2(20, -220),
                new Color(0.1f, 0.8f, 0.2f), "UI/Icons/stamina_icon");

            // Attach SurvivalStatsHUD
            var hud = canvasGO.AddComponent<SurvivalStatsHUD>();
            // References will be linked after creation below

            // --- Time/Season Display (top-right) ---
            var timePanel = new GameObject("TimePanel");
            timePanel.transform.SetParent(canvasGO.transform, false);
            var tpRT = timePanel.AddComponent<RectTransform>();
            tpRT.anchorMin = new Vector2(1, 1);
            tpRT.anchorMax = new Vector2(1, 1);
            tpRT.pivot = new Vector2(1, 1);
            tpRT.anchoredPosition = new Vector2(-20, -20);
            tpRT.sizeDelta = new Vector2(200, 80);

            var timeText = CreateTextElement(timePanel.transform, "TimeText", "06:00", 24,
                new Vector2(0, 0), new Vector2(200, 30));
            var dayText = CreateTextElement(timePanel.transform, "DayText", "Day 1", 20,
                new Vector2(0, -30), new Vector2(200, 25));
            var seasonText = CreateTextElement(timePanel.transform, "SeasonText", "Spring", 20,
                new Vector2(0, -55), new Vector2(200, 25));

            var tsHUD = timePanel.AddComponent<TimeSeasonHUD>();
            tsHUD.timeText = timeText;
            tsHUD.dayText = dayText;
            tsHUD.seasonText = seasonText;

            // --- Compass (top-center) ---
            var compass = new GameObject("CompassHUD");
            compass.transform.SetParent(canvasGO.transform, false);
            var cRT = compass.AddComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0.5f, 1);
            cRT.anchorMax = new Vector2(0.5f, 1);
            cRT.pivot = new Vector2(0.5f, 1);
            cRT.anchoredPosition = new Vector2(0, -20);
            cRT.sizeDelta = new Vector2(200, 60);

            var compassContainer = new GameObject("Container");
            compassContainer.transform.SetParent(compass.transform, false);
            compassContainer.AddComponent<RectTransform>();
            var ccImg = compassContainer.AddComponent<Image>();
            ccImg.color = new Color(0, 0, 0, 0.5f);
            var ccRT = compassContainer.GetComponent<RectTransform>();
            ccRT.anchorMin = Vector2.zero;
            ccRT.anchorMax = Vector2.one;
            ccRT.sizeDelta = Vector2.zero;

            var distText = CreateTextElement(compassContainer.transform, "DistanceText", "--- m", 22,
                new Vector2(0, 0), new Vector2(200, 30));

            var compassHud = compass.AddComponent<CompassHUD>();
            compassHud.compassContainer = compassContainer;
            compassHud.distanceText = distText;

            // --- Tooltip ---
            var tooltip = new GameObject("TooltipPanel");
            tooltip.transform.SetParent(canvasGO.transform, false);
            var ttRT = tooltip.AddComponent<RectTransform>();
            ttRT.sizeDelta = new Vector2(300, 100);
            var ttImg = tooltip.AddComponent<Image>();
            ttImg.color = new Color(0, 0, 0, 0.8f);
            var ttText = CreateTextElement(tooltip.transform, "TooltipText", "", 16,
                Vector2.zero, new Vector2(280, 80));

            var tooltipUI = tooltip.AddComponent<TooltipUI>();
            tooltipUI.panel = ttRT;
            tooltipUI.text = ttText;
            tooltip.Hide();

            Debug.Log("[ProjectSetup] Gameplay UI created.");
        }

        private static void CreateStatBar(Transform parent, string name, string label,
            Vector2 pos, Color fillColor, string iconPath)
        {
            var barGO = new GameObject(name);
            barGO.transform.SetParent(parent, false);
            var barRT = barGO.AddComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0, 1);
            barRT.anchorMax = new Vector2(0, 1);
            barRT.pivot = new Vector2(0, 1);
            barRT.anchoredPosition = pos;
            barRT.sizeDelta = new Vector2(250, 40);

            // Icon
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(barGO.transform, false);
            var iconRT = iconGO.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0, 0);
            iconRT.anchorMax = new Vector2(0, 1);
            iconRT.sizeDelta = new Vector2(40, 40);
            iconRT.anchoredPosition = Vector2.zero;
            var iconImg = iconGO.AddComponent<Image>();
            var iconSprite = LoadSprite(iconPath);
            if (iconSprite != null) iconImg.sprite = iconSprite;
            iconImg.color = Color.white;

            // Background
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(barGO.transform, false);
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0, 0);
            bgRT.anchorMax = new Vector2(1, 1);
            bgRT.offsetMin = new Vector2(45, 5);
            bgRT.offsetMax = new Vector2(-5, -5);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            // Slider
            var slider = barGO.AddComponent<Slider>();
            slider.fillRect = null; // We'll use a child fill
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = 1;
            slider.interactable = false;

            // Fill Area
            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(bgGO.transform, false);
            var faRT = fillArea.AddComponent<RectTransform>();
            faRT.anchorMin = Vector2.zero;
            faRT.anchorMax = Vector2.one;
            faRT.sizeDelta = Vector2.zero;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillRT = fill.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.sizeDelta = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = fillColor;

            slider.fillRect = fillRT;
        }

        private static TextMeshProUGUI CreateTextElement(Transform parent, string name,
            string text, int fontSize, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return tmp;
        }

        // ==================================================================
        // TAGS & LAYERS
        // ==================================================================
        private static void ConfigureTagsAndLayers()
        {
            // Unity tags must be configured via SerializedObject on the TagManager
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

            // Add tags
            AddTag(tagManager, "Player");
            AddTag(tagManager, "Animal");
            AddTag(tagManager, "Water");
            AddTag(tagManager, "Climbable");
            AddTag(tagManager, "Tree");
            AddTag(tagManager, "Rock");
            AddTag(tagManager, "Vegetation");

            // Add layers
            AddLayer(tagManager, 8, "Animal");
            AddLayer(tagManager, 9, "Water");
            AddLayer(tagManager, 10, "Vegetation");
            AddLayer(tagManager, 11, "UI");

            tagManager.ApplyModifiedProperties();
            Debug.Log("[ProjectSetup] Tags and layers configured.");
        }

        private static void AddTag(SerializedObject tagManager, string tag)
        {
            SerializedProperty tagsProp = tagManager.FindProperty("tags");

            // Check if tag already exists
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag)
                    return;
            }

            // Also check built-in tags
            try
            {
                UnityEditorInternal.InternalEditorUtility.GetTagConstants(new[] { tag });
            }
            catch { }

            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
        }

        private static void AddLayer(SerializedObject tagManager, int index, string layerName)
        {
            SerializedProperty layersProp = tagManager.FindProperty("layers");
            if (layersProp.arraySize > index)
            {
                var element = layersProp.GetArrayElementAtIndex(index);
                if (string.IsNullOrEmpty(element.stringValue))
                    element.stringValue = layerName;
            }
        }
    }

    /// <summary>
    /// Simple component to tag a GameObject as a crafting station.
    /// When the player enters the trigger, CraftingSystem.SetNearbyStation is called.
    /// </summary>
    public class CraftingStationTrigger : MonoBehaviour
    {
        public string stationTag = "campfire";

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                var cs = CraftingSystem.Instance;
                if (cs != null) cs.SetNearbyStation(stationTag);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                var cs = CraftingSystem.Instance;
                if (cs != null) cs.SetNearbyStation("");
            }
        }
    }
}
