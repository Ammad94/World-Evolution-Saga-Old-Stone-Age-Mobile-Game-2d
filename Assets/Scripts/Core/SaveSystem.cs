using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PrehistoricSurvival.Core
{
    /// <summary>
    /// Handles saving and loading game state to/from JSON files.
    /// Stores player position, inventory, stats, and world state.
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        public static SaveSystem Instance { get; private set; }

        [Header("Settings")]
        public string saveFileName = "prehistoric_save.json";
        public int maxAutoSaves = 3;
        public float autoSaveInterval = 300f; // 5 minutes

        private float _autoSaveTimer;
        private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

            _autoSaveTimer += Time.deltaTime;
            if (_autoSaveTimer >= autoSaveInterval)
            {
                _autoSaveTimer = 0f;
                SaveGame();
            }
        }

        // ------------------------------------------------------------------
        // Save Data Structure
        // ------------------------------------------------------------------
        [Serializable]
        public class SaveData
        {
            public string version = "1.0.0";
            public long timestamp;

            // Player
            public float[] playerPosition = new float[3];
            public float playerHealth;
            public float playerHunger;
            public float playerThirst;
            public float playerEnergy;
            public float playerStamina;

            // Inventory
            public List<SavedItem> inventory = new List<SavedItem>();

            // World
            public int currentSeason;
            public float timeOfDay;
            public int dayNumber;
            public List<SavedWaypoint> waypoints = new List<SavedWaypoint>();

            // Saga progression (AAA pass)
            public int eraIndex;
            public float eraKnowledge;
            public float tribeFriendship;
            public List<SavedQuest> quests = new List<SavedQuest>();
        }

        [Serializable]
        public class SavedQuest
        {
            public string id;
            public bool done;
            public int[] progress = new int[0];
        }

        [Serializable]
        public class SavedItem
        {
            public string itemId;
            public int quantity;
        }

        [Serializable]
        public class SavedWaypoint
        {
            public string name;
            public float x, y, z;
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------
        public void SaveGame()
        {
            try
            {
                var data = new SaveData
                {
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                // Gather player data
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    var pos = player.transform.position;
                    data.playerPosition = new[] { pos.x, pos.y, pos.z };
                }

                // Gather survival stats
                var stats = FindFirstObjectByType<Survival.SurvivalStats>();
                if (stats != null)
                {
                    data.playerHealth = stats.Health;
                    data.playerHunger = stats.Hunger;
                    data.playerThirst = stats.Thirst;
                    data.playerEnergy = stats.Energy;
                    data.playerStamina = stats.Stamina;
                }

                // Gather inventory
                var inv = InventorySystem.Instance;
                if (inv != null)
                {
                    foreach (var slot in inv.Slots)
                    {
                        if (!slot.IsEmpty)
                        {
                            data.inventory.Add(new SavedItem
                            {
                                itemId = slot.item.itemId,
                                quantity = slot.quantity
                            });
                        }
                    }
                }

                // Gather season/time data
                var seasonMgr = FindFirstObjectByType<Survival.SeasonManager>();
                if (seasonMgr != null)
                {
                    data.currentSeason = (int)seasonMgr.CurrentSeason;
                    data.timeOfDay = seasonMgr.TimeOfDay;
                    data.dayNumber = seasonMgr.DayNumber;
                }

                // Saga progression
                var era = Content.EraProgression.Instance;
                if (era != null)
                {
                    data.eraIndex = (int)era.CurrentEra;
                    data.eraKnowledge = era.Knowledge;
                }
                var camps = Content.TribeCampSystem.Instance;
                if (camps != null) data.tribeFriendship = camps.Friendship;
                var quests = Content.QuestSystem.Instance;
                if (quests != null) data.quests = quests.Snapshot();

                // Serialize and write
                string json = JsonUtility.ToJson(data, true);
                string tempPath = SavePath + ".tmp";
                string backupPath = SavePath + ".bak";
                File.WriteAllText(tempPath, json);
                if (File.Exists(SavePath)) File.Copy(SavePath, backupPath, true);
                File.Copy(tempPath, SavePath, true);
                File.Delete(tempPath);
                Debug.Log($"[SaveSystem] Game saved to {SavePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Failed to save: {e.Message}");
            }
        }

        public bool LoadGame()
        {
            if (!File.Exists(SavePath))
            {
                Debug.LogWarning("[SaveSystem] No save file found.");
                return false;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                var data = JsonUtility.FromJson<SaveData>(json);

                // Restore player position
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    player.transform.position = new Vector3(
                        data.playerPosition[0],
                        data.playerPosition[1],
                        data.playerPosition[2]
                    );
                }

                // Restore survival stats
                var stats = FindFirstObjectByType<Survival.SurvivalStats>();
                if (stats != null)
                {
                    stats.Health = data.playerHealth;
                    stats.Hunger = data.playerHunger;
                    stats.Thirst = data.playerThirst;
                    stats.Energy = data.playerEnergy;
                    stats.Stamina = data.playerStamina;
                }

                // Restore season/time
                var seasonMgr = FindFirstObjectByType<Survival.SeasonManager>();
                if (seasonMgr != null)
                {
                    seasonMgr.SetSeason((Survival.Season)data.currentSeason);
                    seasonMgr.TimeOfDay = data.timeOfDay;
                    seasonMgr.DayNumber = data.dayNumber;
                }

                // Saga progression
                var era = Content.EraProgression.Instance;
                if (era != null) era.Restore(data.eraIndex, data.eraKnowledge);
                var camps = Content.TribeCampSystem.Instance;
                if (camps != null) camps.RestoreFriendship(data.tribeFriendship);
                var quests = Content.QuestSystem.Instance;
                if (quests != null) quests.Restore(data.quests);

                Debug.Log("[SaveSystem] Game loaded successfully.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Failed to load: {e.Message}");
                return false;
            }
        }

        public void DeleteSave()
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
                Debug.Log("[SaveSystem] Save file deleted.");
            }
        }

        public bool HasSaveFile() => File.Exists(SavePath);

        /// <summary>Default save location, usable before any SaveSystem instance exists.</summary>
        public static string DefaultSavePath =>
            Path.Combine(Application.persistentDataPath, "prehistoric_save.json");

        /// <summary>
        /// Static check used by the main menu (which runs in a scene without a SaveSystem).
        /// </summary>
        public static bool HasSave()
        {
            if (Instance != null) return Instance.HasSaveFile();
            return File.Exists(DefaultSavePath);
        }
    }
}
