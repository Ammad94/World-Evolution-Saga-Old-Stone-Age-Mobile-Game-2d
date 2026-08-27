using System.Collections.Generic;
using UnityEngine;
using PrehistoricSurvival.Core;
using PrehistoricSurvival.World;

namespace PrehistoricSurvival.AI
{
    /// <summary>
    /// Keeps the world populated with wildlife.
    ///
    /// Animals are spawned in a ring around the player (just outside the view), with
    /// species and numbers chosen from the biome they appear in, and are despawned
    /// again once the player is far away — so the whole planet feels alive without
    /// ever holding more than a few dozen creatures in memory.
    /// </summary>
    public class AnimalSpawner : MonoBehaviour
    {
        public static AnimalSpawner Instance { get; private set; }

        [Header("References")]
        public Transform player;
        public WorldMap worldMap;

        [Header("Prefabs (taken from GameLibrary when empty)")]
        public GameObject mammothPrefab;
        public GameObject sabertoothPrefab;
        public GameObject caveBearPrefab;
        public GameObject bisonPrefab;

        [Header("Population")]
        [Tooltip("Maximum live animals at once.")]
        public int maxAnimals = 26;
        [Tooltip("Animals appear between these distances from the player (world units).")]
        public float minSpawnDistance = 26f;
        public float maxSpawnDistance = 55f;
        [Tooltip("Animals further than this are removed.")]
        public float despawnDistance = 95f;
        [Tooltip("Seconds between spawn attempts.")]
        public float spawnInterval = 2.5f;
        [Tooltip("Herd animals spawn in small groups of this size.")]
        public Vector2Int herdSize = new Vector2Int(2, 5);

        private readonly List<GameObject> _alive = new List<GameObject>();
        private float _timer;

        public int AliveCount => _alive.Count;

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

            var lib = GameLibrary.Instance;
            if (lib != null)
            {
                if (mammothPrefab == null) mammothPrefab = lib.mammothPrefab;
                if (sabertoothPrefab == null) sabertoothPrefab = lib.sabertoothPrefab;
                if (caveBearPrefab == null) caveBearPrefab = lib.caveBearPrefab;
                if (bisonPrefab == null) bisonPrefab = lib.bisonPrefab;
            }
        }

        private void Update()
        {
            if (player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p == null) return;
                player = p.transform;
            }

            CullFarAnimals();

            _timer += Time.deltaTime;
            if (_timer < spawnInterval) return;
            _timer = 0f;

            if (_alive.Count >= maxAnimals) return;
            TrySpawnGroup();
        }

        // ------------------------------------------------------------------
        private void CullFarAnimals()
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                var animal = _alive[i];
                if (animal == null) { _alive.RemoveAt(i); continue; }
                if (Vector2.Distance(animal.transform.position, player.position) > despawnDistance)
                {
                    Destroy(animal);
                    _alive.RemoveAt(i);
                }
            }
        }

        private void TrySpawnGroup()
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float distance = Random.Range(minSpawnDistance, maxSpawnDistance);
                Vector3 origin = player.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * distance;
                origin.z = 0f;

                var sample = worldMap.SampleWorld(origin);
                if (sample.isWater) continue;

                GameObject prefab = ChooseSpecies(sample.biome, out bool herd);
                if (prefab == null) continue;

                int count = herd ? Random.Range(herdSize.x, herdSize.y + 1) : 1;
                for (int i = 0; i < count && _alive.Count < maxAnimals; i++)
                {
                    Vector3 pos = origin + new Vector3(Random.Range(-4f, 4f), Random.Range(-4f, 4f), 0f);
                    if (worldMap.SampleWorld(pos).isWater) continue;
                    Spawn(prefab, pos);
                }
                return;
            }
        }

        private void Spawn(GameObject prefab, Vector3 position)
        {
            var animal = Instantiate(prefab, position, Quaternion.identity, transform);
            var sr = animal.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = ChunkManager.SortingOrderFor(position.y);
            _alive.Add(animal);
        }

        /// <summary>Species selection table – each biome has its own fauna.</summary>
        private GameObject ChooseSpecies(BiomeType biome, out bool herd)
        {
            herd = false;
            float roll = Random.value;

            switch (biome)
            {
                case BiomeType.Tundra:
                case BiomeType.Glacier:
                case BiomeType.SnowPeak:
                    herd = roll < 0.6f;
                    return roll < 0.6f ? mammothPrefab : caveBearPrefab;

                case BiomeType.Taiga:
                case BiomeType.Mountain:
                    herd = roll < 0.35f;
                    if (roll < 0.35f) return bisonPrefab;
                    return roll < 0.75f ? caveBearPrefab : mammothPrefab;

                case BiomeType.Steppe:
                case BiomeType.Grassland:
                    herd = roll < 0.7f;
                    if (roll < 0.7f) return bisonPrefab;
                    return roll < 0.9f ? mammothPrefab : sabertoothPrefab;

                case BiomeType.Savannah:
                    herd = roll < 0.6f;
                    return roll < 0.6f ? bisonPrefab : sabertoothPrefab;

                case BiomeType.TemperateForest:
                case BiomeType.TropicalRainforest:
                case BiomeType.Swamp:
                    return roll < 0.5f ? caveBearPrefab : sabertoothPrefab;

                case BiomeType.Desert:
                    return roll < 0.7f ? sabertoothPrefab : null;

                case BiomeType.Beach:
                    return roll < 0.4f ? bisonPrefab : null;

                default:
                    return null;
            }
        }

        /// <summary>Remove every spawned animal (used on load / teleport).</summary>
        public void ClearAll()
        {
            foreach (var a in _alive) if (a != null) Destroy(a);
            _alive.Clear();
        }
    }
}
