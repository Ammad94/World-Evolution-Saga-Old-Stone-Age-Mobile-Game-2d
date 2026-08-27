using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace PrehistoricSurvival.World
{
    /// <summary>
    /// Manages loading/unloading of 32×32 tile chunks around the player.
    /// Maintains a 3×3 grid of active chunks and streams them dynamically.
    /// </summary>
    public class ChunkManager : MonoBehaviour
    {
        public static ChunkManager Instance { get; private set; }

        [Header("References")]
        [Tooltip("Parent transform for all loaded chunk objects.")]
        public Transform chunkParent;

        [Tooltip("The player transform (used to determine which chunks to load).")]
        public Transform player;

        [Header("Tile Palettes")]
        [Tooltip("Ground tile palette (indices match ChunkData tile IDs).")]
        public TileBase[] groundTiles;
        [Tooltip("Vegetation prefabs (indices match ChunkData vegetation IDs).")]
        public GameObject[] vegetationPrefabs;
        [Tooltip("Water tile.")]
        public TileBase waterTile;

        [Header("Settings")]
        [Tooltip("How many chunks around the player to keep loaded (radius).")]
        public int loadRadius = 1; // 1 = 3×3 grid

        [Tooltip("How often (seconds) to check for chunk loading/unloading.")]
        public float updateInterval = 1f;

        private Dictionary<Vector2Int, ChunkData> _loadedChunks = new Dictionary<Vector2Int, ChunkData>();
        private Vector2Int _currentChunkCoord = new Vector2Int(int.MinValue, int.MinValue);
        private float _updateTimer;

        public IReadOnlyDictionary<Vector2Int, ChunkData> LoadedChunks => _loadedChunks;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) player = p.transform;
            }
            if (chunkParent == null)
            {
                chunkParent = new GameObject("ChunkParent").transform;
            }
        }

        private void Update()
        {
            if (player == null) return;

            _updateTimer += Time.deltaTime;
            if (_updateTimer < updateInterval) return;
            _updateTimer = 0f;

            UpdateChunks();
        }

        // ------------------------------------------------------------------
        // Chunk Streaming
        // ------------------------------------------------------------------
        private void UpdateChunks()
        {
            Vector3 pos = player.position;
            int cx = Mathf.FloorToInt(pos.x / ChunkData.CHUNK_SIZE);
            int cz = Mathf.FloorToInt(pos.z / ChunkData.CHUNK_SIZE);
            Vector2Int newCoord = new Vector2Int(cx, cz);

            if (newCoord == _currentChunkCoord) return;
            _currentChunkCoord = newCoord;

            // Determine which chunks should be loaded
            HashSet<Vector2Int> needed = new HashSet<Vector2Int>();
            for (int dx = -loadRadius; dx <= loadRadius; dx++)
            {
                for (int dz = -loadRadius; dz <= loadRadius; dz++)
                {
                    needed.Add(new Vector2Int(cx + dx, cz + dz));
                }
            }

            // Unload chunks outside the needed set
            List<Vector2Int> toRemove = new List<Vector2Int>();
            foreach (var kvp in _loadedChunks)
            {
                if (!needed.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var coord in toRemove)
            {
                UnloadChunk(coord);
            }

            // Load new chunks
            foreach (var coord in needed)
            {
                if (!_loadedChunks.ContainsKey(coord))
                {
                    LoadChunk(coord);
                }
            }
        }

        private void LoadChunk(Vector2Int coord)
        {
            ChunkData chunk = new ChunkData(coord.x, coord.y);

            // Determine biome
            BiomeManager.Biome biome = BiomeManager.Biome.Grasslands;
            if (BiomeManager.Instance != null)
                biome = BiomeManager.Instance.GetBiomeAt(chunk.WorldCenter);

            chunk.Generate(biome);

            // Create chunk root object
            GameObject chunkObj = new GameObject($"Chunk_{coord.x}_{coord.y}");
            chunkObj.transform.SetParent(chunkParent);
            chunkObj.transform.position = chunk.WorldPosition;

            // Create Tilemap for ground
            var groundGO = new GameObject("Ground");
            groundGO.transform.SetParent(chunkObj.transform);
            var grid = chunkObj.GetComponent<Grid>();
            if (grid == null) grid = chunkObj.AddComponent<Grid>();
            grid.cellSize = Vector3.one;

            var groundTM = groundGO.AddComponent<Tilemap>();
            var groundRenderer = groundGO.AddComponent<TilemapRenderer>();
            groundRenderer.sortingOrder = 0;

            // Populate ground tiles
            for (int x = 0; x < ChunkData.CHUNK_SIZE; x++)
            {
                for (int z = 0; z < ChunkData.CHUNK_SIZE; z++)
                {
                    int tileId = chunk.groundTiles[x, z];
                    if (tileId >= 0 && tileId < groundTiles.Length && groundTiles[tileId] != null)
                    {
                        groundTM.SetTile(new Vector3Int(x, z, 0), groundTiles[tileId]);
                    }

                    // Water overlay
                    if (chunk.waterTiles[x, z] == 1 && waterTile != null)
                    {
                        groundTM.SetTile(new Vector3Int(x, z, 1), waterTile);
                    }
                }
            }

            // Spawn vegetation as GameObjects
            for (int x = 0; x < ChunkData.CHUNK_SIZE; x++)
            {
                for (int z = 0; z < ChunkData.CHUNK_SIZE; z++)
                {
                    int vegId = chunk.vegetationTiles[x, z];
                    if (vegId > 0 && vegId < vegetationPrefabs.Length && vegetationPrefabs[vegId] != null)
                    {
                        Vector3 worldPos = chunk.WorldPosition + new Vector3(x, 0, z);
                        Instantiate(vegetationPrefabs[vegId], worldPos, Quaternion.identity, chunkObj.transform);
                    }
                }
            }

            chunk.RootObject = chunkObj;
            chunk.IsLoaded = true;
            _loadedChunks[coord] = chunk;
        }

        private void UnloadChunk(Vector2Int coord)
        {
            if (!_loadedChunks.TryGetValue(coord, out var chunk)) return;

            if (chunk.RootObject != null)
                Destroy(chunk.RootObject);

            chunk.IsLoaded = false;
            _loadedChunks.Remove(coord);
        }

        /// <summary>Force reload all chunks (e.g., after season change).</summary>
        public void ForceReloadAll()
        {
            var coords = new List<Vector2Int>(_loadedChunks.Keys);
            foreach (var coord in coords)
                UnloadChunk(coord);
            _currentChunkCoord = new Vector2Int(int.MinValue, int.MinValue);
        }

        /// <summary>Get chunk at a world position, or null if not loaded.</summary>
        public ChunkData GetChunkAt(Vector3 worldPos)
        {
            int cx = Mathf.FloorToInt(worldPos.x / ChunkData.CHUNK_SIZE);
            int cz = Mathf.FloorToInt(worldPos.z / ChunkData.CHUNK_SIZE);
            _loadedChunks.TryGetValue(new Vector2Int(cx, cz), out var chunk);
            return chunk;
        }
    }
}
