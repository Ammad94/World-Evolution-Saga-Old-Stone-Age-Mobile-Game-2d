using UnityEngine;

namespace PrehistoricSurvival.Player
{
    /// <summary>
    /// Smooth orthographic 2D follow camera with look-ahead and pinch/scroll zoom.
    ///
    /// Two modes:
    /// - <see cref="CameraMode.TopDown2D"/> – classic fixed-north follow camera
    ///   (the original behaviour: player locked to screen centre).
    /// - <see cref="CameraMode.GTAChase"/> – GTA-style chase camera. The camera
    ///   hangs behind the player's back and swings around to the new backside as
    ///   the player turns, framing the character in the lower third of the screen
    ///   like GTA V. The view stays north-up so the 8-directional sprites and the
    ///   world map stay readable — this is the 2D equivalent of the GTA V chase
    ///   cam (same approach as GTA 1/2/Chinatown Wars). Drag the right half of the
    ///   screen (right mouse button on PC) to orbit the camera like the GTA right
    ///   stick; it eases back behind the player when they move again.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        public enum CameraMode
        {
            TopDown2D,
            GTAChase
        }

        [Header("Mode")]
        [Tooltip("TopDown2D = player locked to screen centre, north-up. GTAChase = GTA-style chase camera behind the player's back.")]
        public CameraMode cameraMode = CameraMode.GTAChase;

        [Header("Target")]
        [Tooltip("The transform to follow (auto-found by the Player tag when empty).")]
        public Transform target;

        [Header("Offset")]
        [Tooltip("Camera offset from the target (TopDown2D mode). Z must stay negative for 2D.")]
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

        [Header("GTA Chase")]
        [Tooltip("How far behind the player's back the camera hangs (world units).")]
        public float chaseDistance = 3.5f;
        [Tooltip("How quickly the camera swings around to sit behind the player when he turns.")]
        public float anchorSmoothTime = 0.25f;
        [Tooltip("Player speed (units/s) above which the camera starts tracking the heading.")]
        public float headingFollowMinSpeed = 0.5f;
        [Tooltip("How far below screen centre the player is framed (GTA V keeps the character in the lower third).")]
        public float framingBias = 2.5f;
        [Tooltip("How fast a manual camera orbit decays back to heading-follow while moving (0 = never).")]
        public float manualYawDecay = 2.5f;
        [Tooltip("Drag the right half of the screen (mobile) / hold right mouse (PC) to orbit the chase camera.")]
        public bool allowManualOrbit = true;
        [Tooltip("Pull the camera back slightly while the player runs at full speed, like GTA.")]
        [Range(0f, 0.5f)] public float speedZoomOut = 0.15f;

        /// <summary>Additive shake offset driven by GameFeel trauma (set externally).</summary>
        [HideInInspector] public Vector3 shakeOffset;

        private Vector3 _velocity;
        private Vector3 _lookAheadOffset;
        private PlayerController _playerController;
        private Camera _camera;
        private TouchRotationController _rotationInput;

        // GTA chase state.
        private float _anchorAngle = 90f;         // camera orbit angle around the player (degrees)
        private float _anchorAngleVelocity;        // SmoothDampAngle velocity
        private Vector2 _lastFacingDir = new Vector2(0f, -1f); // spawn faces south
        private float _manualYaw;                  // player-controlled orbit offset (degrees)

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
                _rotationInput = GetComponent<TouchRotationController>();
                _anchorAngle = Mathf.Atan2(_lastFacingDir.y, _lastFacingDir.x) * Mathf.Rad2Deg + 180f;
                transform.position = target.position + offset;
                transform.rotation = Quaternion.identity;
            }
        }

        private void LateUpdate()
        {
            if (target == null) { AcquireTarget(); return; }

            if (allowPlayerZoom) HandleZoomInput();
            _camera.orthographicSize = Mathf.Lerp(
                _camera.orthographicSize, DesiredOrthographicSize(), 8f * Time.deltaTime);

            Vector3 desired = cameraMode == CameraMode.GTAChase
                ? ChaseDesiredPosition()
                : TopDownDesiredPosition();

            transform.position = Vector3.SmoothDamp(
                transform.position, desired, ref _velocity, 1f / Mathf.Max(0.01f, positionSmoothSpeed));

            // The view always stays north-up in both modes: the 8-directional
            // sprites, tilemap and world map are all authored for a fixed view.
            transform.rotation = Quaternion.identity;

            // Additive trauma shake (GameFeel sets shakeOffset; decays there).
            if (shakeOffset.sqrMagnitude > 0.000001f)
                transform.position += shakeOffset;
        }

        private float DesiredOrthographicSize()
        {
            float size = baseOrthographicSize * zoomLevel;

            // GTA-style: pull back a little while sprinting.
            if (cameraMode == CameraMode.GTAChase && speedZoomOut > 0f && _playerController != null)
            {
                float refSpeed = Mathf.Max(1f, _playerController.baseSpeed * 1.5f);
                float speedFactor = Mathf.Clamp01(_playerController.CurrentSpeed / refSpeed);
                size *= 1f + speedZoomOut * speedFactor;
            }
            return size;
        }

        /// <summary>Classic fixed-north follow with look-ahead.</summary>
        private Vector3 TopDownDesiredPosition()
        {
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
            return desired;
        }

        /// <summary>
        /// GTA-style chase position: the camera hangs behind the player's back and
        /// swings around as the player turns, framing him in the lower third.
        /// </summary>
        private Vector3 ChaseDesiredPosition()
        {
            bool moving = _playerController != null && _playerController.IsMoving;

            // Manual orbit (GTA right-stick equivalent): right-half drag on mobile,
            // right mouse button on PC. Eases back behind the player while moving.
            if (_rotationInput != null && allowManualOrbit)
            {
                Vector2 delta = _rotationInput.RotationDelta;
                if (Mathf.Abs(delta.x) > 0.01f) _manualYaw += delta.x * 0.5f;
            }
            if (moving)
                _manualYaw = Mathf.MoveTowards(_manualYaw, 0f, manualYawDecay * Time.deltaTime);

            // Track the heading while the player is actually moving.
            if (moving && _playerController.MoveDirection.sqrMagnitude > 0.01f
                      && _playerController.CurrentSpeed >= headingFollowMinSpeed)
            {
                _lastFacingDir = _playerController.MoveDirection.normalized;
            }

            // The camera orbits around the player to sit behind his back.
            float facingYaw = Mathf.Atan2(_lastFacingDir.y, _lastFacingDir.x) * Mathf.Rad2Deg;
            float targetAnchor = facingYaw + 180f + _manualYaw;
            _anchorAngle = Mathf.SmoothDampAngle(_anchorAngle, targetAnchor, ref _anchorAngleVelocity, anchorSmoothTime);

            Vector2 backDir = new Vector2(
                Mathf.Cos(_anchorAngle * Mathf.Deg2Rad),
                Mathf.Sin(_anchorAngle * Mathf.Deg2Rad));

            Vector3 desired = target.position + (Vector3)backDir * chaseDistance;
            desired += new Vector3(0f, framingBias, 0f); // lower-third framing, GTA V style
            desired.z = offset.z;
            return desired;
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

            if (cameraMode == CameraMode.GTAChase)
            {
                Vector2 backDir = new Vector2(
                    Mathf.Cos(_anchorAngle * Mathf.Deg2Rad),
                    Mathf.Sin(_anchorAngle * Mathf.Deg2Rad));
                transform.position = target.position + (Vector3)backDir * chaseDistance
                                   + new Vector3(0f, framingBias, offset.z);
            }
            else
            {
                transform.position = target.position + offset;
            }
            transform.rotation = Quaternion.identity;
            _velocity = Vector3.zero;
        }
    }
}
