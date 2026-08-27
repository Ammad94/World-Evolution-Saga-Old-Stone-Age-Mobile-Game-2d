using UnityEngine;

namespace PrehistoricSurvival.Player
{
    /// <summary>
    /// GTA V-style smooth follow camera with isometric height offset.
    /// Maintains a fixed offset and lerps toward the target each frame.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The transform to follow (usually the player).")]
        public Transform target;

        [Header("Offset")]
        [Tooltip("Camera offset from target in local space.")]
        public Vector3 offset = new Vector3(0f, 8f, -10f);

        [Header("Smoothing")]
        [Tooltip("Position smoothing speed (higher = snappier).")]
        public float positionSmoothSpeed = 5f;
        [Tooltip("Rotation smoothing speed.")]
        public float rotationSmoothSpeed = 3f;

        [Header("Look Ahead")]
        [Tooltip("How far ahead the camera looks based on player velocity.")]
        public float lookAheadDistance = 2f;
        [Tooltip("Smoothing for look-ahead.")]
        public float lookAheadSmooth = 4f;

        [Header("Bounds")]
        [Tooltip("Minimum camera height (prevent going underground).")]
        public float minHeight = 2f;

        [Header("Zoom")]
        [Tooltip("Current zoom level (distance multiplier).")]
        [Range(0.5f, 2f)]
        public float zoomLevel = 1f;

        private Vector3 _velocity;
        private Vector3 _lookAheadOffset;
        private PlayerController _playerController;

        private void Start()
        {
            if (target == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) target = player.transform;
            }
            if (target != null)
                _playerController = target.GetComponent<PlayerController>();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // Calculate desired position
            Vector3 desiredPos = target.position + offset * zoomLevel;

            // Look-ahead based on player movement direction
            if (_playerController != null && _playerController.IsMoving)
            {
                Vector3 lookDir = (Vector3)_playerController.MoveDirection * lookAheadDistance;
                _lookAheadOffset = Vector3.Lerp(_lookAheadOffset, lookDir, lookAheadSmooth * Time.deltaTime);
            }
            else
            {
                _lookAheadOffset = Vector3.Lerp(_lookAheadOffset, Vector3.zero, lookAheadSmooth * Time.deltaTime);
            }

            desiredPos += _lookAheadOffset;

            // Enforce minimum height
            if (desiredPos.y < minHeight)
                desiredPos.y = minHeight;

            // Smooth position
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPos,
                ref _velocity,
                1f / positionSmoothSpeed
            );

            // Smooth rotation – always look at target
            Quaternion targetRot = Quaternion.LookRotation(
                target.position - transform.position + Vector3.up * 2f
            );
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                rotationSmoothSpeed * Time.deltaTime
            );
        }

        /// <summary>Zoom in/out by adjusting the zoom level.</summary>
        public void AdjustZoom(float delta)
        {
            zoomLevel = Mathf.Clamp(zoomLevel + delta, 0.5f, 2f);
        }
    }
}
