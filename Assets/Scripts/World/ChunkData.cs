using System.Collections.Generic;
using UnityEngine;

namespace PrehistoricSurvival.World
{
    /// <summary>
    /// Represents a single 32×32 tile chunk in the world.
    /// Manages its own Tilemap layer and procedural content.
    /// </summary>
    public class ChunkData
    {
        public int ChunkX { get; private set; }
        public int ChunkZ { get; private set; }
        public Vector2Int Coord => new Vector2Int(ChunkX, ChunkZ);
        public BiomeManager.Biome Biome { get; set; }
        public bool IsLoaded { get; set; }

        public GameObject RootObject { get; set; }

        // Tile data (32x32 grid)
        public int[,] groundTiles;
        public int[,] vegetationTiles;
        public int[,] waterTiles;

        public const int CHUNK_SIZE = 32;

        public ChunkData(int chunkX, int chunkZ)
        {
            ChunkX = chunkX;
            ChunkZ = chunkZ;
            groundTiles = new int[CHUNK_SIZE, CHUNK_SIZE];
            vegetationTiles = new int[CHUNK_SIZE, CHUNK_SIZE];
            waterTiles = new int[CHUNK_SIZE, CHUNK_SIZE];
        }

        /// <summary>World-space position of the chunk's bottom-left corner.</summary>
        public Vector3 WorldPosition => new Vector3(ChunkX * CHUNK_SIZE, 0f, ChunkZ * CHUNK_SIZE);

        /// <summary>Center of the chunk in world space.</summary>
        public Vector3 WorldCenter => WorldPosition + new Vector3(CHUNK_SIZE * 0.5f, 0f, CHUNK_SIZE * 0.5f);

        /// <summary>
        /// Generate procedural tile data for this chunk based on its biome.
        /// Uses deterministic noise for consistent results.
        /// </summary>
        public void Generate(BiomeManager.Biome biome)
        {
            Biome = biome;
            int seed = ChunkX * 73856093 ^ ChunkZ * 19349663;
            System.Random rng = new System.Random(seed);

            for (int x = 0; x < CHUNK_SIZE; x++)
            {
                for (int z = 0; z < CHUNK_SIZE; z++)
                {
                    float worldX = ChunkX * CHUNK_SIZE + x;
                    float worldZ = ChunkZ * CHUNK_SIZE + z;

                    // Perlin noise for terrain height
                    float height = Mathf.PerlinNoise(worldX * 0.02f, worldZ * 0.02f);
                    float moisture = Mathf.PerlinNoise(worldX * 0.01f + 100f, worldZ * 0.01f + 100f);

                    // Ground tile assignment
                    groundTiles[x, z] = DetermineGroundTile(height, moisture, biome);

                    // Water tiles in low areas
                    waterTiles[x, z] = height < 0.25f ? 1 : 0;

                    // Vegetation placement
                    vegetationTiles[x, z] = DetermineVegetation(height, moisture, biome, rng);
                }
            }
        }

        private int DetermineGroundTile(float height, float moisture, BiomeManager.Biome biome)
        {
            // Tile IDs: 0=dirt, 1=grass, 2=sand, 3=snow, 4=stone, 5=mud
            switch (biome)
            {
                case BiomeManager.Biome.Tundra:
                    return height > 0.7f ? 4 : 3; // stone or snow
                case BiomeManager.Biome.Savannah:
                    return height < 0.3f ? 2 : 1; // sand or grass
                case BiomeManager.Biome.Subtropical:
                    return moisture > 0.6f ? 5 : 1; // mud or grass
                case BiomeManager.Biome.Grasslands:
                    return height > 0.7f ? 4 : 1; // stone or grass
                default:
                    return 0;
            }
        }

        private int DetermineVegetation(float height, float moisture, BiomeManager.Biome biome, System.Random rng)
        {
            // Vegetation IDs: 0=none, 1=timber tree, 2=fruit tree, 3=berry bush, 4=tall grass
            if (height < 0.25f || height > 0.85f) return 0; // no vegetation in water or mountains

            float chance = rng.NextDouble() < 0.1f ? 1f : 0f;
            if (chance == 0f) return 0;

            switch (biome)
            {
                case BiomeManager.Biome.Tundra:
                    return rng.NextDouble() < 0.3 ? 1 : 0; // sparse trees
                case BiomeManager.Biome.Savannah:
                    if (rng.NextDouble() < 0.2) return 4; // tall grass
                    if (rng.NextDouble() < 0.1) return 2; // rare fruit tree
                    return 0;
                case BiomeManager.Biome.Subtropical:
                    if (rng.NextDouble() < 0.5) return 1; // dense timber
                    if (rng.NextDouble() < 0.3) return 2; // fruit trees
                    if (rng.NextDouble() < 0.4) return 3; // berry bushes
                    return 0;
                case BiomeManager.Biome.Grasslands:
                    if (rng.NextDouble() < 0.3) return 1;
                    if (rng.NextDouble() < 0.2) return 3;
                    return 4; // tall grass
                default:
                    return 0;
            }
        }
    }
}
