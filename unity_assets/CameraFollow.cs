using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Player Follow Camera - GTA V reference edition for 2D / Orthographic + 3D
/// Matches: https://i.ytimg.com/vi/oYlsmbxTVM4/maxresdefault.jpg
///
/// Two modes:
/// 1) 3D Perspective (if camera is Perspective) - acts like a simplified ThirdPersonCamera
///    with shoulder offset and GTA framing.
/// 2) 2D Orthographic / Side-scroll (classic mobile) - GTA-style framing where
///    character is slightly lower in frame, with look-ahead based on movement direction
///    and smooth follow. Same view while idle, walking, etc.
///
/// Features:
/// - Player follow with SmoothDamp / Lerp (frame-rate independent)
/// - GTA framing: characterOffsetY puts character lower (more horizon like ref)
/// - Look-ahead: camera looks slightly ahead in facing direction (like platformers + GTA)
/// - Dead zone: small movements don't jitter camera
/// - Idle bob: subtle breathing motion when idle (matches GTA idle cam)
/// - Snap option: locks dead-center like old version
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Tooltip("The object to follow (drag the Player here).")]
    public Transform target;

    [Tooltip("Base offset from target. Z must stay NEGATIVE (e.g. -10) so camera looks at scene. Y controls framing height.")]
    public Vector3 offset = new Vector3(0f, 1f, -10f);

    [Tooltip("Follow speed. Higher = snappier, lower = floatier. 10 = default, 6 = GTA filmic")]
    public float smoothSpeed = 8f;

    [Tooltip("If ON, camera snaps instantly to target every frame (character dead-center).")]
    public bool snapToTarget = false;

    [Header("=== GTA V Reference Framing (2D / Side) ===")]
    [Tooltip("GTA reference: character slightly lower in frame so you see horizon ahead. 0.8 = like ref image")]
    public float framingOffsetY = 0.8f;

    [Tooltip("Enable look-ahead in movement direction - camera looks slightly ahead like GTA / platformers")]
    public bool useLookAhead = true;

    [Tooltip("How far ahead to look when moving")]
    public float lookAheadDistance = 1.5f;

    [Tooltip("How fast look-ahead catches up")]
    public float lookAheadSmoothing = 2.5f;

    [Tooltip("Dead zone - if player moves less than this, camera stays (prevents micro jitter)")]
    public float deadZone = 0.15f;

    [Header("Idle Camera - Same view while idle")]
    public bool idleBobEnabled = true;
    [Range(0f, 0.3f)] public float idleBobAmount = 0.04f;
    public float idleBobSpeed = 0.8f;

    [Header("3D Mode (if camera is Perspective)")]
    [Tooltip("If camera is Perspective, use this as shoulder offset (GTA over-the-shoulder)")]
    public float shoulderOffset = 0.4f;
    public float pitch = 9f;
    public float lookHeight = 1.1f;

    // internal
    Vector3 currentLookAhead;
    Vector3 lookAheadVel;
    float idlePhase;
    Vector3 posVel;

    void Reset()
    {
        offset = new Vector3(0f, 1f, -10f);
        smoothSpeed = 8f;
        framingOffsetY = 0.8f;
        useLookAhead = true;
        lookAheadDistance = 1.5f;
        idleBobEnabled = true;
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        float dt = Mathf.Max(Time.deltaTime, 1e-5f);
        bool isPerspective = false;
        var cam = GetComponent<Camera>();
        if (cam != null) isPerspective = !cam.orthographic;

        // --- detect facing / moving ---
        Vector3 facing = GetFacing();
        bool isMoving = facing.sqrMagnitude > 0.01f;

        // --- look ahead (GTA / platformer style) ---
        Vector3 targetLookAhead = Vector3.zero;
        if (useLookAhead && isMoving)
        {
            targetLookAhead = facing * lookAheadDistance;
            // for 2D, only XY; for 3D, XZ
            if (!isPerspective)
            {
                targetLookAhead.z = 0f;
            }
            else
            {
                targetLookAhead.y = 0f;
            }
        }
        currentLookAhead = Vector3.SmoothDamp(currentLookAhead, targetLookAhead, ref lookAheadVel, 1f / Mathf.Max(lookAheadSmoothing, 0.1f), Mathf.Infinity, dt);

        // --- base desired position ---
        Vector3 desired;
        if (isPerspective)
        {
            // GTA 3D framing: similar to ThirdPersonCamera but simplified
            Quaternion rot = Quaternion.Euler(pitch, 0f, 0f);
            Vector3 lookPoint = target.position + Vector3.up * lookHeight + Vector3.up * framingOffsetY;
            Vector3 off = rot * new Vector3(shoulderOffset, 0f, -Mathf.Abs(offset.z));
            desired = lookPoint + off + currentLookAhead;
        }
        else
        {
            // 2D orthographic: GTA framing - character lower in frame
            desired = target.position + offset;
            desired.y += framingOffsetY;
            desired += currentLookAhead;
        }

        // --- idle bob (same view while idle, subtle breathing) ---
        if (idleBobEnabled)
        {
            if (!isMoving)
            {
                idlePhase += dt * idleBobSpeed;
                float bobY = Mathf.Sin(idlePhase) * idleBobAmount;
                float bobX = Mathf.Sin(idlePhase * 0.7f) * idleBobAmount * 0.5f;
                desired.y += bobY;
                desired.x += bobX;
            }
            else
            {
                idlePhase = 0f;
            }
        }

        // --- dead zone (prevents jitter on tiny moves) ---
        if (!snapToTarget && deadZone > 0f)
        {
            Vector3 diff = desired - transform.position;
            // ignore Z for deadzone check in 2D
            if (!isPerspective) diff.z = 0f;
            if (diff.magnitude < deadZone)
            {
                // stay, but still allow slow catch up if far
                return;
            }
        }

        if (snapToTarget)
        {
            transform.position = desired;
        }
        else
        {
            if (isPerspective)
            {
                // smooth damp for GTA filmic feel
                transform.position = Vector3.SmoothDamp(transform.position, desired, ref posVel, 1f / Mathf.Max(smoothSpeed, 0.1f), Mathf.Infinity, dt);
                // look at player
                Vector3 lookPt = target.position + Vector3.up * lookHeight;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookPt - transform.position, Vector3.up), 1f - Mathf.Exp(-smoothSpeed * dt));
            }
            else
            {
                float t = 1f - Mathf.Exp(-smoothSpeed * dt);
                transform.position = Vector3.Lerp(transform.position, desired, t);
            }
        }
    }

    Vector3 GetFacing()
    {
        if (target == null) return Vector3.zero;

        // Try BillboardCharacter first (most accurate)
        var bc = target.GetComponent<BillboardCharacter>();
        if (bc != null)
        {
            if (bc.IsMoving) return bc.Facing;
            // if idle, use last facing but no look-ahead (return zero for idle)
            return Vector3.zero;
        }

        var pc3d = target.GetComponent<PlayerController3D>();
        if (pc3d != null)
        {
            // pc3d.Facing is valid even when idle? Use move dir
            return pc3d.Facing * (IsMovingInput() ? 1f : 0f);
        }

        // Rigidbody velocity
        var rb = target.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 v = rb.velocity;
            if (v.sqrMagnitude > 0.05f)
            {
                v.y = 0f;
                return v.normalized;
            }
        }
        var rb2d = target.GetComponent<Rigidbody2D>();
        if (rb2d != null && rb2d.velocity.sqrMagnitude > 0.05f)
        {
            return rb2d.velocity.normalized;
        }

        // Input fallback for look-ahead
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            float h = 0f, v = 0f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) h -= 1f;
            Vector2 inp = new Vector2(h, v);
            if (inp.sqrMagnitude > 0.01f) return new Vector3(inp.x, inp.y, 0f).normalized;
        }
        var gp = Gamepad.current;
        if (gp != null)
        {
            Vector2 st = gp.leftStick.ReadValue();
            if (st.sqrMagnitude > 0.05f) return new Vector3(st.x, st.y, 0f).normalized;
        }
#else
        float hx = Input.GetAxisRaw("Horizontal");
        float hy = Input.GetAxisRaw("Vertical");
        if (Mathf.Abs(hx) > 0.01f || Mathf.Abs(hy) > 0.01f)
            return new Vector3(hx, hy, 0f).normalized;
#endif
        return Vector3.zero;
    }

    bool IsMovingInput()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && (kb.wKey.isPressed || kb.aKey.isPressed || kb.sKey.isPressed || kb.dKey.isPressed || kb.upArrowKey.isPressed || kb.downArrowKey.isPressed || kb.leftArrowKey.isPressed || kb.rightArrowKey.isPressed)) return true;
        var gp = Gamepad.current;
        if (gp != null && gp.leftStick.ReadValue().sqrMagnitude > 0.05f) return true;
        return false;
#else
        return Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.01f;
#endif
    }
}
