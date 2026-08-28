using System.Collections.Generic;
using UnityEngine;
using PrehistoricSurvival.World;

namespace PrehistoricSurvival.Content
{
    /// <summary>Full stat + habitat definition for one species.</summary>
    [System.Serializable]
    public class SpeciesDef
    {
        public string prefabName;      // prefab + sprite folder key, e.g. "DireWolf"
        public string displayName;     // e.g. "Dire Wolf"
        public float maxHealth;
        public float damage;
        public float moveSpeed;
        public float runSpeed;
        public float detectionRange;
        public float attackRange;
        public float leashRange;
        public float fleeThreshold;
        public PrehistoricSurvival.AI.AnimalAI.AggressionLevel aggression;
        public BiomeType[] biomes;     // where herds appear
        public float spawnWeight;      // relative weight within its biomes
        public Vector2Int herdSize = new Vector2Int(2, 4);
        public int meatMin = 2, meatMax = 4;
        public int hideMin = 1, hideMax = 2;
        public bool bird;              // birds use tiny stats + ambient calls
        public float knowledgeReward;  // extra era knowledge on first kills of this species
    }

    /// <summary>
    /// Catalog of all 15 species (4 original + 11 new) with their habitats and
    /// loot yields. The editor setup builds prefabs from the same table and the
    /// runtime spawner uses it to place biome-correct herds.
    /// </summary>
    public static class AnimalCatalog
    {
        public static readonly SpeciesDef[] All =
        {
            new SpeciesDef{ prefabName="Mammoth", displayName="Woolly Mammoth", maxHealth=220f, damage=25f,
                moveSpeed=2.5f, runSpeed=5f, detectionRange=15f, attackRange=2.5f, leashRange=32f, fleeThreshold=0.12f,
                aggression=PrehistoricSurvival.AI.AnimalAI.AggressionLevel.Neutral,
                biomes=new[]{ BiomeType.Steppe, BiomeType.Tundra, BiomeType.Grassland }, spawnWeight=0.5f,
                herdSize=new Vector2Int(2,4), meatMin=6, meatMax=10, hideMin=3, hideMax=5, knowledgeReward=9f },
            new SpeciesDef{ prefabName="Sabertooth", displayName="Sabertooth Tiger", maxHealth=150f, damage=30f,
                moveSpeed=3.5f, runSpeed=7f, detectionRange=16f, attackRange=2f, leashRange=26f, fleeThreshold=0.2f,
                aggression=PrehistoricSurvival.AI.AnimalAI.AggressionLevel.Aggressive,
                biomes=new[]{ BiomeType.TropicalRainforest, BiomeType.Savannah, BiomeType.TemperateForest }, spawnWeight=0.6f,
                herdSize=new Vector2Int(1,2), meatMin=3, meatMax=6, hideMin=2, hideMax=3, knowledgeReward=6f },
            new SpeciesDef{ prefabName="CaveBear", displayName="Cave Bear", maxHealth=190f, damage=22f,
                moveSpeed=3f, runSpeed=6f, detectionRange=14f, attackRange=2.2f, leashRange=28f, fleeThreshold=0.18f,
                aggression=PrehistoricSurvival.AI.AnimalAI.AggressionLevel.Aggressive,
                biomes=new[]{ BiomeType.Taiga, BiomeType.TemperateForest, BiomeType.Mountain }, spawnWeight=0.55f,
                herdSize=new Vector2Int(1,2), meatMin=4, meatMax=8, hideMin=2, hideMax=4, knowledgeReward=6f },
            new SpeciesDef{ prefabName="Bison", displayName="Steppe Bison", maxHealth=170f, damage=15f,
                moveSpeed=3f, runSpeed=5.5f, detectionRange=16f, attackRange=2f, leashRange=32f, fleeThreshold=0.2f,
                aggression=PrehistoricSurvival.AI.AnimalAI.AggressionLevel.Neutral,
                biomes=new[]{ BiomeType.Steppe, BiomeType.Grassland, BiomeType.Savannah }, spawnWeight=1f,
                herdSize=new Vector2Int(3,6), meatMin=4, meatMax=7, hideMin=2, hideMax=3, knowledgeReward=3f },

            new SpeciesDef{ prefabName="WoollyRhino", displayName="Woolly Rhinoceros", maxHealth=240f, damage=28f,
                moveSpeed=2.4f, runSpeed=4.8f, detectionRange=13f, attackRange=2.4f, leashRange=30f, fleeThreshold=0.1f,
                aggression=PrehistoricSurvival.AI.AnimalAI.AggressionLevel.Aggressive,
                biomes=new[]{ BiomeType.Tundra, BiomeType.Steppe }, spawnWeight=0.4f,
                herdSize=new Vector2Int(1,2), meatMin=6, meatMax=9, hideMin=3, hideMax=4, knowledgeReward=9f },
            new SpeciesDef{ prefabName="CaveLion", displayName="Cave Lion", maxHealth=160f, damage=32f,
                moveSpeed=3.6f, runSpeed=7.4f, detectionRange=17f, attackRange=2f, leashRange=28f, fleeThreshold=0.2f,
                aggression=PrehistoricSurvival.AI.AnimalAI.AggressionLevel.Aggressive,
                biomes=new[]{ BiomeType.Steppe, BiomeType.Savannah, BiomeType.Grassland }, spawnWeight=0.45f,
                herdSize=new Vector2Int(1,3), meatMin=3, meatMax=6, hideMin=2, hideMax=3, knowledgeReward=6f },
            new SpeciesDef{ prefabName="DireWolf", displayName="Dire Wolf", maxHealth=110f, damage=18f,
                moveSpeed=3.8f, runSpeed=7.6f, detectionRange=18f, attackRange=1.8f, leashRange=34f, fleeThreshold=0.15f,
                aggression=PrehistoricSurvival.AI.AnimalAI.AggressionLevel.Aggressive,
                biomes=new[]{ BiomeType.Taiga, BiomeType.Tundra, BiomeType.TemperateForest, BiomeType.Steppe }, spawnWeight=0.8f,
                herdSize=new Vector2Int(3,5), meatMin=2, meatMax=4, hideMin=1, hideMax=2, knowledgeReward=4f },
            new SpeciesDef{ prefabName="CaveHyena", displayName="Cave Hyena", maxHealth=95f, damage=14f,
                moveSpeed=3.4f, runSpeed=6.8f, detectionRange=16f, attackRange=1.8f, leashRange=30f, fleeThreshold=0.25f,
                aggression=PrehistoricSurvival.AI.AnimalAI.AggressionLevel.Aggressive,
                biomes=new[]{ BiomeType.Savannah, BiomeType.Steppe, BiomeType.Desert }, spawnWeight=0.6f,
                herdSize=new Vector2Int(2,4), meatMin=2, meatMax=3, hideMin=1, hideMax=2, knowledgeReward=4f },
            new SpeciesDef{ prefabName="Reindeer", displayName="Reindeer", maxHealth=90f, damage=8f,
                moveSpeed=3.2f, runSpeed=6.6f, detectionRange=17f, attackRange=1.6f, leashRange=34f, fleeThreshold=0.35f,
                aggression=PrehistoricSurvival.AI.AnimalAI.AggressionLevel.Passive,
                biomes=new[]{ BiomeType.Tundra, BiomeType.Taiga, BiomeType.Glacier }, spawnWeight=1f,
                herdSize=new Vector2Int(3,6), meatMin=3, meatMax=5, hideMin=2, hideMax=3, knowledgeReward=2f },
            new SpeciesDef{ prefabName="MuskOx", displayName="Musk Ox", maxHealth=180f, damage=16f,
                moveSpeed=2.6f, runSpeed=5f, detectionRange=14f, attackRange=2f, leashRange=30f, fleeThreshold=0.15f,
                aggression=PrehistoricSurvival.AI.AnimalAI.AggressionLevel.Neutral,
                biomes=new[]{ BiomeType.Tundra, BiomeType.Glacier }, spawnWeight=0.7f,
                herdSize=new Vector2Int(2,5), meatMin=4, meatMax=7, hideMin=2, hideMax=4, knowledgeReward=3f },
            new SpeciesDef{ prefabName="GiantElk", displayName="Giant Elk", maxHealth=130f, damage=12f,
                moveSpeed=3.4f, runSpeed=7f, detectionRange=18f, attackRange=1.8f, leashRange=34f, fleeThreshold=0.32f,
                aggression=PrehistoricSurvival.AI.AnimalAI.AggressionLevel.Passive,
                biomes=new[]{ BiomeType.TemperateForest, BiomeType.Grassland, BiomeType.Taiga }, spawnWeight=0.9f,
                herdSize=new Vector2Int(2,4), meatMin=3, meatMax=6, hideMin=2, hideMax=3, knowledgeReward=3f },
            new SpeciesDef{ prefabName="WildBoar", displayName="Wild Boar", maxHealth=70f, damage=10f,
                moveSpeed=3f, runSpeed=6f, detectionRange=12f, attackRange=1.5f, leashRange=24f, fleeThreshold=0.3f,
                aggression=PrehistoricSurvival.AI.AnimalAI.AggressionLevel.Neutral,
                biomes=new[]{ BiomeType.TemperateForest, BiomeType.TropicalRainforest, BiomeType.Swamp }, spawnWeight=1f,
                herdSize=new Vector2Int(2,4), meatMin=2, meatMax=4, hideMin=1, hideMax=2, knowledgeReward=2f },
            new SpeciesDef{ prefabName="SnowHare", displayName="Snow Hare", maxHealth=25f, damage=0f,
                moveSpeed=3.6f, runSpeed=7.2f, detectionRange=12f, attackRange=1f, leashRange=20f, fleeThreshold=0.9f,
                aggression=PrehistoricSurvival.AI.AnimalAI.AggressionLevel.Passive,
                biomes=new[]{ BiomeType.Tundra, BiomeType.Glacier, BiomeType.Taiga }, spawnWeight=1.1f,
                herdSize=new Vector2Int(1,3), meatMin=1, meatMax=1, hideMin=1, hideMax=1, knowledgeReward=1f },
            new SpeciesDef{ prefabName="CavePtarmigan", displayName="Cave Ptarmigan", maxHealth=15f, damage=0f,
                moveSpeed=2f, runSpeed=4f, detectionRange=10f, attackRange=1f, leashRange=18f, fleeThreshold=0.95f,
                aggression=PrehistoricSurvival.AI.AnimalAI.AggressionLevel.Passive, bird=true,
                biomes=new[]{ BiomeType.Tundra, BiomeType.Glacier, BiomeType.Taiga, BiomeType.Mountain }, spawnWeight=0.9f,
                herdSize=new Vector2Int(1,3), meatMin=1, meatMax=1, hideMin=0, hideMax=1, knowledgeReward=1f },
            new SpeciesDef{ prefabName="GreatAuk", displayName="Great Auk", maxHealth=18f, damage=0f,
                moveSpeed=2f, runSpeed=4f, detectionRange=10f, attackRange=1f, leashRange=18f, fleeThreshold=0.95f,
                aggression=PrehistoricSurvival.AI.AnimalAI.AggressionLevel.Passive, bird=true,
                biomes=new[]{ BiomeType.Beach, BiomeType.Ocean, BiomeType.ShallowWater }, spawnWeight=0.9f,
                herdSize=new Vector2Int(2,4), meatMin=1, meatMax=2, hideMin=0, hideMax=1, knowledgeReward=1f },
        };

        public static SpeciesDef Get(string prefabName)
        {
            foreach (var s in All)
                if (s.prefabName == prefabName) return s;
            return null;
        }

        /// <summary>Weighted pick of a species valid for a biome.</summary>
        public static SpeciesDef PickForBiome(BiomeType biome, float random01)
        {
            float total = 0f;
            foreach (var s in All)
                if (System.Array.IndexOf(s.biomes, biome) >= 0) total += s.spawnWeight;
            if (total <= 0f) return null;
            float roll = random01 * total;
            foreach (var s in All)
            {
                if (System.Array.IndexOf(s.biomes, biome) < 0) continue;
                roll -= s.spawnWeight;
                if (roll <= 0f) return s;
            }
            return null;
        }

        public static bool IsPredator(string displayName)
        {
            string n = (displayName ?? "").ToLowerInvariant();
            return n.Contains("sabertooth") || n.Contains("lion") || n.Contains("wolf")
                || n.Contains("bear") || n.Contains("hyena");
        }
    }
}
