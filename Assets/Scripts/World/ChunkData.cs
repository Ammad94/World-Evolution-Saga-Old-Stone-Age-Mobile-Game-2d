using System.Collections.Generic;
using UnityEngine;

namespace PrehistoricSurvival.World
{
    /// <summary>
    /// A single 32×32 tile chunk of the planet (XY plane).
    /// Tile data is generated on demand from <see cref="WorldMap"/>, so the
    /// streamer can walk the whole earth without storing anything.
    /// </summary>
    public class ChunkData
    {
        public const int CHUNK_SIZE = 32;

        public int ChunkX { get; private set; }
        public int ChunkY { get; private set; }
        public Vector2Int Coord => new Vector2Int(ChunkX, ChunkY);

        /// <summary>Dominant biome of the chunk (used for spawn logic and audio).</summary>
        public BiomeType DominantBiome { get; private set; }

        public bool IsLoaded { get; set; }
        public GameObject RootObject { get; set; }

        // 0 dirt, 1 grass, 2 sand, 3 snow, 4 stone, 5 mud
        public readonly int[,] groundTiles = new int[CHUNK_SIZE, CHUNK_SIZE];
        // 0 none, 1 ocean, 2 shallow/lake, 3 river
        public readonly int[,] waterTiles = new int[CHUNK_SIZE, CHUNK_SIZE];
        // 0 none, 1 tree, 2 bush, 3 rock
        public readonly int[,] propTiles = new int[CHUNK_SIZE, CHUNK_SIZE];
        // Per-tile biome, used to pick the right prefab variants.
        public readonly BiomeType[,] biomes = new BiomeType[CHUNK_SIZE, CHUNK_SIZE];

        public int LandTileCount { get; private set; }

        public ChunkData(int chunkX, int chunkY)
        {
            ChunkX = chunkX;
            ChunkY = chunkY;
        }

        /// <summary>World-space position of the chunk's bottom-left corner (XY plane).</summary>
        public Vector3 WorldPosition => new Vector3(ChunkX * CHUNK_SIZE, ChunkY * CHUNK_SIZE, 0f);

        /// <summary>Centre of the chunk in world space.</summary>
        public Vector3 WorldCenter => WorldPosition + new Vector3(CHUNK_SIZE * 0.5f, CHUNK_SIZE * 0.5f, 0f);

        /// <summary>
        /// Fill the chunk from the planet definition.
        /// </summary>
        public void Generate(WorldMap map, float propDensityScale = 1f)
        {
            if (map == null) return;

            var histogram = new Dictionary<BiomeType, int>();
            LandTileCount = 0;

            int baseX = ChunkX * CHUNK_SIZE;
            int baseY = ChunkY * CHUNK_SIZE;

            for (int x = 0; x < CHUNK_SIZE; x++)
            {
                for (int y = 0; y < CHUNK_SIZE; y++)
                {
                    int wx = baseX + x;
                    int wy = baseY + y;
                    WorldSample s = map.Sample(wx, wy);

                    biomes[x, y] = s.biome;
                    groundTiles[x, y] = WorldMap.GroundTileId(s);

                    if (s.isRiver) waterTiles[x, y] = 3;
                    else if (s.biome == BiomeType.Ocean) waterTiles[x, y] = 1;
                    else if (s.biome == BiomeType.ShallowWater) waterTiles[x, y] = 2;
                    else waterTiles[x, y] = 0;

                    histogram.TryGetValue(s.biome, out int count);
                    histogram[s.biome] = count + 1;

                    if (s.isWater)
                    {
                        propTiles[x, y] = 0;
                        continue;
                    }

                    LandTileCount++;
                    propTiles[x, y] = PickProp(wx, wy, s, propDensityScale);
                }
            }

            // Dominant biome.
            int best = -1;
            foreach (var kvp in histogram)
            {
                if (kvp.Value > best) { best = kvp.Value; DominantBiome = kvp.Key; }
            }
        }

        private static int PickProp(int wx, int wy, WorldSample s, float densityScale)
        {
            float roll = WorldMap.Hash01(wx, wy, 1337);

            float tree = WorldMap.TreeDensity(s.biome) * densityScale;
            if (roll < tree) return 1;

            float bush = WorldMap.BushDensity(s.biome) * densityScale;
            if (roll < tree + bush) return 2;

            float rock = WorldMap.RockDensity(s.biome) * densityScale;
            if (roll < tree + bush + rock) return 3;

            return 0;
        }

        /// <summary>Chunk coordinate that contains a world position.</summary>
        public static Vector2Int WorldToChunk(Vector3 worldPos) => new Vector2Int(
            Mathf.FloorToInt(worldPos.x / CHUNK_SIZE),
            Mathf.FloorToInt(worldPos.y / CHUNK_SIZE));
    }
}
