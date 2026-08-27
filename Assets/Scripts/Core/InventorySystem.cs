using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrehistoricSurvival.Core
{
    /// <summary>
    /// Defines a single item type with its metadata.
    /// </summary>
    [Serializable]
    public class ItemData
    {
        public string itemId;
        public string displayName;
        [TextArea(2, 4)]
        public string description;
        public Sprite icon;
        public GameObject worldPrefab;      // prefab dropped in the world
        public int maxStack = 99;
        public float weight = 1f;           // kg per unit

        [Header("Category")]
        public ItemCategory category;

        [Header("Consumable (optional)")]
        public bool isConsumable;
        public float hungerRestore;
        public float thirstRestore;
        public float healthRestore;
        public float energyRestore;
    }

    public enum ItemCategory
    {
        Resource,
        Food,
        Tool,
        Weapon,
        Clothing,
        Building,
        Misc
    }

    /// <summary>
    /// Represents a stack of one item type inside the inventory.
    /// </summary>
    [Serializable]
    public class InventorySlot
    {
        public ItemData item;
        public int quantity;

        public bool IsEmpty => item == null || quantity <= 0;
        public bool IsFull  => !IsEmpty && quantity >= item.maxStack;

        public float TotalWeight => IsEmpty ? 0f : item.weight * quantity;
    }

    /// <summary>
    /// Player inventory with add / remove / query helpers and events.
    /// </summary>
    public class InventorySystem : MonoBehaviour
    {
        public static InventorySystem Instance { get; private set; }

        [Header("Settings")]
        [Tooltip("Maximum distinct item stacks.")]
        public int slotCount = 24;

        [Tooltip("Maximum total carry weight (kg). 0 = unlimited.")]
        public float maxCarryWeight = 80f;

        private List<InventorySlot> _slots;
        public IReadOnlyList<InventorySlot> Slots => _slots;

        // Events
        public event Action<InventorySlot> OnSlotChanged;
        public event Action OnInventoryChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _slots = new List<InventorySlot>(slotCount);
            for (int i = 0; i < slotCount; i++) _slots.Add(new InventorySlot());
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>Returns total weight of all carried items.</summary>
        public float TotalWeight()
        {
            float w = 0f;
            foreach (var s in _slots) w += s.TotalWeight;
            return w;
        }

        /// <summary>Can the inventory accept <paramref name="amount"/> more of <paramref name="item"/>?</summary>
        public bool CanAdd(ItemData item, int amount = 1)
        {
            if (item == null || amount <= 0) return false;
            if (maxCarryWeight > 0f && TotalWeight() + item.weight * amount > maxCarryWeight)
                return false;

            int remaining = amount;
            // First pass: can existing stacks absorb some?
            foreach (var s in _slots)
            {
                if (s.item == item && !s.IsFull)
                    remaining -= Mathf.Min(remaining, item.maxStack - s.quantity);
                if (remaining <= 0) return true;
            }
            // Second pass: empty slots
            foreach (var s in _slots)
            {
                if (s.IsEmpty)
                {
                    remaining -= Mathf.Min(remaining, item.maxStack);
                    if (remaining <= 0) return true;
                }
            }
            return remaining <= 0;
        }

        /// <summary>Add items. Returns the number actually added.</summary>
        public int AddItem(ItemData item, int amount = 1)
        {
            if (item == null || amount <= 0) return 0;
            int added = 0;

            // Stack into existing slots first
            for (int i = 0; i < _slots.Count && added < amount; i++)
            {
                var s = _slots[i];
                if (s.item == item && !s.IsFull)
                {
                    int space = item.maxStack - s.quantity;
                    int add = Mathf.Min(space, amount - added);
                    s.quantity += add;
                    added += add;
                    OnSlotChanged?.Invoke(s);
                }
            }
            // Then fill empty slots
            for (int i = 0; i < _slots.Count && added < amount; i++)
            {
                var s = _slots[i];
                if (s.IsEmpty)
                {
                    s.item = item;
                    int add = Mathf.Min(item.maxStack, amount - added);
                    s.quantity = add;
                    added += add;
                    OnSlotChanged?.Invoke(s);
                }
            }

            if (added > 0)
            {
                OnInventoryChanged?.Invoke();
                EventManager.TriggerEvent(GameEvents.ItemCollected, new ItemEventPayload(item, added));
            }
            return added;
        }

        /// <summary>Remove up to <paramref name="amount"/> of <paramref name="item"/>. Returns amount removed.</summary>
        public int RemoveItem(ItemData item, int amount = 1)
        {
            if (item == null || amount <= 0) return 0;
            int removed = 0;

            for (int i = _slots.Count - 1; i >= 0 && removed < amount; i--)
            {
                var s = _slots[i];
                if (s.item != item) continue;
                int take = Mathf.Min(s.quantity, amount - removed);
                s.quantity -= take;
                removed += take;
                if (s.quantity <= 0) { s.item = null; s.quantity = 0; }
                OnSlotChanged?.Invoke(s);
            }

            if (removed > 0)
            {
                OnInventoryChanged?.Invoke();
            }
            return removed;
        }

        /// <summary>How many of <paramref name="item"/> does the player have?</summary>
        public int GetCount(ItemData item)
        {
            int count = 0;
            foreach (var s in _slots)
                if (s.item == item) count += s.quantity;
            return count;
        }

        /// <summary>Check if player has at least <paramref name="amount"/> of <paramref name="item"/>.</summary>
        public bool HasItem(string itemId, int amount = 1)
        {
            int count = 0;
            foreach (var s in _slots)
                if (s.item != null && s.item.itemId == itemId) count += s.quantity;
            return count >= amount;
        }

        /// <summary>Remove items by ID.</summary>
        public int RemoveItemById(string itemId, int amount = 1)
        {
            int removed = 0;
            for (int i = _slots.Count - 1; i >= 0 && removed < amount; i--)
            {
                var s = _slots[i];
                if (s.item == null || s.item.itemId != itemId) continue;
                int take = Mathf.Min(s.quantity, amount - removed);
                s.quantity -= take;
                removed += take;
                if (s.quantity <= 0) { s.item = null; s.quantity = 0; }
                OnSlotChanged?.Invoke(s);
            }
            if (removed > 0) OnInventoryChanged?.Invoke();
            return removed;
        }

        /// <summary>Clear the entire inventory.</summary>
        public void Clear()
        {
            foreach (var s in _slots) { s.item = null; s.quantity = 0; }
            OnInventoryChanged?.Invoke();
        }
    }

    /// <summary>Payload for item-related events.</summary>
    public class ItemEventPayload
    {
        public ItemData Item;
        public int Amount;
        public ItemEventPayload(ItemData item, int amount) { Item = item; Amount = amount; }
    }
}
