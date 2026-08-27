using System;
using UnityEngine;

namespace PrehistoricSurvival.World
{
    /// <summary>
    /// Defines the four major prehistoric biome zones and provides
    /// biome lookup based on world coordinates.
    /// </summary>
    public class BiomeManager : MonoBehaviour
    {
        public static BiomeManager Instance { get; private set; }

        public enum Biome
        {
            Tundra,        // Eurasia – cold, sparse vegetation
            Savannah,      // Africa – hot, grassy plains
            Subtropical,   // East Asia – dense forests, monsoon
            Grasslands     // Americas – temperate prairies
        }

        [Header("Biome Regions (world-space bounds)")]
        [Tooltip("Tundra zone bounds.")]
        public Bounds tundraBounds = new Bounds(new Vector3(-500, 0, 500), new Vector3(1000, 1, 1000));

        [Tooltip("Savannah zone bounds.")]
        public Bounds savannahBounds = new Bounds(new Vector3(500, 0, -500), new Vector3(1000, 1, 1000));

        [Tooltip("Subtropical zone bounds.")]
        public Bounds subtropicalBounds = new Bounds(new Vector3(500, 0, 500), new Vector3(1000, 1, 1000));

        [Tooltip("Grasslands zone bounds.")]
        public Bounds grasslandsBounds = new Bounds(new Vector3(-500, 0, -500), new Vector3(1000, 1, 1000));

        [Header("Biome Properties")]
        public BiomeProfile tundraProfile;
        public BiomeProfile savannahProfile;
        public BiomeProfile subtropicalProfile;
        public BiomeProfile grasslandsProfile;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>Get the biome at a given world position.</summary>
        public Biome GetBiomeAt(Vector3 worldPos)
        {
            if (tundraBounds.Contains(worldPos)) return Biome.Tundra;
            if (savannahBounds.Contains(worldPos)) return Biome.Savannah;
            if (subtropicalBounds.Contains(worldPos)) return Biome.Subtropical;
            if (grasslandsBounds.Contains(worldPos)) return Biome.Grasslands;

            // Default fallback: pick closest biome center
            float dT = Vector3.Distance(worldPos, tundraBounds.center);
            float dS = Vector3.Distance(worldPos, savannahBounds.center);
            float dU = Vector3.Distance(worldPos, subtropicalBounds.center);
            float dG = Vector3.Distance(worldPos, grasslandsBounds.center);

            float min = Mathf.Min(dT, dS, dU, dG);
            if (min == dT) return Biome.Tundra;
            if (min == dS) return Biome.Savannah;
            if (min == dU) return Biome.Subtropical;
            return Biome.Grasslands;
        }

        /// <summary>Get the profile for a biome.</summary>
        public BiomeProfile GetProfile(Biome biome)
        {
            switch (biome)
            {
                case Biome.Tundra: return tundraProfile;
                case Biome.Savannah: return savannahProfile;
                case Biome.Subtropical: return subtropicalProfile;
                case Biome.Grasslands: return grasslandsProfile;
                default: return grasslandsProfile;
            }
        }
    }

    /// <summary>
    /// Data profile for a biome – defines environmental properties.
    /// </summary>
    [Serializable]
    public class BiomeProfile
    {
        public string biomeName;
        public Color terrainTint = Color.white;
        public Color ambientLightColor = Color.white;

        [Header("Temperature & Weather")]
        [Range(-40f, 50f)]
        public float baseTemperature = 20f;
        [Range(0f, 1f)]
        public float baseHumidity = 0.5f;

        [Header("Survival Modifiers")]
        public float thirstDrainMultiplier = 1f;
        public float hungerDrainMultiplier = 1f;
        public float energyDrainMultiplier = 1f;

        [Header("Vegetation Density")]
        [Range(0f, 1f)]
        public float treeDensity = 0.3f;
        [Range(0f, 1f)]
        public float bushDensity = 0.2f;

        [Header("Tile Palettes")]
        [Tooltip("ScriptableObject references for Tilemap palettes (assign in Inspector).")]
        public UnityEngine.Tilemaps.TileBase groundTile;
        public UnityEngine.Tilemaps.TileBase waterTile;
    }
}
