using System.Collections.Generic;
using UnityEngine;
using PrehistoricSurvival.Core;

namespace PrehistoricSurvival.AI
{
    /// <summary>Lightweight herd coordinator: nearby animals share a leader target and migrate between seasonal waypoints.</summary>
    public class HerdMigrationSystem : MonoBehaviour
    {
        public static HerdMigrationSystem Instance { get; private set; }
        public float herdRadius = 9f, migrationInterval = 180f;
        public Transform[] seasonalWaypoints;
        private float _timer;
        private int _waypoint;
        private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }
        private void Update() { if (GameManager.Instance != null && GameManager.Instance.IsPaused) return; _timer += Time.deltaTime; if (_timer >= migrationInterval) { _timer = 0f; _waypoint = seasonalWaypoints == null || seasonalWaypoints.Length == 0 ? 0 : (_waypoint + 1) % seasonalWaypoints.Length; } }
        public Vector3 MigrationTarget(Vector3 fallback) { if (seasonalWaypoints != null && seasonalWaypoints.Length > 0 && seasonalWaypoints[_waypoint] != null) return seasonalWaypoints[_waypoint].position; return fallback; }
        public List<AnimalAI> HerdAround(Vector3 center) { var result = new List<AnimalAI>(); foreach (var animal in FindObjectsByType<AnimalAI>(FindObjectsSortMode.None)) if (Vector3.Distance(center, animal.transform.position) <= herdRadius) result.Add(animal); return result; }
    }
}
