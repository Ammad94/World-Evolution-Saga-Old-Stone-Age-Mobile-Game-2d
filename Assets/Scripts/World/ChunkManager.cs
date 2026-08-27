using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using PrehistoricSurvival.Core;

namespace PrehistoricSurvival.World
{
    /// <summary>
    /// Streams the planet around the player: builds ground/water tilemaps and
    /// scatters vegetation and rocks for every 32×32 chunk inside the load radius,
    /// and recycles them again once the player walks away.
    ///
    /// Chunks are built over several frames so walking never stutters, and the
    /// world is effectively endless — the whole earth is reachable on foot.
    /// </summary>
    public class ChunkManager : MonoBehaviour
    {
        public static ChunkManager Instance { get; private set; }

        [Header("References")]
        [Tooltip("Parent transform for all loaded chunk objects.")]
        public Transform chunkParent;
        [Tooltip("The player transform (auto-found by tag if empty).")]
        public Transform player;
        [Tooltip("Planet definition (auto-found / created if empty).")]
        public WorldMap worldMap;

        [Header("Tile Palettes (optional – built from GameLibrary when empty)")]
        [Tooltip("Ground tiles, order: dirt, grass, sand, snow, stone, mud.")]
        public TileBase[] groundTiles;
        [Tooltip("Deep ocean tile.")]
        public TileBase waterTile;
        [Tooltip("Shallow water / lake tile.")]
        public TileBase shallowWaterTile;
        [Tooltip("River tile.")]
        public TileBase riverTile;

        [Header("Content Prefabs (optional – taken from GameLibrary when empty)")]
        public GameObject[] coldTreePrefabs;
        public GameObject[] temperateTreePrefabs;
        public GameObject[] tropicalTreePrefabs;
        public GameObject[] bushPrefabs;
        public GameObject[] rockPrefabs;

        [Header("Streaming")]
        [Tooltip("How many chunks around the player stay loaded (radius). 3 = 7×7 chunks = 224×224 tiles.")]
        [Range(1, 6)] public int loadRadius = 3;
        [Tooltip("Seconds between streaming checks.")]
        public float updateInterval = 0.35f;
        [Tooltip("Maximum chunks built per frame (keeps the frame rate stable on mobile).")]
        [Range(1, 8)] public int chunksPerFrame = 1;
        [Tooltip("Global multiplier for vegetation/rock density (lower it on weak devices).")]
        [Range(0.1f, 2f)] public float propDensity = 1f;
        [Tooltip("Hard cap of props spawned per chunk.")]
        public int maxPropsPerChunk = 140;

        private readonly Dictionary<Vector2Int, ChunkData> _loadedChunks = new Dictionary<Vector2Int, ChunkData>();
        private readonly Queue<Vector2Int> _buildQueue = new Queue<Vector2Int>();
        private readonly HashSet<Vector2Int> _queued = new HashSet<Vector2Int>();
        private Vector2Int _currentChunkCoord = new Vector2Int(int.MinValue, int.MinValue);
        private float _updateTimer;
        private bool _tilesReady;

        public IReadOnlyDictionary<Vector2Int, ChunkData> LoadedChunks => _loadedChunks;
        public int LoadedChunkCount => _loadedChunks.Count;

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
            if (chunkParent == null)
            {
                var parent = new GameObject("ChunkParent");
                parent.transform.SetParent(transform, false);
                chunkParent = parent.transform;
            }

            BuildTilePalette();
            PullPrefabsFromLibrary();
            StartCoroutine(BuildLoop());
        }

        // ------------------------------------------------------------------
        // Palette / prefabs
        // ------------------------------------------------------------------
        private void BuildTilePalette()
        {
            if (_tilesReady) return;
            var lib = GameLibrary.Instance;

            if (groundTiles == null || groundTiles.Length < 6)
            {
                var tiles = new TileBase[6];
                for (int i = 0; i < 6; i++)
                {
                    Sprite sprite = lib != null && lib.groundSprites != null && i < lib.groundSprites.Length
                        ? lib.groundSprites[i] : null;
                    tiles[i] = CreateTile(sprite, FallbackGroundColor(i));
                }
                groundTiles = tiles;
            }

            if (waterTile == null)
                waterTile = CreateTile(lib != null ? lib.oceanWaterSprite : null, new Color(0.08f, 0.23f, 0.46f));
            if (shallowWaterTile == null)
                shallowWaterTile = CreateTile(lib != null ? lib.shallowWaterSprite : null, new Color(0.20f, 0.48f, 0.70f));
            if (riverTile == null)
                riverTile = CreateTile(lib != null ? lib.riverWaterSprite : null, new Color(0.25f, 0.55f, 0.78f));

            _tilesReady = true;
        }

        private static Color FallbackGroundColor(int index)
        {
            switch (index)
            {
                case 0: return new Color(0.50f, 0.36f, 0.22f); // dirt
                case 1: return new Color(0.35f, 0.58f, 0.25f); // grass
                case 2: return new Color(0.86f, 0.78f, 0.52f); // sand
                case 3: return new Color(0.93f, 0.95f, 0.97f); // snow
                case 4: return new Color(0.52f, 0.52f, 0.52f); // stone
                default: return new Color(0.36f, 0.28f, 0.18f); // mud
            }
        }

        /// <summary>Create a runtime tile from a sprite, or a flat coloured tile as fallback.</summary>
        private static TileBase CreateTile(Sprite sprite, Color fallbackColor)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite != null ? sprite : SolidSprite(fallbackColor);
            tile.color = Color.white;
            tile.colliderType = Tile.ColliderType.None;
            return tile;
        }

        private static readonly Dictionary<Color, Sprite> _solidSprites = new Dictionary<Color, Sprite>();

        /// <summary>1×1 unit solid-colour sprite (used when art is missing).</summary>
        public static Sprite SolidSprite(Color color)
        {
            if (_solidSprites.TryGetValue(color, out var cached) && cached != null) return cached;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            tex.SetPixels32(pixels);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            _solidSprites[color] = sprite;
            return sprite;
        }

        private void PullPrefabsFromLibrary()
        {
            var lib = GameLibrary.Instance;
            if (lib == null) return;
            if (IsEmpty(coldTreePrefabs)) coldTreePrefabs = lib.coldTreePrefabs;
            if (IsEmpty(temperateTreePrefabs)) temperateTreePrefabs = lib.temperateTreePrefabs;
            if (IsEmpty(tropicalTreePrefabs)) tropicalTreePrefabs = lib.tropicalTreePrefabs;
            if (IsEmpty(bushPrefabs)) bushPrefabs = lib.bushPrefabs;
            if (IsEmpty(rockPrefabs)) rockPrefabs = lib.rockPrefabs;
        }

        private static bool IsEmpty(GameObject[] a)
        {
            if (a == null || a.Length == 0) return true;
            foreach (var o in a) if (o != null) return false;
            return true;
        }

        // ------------------------------------------------------------------
        // Streaming
        // ------------------------------------------------------------------
        private void Update()
        {
            if (player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p == null) return;
                player = p.transform;
            }

            _updateTimer += Time.deltaTime;
            if (_updateTimer < updateInterval) return;
            _updateTimer = 0f;
            UpdateChunks();
        }

        private void UpdateChunks()
        {
            Vector2Int coord = ChunkData.WorldToChunk(player.position);
            if (coord == _currentChunkCoord) return;
            _currentChunkCoord = coord;

            var needed = new HashSet<Vector2Int>();
            for (int dx = -loadRadius; dx <= loadRadius; dx++)
                for (int dy = -loadRadius; dy <= loadRadius; dy++)
                    needed.Add(new Vector2Int(coord.x + dx, coord.y + dy));

            // Unload far chunks.
            var toRemove = new List<Vector2Int>();
            foreach (var kvp in _loadedChunks)
                if (!needed.Contains(kvp.Key)) toRemove.Add(kvp.Key);
            foreach (var c in toRemove) UnloadChunk(c);

            // Queue new chunks, nearest first.
            var queueList = new List<Vector2Int>(needed);
            queueList.Sort((a, b) =>
                ((a - coord).sqrMagnitude).CompareTo((b - coord).sqrMagnitude));

            foreach (var c in queueList)
            {
                if (_loadedChunks.ContainsKey(c) || _queued.Contains(c)) continue;
                _queued.Add(c);
                _buildQueue.Enqueue(c);
            }
        }

        private IEnumerator BuildLoop()
        {
            // Build the chunks the player is standing in immediately.
            yield return null;
            UpdateChunks();

            while (true)
            {
                int built = 0;
                while (_buildQueue.Count > 0 && built < chunksPerFrame)
                {
                    Vector2Int coord = _buildQueue.Dequeue();
                    _queued.Remove(coord);
                    if (!_loadedChunks.ContainsKey(coord)) LoadChunk(coord);
                    built++;
                }
                yield return null;
            }
        }

        /// <summary>Build every chunk in the radius right now (used on spawn / teleport).</summary>
        public void ForceBuildAroundPlayer()
        {
            if (player == null) return;
            _currentChunkCoord = new Vector2Int(int.MinValue, int.MinValue);
            UpdateChunks();
            while (_buildQueue.Count > 0)
            {
                Vector2Int coord = _buildQueue.Dequeue();
                _queued.Remove(coord);
                if (!_loadedChunks.ContainsKey(coord)) LoadChunk(coord);
            }
        }

        // ------------------------------------------------------------------
        // Chunk construction
        // ------------------------------------------------------------------
        private void LoadChunk(Vector2Int coord)
        {
            BuildTilePalette();

            var chunk = new ChunkData(coord.x, coord.y);
            chunk.Generate(worldMap != null ? worldMap : WorldMap.EnsureExists(), propDensity);

            var chunkObj = new GameObject($"Chunk_{coord.x}_{coord.y}");
            chunkObj.transform.SetParent(chunkParent, false);
            chunkObj.transform.position = chunk.WorldPosition;

            var grid = chunkObj.AddComponent<Grid>();
            grid.cellSize = Vector3.one;

            // --- Ground layer ---
            var groundGO = new GameObject("Ground");
            groundGO.transform.SetParent(chunkObj.transform, false);
            var groundTM = groundGO.AddComponent<Tilemap>();
            var groundRenderer = groundGO.AddComponent<TilemapRenderer>();
            groundRenderer.sortingOrder = -32000;

            // --- Water layer ---
            var waterGO = new GameObject("Water");
            waterGO.transform.SetParent(chunkObj.transform, false);
            var waterTM = waterGO.AddComponent<Tilemap>();
            var waterRenderer = waterGO.AddComponent<TilemapRenderer>();
            waterRenderer.sortingOrder = -31000;

            int size = ChunkData.CHUNK_SIZE;
            var positions = new Vector3Int[size * size];
            var groundArray = new TileBase[size * size];
            var waterArray = new TileBase[size * size];

            int i = 0;
            bool anyWater = false;
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++, i++)
                {
                    positions[i] = new Vector3Int(x, y, 0);
                    int gid = Mathf.Clamp(chunk.groundTiles[x, y], 0, groundTiles.Length - 1);
                    groundArray[i] = groundTiles[gid];

                    switch (chunk.waterTiles[x, y])
                    {
                        case 1: waterArray[i] = waterTile; anyWater = true; break;
                        case 2: waterArray[i] = shallowWaterTile; anyWater = true; break;
                        case 3: waterArray[i] = riverTile; anyWater = true; break;
                        default: waterArray[i] = null; break;
                    }
                }
            }

            groundTM.SetTiles(positions, groundArray);
            if (anyWater) waterTM.SetTiles(positions, waterArray);

            // --- Props ---
            SpawnProps(chunkObj.transform, chunk);

            chunk.RootObject = chunkObj;
            chunk.IsLoaded = true;
            _loadedChunks[coord] = chunk;
        }

        private void SpawnProps(Transform parent, ChunkData chunk)
        {
            int spawned = 0;
            int size = ChunkData.CHUNK_SIZE;
            int baseX = chunk.ChunkX * size;
            int baseY = chunk.ChunkY * size;

            for (int x = 0; x < size && spawned < maxPropsPerChunk; x++)
            {
                for (int y = 0; y < size && spawned < maxPropsPerChunk; y++)
                {
                    int prop = chunk.propTiles[x, y];
                    if (prop == 0) continue;

                    int wx = baseX + x;
                    int wy = baseY + y;
                    float variant = WorldMap.Hash01(wx, wy, 91);
                    BiomeType biome = chunk.biomes[x, y];

                    GameObject prefab = null;
                    switch (prop)
                    {
                        case 1: prefab = GameLibrary.Pick(TreeSetFor(biome), variant); break;
                        case 2: prefab = GameLibrary.Pick(bushPrefabs, variant); break;
                        case 3: prefab = GameLibrary.Pick(rockPrefabs, variant); break;
                    }
                    if (prefab == null) continue;

                    float jitterX = (WorldMap.Hash01(wx, wy, 5) - 0.5f) * 0.7f;
                    float jitterY = (WorldMap.Hash01(wx, wy, 6) - 0.5f) * 0.7f;
                    Vector3 pos = new Vector3(wx + 0.5f + jitterX, wy + 0.5f + jitterY, 0f);

                    var instance = Instantiate(prefab, pos, Quaternion.identity, parent);

                    // Slight scale variation so forests do not look cloned.
                    float scale = 0.85f + WorldMap.Hash01(wx, wy, 7) * 0.4f;
                    instance.transform.localScale = new Vector3(scale, scale, 1f);

                    var sr = instance.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null) sr.sortingOrder = SortingOrderFor(pos.y);

                    spawned++;
                }
            }
        }

        private GameObject[] TreeSetFor(BiomeType biome)
        {
            switch (biome)
            {
                case BiomeType.Taiga:
                case BiomeType.Tundra:
                case BiomeType.Mountain:
                case BiomeType.SnowPeak:
                    return !IsEmpty(coldTreePrefabs) ? coldTreePrefabs : temperateTreePrefabs;
                case BiomeType.TropicalRainforest:
                case BiomeType.Savannah:
                case BiomeType.Swamp:
                    return !IsEmpty(tropicalTreePrefabs) ? tropicalTreePrefabs : temperateTreePrefabs;
                default:
                    return !IsEmpty(temperateTreePrefabs) ? temperateTreePrefabs : coldTreePrefabs;
            }
        }

        /// <summary>Shared Y-sorting rule so everything overlaps correctly (pseudo-3D depth).</summary>
        public static int SortingOrderFor(float worldY) =>
            Mathf.Clamp(Mathf.RoundToInt(-worldY * 4f), -30000, 30000);

        private void UnloadChunk(Vector2Int coord)
        {
            if (!_loadedChunks.TryGetValue(coord, out var chunk)) return;
            if (chunk.RootObject != null) Destroy(chunk.RootObject);
            chunk.IsLoaded = false;
            _loadedChunks.Remove(coord);
        }

        /// <summary>Drop every chunk and rebuild (season change, teleport, load game).</summary>
        public void ForceReloadAll()
        {
            var coords = new List<Vector2Int>(_loadedChunks.Keys);
            foreach (var c in coords) UnloadChunk(c);
            _buildQueue.Clear();
            _queued.Clear();
            _currentChunkCoord = new Vector2Int(int.MinValue, int.MinValue);
        }

        /// <summary>Chunk at a world position, or null when it is not loaded.</summary>
        public ChunkData GetChunkAt(Vector3 worldPos)
        {
            _loadedChunks.TryGetValue(ChunkData.WorldToChunk(worldPos), out var chunk);
            return chunk;
        }

        /// <summary>Biome at a world position (works even outside loaded chunks).</summary>
        public BiomeType GetBiomeAt(Vector3 worldPos)
        {
            var map = worldMap != null ? worldMap : WorldMap.Instance;
            return map != null ? map.GetBiome(worldPos) : BiomeType.Grassland;
        }
    }

}
