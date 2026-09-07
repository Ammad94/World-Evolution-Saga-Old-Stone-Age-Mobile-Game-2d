using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// GTA V-style third-person player controller for the Stone Age caveman.
/// - Movement is relative to the CAMERA (W = run away from camera, like GTA).
/// - Optional click-to-move: left-click a point and the player walks there.
/// - The sprite is BILLBOARDED (always faces the camera, stays upright) and
///   the correct one of the 8 directional sprites is chosen automatically
///   based on the player's facing vs. the camera.
///
/// Setup: put this on the Player (needs a SpriteRenderer).
/// The ground click is solved mathematically on the y=0 plane, so no
/// ground collider is required for click-to-move.
/// </summary>
public class PlayerController3D : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Movement speed in world units per second.")]
    public float moveSpeed = 5f;

    [Tooltip("How close (units) to a click point counts as 'arrived'.")]
    public float stopDistance = 0.15f;

    [Header("Sprites (same 8, same order as before)")]
    [Tooltip("0=Front  1=FrontRight  2=Right  3=BackRight  4=Back  5=BackLeft  6=Left  7=FrontLeft")]
    public Sprite[] directionSprites = new Sprite[8];

    [Header("Controls")]
    [Tooltip("Allow left-click on the ground to move there.")]
    public bool clickToMove = true;

    [Header("Fix if left/right look flipped")]
    [Tooltip("Tick this if he faces the wrong way when strafing left/right.")]
    public bool mirrorLeftRight = false;

    /// <summary>Which way the character is currently facing (world XZ).</summary>
    public Vector3 Facing { get; private set; } = Vector3.forward;

    private SpriteRenderer sr;
    private Vector3? moveTarget = null;
    private Vector3 moveDir;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();

        if (directionSprites == null || directionSprites.Length != 8)
            Debug.LogError("PlayerController3D: assign exactly 8 sprites in the directionSprites array.", this);
    }

    void Update()
    {
        moveDir = Vector3.zero;

        // ---- WASD / arrows, relative to the camera ----
        float h = 0f, v = 0f;
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    v += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  v -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  h -= 1f;
        }
#else
        h = Input.GetAxisRaw("Horizontal");
        v = Input.GetAxisRaw("Vertical");
#endif

        if (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f)
        {
            moveTarget = null; // manual input cancels click-to-move

            Camera cam = Camera.main;
            Vector3 f = cam ? cam.transform.forward : Vector3.forward; f.y = 0f; f.Normalize();
            Vector3 r = cam ? cam.transform.right   : Vector3.right;    r.y = 0f; r.Normalize();
            moveDir = (f * v + r * h).normalized;
        }
        else if (clickToMove && MouseLeftClicked())
        {
            TrySetMoveTarget();
        }

        // ---- click-to-move follow ----
        if (moveTarget.HasValue)
        {
            Vector3 to = moveTarget.Value - transform.position;
            to.y = 0f;
            if (to.magnitude <= stopDistance) { moveTarget = null; }
            else moveDir = to.normalized;
        }

        // ---- move & record facing ----
        if (moveDir.sqrMagnitude > 0.001f)
        {
            transform.position += moveDir * moveSpeed * Time.deltaTime;
            Facing = moveDir.normalized;
        }

        UpdateBillboardAndSprite();
    }

    void TrySetMoveTarget()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(MouseScreenPosition());
        Plane ground = new Plane(Vector3.up, Vector3.zero); // ground plane y = 0
        if (ground.Raycast(ray, out float dist))
        {
            moveTarget = ray.GetPoint(dist);
        }
    }

    void UpdateBillboardAndSprite()
    {
        Camera cam = Camera.main;
        if (cam == null || sr == null || directionSprites.Length != 8) return;

        // 1) Upright billboard: rotate only around Y so the sprite faces the camera
        Vector3 camFwd = cam.transform.forward;
        camFwd.y = 0f;
        if (camFwd.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(camFwd.normalized, Vector3.up);

        // 2) Choose the sprite from the angle between facing and the camera yaw
        float camYaw = cam.transform.eulerAngles.y;
        float facingAngle = Mathf.Atan2(Facing.x, Facing.z) * Mathf.Rad2Deg;
        float rel = Mathf.DeltaAngle(camYaw, facingAngle);
        if (rel < 0f) rel += 360f;
        int bin = Mathf.RoundToInt(rel / 45f) % 8;

        // bin: 0=away 1=away-right 2=right 3=toward-right 4=toward 5=toward-left 6=left 7=away-left
        int[] map = mirrorLeftRight
            ? new int[] { 4, 5, 6, 7, 0, 1, 2, 3 }
            : new int[] { 4, 3, 2, 1, 0, 7, 6, 5 };

        sr.sprite = directionSprites[map[bin]];
    }

    // ---------- input helpers (new Input System vs legacy) ----------
    static bool MouseLeftClicked()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse m = Mouse.current;
        return m != null && m.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    static Vector3 MouseScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse m = Mouse.current;
        return m != null ? (Vector3)m.position.ReadValue() : Vector3.zero;
#else
        return Input.mousePosition;
#endif
    }
}
