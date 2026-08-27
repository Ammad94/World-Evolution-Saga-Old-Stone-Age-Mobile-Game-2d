using UnityEngine;
using PrehistoricSurvival.Core;

namespace PrehistoricSurvival.Environment
{
    /// <summary>
    /// Handles mining/digging interaction. Player approaches a destructible tile
    /// or object, holds the interact button, and after a timer the resource is harvested.
    /// </summary>
    public class MiningSystem : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Maximum distance to mine from player.")]
        public float mineRange = 2f;

        [Tooltip("Default time to mine a tile if not specified by the target.")]
        public float defaultMineTime = 2f;

        [Tooltip("Currently equipped tool name (e.g., 'pickaxe', 'shovel').")]
        public string equippedTool;

        [Header("UI")]
        [Tooltip("Progress bar UI (0..1 fill).")]
        public UnityEngine.UI.Image progressBar;

        [Header("Audio")]
        public AudioClip miningSound;
        public AudioClip mineCompleteSound;

        private AudioSource _audio;
        private DestructibleTilemap _currentTarget;
        private Vector3Int _targetCell;
        private float _mineTimer;
        private float _mineDuration;
        private bool _isMining;
        private Transform _player;

        private void Start()
        {
            _audio = GetComponent<AudioSource>();
            if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
            _player = transform;

            if (progressBar != null)
                progressBar.fillAmount = 0f;
        }

        private void Update()
        {
            // Start mining on mouse click or touch (simplified – real impl uses input system)
            if (Input.GetMouseButtonDown(0))
                TryStartMining();

            if (Input.GetMouseButton(0) && _isMining)
                ContinueMining();

            if (Input.GetMouseButtonUp(0))
                StopMining();
        }

        // ------------------------------------------------------------------
        // Mining Logic
        // ------------------------------------------------------------------
        private void TryStartMining()
        {
            // Get world position from mouse/touch
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            worldPos.z = 0f;

            // Check distance
            if (Vector3.Distance(_player.position, worldPos) > mineRange) return;

            // Find destructible tilemap at position
            var allDestructible = FindObjectsOfType<DestructibleTilemap>();
            foreach (var dt in allDestructible)
            {
                Vector3Int cell = dt.tilemap.WorldToCell(worldPos);
                if (dt.HasTile(cell))
                {
                    _currentTarget = dt;
                    _targetCell = cell;
                    _mineDuration = dt.destroyTime > 0 ? dt.destroyTime : defaultMineTime;
                    _mineTimer = 0f;
                    _isMining = true;

                    if (miningSound != null) _audio.PlayOneShot(miningSound);
                    return;
                }
            }
        }

        private void ContinueMining()
        {
            if (_currentTarget == null) { StopMining(); return; }

            // Check still in range
            Vector3 worldPos = _currentTarget.tilemap.GetCellCenterWorld(_targetCell);
            if (Vector3.Distance(_player.position, worldPos) > mineRange * 1.5f)
            {
                StopMining();
                return;
            }

            _mineTimer += Time.deltaTime;
            float progress = _mineTimer / _mineDuration;

            if (progressBar != null)
                progressBar.fillAmount = progress;

            if (_mineTimer >= _mineDuration)
            {
                // Mine complete
                _currentTarget.DestroyTile(_targetCell, equippedTool);
                StopMining();

                if (mineCompleteSound != null) _audio.PlayOneShot(mineCompleteSound);
            }
        }

        private void StopMining()
        {
            _isMining = false;
            _currentTarget = null;
            _mineTimer = 0f;
            if (progressBar != null)
                progressBar.fillAmount = 0f;
        }

        /// <summary>Set the currently equipped tool.</summary>
        public void EquipTool(string toolName)
        {
            equippedTool = toolName;
        }
    }
}
