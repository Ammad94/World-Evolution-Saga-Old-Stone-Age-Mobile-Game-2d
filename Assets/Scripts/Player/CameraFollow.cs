using UnityEngine;

namespace PrehistoricSurvival.Player
{
    /// <summary>
    /// 2.5D / 3D Perspective and Orthographic camera controller for hyper-realistic 2D sprite worlds.
    /// Provides the 2.5D diorama / 3D view demonstrated in:
    /// "How To... Billboarding in Unity - 2D Sprites in 3D" (https://www.youtube.com/watch?v=_LRZcmX_xw0).
    ///
    /// Key capabilities:
    /// - <see cref="ProjectionType.Perspective"/>: True 3D perspective depth with field-of-view,
    ///   depth foreshortening (distant trees/mountains shrink naturally), and smooth parallax.
    /// - <see cref="ProjectionType.Orthographic"/>: Clean 2.5D axonometric diorama view.
    /// - <see cref="CameraMode.GTAChase"/>: Camera hangs behind the player's back, pitched down
    ///   at the world, swinging smoothly as they turn and framing the character in the lower third.
    /// - <see cref="CameraMode.DioramaIsometric"/>: Fixed-angle 2.5D diorama view.
    /// - <see cref="CameraMode.TopDown2D"/>: Classic flat follow camera.
    /// - Right-mouse / mobile touch swipe camera orbiting.
    /// - Dynamic sprint zoom-out and smooth pinch/scroll zooming.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        public enum CameraMode
        {
            /// <summary>2.5D / 3D chase camera (pitched down, swings behind player, lower-third framing).</summary>
            GTAChase,
            /// <summary>Fixed-angle diorama view (isometric style).</summary>
            DioramaIsometric,
            /// <summary>Classic flat 2D follow camera (no tilt).</summary>
            TopDown2D,
            /// <summary>Free 360° orbiting 3D camera around the player.</summary>
            FreeOrbit3D
        }

        public enum ProjectionType
        {
            /// <summary>Perspective 3D camera (realistic depth foreshortening, as in the YouTube video).</summary>
            Perspective,
            /// <summary>Orthographic camera (axonometric diorama).</summary>
            Orthographic
        }

        public static CameraFollow Instance { get; private set; }

        /// <summary>Current camera yaw in degrees (compass convention: 0 = north, 90 = east).</summary>
        public static float CameraYawDeg => Instance != null ? Instance.YawDeg : 0f;

        /// <summary>True when any 2.5D / 3D pitched view is active.</summary>
        public static bool Chase3D => Instance != null && Instance.cameraMode != CameraMode.TopDown2D;

        [Header("Projection & Mode")]
        [Tooltip("Perspective (3D depth / FOV like the YouTube video) or Orthographic (flat diorama).")]
        public ProjectionType projectionType = ProjectionType.Perspective;

        [Tooltip("Camera behavior mode.")]
        public CameraMode cameraMode = CameraMode.GTAChase;

        [Header("Target")]
        [Tooltip("The transform to follow (auto-found by Player tag when empty).")]
        public Transform target;

        [Header("2.5D / 3D Diorama Settings")]
        [Tooltip("How steeply the camera looks down at the world. 45°-55° gives the classic diorama / 3D look.")]
        [Range(10f, 75f)] public float pitchAngle = 48f;

        [Tooltip("Distance behind the target along the view line (world units).")]
        public float chaseDistance = 10f;

        [Tooltip("Framing offset ahead of the target, placing the player in the lower third.")]
        public float framingBias = 2.2f;

        [Tooltip("Field of View in Perspective mode (degrees).")]
        [Range(25f, 90f)] public float fieldOfView = 50f;

        [Tooltip("Base size in Orthographic mode.")]
        public float baseOrthographicSize = 9f;

        [Header("Fixed Diorama Angle (DioramaIsometric Mode)")]
        [Tooltip("Fixed azimuth angle for isometric diorama (degrees, 0 = South view, 45 = SW view).")]
        public float fixedDioramaYaw = 270f;

        [Header("Smoothing & Tracking")]
        [Tooltip("Position smoothing speed (higher = snappier).")]
        public float positionSmoothSpeed = 6f;

        [Tooltip("How smoothly the camera swings around behind the player as they turn.")]
        public float anchorSmoothTime = 0.28f;

        [Tooltip("Speed (units/s) above which the camera starts tracking heading.")]
        public float headingFollowMinSpeed = 0.5f;

        [Header("Look Ahead")]
        public float lookAheadDistance = 2f;
        public float lookAheadSmooth = 4f;

        [Header("Zoom")]
        [Range(0.4f, 3f)] public float zoomLevel = 1f;
        public float minZoom = 0.5f;
        public float maxZoom = 2.5f;
        public bool allowPlayerZoom = true;
        [Tooltip("Pulls the camera back slightly while sprinting at full speed.")]
        [Range(0f, 0.5f)] public float speedZoomOut = 0.15f;

        [Header("Manual Orbit Control")]
        [Tooltip("Drag right half of screen (mobile) / hold right mouse (PC) to orbit the camera.")]
        public bool allowManualOrbit = true;
        [Tooltip("How fast manual orbit decays back behind the player while moving (0 = hold position).")]
        public float manualYawDecay = 2.5f;

        [Header("Flat 2D Fallback")]
        [Tooltip("Camera offset in TopDown2D mode.")]
        public Vector3 flatOffset = new Vector3(0f, 0f, -10f);

        /// <summary>Additive shake offset driven by GameFeel trauma (decays there).</summary>
        [HideInInspector] public Vector3 shakeOffset;

        /// <summary>View direction in compass degrees (0 = north, 90 = east).</summary>
        public float YawDeg { get; private set; }

        private Camera _camera;
        private PlayerController _playerController;
        private TouchRotationController _rotationInput;
        private Vector3 _velocity;
        private Vector3 _lookAheadOffset;

        // GTA chase orbit state
        private float _anchorAngle = 270f;       // camera azimuth angle around target (270 = South)
        private float _anchorAngleVelocity;
        private Vector2 _lastFacingDir = new Vector2(0f, -1f);
        private float _manualYaw;

        private void Awake()
        {
            Instance = this;
            _camera = GetComponent<Camera>();
            ApplyProjectionSettings();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            AcquireTarget();
        }

        /// <summary>Finds the player target automatically if unassigned.</summary>
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
                if (_rotationInput == null)
                    _rotationInput = target.GetComponent<TouchRotationController>();

                _anchorAngle = Mathf.Atan2(_lastFacingDir.y, _lastFacingDir.x) * Mathf.Rad2Deg + 180f;
                SnapToTarget();
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                AcquireTarget();
                if (target == null) return;
            }

            if (allowPlayerZoom) HandleZoomInput();

            ApplyProjectionSettings();

            Vector3 desiredPos;
            Quaternion desiredRot;

            switch (cameraMode)
            {
                case CameraMode.GTAChase:
                case CameraMode.FreeOrbit3D:
                    desiredPos = CalculateChasePosition(out desiredRot);
                    break;

                case CameraMode.DioramaIsometric:
                    desiredPos = CalculateIsometricPosition(out desiredRot);
                    break;

                default:
                    desiredPos = CalculateTopDownPosition(out desiredRot);
                    break;
            }

            // Smooth position dampening
            float smoothFactor = 1f / Mathf.Max(0.01f, positionSmoothSpeed);
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _velocity, smoothFactor);
            transform.rotation = desiredRot;

            // Update Yaw readout
            Vector3 fwd = transform.forward;
            YawDeg = (cameraMode != CameraMode.TopDown2D)
                ? (Mathf.Atan2(fwd.x, fwd.y) * Mathf.Rad2Deg)
                : 0f;

            // Apply trauma shake if present
            if (shakeOffset.sqrMagnitude > 0.00001f)
            {
                transform.position += shakeOffset;
            }
        }

        private void ApplyProjectionSettings()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera == null) return;

            bool isPerspective = (projectionType == ProjectionType.Perspective) && (cameraMode != CameraMode.TopDown2D);

            _camera.orthographic = !isPerspective;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 2000f;

            if (isPerspective)
            {
                float targetFOV = fieldOfView * zoomLevel;
                if (speedZoomOut > 0f && _playerController != null)
                {
                    float refSpeed = Mathf.Max(1f, _playerController.baseSpeed * 1.5f);
                    float factor = Mathf.Clamp01(_playerController.CurrentSpeed / refSpeed);
                    targetFOV *= (1f + speedZoomOut * factor);
                }
                _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFOV, 8f * Time.deltaTime);
            }
            else
            {
                float pitchRad = Mathf.Max(pitchAngle, 10f) * Mathf.Deg2Rad;
                float orthoScale = (cameraMode != CameraMode.TopDown2D) ? Mathf.Sin(pitchRad) : 1f;
                float targetSize = baseOrthographicSize * zoomLevel * Mathf.Max(0.35f, orthoScale);

                if (speedZoomOut > 0f && _playerController != null)
                {
                    float refSpeed = Mathf.Max(1f, _playerController.baseSpeed * 1.5f);
                    float factor = Mathf.Clamp01(_playerController.CurrentSpeed / refSpeed);
                    targetSize *= (1f + speedZoomOut * factor);
                }
                _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, targetSize, 8f * Time.deltaTime);
            }
        }

        /// <summary>
        /// Calculates the pitched chase camera position and rotation in 2.5D space.
        /// </summary>
        private Vector3 CalculateChasePosition(out Quaternion rotation)
        {
            bool moving = _playerController != null && _playerController.IsMoving;

            // Handle manual orbit swipe/drag
            if (_rotationInput != null && allowManualOrbit)
            {
                Vector2 delta = _rotationInput.RotationDelta;
                if (Mathf.Abs(delta.x) > 0.01f)
                {
                    _manualYaw += delta.x * 0.5f;
                }
            }

            if (moving && cameraMode != CameraMode.FreeOrbit3D)
            {
                _manualYaw = Mathf.MoveTowards(_manualYaw, 0f, manualYawDecay * Time.deltaTime);
            }

            // Track heading while moving
            if (moving && _playerController.MoveDirection.sqrMagnitude > 0.01f
                       && _playerController.CurrentSpeed >= headingFollowMinSpeed)
            {
                _lastFacingDir = _playerController.MoveDirection.normalized;
            }

            float facingYaw = Mathf.Atan2(_lastFacingDir.y, _lastFacingDir.x) * Mathf.Rad2Deg;
            float targetAnchor = (cameraMode == CameraMode.FreeOrbit3D)
                ? _manualYaw
                : (facingYaw + 180f + _manualYaw);

            _anchorAngle = Mathf.SmoothDampAngle(_anchorAngle, targetAnchor, ref _anchorAngleVelocity, anchorSmoothTime);

            return PositionFromAngle(_anchorAngle, out rotation);
        }

        /// <summary>Calculates a fixed-angle isometric diorama position.</summary>
        private Vector3 CalculateIsometricPosition(out Quaternion rotation)
        {
            return PositionFromAngle(fixedDioramaYaw, out rotation);
        }

        /// <summary>
        /// Computes camera position and rotation from an azimuth angle around the target,
        /// placing the camera above the XY ground plane (-Z) and pitching down.
        /// </summary>
        private Vector3 PositionFromAngle(float angleDeg, out Quaternion rotation)
        {
            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector2 backDir = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

            float pitchRad = Mathf.Clamp(pitchAngle, 10f, 80f) * Mathf.Deg2Rad;
            float effectiveDist = chaseDistance * zoomLevel;

            // Ground distance (horizontal) and elevation (-Z above ground plane)
            float groundDist = effectiveDist * Mathf.Cos(pitchRad);
            float height = effectiveDist * Mathf.Sin(pitchRad);

            Vector3 camPos = target.position
                           + new Vector3(backDir.x * groundDist, backDir.y * groundDist, -height);

            // Look-ahead bias
            bool moving = _playerController != null && _playerController.IsMoving;
            Vector3 moveDir = moving ? (Vector3)_playerController.MoveDirection : Vector3.zero;
            Vector3 lookAhead = moveDir * lookAheadDistance;
            _lookAheadOffset = Vector3.Lerp(_lookAheadOffset, lookAhead, lookAheadSmooth * Time.deltaTime);

            Vector3 finalCamPos = camPos + _lookAheadOffset;

            // Look target on ground ahead of the player (framing bias)
            Vector3 fwdGround = -new Vector3(backDir.x, backDir.y, 0f);
            Vector3 lookTarget = target.position + fwdGround * framingBias + _lookAheadOffset;

            Vector3 fwd = (lookTarget - finalCamPos).normalized;

            // Compute orthonormal screen up vector relative to ground plane (normal = Vector3.back)
            Vector3 right = Vector3.Cross(fwd, Vector3.back).normalized;
            Vector3 up = (right.sqrMagnitude > 0.001f)
                ? Vector3.Cross(right, fwd).normalized
                : Vector3.up;

            rotation = Quaternion.LookRotation(fwd, up);
            return finalCamPos;
        }

        /// <summary>Classic flat 2D top-down follow.</summary>
        private Vector3 CalculateTopDownPosition(out Quaternion rotation)
        {
            Vector3 desired = target.position + flatOffset;

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
            desired.z = flatOffset.z;
            rotation = Quaternion.identity;
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

        /// <summary>Adjust camera zoom in or out.</summary>
        public void AdjustZoom(float delta) => zoomLevel = Mathf.Clamp(zoomLevel + delta, minZoom, maxZoom);

        /// <summary>Instantly snaps the camera to the target without smoothing (on spawn/load/teleport).</summary>
        public void SnapToTarget()
        {
            if (target == null) return;

            Vector3 pos;
            Quaternion rot;

            if (cameraMode == CameraMode.TopDown2D)
                pos = CalculateTopDownPosition(out rot);
            else if (cameraMode == CameraMode.DioramaIsometric)
                pos = CalculateIsometricPosition(out rot);
            else
                pos = CalculateChasePosition(out rot);

            transform.position = pos;
            transform.rotation = rot;
            _velocity = Vector3.zero;
        }
    }
}
