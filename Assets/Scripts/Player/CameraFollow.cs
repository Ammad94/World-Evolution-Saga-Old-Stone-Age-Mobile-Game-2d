using UnityEngine;

namespace PrehistoricSurvival.Player
{
    /// <summary>
    /// Smooth orthographic 2D follow camera with look-ahead and pinch/scroll zoom.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The transform to follow (auto-found by the Player tag when empty).")]
        public Transform target;

        [Header("Offset")]
        [Tooltip("Camera offset from the target. Z must stay negative for 2D.")]
        public Vector3 offset = new Vector3(0f, 0f, -10f);

        [Header("Smoothing")]
        [Tooltip("Position smoothing speed (higher = snappier).")]
        public float positionSmoothSpeed = 6f;

        [Header("Look Ahead")]
        public float lookAheadDistance = 2f;
        public float lookAheadSmooth = 4f;

        [Header("Zoom")]
        [Tooltip("Orthographic size at zoom 1.")]
        public float baseOrthographicSize = 9f;
        [Range(0.4f, 3f)] public float zoomLevel = 1f;
        public float minZoom = 0.5f;
        public float maxZoom = 2.5f;
        [Tooltip("Allow mouse-wheel / pinch zoom.")]
        public bool allowPlayerZoom = true;

        /// <summary>Additive shake offset driven by GameFeel trauma (set externally).</summary>
        [HideInInspector] public Vector3 shakeOffset;

        private Vector3 _velocity;
        private Vector3 _lookAheadOffset;
        private PlayerController _playerController;
        private Camera _camera;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            transform.rotation = Quaternion.identity;
        }

        private void Start() => AcquireTarget();

        /// <summary>Find the player if no target has been assigned.</summary>
        public void AcquireTarget()
        {
            if (target == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) target = player.transform;
            }
            if (target != null)
            {
                _playerController = target.GetComponent<PlayerController>();
                transform.position = target.position + offset;
            }
        }

        private void LateUpdate()
        {
            if (target == null) { AcquireTarget(); return; }

            if (allowPlayerZoom) HandleZoomInput();
            _camera.orthographicSize = Mathf.Lerp(
                _camera.orthographicSize, baseOrthographicSize * zoomLevel, 8f * Time.deltaTime);

            Vector3 desired = target.position + offset;

            if (_playerController != null && _playerController.IsMoving)
            {
                Vector3 lookDir = (Vector3)_playerController.MoveDirection * lookAheadDistance;
                _lookAheadOffset = Vector3.Lerp(_lookAheadOffset, lookDir, lookAheadSmooth * Time.deltaTime);
            }
            else
            {
                _lookAheadOffset = Vector3.Lerp(_lookAheadOffset, Vector3.zero, lookAheadSmooth * Time.deltaTime);
            }

            desired += new Vector3(_lookAheadOffset.x, _lookAheadOffset.y, 0f);
            desired.z = offset.z;

            transform.position = Vector3.SmoothDamp(
                transform.position, desired, ref _velocity, 1f / Mathf.Max(0.01f, positionSmoothSpeed));

            // Additive trauma shake (GameFeel sets shakeOffset; decays there).
            if (shakeOffset.sqrMagnitude > 0.000001f)
                transform.position += shakeOffset;
        }

        private void HandleZoomInput()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f) AdjustZoom(-scroll * 0.12f);

            if (Input.touchCount == 2)
            {
                Touch a = Input.GetTouch(0);
                Touch b = Input.GetTouch(1);
                float prev = ((a.position - a.deltaPosition) - (b.position - b.deltaPosition)).magnitude;
                float current = (a.position - b.position).magnitude;
                AdjustZoom((prev - current) * 0.002f);
            }
        }

        /// <summary>Zoom in / out.</summary>
        public void AdjustZoom(float delta) => zoomLevel = Mathf.Clamp(zoomLevel + delta, minZoom, maxZoom);

        /// <summary>Snap instantly to the target (after loading or teleporting).</summary>
        public void SnapToTarget()
        {
            if (target == null) return;
            transform.position = target.position + offset;
            _velocity = Vector3.zero;
        }
    }
}
