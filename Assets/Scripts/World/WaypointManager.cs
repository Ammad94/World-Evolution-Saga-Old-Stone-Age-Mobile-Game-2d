using System.Collections.Generic;
using UnityEngine;
using PrehistoricSurvival.Core;

namespace PrehistoricSurvival.World
{
    /// <summary>
    /// Manages map waypoints/pins placed by the player.
    /// Supports adding, removing, and querying waypoints.
    /// </summary>
    public class WaypointManager : MonoBehaviour
    {
        public static WaypointManager Instance { get; private set; }

        [Header("Settings")]
        [Tooltip("Waypoint marker prefab (spawned in world).")]
        public GameObject waypointPrefab;

        [Tooltip("Maximum number of waypoints.")]
        public int maxWaypoints = 20;

        [Header("Active Waypoint")]
        [Tooltip("The currently active waypoint the compass points to.")]
        public Waypoint ActiveWaypoint { get; private set; }

        private List<Waypoint> _waypoints = new List<Waypoint>();
        public IReadOnlyList<Waypoint> Waypoints => _waypoints;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>Place a waypoint at a world position.</summary>
        public Waypoint AddWaypoint(Vector3 worldPos, string name = "Waypoint")
        {
            if (_waypoints.Count >= maxWaypoints)
            {
                Debug.LogWarning("[WaypointManager] Max waypoints reached.");
                return null;
            }

            GameObject obj = null;
            if (waypointPrefab != null)
                obj = Instantiate(waypointPrefab, worldPos, Quaternion.identity);

            var wp = new Waypoint
            {
                name = name,
                position = worldPos,
                worldObject = obj
            };

            _waypoints.Add(wp);
            SetActiveWaypoint(wp);
            EventManager.TriggerEvent(GameEvents.WaypointSet, wp);
            return wp;
        }

        /// <summary>Remove a waypoint.</summary>
        public void RemoveWaypoint(Waypoint wp)
        {
            if (wp == null) return;
            _waypoints.Remove(wp);
            if (wp.worldObject != null) Destroy(wp.worldObject);

            if (ActiveWaypoint == wp)
            {
                ActiveWaypoint = _waypoints.Count > 0 ? _waypoints[_waypoints.Count - 1] : null;
                EventManager.TriggerEvent(GameEvents.WaypointCleared);
            }
        }

        /// <summary>Set the active waypoint for the compass.</summary>
        public void SetActiveWaypoint(Waypoint wp)
        {
            ActiveWaypoint = wp;
            EventManager.TriggerEvent(GameEvents.WaypointSet, wp);
        }

        /// <summary>Clear all waypoints.</summary>
        public void ClearAll()
        {
            foreach (var wp in _waypoints)
                if (wp.worldObject != null) Destroy(wp.worldObject);
            _waypoints.Clear();
            ActiveWaypoint = null;
            EventManager.TriggerEvent(GameEvents.WaypointCleared);
        }

        /// <summary>Get distance from a position to the active waypoint.</summary>
        public float DistanceToActive(Vector3 fromPos)
        {
            if (ActiveWaypoint == null) return -1f;
            return Vector3.Distance(fromPos, ActiveWaypoint.position);
        }

        /// <summary>Get direction from a position to the active waypoint.</summary>
        public Vector3 DirectionToActive(Vector3 fromPos)
        {
            if (ActiveWaypoint == null) return Vector3.zero;
            return (ActiveWaypoint.position - fromPos).normalized;
        }
    }

    /// <summary>Represents a single waypoint.</summary>
    [System.Serializable]
    public class Waypoint
    {
        public string name;
        public Vector3 position;
        [System.NonSerialized]
        public GameObject worldObject;
    }
}
