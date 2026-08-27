using System.Collections.Generic;
using UnityEngine;

namespace PrehistoricSurvival.World
{
    /// <summary>
    /// Biome types used by the whole-earth world map.
    /// </summary>
    public enum BiomeType
    {
        Ocean,
        ShallowWater,
        Beach,
        Glacier,
        Tundra,
        Taiga,
        TemperateForest,
        Grassland,
        Steppe,
        Desert,
        Savannah,
        TropicalRainforest,
        Swamp,
        Mountain,
        SnowPeak
    }

    /// <summary>Single sampled point of the world.</summary>
    public struct WorldSample
    {
        public float elevation;     // 0..1  (0 = deep ocean, 1 = highest peak)
        public float temperature;   // degrees celsius (yearly average)
        public float moisture;      // 0..1
        public BiomeType biome;
        public bool isWater;
        public bool isRiver;
    }

    /// <summary>
    /// Procedural whole-earth map.
    ///
    /// The world is one huge, seamless, deterministic planet surface expressed in tiles
    /// (1 tile = 1 Unity unit on the XY plane). It wraps horizontally (longitude) and is
    /// clamped vertically (latitude), and contains every major landmass, ocean,
    /// mountain range, desert, jungle and ice cap.
    ///
    /// Nothing is stored in memory: any tile of the planet can be sampled on demand,
    /// which is what allows the chunk streamer to walk the entire globe.
    /// </summary>
    public class WorldMap : MonoBehaviour
    {
        public static WorldMap Instance { get; private set; }

        [Header("Planet Size (tiles)")]
        [Tooltip("Width of the planet in tiles (longitude). Wraps around.")]
        public int worldWidth = 16384;
        [Tooltip("Height of the planet in tiles (latitude). Clamped at the poles.")]
        public int worldHeight = 8192;
        [Tooltip("Kilometres represented by one tile (flavour / map readouts only).")]
        public float kilometresPerTile = 2.44f;

        [Header("Generation")]
        [Tooltip("World seed. Same seed always produces the exact same planet.")]
        public int seed = 20260827;
        [Tooltip("Sea level in the 0..1 elevation range.")]
        [Range(0.2f, 0.7f)] public float seaLevel = 0.42f;
        [Tooltip("Extra continent noise strength (coastline raggedness).")]
        [Range(0f, 1f)] public float coastRaggedness = 0.55f;

        [Header("Spawn")]
        [Tooltip("Where a brand new game starts. Leave at 0,0 to auto-pick a habitable spot.")]
        public Vector2Int defaultSpawnTile = Vector2Int.zero;

        // Noise offsets derived from the seed.
        private float _oxA, _oyA, _oxB, _oyB, _oxC, _oyC, _oxD, _oyD;
        private bool _initialised;

        // --------------------------------------------------------------
        // Continents. Positions are normalised: u = 0..1 west→east,
        // v = 0..1 south→north. Radii are normalised too.
        // --------------------------------------------------------------
        [System.Serializable]
        public struct Landmass
        {
            public string name;
            public Vector2 center;
            public Vector2 radius;
            public float strength;
        }

        private static readonly Landmass[] Continents = new[]
        {
            new Landmass { name = "North America",  center = new Vector2(0.16f, 0.74f), radius = new Vector2(0.115f, 0.150f), strength = 1.00f },
            new Landmass { name = "Central America",center = new Vector2(0.215f,0.585f), radius = new Vector2(0.030f, 0.045f), strength = 0.75f },
            new Landmass { name = "South America",  center = new Vector2(0.265f,0.360f), radius = new Vector2(0.065f, 0.135f), strength = 1.00f },
            new Landmass { name = "Greenland",      center = new Vector2(0.325f,0.900f), radius = new Vector2(0.040f, 0.048f), strength = 0.85f },
            new Landmass { name = "Europe",         center = new Vector2(0.520f,0.790f), radius = new Vector2(0.070f, 0.062f), strength = 0.90f },
            new Landmass { name = "Africa",         center = new Vector2(0.535f,0.520f), radius = new Vector2(0.095f, 0.150f), strength = 1.00f },
            new Landmass { name = "Arabia",         center = new Vector2(0.605f,0.600f), radius = new Vector2(0.038f, 0.042f), strength = 0.75f },
            new Landmass { name = "Siberia",        center = new Vector2(0.720f,0.810f), radius = new Vector2(0.165f, 0.080f), strength = 0.95f },
            new Landmass { name = "Central Asia",   center = new Vector2(0.680f,0.700f), radius = new Vector2(0.110f, 0.070f), strength = 0.95f },
            new Landmass { name = "India",          center = new Vector2(0.690f,0.585f), radius = new Vector2(0.045f, 0.055f), strength = 0.85f },
            new Landmass { name = "East Asia",      center = new Vector2(0.790f,0.690f), radius = new Vector2(0.070f, 0.075f), strength = 0.90f },
            new Landmass { name = "Sundaland",      center = new Vector2(0.795f,0.500f), radius = new Vector2(0.055f, 0.030f), strength = 0.70f },
            new Landmass { name = "Australia",      center = new Vector2(0.850f,0.330f), radius = new Vector2(0.070f, 0.055f), strength = 0.90f },
            new Landmass { name = "Beringia",       center = new Vector2(0.935f,0.800f), radius = new Vector2(0.045f, 0.050f), strength = 0.70f },
        };

        // Mountain belts (normalised) – these lift elevation dramatically.
        private static readonly Landmass[] MountainRanges = new[]
        {
            new Landmass { name = "Rockies",   center = new Vector2(0.130f, 0.740f), radius = new Vector2(0.022f, 0.130f), strength = 1.0f },
            new Landmass { name = "Andes",     center = new Vector2(0.230f, 0.360f), radius = new Vector2(0.016f, 0.130f), strength = 1.0f },
            new Landmass { name = "Alps",      center = new Vector2(0.530f, 0.760f), radius = new Vector2(0.050f, 0.014f), strength = 0.8f },
            new Landmass { name = "Atlas",     center = new Vector2(0.505f, 0.640f), radius = new Vector2(0.040f, 0.010f), strength = 0.6f },
            new Landmass { name = "Himalaya",  center = new Vector2(0.690f, 0.650f), radius = new Vector2(0.070f, 0.016f), strength = 1.2f },
            new Landmass { name = "Urals",     center = new Vector2(0.620f, 0.800f), radius = new Vector2(0.010f, 0.070f), strength = 0.7f },
            new Landmass { name = "Great Divide", center = new Vector2(0.895f, 0.320f), radius = new Vector2(0.012f, 0.050f), strength = 0.6f },
        };

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Initialise();
        }

        /// <summary>Prepare the noise offsets. Safe to call multiple times.</summary>
        public void Initialise()
        {
            if (_initialised) return;
            var rng = new System.Random(seed);
            _oxA = (float)rng.NextDouble() * 10000f; _oyA = (float)rng.NextDouble() * 10000f;
            _oxB = (float)rng.NextDouble() * 10000f; _oyB = (float)rng.NextDouble() * 10000f;
            _oxC = (float)rng.NextDouble() * 10000f; _oyC = (float)rng.NextDouble() * 10000f;
            _oxD = (float)rng.NextDouble() * 10000f; _oyD = (float)rng.NextDouble() * 10000f;
            _initialised = true;
        }

        /// <summary>Create a WorldMap if one does not exist yet (used by the bootstrapper).</summary>
        public static WorldMap EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("WorldMap");
            var map = go.AddComponent<WorldMap>();
            map.Initialise();
            return map;
        }

        // ==============================================================
        // COORDINATES
        // ==============================================================

        /// <summary>Wrap an X tile coordinate around the planet.</summary>
        public int WrapX(int x)
        {
            int w = Mathf.Max(1, worldWidth);
            int m = x % w;
            return m < 0 ? m + w : m;
        }

        /// <summary>Clamp a Y tile coordinate to the poles.</summary>
        public int ClampY(int y) => Mathf.Clamp(y, 0, Mathf.Max(1, worldHeight) - 1);

        /// <summary>Normalised planet coordinates (u = longitude 0..1, v = latitude 0..1).</summary>
        public Vector2 ToUV(int x, int y) =>
            new Vector2(WrapX(x) / (float)worldWidth, ClampY(y) / (float)worldHeight);

        /// <summary>Latitude / longitude in degrees for a tile (for the compass and map UI).</summary>
        public Vector2 ToLatLon(int x, int y)
        {
            Vector2 uv = ToUV(x, y);
            float lat = (uv.y - 0.5f) * 180f;
            float lon = (uv.x - 0.5f) * 360f;
            return new Vector2(lat, lon);
        }

        /// <summary>Human readable position, e.g. "12.4°N 33.1°E — Africa".</summary>
        public string DescribePosition(int x, int y)
        {
            Vector2 ll = ToLatLon(x, y);
            string ns = ll.x >= 0f ? "N" : "S";
            string ew = ll.y >= 0f ? "E" : "W";
            return $"{Mathf.Abs(ll.x):0.0}°{ns} {Mathf.Abs(ll.y):0.0}°{ew} — {GetRegionName(x, y)}";
        }

        /// <summary>Name of the nearest landmass / ocean region.</summary>
        public string GetRegionName(int x, int y)
        {
            Vector2 uv = ToUV(x, y);
            if (uv.y < 0.075f) return "Antarctica";
            if (uv.y > 0.965f) return "Arctic Ice";

            string best = null;
            float bestScore = 0f;
            for (int i = 0; i < Continents.Length; i++)
            {
                float s = BlobField(uv, Continents[i]);
                if (s <= 0f) continue;
                if (s > bestScore) { bestScore = s; best = Continents[i].name; }
            }
            if (bestScore > 0.35f && best != null) return best;

            // Otherwise name the ocean by longitude.
            if (uv.x < 0.10f || uv.x > 0.93f) return "Pacific Ocean";
            if (uv.x < 0.20f) return "Pacific Ocean";
            if (uv.x < 0.45f) return "Atlantic Ocean";
            if (uv.x < 0.78f) return "Indian Ocean";
            return "Pacific Ocean";
        }

        // ==============================================================
        // SAMPLING
        // ==============================================================

        /// <summary>Sample the planet at a tile coordinate. Deterministic and allocation free.</summary>
        public WorldSample Sample(int x, int y)
        {
            Initialise();

            int wx = WrapX(x);
            int wy = ClampY(y);
            Vector2 uv = new Vector2(wx / (float)worldWidth, wy / (float)worldHeight);

            float elevation = Elevation(uv, wx, wy);
            bool isWater = elevation < seaLevel;

            float latitude = Mathf.Abs(uv.y - 0.5f) * 2f;              // 0 equator .. 1 pole
            float temperature = 30f - 58f * Mathf.Pow(latitude, 2.6f); // °C at sea level
            if (!isWater)
            {
                float aboveSea = Mathf.Max(0f, elevation - seaLevel) / (1f - seaLevel);
                temperature -= aboveSea * 38f;                          // lapse rate with altitude
            }
            temperature += (Fbm(wx * 0.0009f + _oxC, wy * 0.0009f + _oyC, 3) - 0.5f) * 8f;

            float moisture = Moisture(uv, wx, wy, elevation, isWater, latitude);
            bool isRiver = !isWater && IsRiver(wx, wy, elevation);

            var s = new WorldSample
            {
                elevation = elevation,
                temperature = temperature,
                moisture = moisture,
                isWater = isWater || isRiver,
                isRiver = isRiver
            };
            s.biome = Classify(s, elevation, temperature, moisture, isWater);
            return s;
        }

        /// <summary>Convenience overload for a world-space position (XY plane).</summary>
        public WorldSample SampleWorld(Vector3 worldPos) =>
            Sample(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.y));

        /// <summary>Biome at a world-space position.</summary>
        public BiomeType GetBiome(Vector3 worldPos) => SampleWorld(worldPos).biome;

        /// <summary>True when the given world position is ocean, lake or river.</summary>
        public bool IsWater(Vector3 worldPos) => SampleWorld(worldPos).isWater;

        // --------------------------------------------------------------
        private float Elevation(Vector2 uv, int wx, int wy)
        {
            // 1. Domain warp so continents are irregular instead of blobby.
            float warpU = (Fbm(wx * 0.00035f + _oxA, wy * 0.00035f + _oyA, 4) - 0.5f) * 0.16f;
            float warpV = (Fbm(wx * 0.00035f + _oxD, wy * 0.00035f + _oyD, 4) - 0.5f) * 0.12f;
            Vector2 wuv = new Vector2(Mathf.Repeat(uv.x + warpU, 1f), Mathf.Clamp01(uv.y + warpV));

            // 2. Continent mask from the landmass blobs.
            float land = 0f;
            for (int i = 0; i < Continents.Length; i++)
                land = Mathf.Max(land, BlobField(wuv, Continents[i]) * Continents[i].strength);

            // Polar ice caps.
            float polar = Mathf.InverseLerp(0.085f, 0.02f, uv.y);            // Antarctica
            polar = Mathf.Max(polar, Mathf.InverseLerp(0.955f, 0.995f, uv.y)); // Arctic
            land = Mathf.Max(land, polar * 0.95f);

            // 2. Warp the coastline with fractal noise so continents look natural.
            float warp = Fbm(wx * 0.0012f + _oxA, wy * 0.0012f + _oyA, 5);
            float detail = Fbm(wx * 0.010f + _oxB, wy * 0.010f + _oyB, 4);
            land += (warp - 0.5f) * coastRaggedness;
            land += (detail - 0.5f) * 0.10f;

            // 3. Islands scattered through the oceans.
            float island = Fbm(wx * 0.0035f + _oxD, wy * 0.0035f + _oyD, 3);
            if (island > 0.80f) land = Mathf.Max(land, (island - 0.80f) * 3.2f);

            // 4. Base elevation from the land mask.
            float elevation = Mathf.Lerp(0.16f, 0.84f, Mathf.Clamp01(land));

            // 5. Mountain belts.
            float mountains = 0f;
            for (int i = 0; i < MountainRanges.Length; i++)
                mountains = Mathf.Max(mountains, BlobField(wuv, MountainRanges[i]) * MountainRanges[i].strength);
            if (mountains > 0f && land > 0.25f)
            {
                float ridged = 1f - Mathf.Abs(Fbm(wx * 0.006f + 77f, wy * 0.006f + 31f, 4) * 2f - 1f);
                elevation += mountains * (0.28f + ridged * 0.28f);
            }

            // 6. Local hills.
            elevation += (Fbm(wx * 0.025f + 500f, wy * 0.025f + 500f, 3) - 0.5f) * 0.06f;

            return Mathf.Clamp01(elevation);
        }

        private float Moisture(Vector2 uv, int wx, int wy, float elevation, bool isWater, float latitude)
        {
            if (isWater) return 1f;

            // Hadley-cell style banding: wet equator, dry ~30°, wet ~60°, dry poles.
            float band = Mathf.Cos(latitude * Mathf.PI * 2.35f) * 0.5f + 0.5f;
            float m = Mathf.Lerp(0.15f, 0.9f, band);

            // Noise variation.
            m += (Fbm(wx * 0.0018f + _oxB, wy * 0.0018f + _oyB, 4) - 0.5f) * 0.55f;

            // Rain shadow: high ground is drier on the lee side.
            m -= Mathf.Max(0f, elevation - 0.72f) * 1.2f;

            return Mathf.Clamp01(m);
        }

        private bool IsRiver(int wx, int wy, float elevation)
        {
            if (elevation < seaLevel + 0.01f || elevation > 0.88f) return false;
            float n = Fbm(wx * 0.0045f + 913f, wy * 0.0045f + 271f, 4);
            float ridge = Mathf.Abs(n - 0.5f);
            // Rivers get slightly wider at low altitude.
            float threshold = Mathf.Lerp(0.010f, 0.0035f, Mathf.InverseLerp(0.85f, seaLevel, elevation));
            return ridge < threshold;
        }

        private BiomeType Classify(WorldSample s, float elevation, float temp, float moisture, bool isWater)
        {
            if (s.isRiver) return BiomeType.ShallowWater;

            if (isWater)
            {
                if (temp < -6f) return BiomeType.Glacier;   // frozen polar sea
                return elevation > seaLevel - 0.045f ? BiomeType.ShallowWater : BiomeType.Ocean;
            }

            // Coastal sand.
            if (elevation < seaLevel + 0.012f && temp > -2f) return BiomeType.Beach;

            // High ground.
            if (elevation > 0.90f) return BiomeType.SnowPeak;
            if (elevation > 0.80f) return temp < -12f ? BiomeType.SnowPeak : BiomeType.Mountain;

            // Ice.
            if (temp < -14f) return BiomeType.Glacier;
            if (temp < -4f) return BiomeType.Tundra;
            if (temp < 4f) return moisture > 0.42f ? BiomeType.Taiga : BiomeType.Tundra;

            // Temperate.
            if (temp < 18f)
            {
                if (moisture < 0.22f) return BiomeType.Steppe;
                if (moisture < 0.48f) return BiomeType.Grassland;
                return BiomeType.TemperateForest;
            }

            // Hot.
            if (moisture < 0.18f) return BiomeType.Desert;
            if (moisture < 0.42f) return BiomeType.Savannah;
            if (moisture < 0.72f) return BiomeType.TropicalRainforest;
            return elevation < seaLevel + 0.05f ? BiomeType.Swamp : BiomeType.TropicalRainforest;
        }

        // ==============================================================
        // CONTENT RULES (used by the chunk streamer and spawner)
        // ==============================================================

        /// <summary>Ground tile index for a sample. Matches ChunkManager.groundTiles order:
        /// 0 dirt, 1 grass, 2 sand, 3 snow, 4 stone, 5 mud.</summary>
        public static int GroundTileId(WorldSample s)
        {
            switch (s.biome)
            {
                case BiomeType.Beach: return 2;
                case BiomeType.Desert: return 2;
                case BiomeType.Glacier:
                case BiomeType.SnowPeak: return 3;
                case BiomeType.Tundra: return s.elevation > 0.7f ? 4 : 3;
                case BiomeType.Mountain: return 4;
                case BiomeType.Swamp: return 5;
                case BiomeType.Steppe: return 0;
                case BiomeType.Savannah: return s.moisture < 0.28f ? 0 : 1;
                case BiomeType.Taiga:
                case BiomeType.TemperateForest:
                case BiomeType.TropicalRainforest:
                case BiomeType.Grassland: return 1;
                default: return 1;
            }
        }

        /// <summary>Chance (0..1) per tile of a tree in this biome.</summary>
        public static float TreeDensity(BiomeType b)
        {
            switch (b)
            {
                case BiomeType.TropicalRainforest: return 0.16f;
                case BiomeType.TemperateForest: return 0.13f;
                case BiomeType.Taiga: return 0.11f;
                case BiomeType.Swamp: return 0.07f;
                case BiomeType.Savannah: return 0.025f;
                case BiomeType.Grassland: return 0.02f;
                case BiomeType.Mountain: return 0.015f;
                case BiomeType.Steppe: return 0.006f;
                case BiomeType.Tundra: return 0.004f;
                case BiomeType.Desert: return 0.001f;
                default: return 0f;
            }
        }

        /// <summary>Chance (0..1) per tile of a bush in this biome.</summary>
        public static float BushDensity(BiomeType b)
        {
            switch (b)
            {
                case BiomeType.TropicalRainforest: return 0.09f;
                case BiomeType.TemperateForest: return 0.07f;
                case BiomeType.Grassland: return 0.05f;
                case BiomeType.Savannah: return 0.045f;
                case BiomeType.Swamp: return 0.05f;
                case BiomeType.Taiga: return 0.03f;
                case BiomeType.Steppe: return 0.02f;
                case BiomeType.Tundra: return 0.01f;
                case BiomeType.Desert: return 0.004f;
                case BiomeType.Mountain: return 0.012f;
                default: return 0f;
            }
        }

        /// <summary>Chance (0..1) per tile of a rock in this biome.</summary>
        public static float RockDensity(BiomeType b)
        {
            switch (b)
            {
                case BiomeType.Mountain:
                case BiomeType.SnowPeak: return 0.06f;
                case BiomeType.Tundra:
                case BiomeType.Steppe:
                case BiomeType.Desert: return 0.02f;
                case BiomeType.Beach: return 0.006f;
                case BiomeType.Ocean:
                case BiomeType.ShallowWater:
                case BiomeType.Glacier: return 0f;
                default: return 0.012f;
            }
        }

        /// <summary>Whether a biome is walkable land (used for spawn selection).</summary>
        public static bool IsLandBiome(BiomeType b) =>
            b != BiomeType.Ocean && b != BiomeType.ShallowWater;

        /// <summary>Map colour used by the world-map UI and minimap.</summary>
        public static Color BiomeColor(BiomeType b)
        {
            switch (b)
            {
                case BiomeType.Ocean: return new Color(0.06f, 0.20f, 0.42f);
                case BiomeType.ShallowWater: return new Color(0.16f, 0.42f, 0.66f);
                case BiomeType.Beach: return new Color(0.86f, 0.80f, 0.56f);
                case BiomeType.Glacier: return new Color(0.90f, 0.95f, 0.98f);
                case BiomeType.SnowPeak: return new Color(0.98f, 0.98f, 1.00f);
                case BiomeType.Tundra: return new Color(0.66f, 0.70f, 0.66f);
                case BiomeType.Taiga: return new Color(0.20f, 0.38f, 0.28f);
                case BiomeType.TemperateForest: return new Color(0.18f, 0.48f, 0.22f);
                case BiomeType.Grassland: return new Color(0.42f, 0.64f, 0.28f);
                case BiomeType.Steppe: return new Color(0.62f, 0.62f, 0.34f);
                case BiomeType.Desert: return new Color(0.84f, 0.72f, 0.42f);
                case BiomeType.Savannah: return new Color(0.72f, 0.66f, 0.30f);
                case BiomeType.TropicalRainforest: return new Color(0.10f, 0.40f, 0.16f);
                case BiomeType.Swamp: return new Color(0.28f, 0.38f, 0.26f);
                case BiomeType.Mountain: return new Color(0.48f, 0.44f, 0.40f);
                default: return Color.magenta;
            }
        }

        /// <summary>Friendly biome name for the HUD.</summary>
        public static string BiomeName(BiomeType b)
        {
            switch (b)
            {
                case BiomeType.ShallowWater: return "Shallow Water";
                case BiomeType.TemperateForest: return "Temperate Forest";
                case BiomeType.TropicalRainforest: return "Rainforest";
                case BiomeType.SnowPeak: return "Snow Peak";
                default: return b.ToString();
            }
        }

        // ==============================================================
        // SPAWN POINT
        // ==============================================================

        /// <summary>
        /// Find a pleasant starting tile: temperate, near fresh water, not on a mountain.
        /// </summary>
        public Vector2Int FindSpawnTile()
        {
            if (defaultSpawnTile != Vector2Int.zero) return defaultSpawnTile;

            // Search around East Africa (cradle of humankind) first, then fall back to a scan.
            Vector2 startUV = new Vector2(0.565f, 0.545f);
            Vector2Int origin = new Vector2Int(
                Mathf.RoundToInt(startUV.x * worldWidth),
                Mathf.RoundToInt(startUV.y * worldHeight));

            for (int radius = 0; radius < 5000; radius += 40)
            {
                for (int a = 0; a < 32; a++)
                {
                    float ang = a / 32f * Mathf.PI * 2f;
                    int x = origin.x + Mathf.RoundToInt(Mathf.Cos(ang) * radius);
                    int y = origin.y + Mathf.RoundToInt(Mathf.Sin(ang) * radius);
                    if (IsGoodSpawn(x, y)) return new Vector2Int(WrapX(x), ClampY(y));
                }
            }

            // Fallback: coarse scan of the whole planet for any decent land.
            for (int y = worldHeight / 6; y < worldHeight * 5 / 6; y += 64)
                for (int x = 0; x < worldWidth; x += 64)
                    if (IsGoodSpawn(x, y)) return new Vector2Int(WrapX(x), ClampY(y));

            return origin;
        }

        private bool IsGoodSpawn(int x, int y)
        {
            var s = Sample(x, y);
            if (s.isWater) return false;
            if (s.biome == BiomeType.Mountain || s.biome == BiomeType.SnowPeak) return false;
            if (s.temperature < 5f || s.temperature > 34f) return false;
            if (s.moisture < 0.25f) return false;
            return true;
        }

        // ==============================================================
        // OVERVIEW TEXTURE (world map UI / minimap)
        // ==============================================================

        /// <summary>
        /// Render the whole planet into a texture. Expensive, so cache the result
        /// (WorldMapUI does exactly that).
        /// </summary>
        public Texture2D GenerateOverviewTexture(int width = 512, int height = 256)
        {
            Initialise();
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[width * height];
            for (int py = 0; py < height; py++)
            {
                int wy = Mathf.RoundToInt(py / (float)(height - 1) * (worldHeight - 1));
                for (int px = 0; px < width; px++)
                {
                    int wx = Mathf.RoundToInt(px / (float)(width - 1) * (worldWidth - 1));
                    var s = Sample(wx, wy);
                    Color c = BiomeColor(s.biome);
                    // Cheap relief shading.
                    float shade = Mathf.Lerp(0.82f, 1.12f, Mathf.Clamp01(s.elevation));
                    c = new Color(c.r * shade, c.g * shade, c.b * shade, 1f);
                    pixels[py * width + px] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>Render a small minimap around a world position.</summary>
        public Texture2D GenerateLocalTexture(Vector3 center, int pixels = 128, int tilesPerPixel = 4)
        {
            Initialise();
            var tex = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            int cx = Mathf.FloorToInt(center.x);
            int cy = Mathf.FloorToInt(center.y);
            int half = pixels / 2;
            var buffer = new Color32[pixels * pixels];
            for (int py = 0; py < pixels; py++)
            {
                for (int px = 0; px < pixels; px++)
                {
                    int wx = cx + (px - half) * tilesPerPixel;
                    int wy = cy + (py - half) * tilesPerPixel;
                    buffer[py * pixels + px] = BiomeColor(Sample(wx, wy).biome);
                }
            }
            tex.SetPixels32(buffer);
            tex.Apply(false, false);
            return tex;
        }

        // ==============================================================
        // NOISE HELPERS
        // ==============================================================

        private static float BlobField(Vector2 uv, Landmass blob)
        {
            // Horizontal distance respects wrap-around.
            float dx = Mathf.Abs(uv.x - blob.center.x);
            if (dx > 0.5f) dx = 1f - dx;
            dx /= Mathf.Max(0.0001f, blob.radius.x);
            float dy = (uv.y - blob.center.y) / Mathf.Max(0.0001f, blob.radius.y);
            // Squircle falloff (exponent 2.3) gives less perfectly round landmasses.
            float d = Mathf.Pow(Mathf.Pow(Mathf.Abs(dx), 2.3f) + Mathf.Pow(Mathf.Abs(dy), 2.3f), 1f / 2.3f);
            return Mathf.Clamp01(1f - d);
        }

        /// <summary>Fractal brownian motion built on Unity's Perlin noise. Returns 0..1.</summary>
        private static float Fbm(float x, float y, int octaves)
        {
            float sum = 0f, amp = 0.5f, freq = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x * freq, y * freq) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            return norm > 0f ? sum / norm : 0f;
        }

        /// <summary>Deterministic 0..1 hash for a tile (used for prop placement).</summary>
        public static float Hash01(int x, int y, int salt = 0)
        {
            unchecked
            {
                int h = x * 73856093 ^ y * 19349663 ^ salt * 83492791;
                h = (h ^ 61) ^ (h >> 16);
                h += h << 3;
                h ^= h >> 4;
                h *= 0x27d4eb2d;
                h ^= h >> 15;
                return (h & 0x7fffffff) / (float)0x7fffffff;
            }
        }
    }
}
