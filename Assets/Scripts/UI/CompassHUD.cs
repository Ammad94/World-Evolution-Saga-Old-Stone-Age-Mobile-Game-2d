using UnityEngine;
using TMPro;

namespace PrehistoricSurvival.UI
{
    /// <summary>
    /// HUD compass that rotates an arrow toward the active waypoint
    /// and displays the distance in meters.
    /// </summary>
    public class CompassHUD : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Arrow image that rotates to point at waypoint.")]
        public RectTransform arrow;

        [Tooltip("Text showing distance to waypoint.")]
        public TextMeshProUGUI distanceText;

        [Tooltip("Container that shows/hides based on waypoint presence.")]
        public GameObject compassContainer;

        [Header("Settings")]
        [Tooltip("Smoothing speed for arrow rotation.")]
        public float rotationSmoothSpeed = 8f;

        [Tooltip("Distance unit label.")]
        public string unitLabel = "m";

        private World.WaypointManager _waypointMgr;
        private Transform _player;

        private void Start()
        {
            _waypointMgr = World.WaypointManager.Instance;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _player = player.transform;
        }

        private void Update()
        {
            if (_waypointMgr == null || _player == null) return;

            var active = _waypointMgr.ActiveWaypoint;
            if (active == null)
            {
                if (compassContainer != null) compassContainer.SetActive(false);
                return;
            }

            if (compassContainer != null) compassContainer.SetActive(true);

            // Calculate angle from player to waypoint
            Vector3 dir = active.position - _player.position;
            float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

            // Smoothly rotate arrow
            if (arrow != null)
            {
                Quaternion targetRot = Quaternion.Euler(0, 0, -angle);
                arrow.localRotation = Quaternion.Slerp(
                    arrow.localRotation,
                    targetRot,
                    rotationSmoothSpeed * Time.deltaTime
                );
            }

            // Update distance text
            if (distanceText != null)
            {
                float dist = dir.magnitude;
                if (dist >= 1000f)
                    distanceText.text = $"{dist / 1000f:F1} km";
                else
                    distanceText.text = $"{dist:F0} {unitLabel}";
            }
        }
    }
}
