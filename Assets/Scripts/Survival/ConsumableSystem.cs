using UnityEngine;
using PrehistoricSurvival.Core;

namespace PrehistoricSurvival.Survival
{
    /// <summary>
    /// Handles eating/drinking consumable items from inventory.
    /// Restores survival stats based on item properties.
    /// </summary>
    public class ConsumableSystem : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Time to consume an item (seconds).")]
        public float consumeTime = 2f;

        [Header("Animation")]
        [Tooltip("Overlay sprite to show during eating.")]
        public SpriteRenderer eatingOverlay;
        [Tooltip("Sprite to show while eating.")]
        public Sprite eatingSprite;

        [Header("Audio")]
        public AudioClip eatingSound;
        public AudioClip drinkingSound;

        private AudioSource _audio;
        private SurvivalStats _stats;
        private InventorySystem _inventory;
        private float _consumeTimer;
        private bool _isConsuming;
        private ItemData _currentItem;

        private void Start()
        {
            _audio = GetComponent<AudioSource>();
            if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
            _stats = GetComponent<SurvivalStats>();
            _inventory = InventorySystem.Instance;

            if (eatingOverlay != null) eatingOverlay.enabled = false;
        }

        private void Update()
        {
            // Press F to consume selected item (simplified – real impl uses UI selection)
            if (Input.GetKeyDown(KeyCode.F))
                TryConsume();

            if (_isConsuming)
            {
                _consumeTimer += Time.deltaTime;
                if (_consumeTimer >= consumeTime)
                    CompleteConsumption();
            }
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>Attempt to consume an item from inventory.</summary>
        public bool TryConsume(ItemData item = null)
        {
            if (_isConsuming) return false;
            if (_stats == null || _inventory == null) return false;

            // If no item specified, find first consumable in inventory
            if (item == null)
            {
                foreach (var slot in _inventory.Slots)
                {
                    if (!slot.IsEmpty && slot.item.isConsumable)
                    {
                        item = slot.item;
                        break;
                    }
                }
            }

            if (item == null || !item.isConsumable) return false;
            if (!_inventory.HasItem(item.itemId, 1)) return false;

            _currentItem = item;
            _consumeTimer = 0f;
            _isConsuming = true;

            // Show eating overlay
            if (eatingOverlay != null && eatingSprite != null)
            {
                eatingOverlay.sprite = eatingSprite;
                eatingOverlay.enabled = true;
            }

            // Play sound
            if (_audio != null)
            {
                AudioClip clip = item.thirstRestore > 0f ? drinkingSound : eatingSound;
                if (clip != null) _audio.PlayOneShot(clip);
            }

            return true;
        }

        private void CompleteConsumption()
        {
            _isConsuming = false;

            if (eatingOverlay != null) eatingOverlay.enabled = false;

            if (_currentItem == null || _inventory == null || _stats == null) return;

            // Remove item from inventory
            _inventory.RemoveItemById(_currentItem.itemId, 1);

            // Restore stats
            _stats.Consume(
                _currentItem.hungerRestore,
                _currentItem.thirstRestore,
                _currentItem.healthRestore,
                _currentItem.energyRestore
            );

            EventManager.TriggerEvent(GameEvents.ItemConsumed, new ItemEventPayload(_currentItem, 1));
            _currentItem = null;
        }

        /// <summary>Cancel current consumption.</summary>
        public void CancelConsumption()
        {
            _isConsuming = false;
            _consumeTimer = 0f;
            _currentItem = null;
            if (eatingOverlay != null) eatingOverlay.enabled = false;
        }

        public bool IsConsuming => _isConsuming;
    }
}
