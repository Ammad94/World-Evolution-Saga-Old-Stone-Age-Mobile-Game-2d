using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Old Stone Age - Side Scroller / GTA-inspired Player Follow Camera
/// For World Evolution Saga - 2D Mobile Game
/// 
/// Reference: https://i.ytimg.com/vi/oYlsmbxTVM4/maxresdefault.jpg (GTA V third-person)
/// This script recreates that framing for a 2D side-scroller / 2.5D billboard game:
/// - Camera behind player, slightly above, low pitch feel even in orthographic
/// - Character lower in frame (40% from bottom) to show horizon/ground ahead - like GTA ref
/// - Smooth follow with look-ahead in movement direction
/// - Same view while idle, walking, running (no jarring changes)
/// - Mobile-friendly: works with joystick, touch, no right-mouse needed
/// - Cinemachine-free, lightweight for mobile
///
/// Use this if your game is primarily side-scrolling (left/right) but you want
/// the GTA V camera feel from the reference image.
///
/// Setup:
/// 1. Main Camera -> Add Component -> SideScrollerCamera
/// 2. Drag Player into Target
/// 3. Set Pixels Per Unit and Orthographic Size to match your art
/// 4. For billboard 3D, set Camera to Perspective and enable usePerspectiveMode
/// </summary>
public class SideScrollerCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("GTA Reference Framing")]
    [Tooltip("How far behind (Z). Keep negative, e.g. -10")]
    public float distanceZ = -10f;

    [Tooltip("Vertical offset - puts character lower in frame like GTA ref. 1.2 = horizon near top")]
    public float verticalFraming = 1.2f;

    [Tooltip("Horizontal framing offset - 0 = centered, negative = character slightly right (GTA shoulder)")]
    public float horizontalFraming = 0f;

    [Header("Follow - Player Follow Camera")]
    [Range(0.1f, 20f)] public float followSmooth = 5f;
    public bool snapOnStart = true;

    [Tooltip("Dead zone X - camera won't move if player inside this horizontal range (prevents jitter)")]
    public float deadZoneX = 0.5f;
    [Tooltip("Dead zone Y")]
    public float deadZoneY = 0.3f;

    [Header("Look Ahead - GTA / Platformer")]
    [Tooltip("Look ahead in facing direction - like GTA camera looking where you go")]
    public bool useLookAhead = true;
    public float lookAheadDistance = 2f;
    public float lookAheadSmoothing = 2f;
    [Tooltip("Only look ahead when moving faster than this")]
    public float lookAheadMoveThreshold = 0.1f;

    [Header("Idle Camera - Same view while idle")]
    [Tooltip("Keep same camera view while idle (like reference) - no snap back")]
    public bool keepSameViewOnIdle = true;
    public bool idleBob = true;
    public float idleBobAmount = 0.05f;
    public float idleBobSpeed = 0.6f;

    [Header("Bounds (optional)")]
    public bool useBounds = false;
    public Vector2 minBounds = new Vector2(-20f, -10f);
    public Vector2 maxBounds = new Vector2(20f, 10f);

    [Header("Perspective Mode (for BillboardCharacter)")]
    [Tooltip("If true and camera is Perspective, adds GTA shoulder offset + pitch")]
    public bool usePerspectiveMode = false;
    public float perspectivePitch = 8f;
    public float perspectiveShoulder = 0.5f;
    public float perspectiveLookHeight = 1f;

    // internal
    Vector3 currentLookAhead;
    Vector3 lookAheadVel;
    Vector3 posVel;
    float idlePhase;
    Vector3 lastTargetPos;

    void Reset()
    {
        distanceZ = -10f;
        verticalFraming = 1.2f;
        followSmooth = 5f;
        deadZoneX = 0.5f;
        deadZoneY = 0.3f;
        useLookAhead = true;
        lookAheadDistance = 2f;
        keepSameViewOnIdle = true;
    }

    void Awake()
    {
        if (target != null) lastTargetPos = target.position;
        if (snapOnStart && target != null)
        {
            Vector3 desired = GetDesiredPosition(0f, true);
            transform.position = desired;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;
        float dt = Mathf.Max(Time.deltaTime, 1e-5f);

        Vector3 facing = GetFacingDirection();
        bool isMoving = facing.sqrMagnitude > lookAheadMoveThreshold * lookAheadMoveThreshold;

        // look ahead target
        Vector3 targetLookAhead = Vector3.zero;
        if (useLookAhead && isMoving)
        {
            targetLookAhead = facing * lookAheadDistance;
            targetLookAhead.z = 0f;
            if (usePerspectiveMode && GetComponent<Camera>() != null && !GetComponent<Camera>().orthographic)
            {
                targetLookAhead.y = 0f; // in 3D, only XZ
            }
        }
        currentLookAhead = Vector3.SmoothDamp(currentLookAhead, targetLookAhead, ref lookAheadVel, 1f / Mathf.Max(lookAheadSmoothing, 0.1f), Mathf.Infinity, dt);

        Vector3 desired = GetDesiredPosition(dt, false);

        // dead zone check - if inside dead zone, don't move camera (GTA stable cam)
        if (!snapOnStart && (deadZoneX > 0f || deadZoneY > 0f))
        {
            Vector3 diff = desired - transform.position;
            bool insideX = Mathf.Abs(diff.x) < deadZoneX;
            bool insideY = Mathf.Abs(diff.y) < deadZoneY;
            if (insideX && insideY && !isMoving && keepSameViewOnIdle)
            {
                // stay - but still apply idle bob
                if (idleBob)
                {
                    idlePhase += dt * idleBobSpeed;
                    float bobY = Mathf.Sin(idlePhase) * idleBobAmount;
                    transform.position += new Vector3(0f, bobY * dt * 2f, 0f);
                }
                return;
            }
            // if only X inside, only move Y, etc. - for simplicity, if inside both, skip
            if (insideX && insideY) return;
        }

        // smooth follow
        if (snapOnStart && Time.time < 0.1f)
        {
            transform.position = desired;
        }
        else
        {
            // GTA filmic glide - SmoothDamp
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref posVel, 1f / Mathf.Max(followSmooth, 0.1f), Mathf.Infinity, dt);
        }

        // perspective look
        if (usePerspectiveMode)
        {
            var cam = GetComponent<Camera>();
            if (cam != null && !cam.orthographic)
            {
                Vector3 lookPt = target.position + Vector3.up * perspectiveLookHeight;
                Quaternion lookRot = Quaternion.LookRotation(lookPt - transform.position, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, 1f - Mathf.Exp(-followSmooth * dt));
            }
        }

        lastTargetPos = target.position;
    }

    Vector3 GetDesiredPosition(float dt, bool ignoreLookAhead)
    {
        Vector3 basePos = target.position;
        basePos.z = 0f; // keep player Z at 0 for 2D

        Vector3 desired = basePos;
        desired.x += horizontalFraming;
        desired.y += verticalFraming;
        desired.z = distanceZ;

        if (!ignoreLookAhead)
        {
            desired += currentLookAhead;
        }

        // idle bob - same view while idle but alive
        if (idleBob && dt > 0f)
        {
            bool isMoving = GetFacingDirection().sqrMagnitude > 0.01f;
            if (!isMoving)
            {
                idlePhase += dt * idleBobSpeed;
                float bobY = Mathf.Sin(idlePhase) * idleBobAmount;
                float bobX = Mathf.Sin(idlePhase * 0.6f) * idleBobAmount * 0.4f;
                desired.y += bobY;
                desired.x += bobX;
            }
        }

        // perspective offset
        if (usePerspectiveMode)
        {
            var cam = GetComponent<Camera>();
            if (cam != null && !cam.orthographic)
            {
                Quaternion rot = Quaternion.Euler(perspectivePitch, 0f, 0f);
                Vector3 lookPoint = target.position + Vector3.up * perspectiveLookHeight;
                Vector3 off = rot * new Vector3(perspectiveShoulder, verticalFraming * 0.3f, distanceZ);
                desired = lookPoint + off;
                if (!ignoreLookAhead) desired += currentLookAhead;
            }
        }

        // bounds
        if (useBounds)
        {
            desired.x = Mathf.Clamp(desired.x, minBounds.x, maxBounds.x);
            desired.y = Mathf.Clamp(desired.y, minBounds.y, maxBounds.y);
        }

        return desired;
    }

    Vector3 GetFacingDirection()
    {
        if (target == null) return Vector3.zero;

        // BillboardCharacter is most reliable
        var bc = target.GetComponent<BillboardCharacter>();
        if (bc != null)
        {
            if (!bc.IsMoving) return Vector3.zero;
            Vector3 f = bc.Facing;
            // for side scroller, we care about X direction primarily
            return new Vector3(f.x, 0f, f.z).normalized;
        }

        var pc3d = target.GetComponent<PlayerController3D>();
        if (pc3d != null)
        {
            // check if actually moving via input
            if (!IsInputMoving()) return Vector3.zero;
            Vector3 f = pc3d.Facing;
            f.y = 0f;
            return f.normalized;
        }

        // velocity based
        Vector3 delta = target.position - lastTargetPos;
        if (delta.sqrMagnitude > 0.0001f)
        {
            delta.y = 0f;
            if (delta.sqrMagnitude > 0.0001f) return delta.normalized;
        }

        // input fallback
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            float h = 0f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) h -= 1f;
            if (Mathf.Abs(h) > 0.01f) return new Vector3(h, 0f, 0f).normalized;
        }
        var gp = Gamepad.current;
        if (gp != null)
        {
            Vector2 st = gp.leftStick.ReadValue();
            if (Mathf.Abs(st.x) > 0.1f) return new Vector3(st.x, 0f, 0f).normalized;
        }
#else
        float hx = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(hx) > 0.01f) return new Vector3(hx, 0f, 0f).normalized;
#endif
        return Vector3.zero;
    }

    bool IsInputMoving()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && (kb.wKey.isPressed || kb.aKey.isPressed || kb.sKey.isPressed || kb.dKey.isPressed)) return true;
        var gp = Gamepad.current;
        if (gp != null && gp.leftStick.ReadValue().sqrMagnitude > 0.05f) return true;
        return false;
#else
        return Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.01f;
#endif
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!useBounds) return;
        Gizmos.color = Color.yellow;
        Vector3 min = new Vector3(minBounds.x, minBounds.y, 0f);
        Vector3 max = new Vector3(maxBounds.x, maxBounds.y, 0f);
        Vector3 center = (min + max) * 0.5f;
        Vector3 size = max - min;
        Gizmos.DrawWireCube(center, size);
    }
#endif
}
