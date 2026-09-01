using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// GTA V-style third-person camera with VERY SMOOTH orbit.
/// - HOLD RIGHT MOUSE + drag to orbit HORIZONTALLY (yaw only; pitch is fixed).
/// - Mouse input is low-pass filtered (no jitter) and the orbit has INERTIA,
///   so it glides to a stop after you release the button instead of snapping.
/// - Scroll wheel to zoom.
/// </summary>
public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Drag the Player here.")]
    public Transform target;

    [Header("Framing")]
    [Tooltip("How far behind the player the camera sits.")]
    public float distance = 7f;

    [Tooltip("Fixed look-down angle. 0 = level with player, 90 = straight down. ~12 = GTA V feel.")]
    public float pitch = 12f;

    [Tooltip("Horizontal orbit angle around the player (degrees). 0 = directly behind.")]
    public float yaw = 0f;

    [Tooltip("How high up on the player the camera looks (1.5 = their chest).")]
    public float lookHeight = 1.5f;

    [Header("Follow feel")]
    public float smoothSpeed = 8f;

    [Header("Orbit feel")]
    [Tooltip("Allow right-mouse-drag orbiting (horizontal only).")]
    public bool allowOrbit = true;

    [Tooltip("Orbit speed — degrees per pixel of mouse drag.")]
    public float mouseSensitivity = 0.25f;

    [Tooltip("Higher = orbit tracks the mouse faster; lower = floatier.")]
    public float orbitSmoothing = 12f;

    [Tooltip("Glide after releasing the mouse. Higher = stops faster. ~4 = nice GTA-like glide.")]
    public float inertiaDamping = 4f;

    [Tooltip("Camera slowly swings behind the player while they move.")]
    public bool autoFollowFacing = false;

    [Header("Zoom limits")]
    public float minDistance = 3f;
    public float maxDistance = 30f;
    public float zoomStep = 1.5f;

    private float orbitX;     // yaw  (around player)
    private float orbitY;     // pitch (look-down angle) — FIXED
    private float orbitVel;   // angular velocity (deg/sec) for inertia

    void Awake()
    {
        orbitX = yaw;
        orbitY = pitch;

        Camera cam = GetComponent<Camera>();
        if (cam != null && cam.orthographic)
        {
            cam.orthographic = false;
            Debug.Log("ThirdPersonCamera: switched the camera to Perspective.", this);
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // ---- smooth horizontal orbit with inertia ----
        bool dragging = allowOrbit && RightMouseHeld();
        float rawDelta = dragging ? MouseDeltaX() * mouseSensitivity : 0f;

        // instantaneous angular velocity from the mouse (deg/sec)
        float instantVel = rawDelta / Mathf.Max(Time.deltaTime, 1e-4f);

        // low-pass filter the velocity (removes hand jitter)
        orbitVel = Mathf.Lerp(orbitVel, instantVel, 1f - Mathf.Exp(-orbitSmoothing * Time.deltaTime));

        // when the mouse is released, decay the velocity -> glides to a stop
        if (!dragging)
            orbitVel = Mathf.Lerp(orbitVel, 0f, 1f - Mathf.Exp(-inertiaDamping * Time.deltaTime));

        orbitX += orbitVel * Time.deltaTime;
        orbitY = pitch; // pitch stays locked (no vertical orbit)

        // ---- optional: swing behind the player while moving ----
        if (autoFollowFacing)
        {
            PlayerController3D pc = target.GetComponent<PlayerController3D>();
            if (pc != null && pc.Facing.sqrMagnitude > 0.001f)
            {
                float faceAngle = Mathf.Atan2(pc.Facing.x, pc.Facing.z) * Mathf.Rad2Deg;
                orbitX = Mathf.LerpAngle(orbitX, faceAngle, 3f * Time.deltaTime);
            }
        }

        // ---- zoom: scroll wheel ----
        distance = Mathf.Clamp(distance - ScrollDelta() * zoomStep, minDistance, maxDistance);

        // ---- position: behind the target at (pitch, yaw) ----
        Quaternion rot = Quaternion.Euler(orbitY, orbitX, 0f);
        Vector3 desiredPos = target.position + rot * new Vector3(0f, 0f, -distance);

        // smooth, frame-rate independent follow
        float t = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPos, t);

        // ---- look at the player's chest ----
        Vector3 lookPoint = target.position + Vector3.up * lookHeight;
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(lookPoint - transform.position, Vector3.up), t);
    }

    // ---------- input helpers (new Input System vs legacy) ----------
    static bool RightMouseHeld()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse m = Mouse.current;
        return m != null && m.rightButton.isPressed;
#else
        return Input.GetMouseButton(1);
#endif
    }

    static float MouseDeltaX()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse m = Mouse.current;
        return m != null ? m.delta.x.ReadValue() : 0f;
#else
        return Input.GetAxis("Mouse X");
#endif
    }

    static float ScrollDelta()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse m = Mouse.current;
        return m != null ? m.scroll.ReadValue().y : 0f;
#else
        return Input.GetAxis("Mouse ScrollWheel");
#endif
    }
}
