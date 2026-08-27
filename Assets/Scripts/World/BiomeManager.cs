using System;
using UnityEngine;
using PrehistoricSurvival.Core;

namespace PrehistoricSurvival.World
{
    /// <summary>
    /// Turns the raw planet data from <see cref="WorldMap"/> into gameplay values:
    /// per-biome survival modifiers, ambient colour, and the biome the player is
    /// currently standing in (broadcast to the HUD and weather system).
    /// </summary>
    public class BiomeManager : MonoBehaviour
    {
        public static BiomeManager Instance { get; private set; }

        [Header("References")]
        public WorldMap worldMap;
        public Transform player;

        [Header("Runtime State")]
        [SerializeField] private BiomeType _currentBiome = BiomeType.Grassland;
        public BiomeType CurrentBiome => _currentBiome;
        public WorldSample CurrentSample { get; private set; }

        /// <summary>Raised when the player walks into a different biome.</summary>
        public event Action<BiomeType> OnBiomeChanged;

        private float _timer;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (worldMap == null) worldMap = WorldMap.Instance != null ? WorldMap.Instance : WorldMap.EnsureExists();
            if (player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) player = p.transform;
            }
        }

        private void Update()
        {
            if (player == null || worldMap == null) return;
            _timer += Time.deltaTime;
            if (_timer < 0.5f) return;
            _timer = 0f;

            CurrentSample = worldMap.SampleWorld(player.position);
            if (CurrentSample.biome != _currentBiome)
            {
                _currentBiome = CurrentSample.biome;
                OnBiomeChanged?.Invoke(_currentBiome);
                EventManager.TriggerEvent(GameEvents.BiomeChanged, _currentBiome);
            }
        }

        /// <summary>Biome at any world position.</summary>
        public BiomeType GetBiomeAt(Vector3 worldPos)
        {
            var map = worldMap != null ? worldMap : WorldMap.Instance;
            return map != null ? map.GetBiome(worldPos) : BiomeType.Grassland;
        }

        /// <summary>Gameplay profile for a biome.</summary>
        public static BiomeProfile GetProfile(BiomeType biome)
        {
            switch (biome)
            {
                case BiomeType.Desert:
                    return new BiomeProfile("Desert", new Color(1f, 0.93f, 0.72f), 38f, 0.05f, 2.2f, 1.1f, 1.3f);
                case BiomeType.Savannah:
                    return new BiomeProfile("Savannah", new Color(1f, 0.95f, 0.78f), 30f, 0.3f, 1.5f, 1.0f, 1.1f);
                case BiomeType.TropicalRainforest:
                    return new BiomeProfile("Rainforest", new Color(0.82f, 1f, 0.85f), 27f, 0.9f, 1.2f, 1.0f, 1.2f);
                case BiomeType.Swamp:
                    return new BiomeProfile("Swamp", new Color(0.80f, 0.92f, 0.80f), 24f, 1f, 1.1f, 1.1f, 1.3f);
                case BiomeType.TemperateForest:
                    return new BiomeProfile("Temperate Forest", new Color(0.92f, 1f, 0.92f), 14f, 0.6f, 1f, 1f, 1f);
                case BiomeType.Grassland:
                    return new BiomeProfile("Grassland", new Color(1f, 1f, 0.95f), 16f, 0.45f, 1f, 1f, 1f);
                case BiomeType.Steppe:
                    return new BiomeProfile("Steppe", new Color(1f, 0.98f, 0.88f), 12f, 0.2f, 1.3f, 1f, 1f);
                case BiomeType.Taiga:
                    return new BiomeProfile("Taiga", new Color(0.88f, 0.94f, 1f), 0f, 0.55f, 0.9f, 1.2f, 1.2f);
                case BiomeType.Tundra:
                    return new BiomeProfile("Tundra", new Color(0.85f, 0.92f, 1f), -8f, 0.35f, 0.9f, 1.35f, 1.4f);
                case BiomeType.Glacier:
                    return new BiomeProfile("Glacier", new Color(0.82f, 0.90f, 1f), -22f, 0.2f, 0.9f, 1.6f, 1.7f);
                case BiomeType.Mountain:
                    return new BiomeProfile("Mountain", new Color(0.95f, 0.95f, 1f), 2f, 0.4f, 1.1f, 1.2f, 1.5f);
                case BiomeType.SnowPeak:
                    return new BiomeProfile("Snow Peak", new Color(0.90f, 0.95f, 1f), -18f, 0.3f, 1f, 1.5f, 1.8f);
                case BiomeType.Beach:
                    return new BiomeProfile("Beach", new Color(1f, 0.98f, 0.88f), 22f, 0.5f, 1.3f, 1f, 1f);
                case BiomeType.ShallowWater:
                    return new BiomeProfile("Shallow Water", new Color(0.85f, 0.95f, 1f), 18f, 1f, 0.8f, 1f, 1.4f);
                default:
                    return new BiomeProfile("Ocean", new Color(0.80f, 0.90f, 1f), 15f, 1f, 0.8f, 1.1f, 1.6f);
            }
        }

        /// <summary>Profile for the biome the player is standing in.</summary>
        public BiomeProfile CurrentProfile => GetProfile(_currentBiome);
    }

    /// <summary>Gameplay properties of a biome.</summary>
    [Serializable]
    public struct BiomeProfile
    {
        public string biomeName;
        public Color ambientTint;
        public float baseTemperature;
        public float baseHumidity;
        public float thirstDrainMultiplier;
        public float hungerDrainMultiplier;
        public float energyDrainMultiplier;

        public BiomeProfile(string name, Color tint, float temperature, float humidity,
            float thirst, float hunger, float energy)
        {
            biomeName = name;
            ambientTint = tint;
            baseTemperature = temperature;
            baseHumidity = humidity;
            thirstDrainMultiplier = thirst;
            hungerDrainMultiplier = hunger;
            energyDrainMultiplier = energy;
        }
    }
}
