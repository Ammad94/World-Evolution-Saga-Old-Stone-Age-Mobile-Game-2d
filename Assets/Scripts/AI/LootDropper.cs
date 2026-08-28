using UnityEngine;
using PrehistoricSurvival.Core;

namespace PrehistoricSurvival.AI
{
    /// <summary>
    /// Drops loot items when an animal dies.
    /// Attached to animal prefabs alongside AnimalAI.
    /// </summary>
    public class LootDropper : MonoBehaviour
    {
        [System.Serializable]
        public class LootEntry
        {
            public ItemData item;
            [Tooltip("Min quantity to drop.")]
            public int minAmount = 1;
            [Tooltip("Max quantity to drop.")]
            public int maxAmount = 3;
            [Tooltip("Chance to drop (0-1).")]
            [Range(0f, 1f)]
            public float dropChance = 1f;
        }

        [Header("Loot Table")]
        public LootEntry[] lootTable;

        [Header("Settings")]
        [Tooltip("Scatter radius for dropped items.")]
        public float scatterRadius = 1f;

        [Tooltip("World item prefab template (spawned for each drop).")]
        public GameObject worldItemPrefab;

        /// <summary>Roll the loot table and spawn/drop items.</summary>
        public void DropLoot()
        {
            if (lootTable == null || lootTable.Length == 0) return;

            foreach (var entry in lootTable)
            {
                if (entry.item == null) continue;
                if (Random.value > entry.dropChance) continue;

                int amount = Random.Range(entry.minAmount, entry.maxAmount + 1);

                // Add directly to inventory
                if (InventorySystem.Instance != null)
                {
                    int added = InventorySystem.Instance.AddItem(entry.item, amount);
                    if (added > 0)
                        Debug.Log($"[LootDropper] Dropped {added}x {entry.item.displayName}");
                }

                // Also spawn a visual pickup in the world
                if (worldItemPrefab != null)
                {
                    Vector3 pos = transform.position + Random.insideUnitSphere * scatterRadius;
                    pos.y = 0f;
                    var obj = Instantiate(worldItemPrefab, pos, Quaternion.identity);
                    PrehistoricSurvival.Player.Fake3D.Ensure(obj);
                    var pickup = obj.GetComponent<WorldItemPickup>();
                    if (pickup != null)
                        pickup.SetItem(entry.item, 1);
                }
            }
        }
    }

    /// <summary>
    /// A pickupable item in the world. Player walks over it to collect.
    /// </summary>
    public class WorldItemPickup : MonoBehaviour
    {
        private ItemData _item;
        private int _amount;

        [Header("Settings")]
        public float pickupRange = 1.5f;
        public float bobSpeed = 2f;
        public float bobHeight = 0.2f;

        private Vector3 _startPos;
        private SpriteRenderer _sr;

        private void Start()
        {
            _startPos = transform.position;
            _sr = GetComponent<SpriteRenderer>();
            PrehistoricSurvival.Player.Fake3D.Ensure(gameObject);
        }

        public void SetItem(ItemData item, int amount)
        {
            _item = item;
            _amount = amount;
            if (_sr != null && item != null && item.icon != null)
                _sr.sprite = item.icon;
        }

        private void Update()
        {
            // Bob animation
            float y = _startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(_startPos.x, y, 0f);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (_item == null) return;

            if (InventorySystem.Instance != null)
            {
                int added = InventorySystem.Instance.AddItem(_item, _amount);
                if (added > 0)
                {
                    PrehistoricSurvival.Art.FX.Spawn("spark", transform.position, 0.6f, 16f,
                        new Color(1f, 0.95f, 0.7f, 0.9f));
                    Debug.Log($"[Pickup] Collected {added}x {_item.displayName}");
                    Destroy(gameObject);
                }
            }
        }
    }
}
