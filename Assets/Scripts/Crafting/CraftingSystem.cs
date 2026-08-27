using System;
using System.Collections.Generic;
using UnityEngine;
using PrehistoricSurvival.Core;

namespace PrehistoricSurvival.Crafting
{
    /// <summary>
    /// Manages the crafting process: checks ingredients, consumes them,
    /// and produces the output item after a timer.
    /// </summary>
    public class CraftingSystem : MonoBehaviour
    {
        public static CraftingSystem Instance { get; private set; }

        [Header("References")]
        [Tooltip("Recipe database ScriptableObject.")]
        public RecipeDatabase recipeDatabase;

        [Header("UI")]
        [Tooltip("Crafting progress bar (0..1 fill).")]
        public UnityEngine.UI.Image progressBar;

        [Header("Audio")]
        public AudioClip craftStartSound;
        public AudioClip craftCompleteSound;

        private AudioSource _audio;
        private Recipe _currentRecipe;
        private float _craftTimer;
        private bool _isCrafting;
        private string _nearbyStation;

        // Events
        public event Action<Recipe> OnCraftStarted;
        public event Action<Recipe> OnCraftCompleted;
        public event Action OnCraftCancelled;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _audio = GetComponent<AudioSource>();
            if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
        }

        private void Update()
        {
            if (_isCrafting)
            {
                _craftTimer += Time.deltaTime;

                if (progressBar != null)
                    progressBar.fillAmount = _craftTimer / _currentRecipe.craftTime;

                if (_craftTimer >= _currentRecipe.craftTime)
                    CompleteCraft();
            }
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>Check if the player can craft a recipe.</summary>
        public bool CanCraft(Recipe recipe)
        {
            if (recipe == null || recipe.ingredients == null) return false;

            // Check station requirement
            if (!string.IsNullOrEmpty(recipe.requiredStation) && _nearbyStation != recipe.requiredStation)
                return false;

            // Check ingredients
            var inv = InventorySystem.Instance;
            if (inv == null) return false;

            foreach (var ing in recipe.ingredients)
            {
                if (!inv.HasItem(ing.itemId, ing.amount))
                    return false;
            }

            return true;
        }

        /// <summary>Start crafting a recipe.</summary>
        public bool StartCraft(Recipe recipe)
        {
            if (_isCrafting) return false;
            if (!CanCraft(recipe)) return false;

            _currentRecipe = recipe;
            _craftTimer = 0f;
            _isCrafting = true;

            if (progressBar != null) progressBar.fillAmount = 0f;
            if (craftStartSound != null) _audio.PlayOneShot(craftStartSound);

            OnCraftStarted?.Invoke(recipe);
            return true;
        }

        /// <summary>Cancel current crafting.</summary>
        public void CancelCraft()
        {
            if (!_isCrafting) return;
            _isCrafting = false;
            _currentRecipe = null;
            _craftTimer = 0f;
            if (progressBar != null) progressBar.fillAmount = 0f;
            OnCraftCancelled?.Invoke();
        }

        private void CompleteCraft()
        {
            _isCrafting = false;

            if (_currentRecipe == null) return;

            var inv = InventorySystem.Instance;
            if (inv == null) return;

            // Consume ingredients
            foreach (var ing in _currentRecipe.ingredients)
                inv.RemoveItemById(ing.itemId, ing.amount);

            // Produce output
            if (_currentRecipe.outputItem != null)
            {
                inv.AddItem(_currentRecipe.outputItem, _currentRecipe.outputAmount);
                EventManager.TriggerEvent(GameEvents.ItemCrafted, _currentRecipe);
            }

            if (craftCompleteSound != null) _audio.PlayOneShot(craftCompleteSound);
            OnCraftCompleted?.Invoke(_currentRecipe);

            if (progressBar != null) progressBar.fillAmount = 0f;
            _currentRecipe = null;
        }

        /// <summary>Get all craftable recipes given current inventory.</summary>
        public List<Recipe> GetCraftableRecipes()
        {
            var result = new List<Recipe>();
            if (recipeDatabase == null) return result;

            foreach (var recipe in recipeDatabase.GetAllRecipes())
            {
                if (CanCraft(recipe))
                    result.Add(recipe);
            }
            return result;
        }

        /// <summary>Called when player enters/exits a crafting station trigger.</summary>
        public void SetNearbyStation(string stationTag)
        {
            _nearbyStation = stationTag;
        }

        public bool IsCrafting => _isCrafting;
    }
}
