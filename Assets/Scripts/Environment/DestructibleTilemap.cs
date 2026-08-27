using UnityEngine;
using UnityEngine.Tilemaps;
using PrehistoricSurvival.Core;

namespace PrehistoricSurvival.Environment
{
    /// <summary>
    /// Makes a Tilemap destructible – tiles can be removed by mining/digging.
    /// Tracks which tiles have been destroyed and optionally drops resources.
    /// </summary>
    [RequireComponent(typeof(Tilemap))]
    public class DestructibleTilemap : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("The Tilemap component to modify.")]
        public Tilemap tilemap;

        [Tooltip("Tool required to destroy tiles (null = any tool works).")]
        public string requiredTool;

        [Tooltip("Time (seconds) to destroy one tile.")]
        public float destroyTime = 1f;

        [Header("Drops")]
        [Tooltip("Item dropped when a tile is destroyed (optional).")]
        public ItemData dropItem;
        [Tooltip("Chance (0..1) that a tile drops an item.")]
        [Range(0f, 1f)]
        public float dropChance = 0.5f;

        [Tooltip("Particle effect to play when a tile is destroyed.")]
        public GameObject destroyEffect;

        private void Awake()
        {
            if (tilemap == null) tilemap = GetComponent<Tilemap>();
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>
        /// Attempt to destroy the tile at the given grid position.
        /// Returns true if a tile was present and destroyed.
        /// </summary>
        public bool DestroyTile(Vector3Int gridPos, string toolUsed = null)
        {
            // Check if tool is valid
            if (!string.IsNullOrEmpty(requiredTool) && toolUsed != requiredTool)
            {
                Debug.Log($"[DestructibleTilemap] Tool '{toolUsed}' cannot break this tile. Need '{requiredTool}'.");
                return false;
            }

            // Check if tile exists
            TileBase tile = tilemap.GetTile(gridPos);
            if (tile == null) return false;

            // Remove the tile
            tilemap.SetTile(gridPos, null);

            // Spawn particle effect
            if (destroyEffect != null)
            {
                Vector3 worldPos = tilemap.GetCellCenterWorld(gridPos);
                var fx = Instantiate(destroyEffect, worldPos, Quaternion.identity);
                Destroy(fx, 2f);
            }

            // Drop item
            if (dropItem != null && Random.value <= dropChance)
            {
                if (InventorySystem.Instance != null)
                    InventorySystem.Instance.AddItem(dropItem, 1);
            }

            // Fire event
            EventManager.TriggerEvent(GameEvents.TileDestroyed, new TileDestroyedPayload(gridPos, tile));
            return true;
        }

        /// <summary>Restore a tile at the given position.</summary>
        public void RestoreTile(Vector3Int gridPos, TileBase tile)
        {
            tilemap.SetTile(gridPos, tile);
        }

        /// <summary>Check if a tile exists at the given grid position.</summary>
        public bool HasTile(Vector3Int gridPos)
        {
            return tilemap.GetTile(gridPos) != null;
        }
    }

    public class TileDestroyedPayload
    {
        public Vector3Int GridPos;
        public TileBase Tile;
        public TileDestroyedPayload(Vector3Int pos, TileBase tile) { GridPos = pos; Tile = tile; }
    }
}
