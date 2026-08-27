using System.Collections;
using UnityEngine;

namespace PrehistoricSurvival.Player
{
    /// <summary>
    /// Spawns footprint sprites behind the player as they walk.
    /// Footprints fade out and are destroyed after a set lifetime.
    /// </summary>
    public class FootprintSystem : MonoBehaviour
    {
        [Header("Footprint Settings")]
        [Tooltip("Footprint sprite prefab.")]
        public GameObject footprintPrefab;

        [Tooltip("Minimum distance between footprints (units).")]
        public float spawnDistance = 0.8f;

        [Tooltip("How long footprints persist before destruction (seconds).")]
        public float lifetime = 300f; // 5 minutes

        [Tooltip("Time to fade out footprint alpha.")]
        public float fadeDuration = 30f;

        [Tooltip("Maximum number of active footprints.")]
        public int maxFootprints = 100;

        [Header("Season Variants")]
        [Tooltip("Snow footprint prefab (winter).")]
        public GameObject snowFootprintPrefab;

        [Header("Conditions")]
        [Tooltip("Only spawn footprints when moving above this speed.")]
        public float minSpeed = 0.5f;

        private PlayerController _player;
        private Vector3 _lastSpawnPos;
        private bool _isLeftFoot;
        private bool _inSnow;
        private bool _inWater;

        private void Start()
        {
            _player = GetComponent<PlayerController>();
            _lastSpawnPos = transform.position;
        }

        private void Update()
        {
            if (_player == null || !_player.IsMoving) return;
            if (_inWater) return; // No footprints in water

            if (_player.CurrentSpeed < minSpeed) return;

            float dist = Vector3.Distance(transform.position, _lastSpawnPos);
            if (dist >= spawnDistance)
            {
                SpawnFootprint();
                _lastSpawnPos = transform.position;
            }
        }

        private void SpawnFootprint()
        {
            if (footprintPrefab == null) return;

            GameObject prefab = (_inSnow && snowFootprintPrefab != null)
                ? snowFootprintPrefab
                : footprintPrefab;

            // Position slightly behind and to the side
            Vector3 offset = transform.right * (_isLeftFoot ? -0.2f : 0.2f);
            Vector3 spawnPos = transform.position + offset;

            GameObject fp = Instantiate(prefab, spawnPos, Quaternion.identity);

            // Align footprint to player's facing direction
            float angle = Mathf.Atan2(_player.MoveDirection.y, _player.MoveDirection.x) * Mathf.Rad2Deg;
            fp.transform.rotation = Quaternion.Euler(0, 0, angle - 90f);

            // Set sorting order
            var sr = fp.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.sortingOrder = Mathf.RoundToInt(-spawnPos.y * 100) - 1;

            // Start decay coroutine
            StartCoroutine(DecayFootprint(fp));

            _isLeftFoot = !_isLeftFoot;
        }

        private IEnumerator DecayFootprint(GameObject fp)
        {
            var sr = fp.GetComponent<SpriteRenderer>();
            if (sr == null) yield break;

            Color c = sr.color;
            float elapsed = 0f;
            float fadeStart = lifetime - fadeDuration;

            // Wait until fade should begin
            while (elapsed < fadeStart)
            {
                elapsed += Time.deltaTime;
                yield return null;
                if (fp == null) yield break;
            }

            // Fade out
            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float t = (elapsed - fadeStart) / fadeDuration;
                c.a = 1f - t;
                if (sr != null) sr.color = c;
                yield return null;
                if (fp == null) yield break;
            }

            if (fp != null) Destroy(fp);
        }

        /// <summary>Called by SwimmingSystem to disable footprints in water.</summary>
        public void SetInWater(bool inWater) => _inWater = inWater;

        /// <summary>Called by SeasonManager to switch to snow footprints.</summary>
        public void SetSnowMode(bool snow) => _inSnow = snow;
    }
}
