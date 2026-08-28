using UnityEngine;

namespace PrehistoricSurvival.Art
{
    /// <summary>
    /// Billboarding component for 2D sprites in a 3D / 2.5D perspective or diorama world,
    /// based on the technique demonstrated in "How To... Billboarding in Unity - 2D Sprites in 3D"
    /// (https://www.youtube.com/watch?v=_LRZcmX_xw0).
    ///
    /// Keeps 2D sprites standing upright and facing the camera so they never look like flat
    /// paper cutouts or disappear at steep angles.
    ///
    /// Modes:
    /// - <see cref="useStaticBillboard"/> = true (Static / Camera-Aligned):
    ///   Matches the camera's view orientation. Sprites stay perfectly parallel to the screen
    ///   and do NOT pivot or fade away as the player walks past them.
    /// - <see cref="useStaticBillboard"/> = false (Cylindrical LookAt):
    ///   Rotates around the vertical (Y) axis to look directly at the camera position, with
    ///   pitch (X) and roll (Z) locked so sprites don't flop backwards when looking up/down.
    /// - <see cref="BillboardMode.SphericalLookAt"/>:
    ///   Full 3D LookAt for floating icons, damage numbers, and particles.
    /// </summary>
    [ExecuteAlways]
    public class Billboard : MonoBehaviour
    {
        public enum BillboardMode
        {
            /// <summary>Static camera alignment: matches camera view plane without pivoting when moving close.</summary>
            StaticCameraAligned,
            /// <summary>Cylindrical LookAt: rotates around vertical axis to face camera position (X/Z locked).</summary>
            CylindricalLookAt,
            /// <summary>Spherical LookAt: faces camera on all axes (floating markers, particles, UI).</summary>
            SphericalLookAt,
            /// <summary>Diorama upright: stands perpendicular to ground plane matching camera pitch.</summary>
            DioramaTilt
        }

        [Header("Billboard Settings")]
        [Tooltip("When true, sprites stay parallel to the camera view plane and do not spin when walked past. When false, rotates to look at the camera's world position.")]
        public bool useStaticBillboard = true;

        [Tooltip("Specific billboard mode.")]
        public BillboardMode mode = BillboardMode.StaticCameraAligned;

        [Header("Axis Constraints")]
        [Tooltip("Lock X-axis (pitch) so the sprite does not flop backwards or forwards.")]
        public bool lockPitch = true;
        [Tooltip("Lock Z-axis (roll) so the sprite does not roll sideways.")]
        public bool lockRoll = true;
        [Tooltip("Lock Y-axis (yaw).")]
        public bool lockYaw = false;

        [Header("Offset & Tuning")]
        [Tooltip("Additional Euler rotation offset (degrees).")]
        public Vector3 rotationOffset = Vector3.zero;

        [Header("Camera Reference")]
        [Tooltip("Cached camera reference (auto-found from Camera.main if empty).")]
        public Camera theCam;

        private Transform _camTransform;
        private Quaternion _lastCamRotation;
        private Vector3 _lastCamPosition;
        private bool _hasLastState;

        private void Awake()
        {
            CacheCamera();
        }

        private void Start()
        {
            CacheCamera();
        }

        private void OnEnable()
        {
            CacheCamera();
        }

        /// <summary>Caches the camera reference efficiently to avoid calling Camera.main every frame.</summary>
        public void CacheCamera()
        {
            if (theCam == null)
            {
                theCam = Camera.main;
                if (theCam == null)
                {
                    var follow = PrehistoricSurvival.Player.CameraFollow.Instance;
                    if (follow != null) theCam = follow.GetComponent<Camera>();
                }
                if (theCam == null) theCam = FindFirstObjectByType<Camera>();
            }

            if (theCam != null)
                _camTransform = theCam.transform;
        }

        private void LateUpdate()
        {
            if (_camTransform == null)
            {
                CacheCamera();
                if (_camTransform == null) return;
            }

            // Sync boolean toggle with enum mode for 1:1 tutorial compatibility
            BillboardMode activeMode = mode;
            if (useStaticBillboard && activeMode == BillboardMode.CylindricalLookAt)
                activeMode = BillboardMode.StaticCameraAligned;
            else if (!useStaticBillboard && activeMode == BillboardMode.StaticCameraAligned)
                activeMode = BillboardMode.CylindricalLookAt;

            switch (activeMode)
            {
                case BillboardMode.StaticCameraAligned:
                    ApplyStaticBillboard();
                    break;

                case BillboardMode.CylindricalLookAt:
                    ApplyCylindricalLookAt();
                    break;

                case BillboardMode.SphericalLookAt:
                    ApplySphericalLookAt();
                    break;

                case BillboardMode.DioramaTilt:
                    ApplyDioramaTilt();
                    break;
            }

            // Inherit parent wind sway (Z oscillation) if attached as a child visual
            float parentSwayZ = 0f;
            if (transform.parent != null)
            {
                parentSwayZ = transform.parent.localEulerAngles.z;
                if (parentSwayZ > 180f) parentSwayZ -= 360f;
            }

            // Apply wind sway & rotation offset in camera screen-space
            if (Mathf.Abs(parentSwayZ) > 0.01f || rotationOffset.sqrMagnitude > 0.001f)
            {
                transform.rotation = transform.rotation * Quaternion.Euler(rotationOffset.x, rotationOffset.y, parentSwayZ + rotationOffset.z);
            }
        }

        /// <summary>
        /// Static Billboard (gamesplusjames method 2):
        /// Matches the camera's orientation so sprites stay flat to the camera view plane.
        /// When walking past objects, they don't pivot or twist.
        /// </summary>
        private void ApplyStaticBillboard()
        {
            // For a 2D quad/sprite in 2.5D space:
            // The camera rotation determines the sprite orientation
            Quaternion camRot = _camTransform.rotation;

            if (lockPitch || lockRoll)
            {
                Vector3 euler = camRot.eulerAngles;
                float x = lockPitch ? 0f : euler.x;
                float y = lockYaw ? 0f : euler.y;
                float z = lockRoll ? 0f : euler.z;
                transform.rotation = Quaternion.Euler(x, y, z);
            }
            else
            {
                transform.rotation = camRot;
            }
        }

        /// <summary>
        /// Cylindrical LookAt Billboard (gamesplusjames method 1):
        /// Rotates the sprite around the vertical (Y) axis to look towards the camera's position,
        /// but locks X and Z rotation to 0 so it stays upright on the ground.
        /// </summary>
        private void ApplyCylindricalLookAt()
        {
            Vector3 toCam = _camTransform.position - transform.position;
            if (toCam.sqrMagnitude < 0.0001f) return;

            // In Unity 3D coordinates: ground is XZ, up is Y
            // In Unity 2D coordinates: ground is XY, up is Y/Z
            // Use LookAt towards camera transform
            transform.LookAt(_camTransform.position, Vector3.up);

            // Constrain rotation: keep only the Y-axis rotation (upright standing)
            Vector3 euler = transform.rotation.eulerAngles;
            float x = lockPitch ? 0f : euler.x;
            float y = lockYaw ? 0f : euler.y;
            float z = lockRoll ? 0f : euler.z;

            transform.rotation = Quaternion.Euler(x, y, z);
        }

        /// <summary>
        /// Spherical LookAt Billboard:
        /// Completely faces the camera on all 3 axes.
        /// </summary>
        private void ApplySphericalLookAt()
        {
            transform.LookAt(
                transform.position + _camTransform.rotation * Vector3.forward,
                _camTransform.rotation * Vector3.up
            );
        }

        /// <summary>
        /// Diorama Tilt:
        /// Specifically tilts the sprite perpendicular to the 2.5D pitched ground plane.
        /// </summary>
        private void ApplyDioramaTilt()
        {
            // Pitch sprite backward by camera pitch angle so it appears fully upright to the camera
            float camPitch = _camTransform.eulerAngles.x;
            float camYaw = _camTransform.eulerAngles.y;

            transform.rotation = Quaternion.Euler(camPitch, camYaw, 0f);
        }
    }
}
