using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
#endif

/// <summary>
/// GTA V-style third-person camera with VERY SMOOTH orbit.
///
/// - HOLD RIGHT MOUSE + drag to orbit (yaw + optional pitch), or one-finger
///   drag on touch, or right gamepad stick.
/// - Input is low-pass filtered (no jitter) and the orbit has INERTIA, so it
///   glides to a stop after you release instead of snapping.
/// - Zoom (scroll / pinch) is SmoothDamp-ed — no more staircase zoom.
/// - Position follow uses SmoothDamp for a filmic glide.
///
/// Designed to run BEFORE BillboardCharacter.LateUpdate so the character
/// billboard uses the final camera pose of the frame (see DefaultExecutionOrder).
/// </summary>
[DefaultExecutionOrder(-10)]
public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Framing")]
    [Tooltip("How far behind the player the camera sits.")]
    public float distance = 7f;

    [Tooltip("Default look-down angle. ~12 = GTA V feel.")]
    public float pitch = 12f;

    [Tooltip("Horizontal orbit angle around the player (degrees). 0 = directly behind.")]
    public float yaw = 0f;

    [Tooltip("How high up on the player the camera looks (1.5 = chest).")]
    public float lookHeight = 1.5f;

    [Header("Follow feel")]
    public float smoothSpeed = 8f;

    [Tooltip("Snap to the player on start instead of flying in from nowhere.")]
    public bool snapOnStart = true;

    [Header("Orbit feel")]
    [Tooltip("Allow right-mouse-drag / touch / gamepad orbiting.")]
    public bool allowOrbit = true;

    [Tooltip("Orbit speed — degrees per pixel of mouse drag.")]
    public float mouseSensitivity = 0.25f;

    [Tooltip("Higher = orbit tracks input faster; lower = floatier.")]
    public float orbitSmoothing = 14f;

    [Tooltip("Glide after releasing the input. Higher = stops faster. ~3.5 = GTA-like glide.")]
    public float inertiaDamping = 3.5f;

    [Header("Vertical orbit (pitch)")]
    [Tooltip("Allow looking down/up while orbiting (mouse Y, touch, right stick).")]
    public bool allowPitchOrbit = true;

    [Tooltip("Degrees per pixel of vertical drag.")]
    public float pitchSensitivity = 0.18f;

    public float minPitch = 2f;
    public float maxPitch = 45f;

    [Header("Gamepad")]
    public bool gamepadOrbit = true;
    public float gamepadOrbitSpeed = 140f;   // deg/sec at full stick

    [Header("Touch (mobile)")]
    [Tooltip("One-finger drag orbits the camera.")]
    public bool touchOrbit = true;
    [Tooltip("Two-finger pinch zooms.")]
    public bool pinchZoom = true;

    [Header("Auto follow")]
    [Tooltip("Camera slowly swings behind the player while they move.")]
    public bool autoFollowFacing = false;

    [Header("Zoom limits")]
    public float minDistance = 3f;
    public float maxDistance = 30f;
    public float zoomStep = 1.5f;

    [Tooltip("Zoom glide time (SmoothDamp).")]
    public float zoomSmoothTime = 0.25f;

    // internal state
    float orbitX;            // yaw around the player
    float orbitY;            // pitch
    float yawVel;            // deg/sec
    float pitchVel;          // deg/sec
    float currentDistance;   // smoothed
    float desiredDistance;   // target of the smoothing
    float zoomVel;
    Vector3 posVel;

    void Awake()
    {
        orbitX = yaw;
        orbitY = Mathf.Clamp(pitch, minPitch, maxPitch);
        desiredDistance = currentDistance = Mathf.Clamp(distance, minDistance, maxDistance);

        Camera cam = GetComponent<Camera>();
        if (cam != null && cam.orthographic)
        {
            cam.orthographic = false;
            Debug.Log("ThirdPersonCamera: switched the camera to Perspective.", this);
        }

        if (snapOnStart && target != null)
        {
            transform.position = target.position + Quaternion.Euler(orbitY, orbitX, 0f) * new Vector3(0f, 0f, -currentDistance);
            Vector3 lookPoint = target.position + Vector3.up * lookHeight;
            transform.rotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        }
    }

    struct OrbitInput
    {
        public float yawDeg;      // raw yaw delta this frame (degrees)
        public float pitchDeg;    // raw pitch delta this frame (degrees)
        public float pinchDelta;  // positive = fingers moving apart
        public bool active;       // any orbit input held this frame
    }

    void LateUpdate()
    {
        if (target == null) return;
        float dt = Mathf.Max(Time.deltaTime, 1e-5f);

        // ---------------- gather orbit input ----------------
        OrbitInput oi = ReadInput(dt);

        // ---------------- yaw: filtered velocity + inertia ----------------
        float targetYawVel = oi.active ? oi.yawDeg / dt : 0f;
        yawVel = Mathf.Lerp(yawVel, targetYawVel, 1f - Mathf.Exp(-orbitSmoothing * dt));
        if (!oi.active)
            yawVel = Mathf.Lerp(yawVel, 0f, 1f - Mathf.Exp(-inertiaDamping * dt));
        orbitX += yawVel * dt;

        // ---------------- pitch ----------------
        if (allowPitchOrbit)
        {
            float targetPitchVel = oi.active ? oi.pitchDeg / dt : 0f;
            pitchVel = Mathf.Lerp(pitchVel, targetPitchVel, 1f - Mathf.Exp(-orbitSmoothing * dt));
            if (!oi.active)
                pitchVel = Mathf.Lerp(pitchVel, 0f, 1f - Mathf.Exp(-inertiaDamping * dt));
            orbitY = Mathf.Clamp(orbitY + pitchVel * dt, minPitch, maxPitch);
        }
        else
        {
            orbitY = Mathf.Clamp(pitch, minPitch, maxPitch);
            pitchVel = 0f;
        }

        // ---------------- optional: swing behind the player ----------------
        if (autoFollowFacing && !oi.active)
        {
            BillboardCharacter bc = target.GetComponent<BillboardCharacter>();
            PlayerController3D pc = target.GetComponent<PlayerController3D>();
            Vector3 facing = bc != null ? bc.Facing : (pc != null ? pc.Facing : Vector3.zero);
            if (facing.sqrMagnitude > 0.001f)
            {
                float faceAngle = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
                orbitX = Mathf.LerpAngle(orbitX, faceAngle, 3f * dt);
            }
        }

        // ---------------- zoom: scroll / pinch, smoothed ----------------
        desiredDistance = Mathf.Clamp(desiredDistance - ScrollDelta() * zoomStep - oi.pinchDelta, minDistance, maxDistance);
        currentDistance = Mathf.SmoothDamp(currentDistance, desiredDistance, ref zoomVel, zoomSmoothTime);

        // ---------------- position: SmoothDamp glide ----------------
        Quaternion rot = Quaternion.Euler(orbitY, orbitX, 0f);
        Vector3 desiredPos = target.position + rot * new Vector3(0f, 0f, -currentDistance);
        float glide = 1f - Mathf.Exp(-smoothSpeed * dt);
        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref posVel, 1f / Mathf.Max(smoothSpeed, 0.1f), Mathf.Infinity, dt);

        // ---------------- look at the player's chest ----------------
        Vector3 lookPoint = target.position + Vector3.up * lookHeight;
        Quaternion lookRot = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, glide);
    }

    // ------------------------------------------------------------------ input
    OrbitInput ReadInput(float dt)
    {
        OrbitInput oi = default;

#if ENABLE_INPUT_SYSTEM
        // ---- mouse (hold right button) ----
        Mouse mo = Mouse.current;
        bool rmb = mo != null && mo.rightButton.isPressed;
        if (allowOrbit && rmb)
        {
            oi.active = true;
            oi.yawDeg += mo.delta.x.ReadValue() * mouseSensitivity;
            oi.pitchDeg -= mo.delta.y.ReadValue() * pitchSensitivity;
        }

        // ---- gamepad right stick ----
        if (allowOrbit && gamepadOrbit && Gamepad.current != null)
        {
            Vector2 st = Gamepad.current.rightStick.ReadValue();
            if (st.sqrMagnitude > 0.02f)
            {
                oi.active = true;
                oi.yawDeg += st.x * gamepadOrbitSpeed * dt;
                oi.pitchDeg -= st.y * gamepadOrbitSpeed * 0.6f * dt;
            }
        }

        // ---- touch: 1 finger orbit, 2 finger pinch ----
        if (allowOrbit || pinchZoom)
        {
            Touchscreen ts = Touchscreen.current;
            if (ts != null)
            {
                Vector2 d0 = Vector2.zero, p0 = Vector2.zero, p1 = Vector2.zero;
                int activeCount = 0;
                var touches = ts.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    var ph = touches[i].phase.ReadValue();
                    if (ph == TouchPhase.Began || ph == TouchPhase.Moved || ph == TouchPhase.Stationary)
                    {
                        if (activeCount == 0) { d0 = touches[i].delta.ReadValue(); p0 = touches[i].position.ReadValue(); activeCount++; }
                        else if (activeCount == 1) { p1 = touches[i].position.ReadValue(); activeCount++; }
                        else activeCount++;
                    }
                }

                if (activeCount == 1 && allowOrbit && touchOrbit)
                {
                    oi.active = true;
                    oi.yawDeg += d0.x * mouseSensitivity;
                    oi.pitchDeg -= d0.y * pitchSensitivity;
                }
                else if (activeCount >= 2 && pinchZoom)
                {
                    float distNow = Vector2.Distance(p0, p1);
                    float distPrev = Vector2.Distance(p0 - d0, p1);
                    oi.pinchDelta += (distNow - distPrev) * 0.02f; // pinch out = zoom in
                }
            }
        }
#else
        // ---- legacy input ----
        if (allowOrbit && Input.GetMouseButton(1))
        {
            oi.active = true;
            oi.yawDeg += Input.GetAxis("Mouse X") * mouseSensitivity;
            oi.pitchDeg -= Input.GetAxis("Mouse Y") * pitchSensitivity;
        }
        if (allowOrbit && touchOrbit && Input.touchCount == 1)
        {
            Touch t = Input.GetTouch(0);
            oi.active = true;
            oi.yawDeg += t.deltaPosition.x * mouseSensitivity;
            oi.pitchDeg -= t.deltaPosition.y * pitchSensitivity;
        }
        if (pinchZoom && Input.touchCount == 2)
        {
            Touch t0 = Input.GetTouch(0), t1 = Input.GetTouch(1);
            float d = Vector2.Distance(t0.position, t1.position);
            float dp = Vector2.Distance(t0.position - t0.deltaPosition, t1.position - t1.deltaPosition);
            oi.pinchDelta += (d - dp) * 0.02f;
        }
#endif
        return oi;
    }

    static float ScrollDelta()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse m = Mouse.current;
        return m != null ? m.scroll.ReadValue().y * 0.01f : 0f;
#else
        return Input.GetAxis("Mouse ScrollWheel");
#endif
    }
}
