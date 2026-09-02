using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
#endif

/// <summary>
/// GTA V-style third-person camera - REFERENCE MATCH EDITION
/// Matches: https://i.ytimg.com/vi/oYlsmbxTVM4/maxresdefault.jpg
/// 
/// That reference is a classic GTA V third-person view:
/// - Camera behind & slightly above player, low pitch (~8-12 deg)
/// - Character slightly off-center (over-the-shoulder, shoulderOffset)
/// - Character occupies ~60-70% of screen height, grounded, horizon near top
/// - Same framing while idle, walking, running - no dramatic changes
/// - Smooth player follow with inertia, auto-recenter behind player
///
/// This version adds:
/// - Shoulder offset for true GTA over-the-shoulder look
/// - GTA reference preset (ApplyReferencePreset button in inspector via Reset)
/// - Idle camera: subtle breathing bob + slow orbit after long idle (like GTA V idle cam)
/// - Player follow: smooth damp + auto follow facing when moving
/// - Collision: raycast prevents camera going through ground/walls
/// - Mobile: touch orbit + pinch zoom, gamepad support
/// - FOV control matching GTA V (48 deg default)
///
/// Designed to run BEFORE BillboardCharacter.LateUpdate
/// </summary>
[DefaultExecutionOrder(-10)]
public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target - drag Player here")]
    public Transform target;

    [Header("=== GTA V REFERENCE FRAMING ===")]
    [Tooltip("Matches reference screenshot - distance behind player. 5.5 = GTA close view like ref image")]
    public float distance = 5.5f;

    [Tooltip("Low look-down angle like GTA V reference. 8-12 = eye-level behind view (ref ~9 deg). Horizon near top of frame.")]
    [Range(2f, 45f)] public float pitch = 9f;

    [Tooltip("Horizontal orbit angle. 0 = directly behind. -12 = slight right shoulder (GTA default)")]
    public float yaw = -6f;

    [Tooltip("How high on player to look - 1.1 = chest, like reference")]
    public float lookHeight = 1.1f;

    [Tooltip("Over-the-shoulder offset - GTA V has character slightly left of center. 0.55 = right shoulder view like ref")]
    public float shoulderOffset = 0.55f;

    [Tooltip("Vertical offset for framing - positive = character lower in frame (more sky)")]
    public float verticalOffset = 0.15f;

    [Tooltip("FOV matching GTA V - 48 = close cinematic like reference, 60 = wider")]
    [Range(20f, 90f)] public float fieldOfView = 48f;

    [Header("Follow Feel - Player Follow Camera")]
    [Tooltip("Position follow smoothness. Higher = snappier. 6 = filmic GTA glide")]
    public float smoothSpeed = 6f;
    public bool snapOnStart = true;
    public bool useSmoothDamp = true;

    [Header("Orbit - Right Mouse Drag")]
    public bool allowOrbit = true;
    [Tooltip("Degrees per pixel of drag. 0.25 = GTA-like")]
    public float mouseSensitivity = 0.25f;
    public float pitchSensitivity = 0.18f;
    public float orbitSmoothing = 14f;
    [Tooltip("Glide after release. 3.5 = GTA-like inertia")]
    public float inertiaDamping = 3.5f;

    [Header("Vertical Orbit (Pitch)")]
    [Tooltip("If OFF, pitch stays locked to reference angle (recommended for GTA ref). If ON, you can look up/down")]
    public bool allowPitchOrbit = false;
    public float minPitch = 2f;
    public float maxPitch = 35f;

    [Header("Auto Follow - GTA Style")]
    [Tooltip("Camera slowly swings behind player while moving - very GTA")]
    public bool autoFollowFacing = true;
    [Tooltip("Delay before auto-follow starts after orbit input")]
    public float autoFollowDelay = 0.8f;
    [Tooltip("How fast camera swings behind player")]
    public float autoFollowSpeed = 2.2f;

    [Header("Idle Camera - Same view while idle")]
    [Tooltip("Subtle breathing bob while idle, like GTA V idle cam")]
    public bool idleBobEnabled = true;
    [Range(0f, 0.5f)] public float idleBobAmount = 0.12f;
    public float idleBobSpeed = 0.35f;
    [Tooltip("After idleDelay seconds, camera slowly orbits (cinematic idle)")]
    public bool idleSlowOrbit = true;
    public float idleDelay = 3f;
    [Tooltip("Degrees per second when idle orbiting")]
    public float idleOrbitSpeed = 2f;

    [Header("Zoom")]
    public float minDistance = 3f;
    public float maxDistance = 12f;
    public float zoomStep = 1.5f;
    public float zoomSmoothTime = 0.25f;

    [Header("Collision")]
    public bool enableCollision = true;
    public LayerMask collisionMask = -1;
    public float collisionRadius = 0.3f;
    public float collisionPadding = 0.4f;

    [Header("Gamepad / Touch")]
    public bool gamepadOrbit = true;
    public float gamepadOrbitSpeed = 140f;
    public bool touchOrbit = true;
    public bool pinchZoom = true;

    // internal
    float orbitX;
    float orbitY;
    float yawVel;
    float pitchVel;
    float currentDistance;
    float desiredDistance;
    float zoomVel;
    Vector3 posVel;
    float currentFOV;
    float lastOrbitInputTime;
    float idleBobPhase;

    void OnValidate()
    {
        distance = Mathf.Clamp(distance, minDistance, maxDistance);
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        fieldOfView = Mathf.Clamp(fieldOfView, 20f, 90f);
    }

    void Reset()
    {
        ApplyGTAReferencePreset();
    }

    [ContextMenu("Apply GTA V Reference Preset (from screenshot)")]
    public void ApplyGTAReferencePreset()
    {
        // Exact match to https://i.ytimg.com/vi/oYlsmbxTVM4/maxresdefault.jpg
        distance = 5.5f;
        pitch = 9f;
        yaw = -6f;
        lookHeight = 1.1f;
        shoulderOffset = 0.55f;
        verticalOffset = 0.15f;
        fieldOfView = 48f;
        smoothSpeed = 6f;
        allowPitchOrbit = false;
        autoFollowFacing = true;
        autoFollowDelay = 0.8f;
        autoFollowSpeed = 2.2f;
        idleBobEnabled = true;
        idleBobAmount = 0.12f;
        idleBobSpeed = 0.35f;
        idleDelay = 3f;
        idleSlowOrbit = true;
        idleOrbitSpeed = 2f;
        minDistance = 3f;
        maxDistance = 12f;
        mouseSensitivity = 0.25f;
        pitchSensitivity = 0.18f;
        orbitSmoothing = 14f;
        inertiaDamping = 3.5f;
        enableCollision = true;
    }

    void Awake()
    {
        orbitX = yaw;
        orbitY = Mathf.Clamp(pitch, minPitch, maxPitch);
        desiredDistance = currentDistance = Mathf.Clamp(distance, minDistance, maxDistance);
        currentFOV = fieldOfView;

        Camera cam = GetComponent<Camera>();
        if (cam != null)
        {
            if (cam.orthographic)
            {
                cam.orthographic = false;
                Debug.Log("ThirdPersonCamera: switched to Perspective for GTA view (was Orthographic).", this);
            }
            cam.fieldOfView = fieldOfView;
        }

        if (snapOnStart && target != null)
        {
            Quaternion rot = Quaternion.Euler(orbitY, orbitX, 0f);
            Vector3 lookPoint = GetLookPoint();
            Vector3 offset = rot * new Vector3(shoulderOffset, verticalOffset, -currentDistance);
            transform.position = lookPoint + offset;
            transform.rotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        }

        lastOrbitInputTime = Time.time;
    }

    struct OrbitInput
    {
        public float yawDeg;
        public float pitchDeg;
        public float pinchDelta;
        public bool active;
    }

    void LateUpdate()
    {
        if (target == null) return;
        float dt = Mathf.Max(Time.deltaTime, 1e-5f);

        Camera cam = GetComponent<Camera>();
        if (cam != null && !Mathf.Approximately(cam.fieldOfView, fieldOfView))
        {
            currentFOV = Mathf.Lerp(currentFOV, fieldOfView, 1f - Mathf.Exp(-5f * dt));
            cam.fieldOfView = currentFOV;
        }

        bool isMoving = IsTargetMoving();
        OrbitInput oi = ReadInput(dt);

        if (oi.active) lastOrbitInputTime = Time.time;

        // --- yaw with smoothing + inertia (GTA glide) ---
        float targetYawVel = oi.active ? oi.yawDeg / dt : 0f;
        yawVel = Mathf.Lerp(yawVel, targetYawVel, 1f - Mathf.Exp(-orbitSmoothing * dt));
        if (!oi.active)
            yawVel = Mathf.Lerp(yawVel, 0f, 1f - Mathf.Exp(-inertiaDamping * dt));
        orbitX += yawVel * dt;

        // --- pitch ---
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
            // Locked pitch like reference - but allow tiny idle bob
            float bob = 0f;
            if (idleBobEnabled && !isMoving && !oi.active)
            {
                idleBobPhase += dt * idleBobSpeed;
                bob = Mathf.Sin(idleBobPhase) * idleBobAmount;
            }
            orbitY = Mathf.Clamp(pitch + bob, minPitch, maxPitch);
            pitchVel = 0f;
        }

        // --- auto follow facing (GTA: camera swings behind while moving) ---
        float timeSinceOrbit = Time.time - lastOrbitInputTime;
        if (autoFollowFacing && !oi.active && isMoving && timeSinceOrbit > autoFollowDelay)
        {
            Vector3 facing = GetTargetFacing();
            if (facing.sqrMagnitude > 0.001f)
            {
                float faceAngle = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
                // lerp orbitX towards facing angle
                orbitX = Mathf.LerpAngle(orbitX, faceAngle + yaw, autoFollowSpeed * dt);
            }
        }

        // --- idle slow orbit (cinematic, after long idle) ---
        if (idleSlowOrbit && !isMoving && !oi.active && timeSinceOrbit > idleDelay)
        {
            orbitX += idleOrbitSpeed * dt;
        }

        // --- zoom ---
        desiredDistance = Mathf.Clamp(desiredDistance - ScrollDelta() * zoomStep - oi.pinchDelta, minDistance, maxDistance);
        currentDistance = Mathf.SmoothDamp(currentDistance, desiredDistance, ref zoomVel, zoomSmoothTime);

        // --- calculate desired position with shoulder offset (GTA over-the-shoulder) ---
        Quaternion rot = Quaternion.Euler(orbitY, orbitX, 0f);
        Vector3 lookPoint = GetLookPoint();

        // idle vertical bob on look point
        if (idleBobEnabled && !isMoving)
        {
            float vBob = Mathf.Sin(idleBobPhase * 1.3f) * idleBobAmount * 0.15f;
            lookPoint.y += vBob;
        }

        Vector3 offsetVec = rot * new Vector3(shoulderOffset, verticalOffset, -currentDistance);
        Vector3 desiredPos = lookPoint + offsetVec;

        // --- collision: prevent clipping through ground/walls ---
        if (enableCollision)
        {
            Vector3 dir = desiredPos - lookPoint;
            float dist = dir.magnitude;
            if (dist > 0.1f)
            {
                dir /= dist;
                if (Physics.SphereCast(lookPoint, collisionRadius, dir, out RaycastHit hit, dist, collisionMask, QueryTriggerInteraction.Ignore))
                {
                    float safeDist = Mathf.Max(hit.distance - collisionPadding, 0.5f);
                    desiredPos = lookPoint + dir * safeDist;
                }
            }
        }

        // --- smooth follow (player follow camera) ---
        if (useSmoothDamp)
        {
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref posVel, 1f / Mathf.Max(smoothSpeed, 0.1f), Mathf.Infinity, dt);
        }
        else
        {
            float t = 1f - Mathf.Exp(-smoothSpeed * dt);
            transform.position = Vector3.Lerp(transform.position, desiredPos, t);
        }

        // --- look at player chest (with shoulder framing) ---
        Quaternion lookRot = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        float rotT = 1f - Mathf.Exp(-smoothSpeed * dt);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, rotT);
    }

    Vector3 GetLookPoint()
    {
        if (target == null) return Vector3.zero;
        return target.position + Vector3.up * lookHeight;
    }

    Vector3 GetTargetFacing()
    {
        if (target == null) return Vector3.forward;
        var bc = target.GetComponent<BillboardCharacter>();
        if (bc != null) return bc.Facing;
        var pc3d = target.GetComponent<PlayerController3D>();
        if (pc3d != null) return pc3d.Facing;
        // fallback: use velocity if rigidbody
        var rb = target.GetComponent<Rigidbody>();
        if (rb != null && rb.velocity.sqrMagnitude > 0.01f)
        {
            Vector3 v = rb.velocity; v.y = 0; return v.normalized;
        }
        var rb2d = target.GetComponent<Rigidbody2D>();
        if (rb2d != null && rb2d.velocity.sqrMagnitude > 0.01f)
        {
            Vector3 v = rb2d.velocity; return v.normalized;
        }
        return target.forward;
    }

    bool IsTargetMoving()
    {
        if (target == null) return false;
        var bc = target.GetComponent<BillboardCharacter>();
        if (bc != null) return bc.IsMoving;
        var pc3d = target.GetComponent<PlayerController3D>();
        if (pc3d != null) return pc3d.Facing.sqrMagnitude > 0.01f && (target.GetComponent<Rigidbody>()?.velocity.sqrMagnitude > 0.01f || true);
        // check via position delta or input
        var rb = target.GetComponent<Rigidbody>();
        if (rb != null) return rb.velocity.magnitude > 0.1f;
        var rb2d = target.GetComponent<Rigidbody2D>();
        if (rb2d != null) return rb2d.velocity.magnitude > 0.1f;
        // fallback: check if WASD pressed (approx)
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && (kb.wKey.isPressed || kb.aKey.isPressed || kb.sKey.isPressed || kb.dKey.isPressed)) return true;
        var gp = Gamepad.current;
        if (gp != null && gp.leftStick.ReadValue().sqrMagnitude > 0.05f) return true;
#else
        if (Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f) return true;
#endif
        return false;
    }

    // ---------------- input ----------------
    OrbitInput ReadInput(float dt)
    {
        OrbitInput oi = default;

#if ENABLE_INPUT_SYSTEM
        Mouse mo = Mouse.current;
        bool rmb = mo != null && mo.rightButton.isPressed;
        bool mmb = mo != null && mo.middleButton.isPressed;
        if (allowOrbit && (rmb || mmb))
        {
            oi.active = true;
            Vector2 delta = mo.delta.ReadValue();
            oi.yawDeg += delta.x * mouseSensitivity;
            if (allowPitchOrbit) oi.pitchDeg -= delta.y * pitchSensitivity;
        }

        if (allowOrbit && gamepadOrbit && Gamepad.current != null)
        {
            Vector2 st = Gamepad.current.rightStick.ReadValue();
            if (st.sqrMagnitude > 0.02f)
            {
                oi.active = true;
                oi.yawDeg += st.x * gamepadOrbitSpeed * dt;
                if (allowPitchOrbit) oi.pitchDeg -= st.y * gamepadOrbitSpeed * 0.6f * dt;
            }
        }

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
                    if (allowPitchOrbit) oi.pitchDeg -= d0.y * pitchSensitivity;
                }
                else if (activeCount >= 2 && pinchZoom)
                {
                    float distNow = Vector2.Distance(p0, p1);
                    float distPrev = Vector2.Distance(p0 - d0, p1);
                    oi.pinchDelta += (distNow - distPrev) * 0.02f;
                }
            }
        }
#else
        if (allowOrbit && Input.GetMouseButton(1))
        {
            oi.active = true;
            oi.yawDeg += Input.GetAxis("Mouse X") * mouseSensitivity * 100f * dt;
            if (allowPitchOrbit) oi.pitchDeg -= Input.GetAxis("Mouse Y") * pitchSensitivity * 100f * dt;
        }
        if (allowOrbit && touchOrbit && Input.touchCount == 1)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Moved)
            {
                oi.active = true;
                oi.yawDeg += t.deltaPosition.x * mouseSensitivity;
                if (allowPitchOrbit) oi.pitchDeg -= t.deltaPosition.y * pitchSensitivity;
            }
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
