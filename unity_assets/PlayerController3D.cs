using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// GTA V-style third-person player controller for the Stone Age caveman.
/// - Movement is relative to the CAMERA (W = run away from camera, like GTA).
/// - Optional click-to-move.
/// - Billboarded sprite that always faces the camera, with the correct directional
///   sprite chosen automatically.
/// - Supports ANY number of directions: 8 sprites = 45-degree steps,
///   16 sprites = 22.5-degree steps (smoother when the camera orbits).
/// - If an IdleAnimator is attached, IT takes over sprite assignment so the
///   idle animation (breathing / hair sway) plays; otherwise this script
///   sets the static sprite directly.
/// </summary>
public class PlayerController3D : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Movement speed in world units per second.")]
    public float moveSpeed = 5f;

    [Tooltip("How close (units) to a click point counts as 'arrived'.")]
    public float stopDistance = 0.15f;

    [Header("Sprites — turntable order")]
    [Tooltip("Front, then rotating around the character: front -> right -> back -> left -> front.\n" +
             "8 sprites: 0=front 1=frontRight 2=right 3=backRight 4=back 5=backLeft 6=left 7=frontLeft\n" +
             "16 sprites: the same circle with 22.5-degree steps between each.")]
    public Sprite[] directionSprites = new Sprite[8];

    [Header("Controls")]
    [Tooltip("Allow left-click on the ground to move there.")]
    public bool clickToMove = true;

    [Header("Fix if left/right look flipped")]
    [Tooltip("Tick this if he faces the wrong way when strafing left/right.")]
    public bool mirrorLeftRight = false;

    [Header("Sprite switching")]
    [Tooltip("Extra degrees of 'dead zone' before switching sprite (reduces flicker).")]
    public float switchDeadZone = 4f;

    /// <summary>Which way the character is currently facing (world XZ).</summary>
    public Vector3 Facing { get; private set; } = Vector3.forward;

    /// <summary>Current turntable direction index (0..N-1). Used by IdleAnimator.</summary>
    public int CurrentDirectionIndex { get; private set; } = -1;

    private SpriteRenderer sr;
    private IdleAnimator idleAnim;
    private Vector3? moveTarget = null;
    private Vector3 moveDir;
    private int currentSpriteIndex = -1;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        idleAnim = GetComponent<IdleAnimator>();

        if (directionSprites == null || directionSprites.Length < 2)
            Debug.LogError("PlayerController3D: assign at least 2 sprites in the directionSprites array.", this);
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
        if (cam == null || sr == null) return;
        int N = directionSprites.Length;
        if (N < 2) return;

        // 1) Upright billboard: rotate only around Y so the sprite faces the camera
        Vector3 camFwd = cam.transform.forward;
        camFwd.y = 0f;
        if (camFwd.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(camFwd.normalized, Vector3.up);

        // 2) Angle of the character's facing relative to the camera yaw (0..360)
        float camYaw = cam.transform.eulerAngles.y;
        float facingAngle = Mathf.Atan2(Facing.x, Facing.z) * Mathf.Rad2Deg;
        float rel = Mathf.DeltaAngle(camYaw, facingAngle);
        if (rel < 0f) rel += 360f;
        if (mirrorLeftRight) rel = (360f - rel) % 360f;

        // 3) Map the angle to a sprite index (works for 8, 16, or any N)
        float binSize = 360f / N;
        int target = Mathf.RoundToInt((180f - rel) / binSize);
        target = ((target % N) + N) % N;

        // 4) Hysteresis: only switch once we've moved clearly past the current sprite
        if (currentSpriteIndex < 0 || currentSpriteIndex >= N)
        {
            currentSpriteIndex = target;
        }
        else if (target != currentSpriteIndex)
        {
            float center = 180f - currentSpriteIndex * binSize;
            float d = Mathf.Abs(Mathf.DeltaAngle(center, rel));
            if (d > binSize * 0.5f + switchDeadZone)
                currentSpriteIndex = target;
        }

        CurrentDirectionIndex = currentSpriteIndex;

        // 5) If an IdleAnimator is present it owns the sprite; otherwise set it here
        if (idleAnim == null)
            sr.sprite = directionSprites[currentSpriteIndex];
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
