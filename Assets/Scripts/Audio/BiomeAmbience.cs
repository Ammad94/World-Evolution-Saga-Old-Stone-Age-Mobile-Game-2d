using UnityEngine;
using PrehistoricSurvival.Core;
using PrehistoricSurvival.Survival;
using PrehistoricSurvival.World;

namespace PrehistoricSurvival.Audio
{
    /// <summary>
    /// Crossfades an ambient loop for the biome the player is standing in,
    /// with day/night variants for forests. Loops live in Resources/Audio/ambience.
    /// </summary>
    public class BiomeAmbience : MonoBehaviour
    {
        public static BiomeAmbience Instance { get; private set; }
        public float reevaluateEvery = 2.5f;

        private string _current;
        private float _timer;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer < reevaluateEvery) return;
            _timer = 0f;

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null || ChunkManager.Instance == null || AudioManager.Instance == null) return;

            var biome = ChunkManager.Instance.GetBiomeAt(player.transform.position);
            bool night = SeasonManager.Instance != null && SeasonManager.Instance.IsNight;
            string target = ClipFor(biome, night);
            if (target != _current)
            {
                _current = target;
                AudioManager.Instance.CrossfadeAmbience(target, 3.5f);
            }
        }

        private static string ClipFor(BiomeType biome, bool night)
        {
            switch (biome)
            {
                case BiomeType.Ocean:
                case BiomeType.ShallowWater: return "ambience/amb_ocean";
                case BiomeType.Beach: return "ambience/amb_ocean";
                case BiomeType.Glacier:
                case BiomeType.SnowPeak: return "ambience/amb_tundra";
                case BiomeType.Tundra: return "ambience/amb_tundra";
                case BiomeType.Taiga: return night ? "ambience/amb_forest_night" : "ambience/amb_forest_day";
                case BiomeType.TemperateForest: return night ? "ambience/amb_forest_night" : "ambience/amb_forest_day";
                case BiomeType.Grassland:
                case BiomeType.Steppe: return "ambience/amb_steppe";
                case BiomeType.Desert: return "ambience/amb_desert";
                case BiomeType.Savannah: return "ambience/amb_savanna";
                case BiomeType.TropicalRainforest: return "ambience/amb_jungle";
                case BiomeType.Swamp: return "ambience/amb_swamp";
                case BiomeType.Mountain: return "ambience/amb_tundra";
                default: return "ambience/amb_steppe";
            }
        }
    }
}
