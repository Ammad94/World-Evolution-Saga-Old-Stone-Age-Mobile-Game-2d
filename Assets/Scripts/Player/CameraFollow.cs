using UnityEngine;

namespace PrehistoricSurvival.Player
{
    /// <summary>
    /// GTA V-style 2.5D chase camera for a 2D sprite world ("2D, but looks 3D").
    ///
    /// Two modes:
    /// - <see cref="CameraMode.TopDown2D"/> – classic flat follow cam (north-up,
    ///   player centred, no tilt) — the original behaviour, kept as a fallback.
    /// - <see cref="CameraMode.GTAChase"/> (default) – the GTA V look in 2D:
    ///   • the camera hangs behind the player's back and swings around as he turns,
    ///     so the world pivots around the character (the GTA 1/2/Chinatown Wars
    ///     approach adapted to 2D sprites),
    ///   • the camera is pitched down (<see cref="pitchAngle"/>), turning the flat
    ///     2D world into a 3D diorama — the "fake 3D" 2.5D look,
    ///   • the player is framed in the lower third of the screen,
    ///   • drag the right half of the screen (right mouse on PC) to orbit the
    ///     camera like the GTA right stick; it eases back behind the player,
    ///   • the camera pulls back slightly at full speed.
    /// Sprites are billboarded by <see cref="BillboardSprite"/> so characters,
    /// animals and trees stand upright in the tilted view.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        public enum CameraMode
        {
            TopDown2D,
            GTAChase
        }

        public static CameraFollow Instance { get; private set; }

        /// <summary>Current camera yaw in degrees (compass convention: 0 = north, 90 = east). 0 in TopDown2D mode.</summary>
        public static float CameraYawDeg => Instance != null ? Instance.YawDeg : 0f;

        /// <summary>True when the GTA-style 2.5D chase view is active.</summary>
        public static bool Chase3D => Instance != null && Instance.cameraMode == CameraMode.GTAChase;

        [Header("Mode")]
        [Tooltip("GTAChase = GTA V-style 2.5D chase camera (pitched down, behind the player's back). TopDown2D = classic flat follow cam.")]
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

        [Header("GTA Chase (2.5D)")]
        [Tooltip("How steeply the camera looks down at the world. 50° gives the GTA diorama look; 0 = flat 2D chase.")]
        [Range(0f, 75f)] public float pitchAngle = 50f;
        [Tooltip("How far behind the player's back the camera hangs (world units).")]
        public float chaseDistance = 4.5f;
        [Tooltip("How quickly the camera swings around to sit behind the player when he turns.")]
        public float anchorSmoothTime = 0.25f;
        [Tooltip("Player speed (units/s) above which the camera starts tracking the heading.")]
        public float headingFollowMinSpeed = 0.5f;
        [Tooltip("How far ahead of the player the camera looks, framing him in the lower third like GTA V.")]
        public float framingBias = 2.5f;
        [Tooltip("How fast a manual camera orbit decays back to heading-follow while moving (0 = never).")]
        public float manualYawDecay = 2.5f;
        [Tooltip("Drag the right half of the screen (mobile) / hold right mouse (PC) to orbit the chase camera.")]
        public bool allowManualOrbit = true;
        [Tooltip("Pull the camera back slightly while the player runs at full speed, like GTA.")]
        [Range(0f, 0.5f)] public float speedZoomOut = 0.15f;

        /// <summary>Additive shake offset driven by GameFeel trauma (set externally).</summary>
        [HideInInspector] public Vector3 shakeOffset;

        /// <summary>The direction the camera looks at, in compass degrees (0 = north, 90 = east).</summary>
        public float YawDeg { get; private set; }

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
            // Take ownership immediately — during a scene transition the previous
            // scene's camera may still be dying, so self-destructing would leave
            // the fresh camera without a follow component.
            Instance = this;
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            transform.rotation = Quaternion.identity;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

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

            bool chase = cameraMode == CameraMode.GTAChase;
            float pitchRad = Mathf.Max(pitchAngle, 10f) * Mathf.Deg2Rad;

            // In the tilted view the vertical span of the world is orthoSize / sin(pitch),
            // so scale the ortho size down to keep the same visible world height.
            float ortho = baseOrthographicSize * zoomLevel * (chase ? Mathf.Sin(pitchRad) : 1f);
            if (chase && speedZoomOut > 0f && _playerController != null)
            {
                float refSpeed = Mathf.Max(1f, _playerController.baseSpeed * 1.5f);
                float speedFactor = Mathf.Clamp01(_playerController.CurrentSpeed / refSpeed);
                ortho *= 1f + speedZoomOut * speedFactor;
            }
            _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, ortho, 8f * Time.deltaTime);

            Vector3 desired;
            Quaternion desiredRotation = Quaternion.identity;
            if (chase)
                desired = ChaseDesiredPosition(out desiredRotation);
            else
                desired = TopDownDesiredPosition();

            transform.position = Vector3.SmoothDamp(
                transform.position, desired, ref _velocity, 1f / Mathf.Max(0.01f, positionSmoothSpeed));

            transform.rotation = desiredRotation;

            // Compass-style yaw of the view direction (0 = north, 90 = east).
            YawDeg = chase
                ? Mathf.Atan2(transform.forward.x, transform.forward.y) * Mathf.Rad2Deg
                : 0f;

            // Additive trauma shake (GameFeel sets shakeOffset; decays there).
            if (shakeOffset.sqrMagnitude > 0.000001f)
                transform.position += shakeOffset;
        }

        /// <summary>Classic flat fixed-north follow with look-ahead.</summary>
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
        /// GTA-style 2.5D chase: the camera hangs behind the player's back, pitched
        /// down at the world, swinging around as the player turns and looking past
        /// him so he sits in the lower third of the screen.
        /// </summary>
        private Vector3 ChaseDesiredPosition(out Quaternion rotation)
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

            // Pitched chase position: behind the back, raised to look down at the world.
            // A small minimum tilt keeps the ground visible even if pitchAngle is 0.
            float pitchRad = Mathf.Max(pitchAngle, 10f) * Mathf.Deg2Rad;
            float height = chaseDistance * Mathf.Tan(pitchRad);
            Vector3 camPos = target.position + (Vector3)backDir * chaseDistance + Vector3.up * height;

            // Look-ahead along the heading while moving.
            Vector3 lookDir = moving ? (Vector3)_lastFacingDir * lookAheadDistance : Vector3.zero;
            _lookAheadOffset = Vector3.Lerp(_lookAheadOffset, lookDir, lookAheadSmooth * Time.deltaTime);
            Vector3 desired = camPos + _lookAheadOffset;

            // Look past the player (a point ahead of him) so he sits in the lower third.
            Vector3 fwd = -new Vector3(backDir.x, backDir.y, 0f);
            Vector3 lookTarget = target.position + fwd * framingBias;
            rotation = Quaternion.LookRotation(lookTarget - camPos, Vector3.up);
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
                float pitchRad = Mathf.Max(pitchAngle, 10f) * Mathf.Deg2Rad;
                float height = chaseDistance * Mathf.Tan(pitchRad);
                Vector3 camPos = target.position + (Vector3)backDir * chaseDistance + Vector3.up * height;
                Vector3 fwd = -new Vector3(backDir.x, backDir.y, 0f);
                Vector3 lookTarget = target.position + fwd * framingBias;
                transform.position = camPos;
                transform.rotation = Quaternion.LookRotation(lookTarget - camPos, Vector3.up);
            }
            else
            {
                transform.position = target.position + offset;
                transform.rotation = Quaternion.identity;
            }
            _velocity = Vector3.zero;
        }
    }
}
