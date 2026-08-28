using UnityEngine;
using PrehistoricSurvival.Content;
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

            EditorUtility.DisplayProgressBar("Setting up...", "Configuring tags and layers", 0.25f);
            ConfigureTagsAndLayers();
            ConfigureRenderPipeline();

            EditorUtility.DisplayProgressBar("Setting up...", "Creating prefabs", 0.35f);
            CreateAllPrefabs();

            EditorUtility.DisplayProgressBar("Setting up...", "Creating the game library", 0.60f);
            CreateGameLibrary();

            EditorUtility.DisplayProgressBar("Setting up...", "Creating MainMenu scene", 0.70f);
            CreateMainMenuScene();

            EditorUtility.DisplayProgressBar("Setting up...", "Creating GameplayWorld scene", 0.82f);
            CreateGameplayWorldScene();

            EditorUtility.DisplayProgressBar("Setting up...", "Configuring build settings", 0.92f);
            ConfigureBuildSettings();

            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            bool tmpReady = TMPro.TMP_Settings.instance != null;

            Debug.Log("✅ [ProjectSetup] Project setup complete! Open Assets/Scenes/MainMenu.unity and press Play.");
            EditorUtility.DisplayDialog("Setup Complete",
                "Prefabs, GameLibrary, scenes and build settings are ready!\n\n" +
                "▶ Open Assets/Scenes/MainMenu.unity and press Play.\n" +
                "   NEW GAME drops you on a full procedural earth.\n\n" +
                (tmpReady ? "" : "⚠ TextMeshPro essentials are missing — run\n" +
                                 "Window → TextMeshPro → Import TMP Essential Resources\n" +
                                 "or the HUD text will not render.\n\n") +
                "If input does not respond, restart Unity once (the input backend was switched to 'Both').",
                "OK");
        }

        [MenuItem("PrehistoricSurvival/Rebuild Game Library")]
        public static void RebuildGameLibrary()
        {
            CreateDirectories();
            CreateGameLibrary();
            Debug.Log("✅ [ProjectSetup] GameLibrary rebuilt.");
        }

        [MenuItem("PrehistoricSurvival/Create Prefabs Only")]
        public static void CreatePrefabsOnly()
        {
            CreateDirectories();
            CreateAllPrefabs();
            CreateGameLibrary();
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
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("✅ [ProjectSetup] All scenes created and added to Build Settings.");
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

            // Mirror selected prefabs into Resources for runtime spawning
            // (tribe camps, NPC traders...).
            if (path.Contains("/NPC/") || path.Contains("/Structures/"))
            {
                EnsureFolder("Assets/Resources/Prefabs");
                string sub = path.Contains("/NPC/") ? "Prefabs/NPC" : "Prefabs/Structures";
                EnsureFolder("Assets/" + sub);
                string resPath = "Assets/Resources" + fullPath.Substring(fullPath.IndexOf('/', 7));
                // resPath example: Assets/Resources/Prefabs/NPC/Villager.prefab
                if (!File.Exists(resPath))
                    AssetDatabase.CopyPath(fullPath, resPath);
            }
            return prefab;
        }

        /// <summary>Create the two NPC prefabs used by the tribe camps.</summary>
        private static void CreateNpcPrefabs()
        {
            EnsureFolder("Assets/Prefabs/NPC");
            CreateNpcPrefab("Villager", "Sprites/NPC/Villager/south/villager_south_0",
                "Sprites/NPC/Villager/south/villager_south_{0}", 4);
            CreateNpcPrefab("Elder", "Sprites/NPC/Elder/south/villager_south_0",
                "Sprites/NPC/Elder/south/villager_south_{0}", 4);
        }

        private static void CreateNpcPrefab(string name, string stillPath, string walkFormat, int frames)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite(stillPath);
            sr.sortingOrder = 0;
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.8f, 1.4f);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            var walker = go.AddComponent<NpcWalker>();
            var arr = new Sprite[frames];
            for (int i = 0; i < frames; i++)
                arr[i] = LoadSprite(string.Format(walkFormat, i));
            walker.walkSouth = arr;

            CreatePrefab(go, PREFAB_PATH + "NPC/" + name);
            Debug.Log($"[ProjectSetup] NPC prefab '{name}' created.");
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

            // --- AAA pass: expanded item catalog ---
            CreateItemSO("flint_shard", "Flint Shard", "A sharp-knapped flake of flint.",
                ItemCategory.Resource, false, 0f, 0f, 0f, 0f, 0.2f, "Items/flint_shard");
            CreateItemSO("bone", "Bone", "Sturdy bone from a hunted animal.",
                ItemCategory.Resource, false, 0f, 0f, 0f, 0f, 1f, "Items/bone");
            CreateItemSO("sinew", "Sinew", "Tough animal fibre, nature's rope.",
                ItemCategory.Resource, false, 0f, 0f, 0f, 0f, 0.1f, "Items/sinew");
            CreateItemSO("obsidian", "Obsidian", "Volcanic glass, sharper than any flint.",
                ItemCategory.Resource, false, 0f, 0f, 0f, 0f, 0.3f, "Items/obsidian");
            CreateItemSO("copper_ore", "Copper Ore", "Green-flecked rock hiding the age of metal.",
                ItemCategory.Resource, false, 0f, 0f, 0f, 0f, 2f, "Items/copper_ore");
            CreateItemSO("fur_pelt", "Thick Pelt", "A dense winter pelt.",
                ItemCategory.Resource, false, 0f, 0f, 0f, 0f, 2.5f, "Items/fur_pelt");
            CreateItemSO("bone_spear", "Bone Spear", "Fire-hardened point lashed to a shaft.",
                ItemCategory.Weapon, false, 0f, 0f, 0f, 0f, 1.5f, "Items/bone_spear");
            CreateItemSO("obsidian_knife", "Obsidian Knife", "Razor edge for skinning and cutting.",
                ItemCategory.Tool, false, 0f, 0f, 0f, 0f, 0.4f, "Items/obsidian_knife");
            CreateItemSO("fur_cloak", "Fur Cloak", "Warmth through the longest winter.",
                ItemCategory.Clothing, false, 0f, 0f, 0f, 0f, 3f, "Items/fur_cloak");
            CreateItemSO("hide_leggings", "Hide Leggings", "Simple leg protection.",
                ItemCategory.Clothing, false, 0f, 0f, 0f, 0f, 1.2f, "Items/hide_leggings");
            CreateItemSO("water_skin", "Waterskin", "Carry water across the dry lands.",
                ItemCategory.Tool, false, 0f, 0f, 0f, 0f, 0.8f, "Items/water_skin");
            CreateItemSO("healing_salve", "Healing Salve", "Crushed herbs in animal fat.",
                ItemCategory.Food, true, 0f, 0f, 30f, 5f, 0.3f, "Items/healing_salve");
            CreateItemSO("wooden_bowl", "Wooden Bowl", "Carved from a burl, holds a meal.",
                ItemCategory.Resource, false, 0f, 0f, 0f, 0f, 0.7f, "Items/wooden_bowl");
            CreateItemSO("dried_meat", "Dried Meat", "Smoked strips that keep for weeks.",
                ItemCategory.Food, true, 40f, 0f, 8f, 0f, 0.6f, "Items/dried_meat");
            CreateItemSO("atlatl", "Spear-Thrower", "Lever and grip that hurl spears far.",
                ItemCategory.Weapon, false, 0f, 0f, 0f, 0f, 0.9f, "Items/atlatl");
            CreateItemSO("totem", "Tribe Totem", "Carved guardian of the camp.",
                ItemCategory.Building, false, 0f, 0f, 0f, 0f, 8f, "Items/totem");
            CreateItemSO("drum", "Shaman's Drum", "The heartbeat of the tribe.",
                ItemCategory.Resource, false, 0f, 0f, 0f, 0f, 1.5f, "Items/drum");
            CreateItemSO("copper_amulet", "Copper Amulet", "First metal of a new age.",
                ItemCategory.Resource, false, 0f, 0f, 0f, 0f, 0.2f, "Items/copper_amulet");
            CreateItemSO("herb_pouch", "Herb Pouch", "Dried meadow herbs and roots.",
                ItemCategory.Resource, false, 0f, 0f, 0f, 0f, 0.4f, "Items/herb_pouch");
            CreateItemSO("workbench", "Workbench", "A steady place for finer work.",
                ItemCategory.Building, false, 0f, 0f, 0f, 0f, 15f, "Items/workbench");
            CreateItemSO("tent", "Hide Tent", "Poles and hides: a portable home.",
                ItemCategory.Building, false, 0f, 0f, 0f, 0f, 12f, "Items/tent");
            CreateItemSO("hut", "Tribe Hut", "A sturdy shelter for the whole fire circle.",
                ItemCategory.Building, false, 0f, 0f, 0f, 0f, 40f, "Items/hut");

            // --- AAA pass: expanded recipes (era-gated) ---
            CreateRecipeSO("flint_knapping", "Flint Shard", "Knock sharp flakes from flint.",
                new (string, int)[] { ("stone", 2) }, "flint_shard", 2, 3f, "");
            CreateRecipeSO("bone_spear", "Bone Spear", "A proper hunting weapon.",
                new (string, int)[] { ("bone", 2), ("wood_log", 2), ("sinew", 2) },
                "bone_spear", 1, 8f, "", 1);
            CreateRecipeSO("obsidian_knife", "Obsidian Knife", "Skin game twice as fast.",
                new (string, int)[] { ("obsidian", 1), ("wood_log", 1), ("sinew", 1) },
                "obsidian_knife", 1, 6f, "", 1);
            CreateRecipeSO("fur_cloak", "Fur Cloak", "Sew pelts into winter gear.",
                new (string, int)[] { ("fur_pelt", 3), ("sinew", 2) },
                "fur_cloak", 1, 12f, "", 1);
            CreateRecipeSO("hide_leggings", "Hide Leggings", "Basic leg armour.",
                new (string, int)[] { ("animal_hide", 2), ("sinew", 1) },
                "hide_leggings", 1, 8f, "", 1);
            CreateRecipeSO("water_skin", "Waterskin", "An empty stomach-shaped pouch.",
                new (string, int)[] { ("animal_hide", 1), ("sinew", 2) },
                "water_skin", 1, 6f, "");
            CreateRecipeSO("healing_salve", "Healing Salve", "Herbal medicine.",
                new (string, int)[] { ("berries", 4), ("animal_hide", 1) },
                "healing_salve", 1, 5f, "");
            CreateRecipeSO("wooden_bowl", "Wooden Bowl", "Carve a bowl from a log section.",
                new (string, int)[] { ("wood_log", 1) }, "wooden_bowl", 1, 4f, "");
            CreateRecipeSO("dried_meat", "Dried Meat", "Preserve the hunt.",
                new (string, int)[] { ("raw_meat", 2) }, "dried_meat", 2, 14f, "campfire");
            CreateRecipeSO("atlatl", "Spear-Thrower", "A lever that multiplies the arm.",
                new (string, int)[] { ("wood_log", 1), ("bone", 1), ("sinew", 1) },
                "atlatl", 1, 8f, "", 1);
            CreateRecipeSO("totem", "Tribe Totem", "Raise a guardian totem.",
                new (string, int)[] { ("wood_log", 4), ("bone", 2), ("fur_pelt", 1) },
                "totem", 1, 15f, "", 1);
            CreateRecipeSO("drum", "Shaman's Drum", "Hide stretched over a hollow frame.",
                new (string, int)[] { ("wood_log", 2), ("animal_hide", 1), ("sinew", 2) },
                "drum", 1, 10f, "", 1);
            CreateRecipeSO("copper_amulet", "Copper Amulet", "Shape the first metal.",
                new (string, int)[] { ("copper_ore", 2), ("sinew", 1) },
                "copper_amulet", 1, 14f, "workbench", 2);
            CreateRecipeSO("herb_pouch", "Herb Pouch", "Gather meadow medicine.",
                new (string, int)[] { ("fiber", 4), ("berries", 2) },
                "herb_pouch", 1, 4f, "");
            CreateRecipeSO("workbench", "Workbench", "Fine craft needs a firm bench.",
                new (string, int)[] { ("wood_log", 6), ("stone", 4) },
                "workbench", 1, 15f, "", 1);
            CreateRecipeSO("tent", "Hide Tent", "Build a shelter to sleep safe.",
                new (string, int)[] { ("wood_log", 6), ("animal_hide", 4), ("sinew", 4) },
                "tent", 1, 18f, "");
            CreateRecipeSO("hut", "Tribe Hut", "A home for the whole fire circle.",
                new (string, int)[] { ("wood_log", 14), ("animal_hide", 8), ("sinew", 8), ("stone", 6) },
                "hut", 1, 30f, "", 1);

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

            // Mirror into Resources so runtime systems can load items by id
            // (quest rewards, trade offers, pickup prefabs...).
            EnsureFolder("Assets/Resources/Items");
            string resPath = "Assets/Resources/Items/" + id + ".asset";
            if (!File.Exists(resPath))
            {
                var resSo = ScriptableObject.CreateInstance<ItemDataSO>();
                resSo.data = new ItemData
                {
                    itemId = so.data.itemId,
                    displayName = so.data.displayName,
                    description = so.data.description,
                    category = so.data.category,
                    isConsumable = so.data.isConsumable,
                    hungerRestore = so.data.hungerRestore,
                    thirstRestore = so.data.thirstRestore,
                    healthRestore = so.data.healthRestore,
                    energyRestore = so.data.energyRestore,
                    weight = so.data.weight,
                    maxStack = so.data.maxStack,
                    icon = so.data.icon
                };
                AssetDatabase.CreateAsset(resSo, resPath);
            }
        }

        private static void CreateRecipeSO(string id, string displayName, string desc,
            (string itemId, int amount)[] ingredients, string outputId, int outputAmount,
            float craftTime, string station, int requiredEra = 0)
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
                requiredEra = requiredEra,
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
            CreateNpcPrefabs();
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

            // Full action animation sets (AAA art pass)
            var actionAnim = player.AddComponent<PrehistoricSurvival.Art.PlayerActionAnimator>();
            string[] dirs8 = { "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest" };
            string[] dirFolders = { "North", "NorthEast", "East", "SouthEast", "South", "SouthWest", "West", "NorthWest" };
            actionAnim.attack = LoadActionSet(dirs8, dirFolders, "attack", 3);
            actionAnim.gather = LoadActionSet(dirs8, dirFolders, "gather", 3);
            actionAnim.swim = LoadActionSet(dirs8, dirFolders, "swim", 4);
            actionAnim.climb = LoadActionSet(dirs8, dirFolders, "climb", 2);
            actionAnim.hit = LoadActionSet(dirs8, dirFolders, "hit", 1);
            for (int i = 0; i < 3; i++)
                actionAnim.die[i] = LoadSprite($"Player/Die/south/player_south_die_{i}");

            // Player Controller
            var pc = player.AddComponent<PlayerController>();
            pc.spritesNorth = LoadWalkFrames("North", "north");
            pc.spritesNorthEast = LoadWalkFrames("NorthEast", "northeast");
            pc.spritesEast = LoadWalkFrames("East", "east");
            pc.spritesSouthEast = LoadWalkFrames("SouthEast", "southeast");
            pc.spritesSouth = LoadWalkFrames("South", "south");
            pc.spritesSouthWest = LoadWalkFrames("SouthWest", "southwest");
            pc.spritesWest = LoadWalkFrames("West", "west");
            pc.spritesNorthWest = LoadWalkFrames("NorthWest", "northwest");
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

        private static Sprite[] LoadWalkFrames(string folder, string dir)
        {
            var frames = new Sprite[6];
            for (int i = 0; i < 6; i++)
                frames[i] = LoadSprite($"Player/Walk/{folder}/player_{dir}_walk_{i}");
            return frames;
        }

        private static Sprite[][] LoadActionSet(string[] dirsLower, string[] dirsUpper, string action, int frames)
        {
            var set = new Sprite[8][];
            for (int d = 0; d < 8; d++)
            {
                var arr = new Sprite[frames];
                for (int f = 0; f < frames; f++)
                    arr[f] = LoadSprite($"Player/{char.ToUpper(action[0]) + action.Substring(1)}/{dirsUpper[d]}/player_{dirsLower[d]}_{action}_{f}");
                set[d] = arr;
            }
            return set;
        }

        // --- ANIMALS ---
        private static void CreateAnimalPrefabs()
        {
            // All 15 species, driven by the shared catalog.
            foreach (var def in PrehistoricSurvival.Content.AnimalCatalog.All)
            {
                string folder = "Animals/" + def.prefabName;
                string key = def.prefabName.ToLowerInvariant();
                var go = new GameObject(def.prefabName);
                go.tag = "Animal";

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = LoadSprite(folder + "/south/" + key + "_south_walk_0");
                sr.sortingOrder = 0;

                var col = go.AddComponent<BoxCollider2D>();
                col.size = def.bird ? new Vector2(0.9f, 0.9f) : new Vector2(1.5f, 1.5f);

                var rb = go.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.freezeRotation = true;

                var ai = go.AddComponent<AnimalAI>();
                ai.animalName = def.displayName;
                ai.maxHealth = def.maxHealth;
                ai.damage = def.damage;
                ai.moveSpeed = def.moveSpeed;
                ai.runSpeed = def.runSpeed;
                ai.detectionRange = def.detectionRange;
                ai.attackRange = def.attackRange;
                ai.leashRange = def.leashRange;
                ai.fleeThreshold = def.fleeThreshold;
                ai.aggression = def.aggression;
                ai.spriteRenderer = sr;

                var animator = go.AddComponent<AnimalWalkAnimator>();
                string[] dirs = { "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest" };
                var loaded = new Sprite[8][];
                for (int d = 0; d < dirs.Length; d++)
                {
                    var frames = new Sprite[4];
                    for (int f = 0; f < 4; f++)
                        frames[f] = LoadSprite($"{folder}/{dirs[d]}/{key}_{dirs[d]}_walk_{f}");
                    loaded[d] = frames;
                }
                animator.north = loaded[0]; animator.northEast = loaded[1]; animator.east = loaded[2];
                animator.southeast = loaded[3]; animator.south = loaded[4]; animator.southWest = loaded[5];
                animator.west = loaded[6]; animator.northWest = loaded[7];
                for (int f = 0; f < 3; f++)
                {
                    animator.attackFrames[f] = LoadSprite($"{folder}/east/{key}_east_attack_{f}");
                    animator.deathFrames[f] = LoadSprite($"{folder}/east/{key}_death_{f}");
                }

                var dropper = go.AddComponent<LootDropper>();
                dropper.scatterRadius = 1.5f;
                dropper.lootTable = new LootDropper.LootEntry[2];
                var rawMeatSO = AssetDatabase.LoadAssetAtPath<ItemDataSO>(SO_PATH + "Items/raw_meat.asset");
                var hideSO = AssetDatabase.LoadAssetAtPath<ItemDataSO>(SO_PATH + "Items/animal_hide.asset");
                dropper.lootTable[0] = new LootDropper.LootEntry
                {
                    item = rawMeatSO != null ? rawMeatSO.data : null,
                    minAmount = def.meatMin, maxAmount = def.meatMax, dropChance = 1f
                };
                dropper.lootTable[1] = new LootDropper.LootEntry
                {
                    item = hideSO != null ? hideSO.data : null,
                    minAmount = def.hideMin, maxAmount = def.hideMax, dropChance = def.hideMin > 0 ? 1f : 0f
                };

                go.AddComponent<UnityEngine.Rendering.Universal.ShadowCaster2D>();

                CreatePrefab(go, PREFAB_PATH + "Animals/" + def.prefabName);
            }
            Debug.Log("[ProjectSetup] 15 animal prefabs created (catalog-driven).");
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

            // Directional walk animation. Multiple imported frames can be added to
            // these arrays later without changing the prefab or runtime code.
            var animator = go.AddComponent<AnimalWalkAnimator>();
            animator.north = new[] { LoadSprite(spriteFolder + folder.ToLower() + "_north") };
            animator.northEast = new[] { LoadSprite(spriteFolder + folder.ToLower() + "_northeast") };
            animator.east = new[] { LoadSprite(spriteFolder + folder.ToLower() + "_east") };
            animator.southEast = new[] { LoadSprite(spriteFolder + folder.ToLower() + "_southeast") };
            animator.south = new[] { LoadSprite(spriteFolder + folder.ToLower() + "_south") };
            animator.southWest = new[] { LoadSprite(spriteFolder + folder.ToLower() + "_southwest") };
            animator.west = new[] { LoadSprite(spriteFolder + folder.ToLower() + "_west") };
            animator.northWest = new[] { LoadSprite(spriteFolder + folder.ToLower() + "_northwest") };

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
            CreateBushPrefab("FlowerBush", "Flowering Shrub", VegetationInteraction.VegetationType.BerryBush,
                "Vegetation/Bushes/flower_bush", "herb_pouch", 2);
            CreateBushPrefab("Reeds", "Riverside Reeds", VegetationInteraction.VegetationType.Vine,
                "Vegetation/Bushes/reeds", "fiber", 3);

            // New trees (biome variety)
            CreateTreePrefab("BirchTree", "Birch Tree", VegetationInteraction.VegetationType.TimberTree,
                "Vegetation/Trees/birch_tree", "wood_log", 3);
            CreateTreePrefab("PalmTree", "Palm Tree", VegetationInteraction.VegetationType.TimberTree,
                "Vegetation/Trees/palm_tree", "wood_log", 3);
            CreateTreePrefab("JungleTree", "Kapok Tree", VegetationInteraction.VegetationType.TimberTree,
                "Vegetation/Trees/jungle_tree", "wood_log", 6);
            CreateTreePrefab("DeadTree", "Dead Tree", VegetationInteraction.VegetationType.TimberTree,
                "Vegetation/Trees/dead_tree", "wood_log", 2);
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
            CreateRockPrefab("FlintOutcrop", "Vegetation/Rocks/flint_outcrop");
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
                "stone_pickaxe", "stone_axe", "torch",
                "flint_shard", "bone", "sinew", "obsidian", "copper_ore", "fur_pelt",
                "bone_spear", "obsidian_knife", "fur_cloak", "hide_leggings", "water_skin",
                "healing_salve", "wooden_bowl", "dried_meat", "atlatl", "herb_pouch"
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

            // AAA pass: tribe structures (used by build mode and the camp system)
            CreateStructure("Tent", "Structures/tent");
            CreateStructure("Workbench", "Structures/workbench");
            CreateStructure("Hut", "Structures/hut");
            CreateStructure("TradePost", "Structures/trade_post");

            Debug.Log("[ProjectSetup] Structure prefabs created.");
        }

        private static void CreateStructure(string name, string spritePath)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite(spritePath);
            sr.sortingOrder = 0;
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(2f, 2f);
            CreatePrefab(go, PREFAB_PATH + "Structures/" + name);
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

            // --- Camera ---
            var cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 8f;
                cam.transform.position = new Vector3(0f, 0f, -10f);
                cam.transform.rotation = Quaternion.identity;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.10f, 0.08f, 0.05f);
            }

            // --- Canvas ---
            var canvasGO = new GameObject("MenuCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            EnsureEventSystem();

            // --- Background ---
            var bg = new GameObject("Background");
            bg.transform.SetParent(canvasGO.transform, false);
            var bgRT = bg.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            bg.AddComponent<Image>().color = new Color(0.13f, 0.10f, 0.06f);

            // --- Title ---
            var title = CreateTextElement(canvasGO.transform, "Title", "WORLD EVOLUTION SAGA", 78,
                new Vector2(0, 0), new Vector2(1600, 130));
            title.color = new Color(0.93f, 0.83f, 0.55f);
            AnchorRect(title.rectTransform, new Vector2(0.5f, 0.82f));

            var subtitle = CreateTextElement(canvasGO.transform, "Subtitle",
                "Old Stone Age — survive an entire planet", 32, Vector2.zero, new Vector2(1400, 60));
            subtitle.color = new Color(0.72f, 0.66f, 0.52f);
            AnchorRect(subtitle.rectTransform, new Vector2(0.5f, 0.74f));

            // --- Buttons ---
            var play = CreateMenuButton(canvasGO.transform, "PlayButton", "NEW GAME",
                new Vector2(0.5f, 0.55f), new Color(0.26f, 0.18f, 0.10f, 0.95f));
            var load = CreateMenuButton(canvasGO.transform, "LoadButton", "CONTINUE",
                new Vector2(0.5f, 0.43f), new Color(0.26f, 0.18f, 0.10f, 0.95f));
            var settings = CreateMenuButton(canvasGO.transform, "SettingsButton", "SETTINGS",
                new Vector2(0.5f, 0.31f), new Color(0.22f, 0.17f, 0.10f, 0.95f));
            var quit = CreateMenuButton(canvasGO.transform, "QuitButton", "QUIT",
                new Vector2(0.5f, 0.19f), new Color(0.30f, 0.13f, 0.10f, 0.95f));

            // --- Controller: wires every button (also at runtime) ---
            var controllerGO = new GameObject("MainMenuController");
            var controller = controllerGO.AddComponent<MainMenuController>();
            controller.playButton = play;
            controller.continueButton = load;
            controller.settingsButton = settings;
            controller.quitButton = quit;
            controller.buildUIIfMissing = false;

            // Persistent onClick hooks so the buttons work even before Start() runs.
            UnityEditor.Events.UnityEventTools.AddPersistentListener(play.onClick, controller.OnPlay);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(load.onClick, controller.OnContinue);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(settings.onClick, controller.OnSettings);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(quit.onClick, controller.OnQuit);

            EditorSceneManager.SaveScene(scene, SCENE_PATH + "MainMenu.unity");
            Debug.Log("[ProjectSetup] MainMenu scene created (buttons wired).");
        }

        private static void AnchorRect(RectTransform rt, Vector2 anchor)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        private static Button CreateMenuButton(Transform parent, string name, string label,
            Vector2 anchor, Color bgColor)
        {
            var btnGO = new GameObject(name);
            btnGO.transform.SetParent(parent, false);
            var rt = btnGO.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(460, 96);
            rt.anchoredPosition = Vector2.zero;

            var img = btnGO.AddComponent<Image>();
            img.color = bgColor;

            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = img;

            var text = CreateTextElement(btnGO.transform, "Label", label, 34, Vector2.zero, new Vector2(440, 90));
            text.color = new Color(0.93f, 0.86f, 0.70f);
            var trt = text.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            return btn;
        }

        // ==================================================================
        // GAMEPLAY SCENE
        //
        // The gameplay scene stays deliberately tiny: a camera, an EventSystem and
        // the GameBootstrap component. Everything else — the planet, streaming
        // chunks, player, managers, joystick and HUD — is created at runtime, which
        // is what makes the whole-earth world possible (it cannot be baked into a
        // scene file) and keeps the scene mergeable and tiny.
        // ==================================================================
        private static void CreateGameplayWorldScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 9f;
                cam.transform.position = new Vector3(0f, 0f, -10f);
                cam.transform.rotation = Quaternion.identity;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.35f, 0.55f, 0.75f);
                cam.gameObject.AddComponent<CameraFollow>();
            }

            var bootstrapGO = new GameObject("GameBootstrap");
            var bootstrap = bootstrapGO.AddComponent<GameBootstrap>();
            bootstrap.loadRadius = 3;
            bootstrap.propDensity = 1f;
            bootstrap.spawnAnimals = true;
            bootstrap.createHUD = true;
            bootstrap.createPauseMenu = true;
            bootstrap.createWorldMap = true;

            // A global light so URP 2D lighting is visible from the first frame.
            var lightGO = new GameObject("GlobalLight2D");
            var globalLight = lightGO.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            globalLight.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;
            globalLight.color = new Color(1f, 0.96f, 0.88f);
            globalLight.intensity = 1f;

            EnsureEventSystem();

            EditorSceneManager.SaveScene(scene, SCENE_PATH + "GameplayWorld.unity");
            Debug.Log("[ProjectSetup] GameplayWorld scene created (runtime bootstrapped world).");
        }

        // ==================================================================
        // GAME LIBRARY  (Resources asset used by the runtime bootstrapper)
        // ==================================================================
        private static void CreateGameLibrary()
        {
            EnsureFolder("Assets/Resources");
            const string libPath = "Assets/Resources/GameLibrary.asset";

            var lib = AssetDatabase.LoadAssetAtPath<GameLibrary>(libPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<GameLibrary>();
                AssetDatabase.CreateAsset(lib, libPath);
            }

            lib.playerPrefab = LoadPrefab("Player/Player");

            lib.groundSprites = new[]
            {
                LoadSprite("Terrain/Ground/dirt_tile"),
                LoadSprite("Terrain/Ground/grass_tile"),
                LoadSprite("Terrain/Ground/sand_tile"),
                LoadSprite("Terrain/Ground/snow_tile"),
                LoadSprite("Terrain/Ground/stone_tile"),
                LoadSprite("Terrain/Ground/mud_tile"),
            };
            lib.oceanWaterSprite = LoadSprite("Terrain/Water/ocean_water_tile");
            lib.shallowWaterSprite = LoadSprite("Terrain/Water/calm_water_tile");
            lib.riverWaterSprite = LoadSprite("Terrain/Water/river_water_tile");

            lib.coldTreePrefabs = new[] { LoadPrefab("Vegetation/PineTree"), LoadPrefab("Vegetation/BirchTree"), LoadPrefab("Vegetation/DeadTree") };
            lib.temperateTreePrefabs = new[] { LoadPrefab("Vegetation/OakTree"), LoadPrefab("Vegetation/AppleTree"), LoadPrefab("Vegetation/BirchTree") };
            lib.tropicalTreePrefabs = new[] { LoadPrefab("Vegetation/FigTree"), LoadPrefab("Vegetation/AppleTree"), LoadPrefab("Vegetation/PalmTree"), LoadPrefab("Vegetation/JungleTree") };
            lib.bushPrefabs = new[] { LoadPrefab("Vegetation/BerryBush"), LoadPrefab("Vegetation/Vine"), LoadPrefab("Vegetation/FlowerBush"), LoadPrefab("Vegetation/Reeds") };
            lib.rockPrefabs = new[] { LoadPrefab("Terrain/LargeRock"), LoadPrefab("Terrain/StoneCluster"), LoadPrefab("Terrain/FlintOutcrop") };

            lib.mammothPrefab = LoadPrefab("Animals/Mammoth");
            lib.sabertoothPrefab = LoadPrefab("Animals/Sabertooth");
            lib.caveBearPrefab = LoadPrefab("Animals/CaveBear");
            lib.bisonPrefab = LoadPrefab("Animals/Bison");
            lib.extraAnimalPrefabs = new[]
            {
                LoadPrefab("Animals/WoollyRhino"), LoadPrefab("Animals/CaveLion"),
                LoadPrefab("Animals/DireWolf"), LoadPrefab("Animals/CaveHyena"),
                LoadPrefab("Animals/Reindeer"), LoadPrefab("Animals/MuskOx"),
                LoadPrefab("Animals/GiantElk"), LoadPrefab("Animals/WildBoar"),
                LoadPrefab("Animals/SnowHare"), LoadPrefab("Animals/CavePtarmigan"),
                LoadPrefab("Animals/GreatAuk"),
            };

            lib.healthIcon = LoadSprite("UI/Icons/health_icon");
            lib.hungerIcon = LoadSprite("UI/Icons/hunger_icon");
            lib.thirstIcon = LoadSprite("UI/Icons/thirst_icon");
            lib.energyIcon = LoadSprite("UI/Icons/energy_icon");
            lib.staminaIcon = LoadSprite("UI/Icons/stamina_icon");

            // Audio is a separate Resources asset so designers can swap sound banks
            // without touching scenes or prefabs.
            EnsureFolder("Assets/Resources");
            const string audioPath = "Assets/Resources/AudioLibrary.asset";
            var audio = AssetDatabase.LoadAssetAtPath<AudioLibrary>(audioPath);
            if (audio == null)
            {
                audio = ScriptableObject.CreateInstance<AudioLibrary>();
                AssetDatabase.CreateAsset(audio, audioPath);
            }
            lib.audioLibrary = audio;
            audio.footsteps = new[] { LoadAudio("stone_step") };
            audio.pickup = new[] { LoadAudio("pickup_chime") };
            audio.craft = new[] { LoadAudio("craft_tap") };
            audio.impact = new[] { LoadAudio("impact") };
            audio.water = new[] { LoadAudio("water_splash") };
            audio.ui = new[] { LoadAudio("ui_click") };

            lib.recipeDatabase = AssetDatabase.LoadAssetAtPath<RecipeDatabase>(
                SO_PATH + "Recipes/RecipeDatabase.asset");

            EditorUtility.SetDirty(audio);
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] GameLibrary created at Assets/Resources/GameLibrary.asset");
        }

        private static AudioClip LoadAudio(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/" + name + ".wav");
            if (clip == null) Debug.LogWarning($"[ProjectSetup] Audio clip not found: {name}");
            return clip;
        }

        private static GameObject LoadPrefab(string relativePath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH + relativePath + ".prefab");
            if (prefab == null) Debug.LogWarning($"[ProjectSetup] Prefab not found: {relativePath}");
            return prefab;
        }

        // ==================================================================
        // BUILD SETTINGS / PLAYER SETTINGS
        // ==================================================================
        private static void ConfigureBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(SCENE_PATH + "MainMenu.unity", true),
                new EditorBuildSettingsScene(SCENE_PATH + "GameplayWorld.unity", true)
            };
            EditorBuildSettings.scenes = scenes.ToArray();

            PlayerSettings.companyName = "World Evolution Saga";
            PlayerSettings.productName = "World Evolution Saga";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            // Old Input Manager APIs (Input.GetAxisRaw) are used by the player controller,
            // so make sure both input backends are enabled.
            EnableBothInputBackends();

            Debug.Log("[ProjectSetup] Build settings configured (MainMenu + GameplayWorld).");
        }

        // ==================================================================
        // UNIVERSAL RENDER PIPELINE (2D renderer + Light2D support)
        // ==================================================================
        private static void ConfigureRenderPipeline()
        {
            const string rendererPath = "Assets/Settings/Renderer2D.asset";
            const string pipelinePath = "Assets/Settings/URP-2D.asset";

            try
            {
                if (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null)
                {
                    Debug.Log("[ProjectSetup] A render pipeline asset is already assigned — leaving it alone.");
                    return;
                }

                EnsureFolder("Assets/Settings");

                // Renderer2DData / UniversalRenderPipelineAsset are resolved by reflection so
                // this keeps compiling across URP versions.
                var rendererType = System.Type.GetType(
                    "UnityEngine.Rendering.Universal.Renderer2DData, Unity.RenderPipelines.Universal.Runtime");
                var pipelineType = System.Type.GetType(
                    "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime");
                if (rendererType == null || pipelineType == null)
                {
                    Debug.LogWarning("[ProjectSetup] URP types not found — create a URP asset manually " +
                                     "(Assets → Create → Rendering → URP Asset with 2D Renderer).");
                    return;
                }

                var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(rendererPath);
                if (rendererData == null)
                {
                    rendererData = ScriptableObject.CreateInstance(rendererType);
                    rendererData.name = "Renderer2D";
                    AssetDatabase.CreateAsset(rendererData, rendererPath);
                }

                var createMethod = pipelineType.GetMethod("Create",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (createMethod == null)
                {
                    Debug.LogWarning("[ProjectSetup] Could not create the URP asset automatically.");
                    return;
                }

                var pipeline = createMethod.Invoke(null, new object[] { rendererData })
                    as UnityEngine.Rendering.RenderPipelineAsset;
                if (pipeline == null) return;

                AssetDatabase.CreateAsset(pipeline, pipelinePath);
                UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline;
                QualitySettings.renderPipeline = pipeline;
                AssetDatabase.SaveAssets();

                Debug.Log("[ProjectSetup] URP 2D render pipeline created and assigned (Light2D now works).");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ProjectSetup] Render pipeline setup skipped: " + e.Message);
            }
        }

        private static void EnableBothInputBackends()        {
            try
            {
                var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
                if (settings == null || settings.Length == 0) return;
                var so = new SerializedObject(settings[0]);
                var prop = so.FindProperty("activeInputHandler");
                if (prop != null && prop.intValue != 2)
                {
                    prop.intValue = 2; // 0 = old, 1 = new, 2 = both
                    so.ApplyModifiedProperties();
                    AssetDatabase.SaveAssets();
                    Debug.Log("[ProjectSetup] Enabled both input backends (editor restart may be required).");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ProjectSetup] Could not set the active input handler: " + e.Message);
            }
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
