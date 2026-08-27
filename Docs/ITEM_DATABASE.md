# Prehistoric Survival - Item Database Reference

## How to Create Items

### 1. Create ItemData ScriptableObject
1. In Unity, go to **Assets → Create → PrehistoricSurvival → Item Data**
   (You'll need to create this menu option first - see below)
2. Or create a new C# script for ItemData ScriptableObject:

```csharp
// Assets/Scripts/Core/ItemDataSO.cs
using UnityEngine;

namespace PrehistoricSurvival.Core
{
    [CreateAssetMenu(fileName = "NewItem", menuName = "PrehistoricSurvival/Item Data")]
    public class ItemDataSO : ScriptableObject
    {
        public ItemData data;
    }
}
```

### 2. Example Items

#### Food Items
- **Raw Meat** (itemId: "raw_meat")
  - Hunger: +20, Health: -5 (food poisoning risk)
  - Category: Food
  
- **Cooked Meat** (itemId: "cooked_meat")
  - Hunger: +50, Health: +10
  - Category: Food

- **Wild Apple** (itemId: "wild_apple")
  - Hunger: +15, Thirst: +10
  - Category: Food

- **Wild Carrot** (itemId: "wild_carrot")
  - Hunger: +10, Health: +5
  - Category: Food

- **Berries** (itemId: "berries")
  - Hunger: +8, Thirst: +5
  - Category: Food

#### Resources
- **Wood Log** (itemId: "wood_log")
  - Weight: 25 kg
  - Category: Resource

- **Stone** (itemId: "stone")
  - Weight: 5 kg
  - Category: Resource

- **Animal Hide** (itemId: "animal_hide")
  - Weight: 3 kg
  - Category: Resource

- **Fiber** (itemId: "fiber")
  - Weight: 0.1 kg
  - Category: Resource

#### Tools
- **Stone Pickaxe** (itemId: "stone_pickaxe")
  - Durability: 50 uses
  - Category: Tool

- **Stone Axe** (itemId: "stone_axe")
  - Durability: 40 uses
  - Category: Tool

- **Wooden Shovel** (itemId: "wooden_shovel")
  - Durability: 30 uses
  - Category: Tool

- **Torch** (itemId: "torch")
  - Burn time: 600 seconds
  - Category: Tool

---

## How to Create Recipes

### 1. Create RecipeDatabase
1. **Assets → Create → PrehistoricSurvival → Recipe Database**
2. Name it `RecipeDatabase`
3. Add recipes to the list

### 2. Example Recipes

#### Stone Pickaxe
- **Ingredients:**
  - 3x Stone
  - 2x Wood Log
  - 1x Fiber
- **Output:** 1x Stone Pickaxe
- **Craft Time:** 5 seconds
- **Station:** None (craft anywhere)

#### Campfire
- **Ingredients:**
  - 5x Wood Log
  - 3x Stone
- **Output:** 1x Campfire (building item)
- **Craft Time:** 10 seconds
- **Station:** None

#### Leather Tunic
- **Ingredients:**
  - 4x Animal Hide
  - 2x Fiber
- **Output:** 1x Leather Tunic
- **Craft Time:** 15 seconds
- **Station:** None

#### Log Raft
- **Ingredients:**
  - 10x Wood Log
  - 5x Fiber
- **Output:** 1x Log Raft
- **Craft Time:** 30 seconds
- **Station:** Near water

#### Cooked Meat
- **Ingredients:**
  - 1x Raw Meat
- **Output:** 1x Cooked Meat
- **Craft Time:** 10 seconds
- **Station:** campfire

---

## Tips for Balancing

### Survival Rates
- **Hunger drain:** 0.15/sec → ~11 minutes to starve from full
- **Thirst drain:** 0.25/sec → ~6.5 minutes to dehydrate from full
- **Energy drain:** 0.1/sec → ~16 minutes to exhaust from full

### Food Values
- Small snacks (berries): +8 hunger
- Medium food (apple): +15 hunger
- Large meal (cooked meat): +50 hunger

### Resource Gathering
- Trees: 3-5 wood logs, 3 seconds to chop
- Stone deposits: 2-4 stone, 2 seconds to mine
- Berry bushes: 5 berries, 1 second to harvest
- Root digging: 1 root, 2 seconds to dig

### Animal Loot
- Small animals (rabbit): 1 raw meat, 1 hide
- Medium animals (deer): 3 raw meat, 2 hide
- Large animals (mammoth): 10 raw meat, 5 hide

---

## Quick Reference

| Item ID | Name | Category | Weight |
|---------|------|----------|--------|
| raw_meat | Raw Meat | Food | 1.0 |
| cooked_meat | Cooked Meat | Food | 1.0 |
| wild_apple | Wild Apple | Food | 0.2 |
| berries | Berries | Food | 0.1 |
| wood_log | Wood Log | Resource | 25.0 |
| stone | Stone | Resource | 5.0 |
| animal_hide | Animal Hide | Resource | 3.0 |
| fiber | Fiber | Resource | 0.1 |
| stone_pickaxe | Stone Pickaxe | Tool | 2.0 |
| stone_axe | Stone Axe | Tool | 2.5 |
| torch | Torch | Tool | 0.5 |
