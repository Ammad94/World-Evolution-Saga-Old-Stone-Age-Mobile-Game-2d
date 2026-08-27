using UnityEngine;
using PrehistoricSurvival.Core;

namespace PrehistoricSurvival.Environment
{
    /// <summary>
    /// Allows the player to dig soil tiles to harvest wild roots.
    /// Requires a shovel or digging tool.
    /// </summary>
    public class RootDigging : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Items that can be found by digging.")]
        public ItemData[] possibleRoots;

        [Tooltip("Time to dig one spot (seconds).")]
        public float digTime = 2f;

        [Tooltip("Required tool to dig.")]
        public string requiredTool = "shovel";

        [Header("Visual")]
        public GameObject digEffectPrefab;

        [Header("Audio")]
        public AudioClip digSound;

        private AudioSource _audio;
        private float _digTimer;
        private bool _isDigging;
        private Vector3 _digPosition;

        private void Start()
        {
            _audio = GetComponent<AudioSource>();
            if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.E))
                TryStartDigging();

            if (_isDigging)
            {
                _digTimer += Time.deltaTime;
                if (_digTimer >= digTime)
                {
                    CompleteDig();
                }
            }
        }

        private void TryStartDigging()
        {
            // Get position in front of player
            Vector3 digPos = transform.position + transform.forward * 1f;
            _digPosition = digPos;
            _digTimer = 0f;
            _isDigging = true;

            if (digSound != null) _audio.PlayOneShot(digSound);
        }

        private void CompleteDig()
        {
            _isDigging = false;

            // Spawn visual effect
            if (digEffectPrefab != null)
            {
                var fx = Instantiate(digEffectPrefab, _digPosition, Quaternion.identity);
                Destroy(fx, 2f);
            }

            // Random root drop
            if (possibleRoots != null && possibleRoots.Length > 0)
            {
                int index = Random.Range(0, possibleRoots.Length);
                ItemData root = possibleRoots[index];
                if (root != null && InventorySystem.Instance != null)
                {
                    InventorySystem.Instance.AddItem(root, 1);
                    Debug.Log($"[RootDigging] Found: {root.displayName}");
                }
            }
        }
    }
}
