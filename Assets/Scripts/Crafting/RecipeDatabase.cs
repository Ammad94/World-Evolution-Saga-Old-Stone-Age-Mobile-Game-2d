using System;
using System.Collections.Generic;
using UnityEngine;
using PrehistoricSurvival.Core;

namespace PrehistoricSurvival.Crafting
{
    /// <summary>
    /// Defines a crafting recipe: required ingredients and output.
    /// </summary>
    [Serializable]
    public class Recipe
    {
        public string recipeId;
        public string displayName;
        [TextArea(2, 4)]
        public string description;
        public Sprite icon;

        [Header("Ingredients")]
        public Ingredient[] ingredients;

        [Header("Output")]
        public ItemData outputItem;
        public int outputAmount = 1;

        [Header("Requirements")]
        [Tooltip("Required crafting station tag (e.g., 'campfire', 'workbench'). Empty = anywhere.")]
        public string requiredStation;

        [Tooltip("Required era level (0 = Paleolithic, 1 = AdvancedStone, 2 = CopperAge).")]
        public int requiredEra = 0;

        [Tooltip("Crafting time in seconds.")]
        public float craftTime = 5f;
    }

    [Serializable]
    public class Ingredient
    {
        public string itemId;
        public int amount = 1;
    }

    /// <summary>
    /// ScriptableObject-based recipe database.
    /// Create via Assets > Create > PrehistoricSurvival > Recipe Database.
    /// </summary>
    [CreateAssetMenu(fileName = "RecipeDatabase", menuName = "PrehistoricSurvival/Recipe Database")]
    public class RecipeDatabase : ScriptableObject
    {
        public List<Recipe> recipes = new List<Recipe>();

        public Recipe GetRecipe(string recipeId)
        {
            foreach (var r in recipes)
                if (r.recipeId == recipeId) return r;
            return null;
        }

        public List<Recipe> GetAllRecipes() => recipes;
    }
}
