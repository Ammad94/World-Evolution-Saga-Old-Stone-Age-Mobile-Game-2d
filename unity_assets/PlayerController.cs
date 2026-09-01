using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 8-directional top-down player controller for the Stone Age caveman sprite set.
/// Swaps between 8 pre-sliced idle sprites based on movement direction.
/// Works with BOTH the new Input System and the legacy Input Manager,
/// and with OR WITHOUT a Rigidbody2D (falls back to direct transform movement).
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Movement speed in world units per second.")]
    public float moveSpeed = 5f;

    [Header("Sprites (assign in this EXACT order)")]
    [Tooltip("0=Front  1=FrontRight  2=Right  3=BackRight  4=Back  5=BackLeft  6=Left  7=FrontLeft")]
    public Sprite[] directionSprites = new Sprite[8];

    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private Vector2 moveInput;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();

        if (directionSprites == null || directionSprites.Length != 8)
            Debug.LogError("PlayerController: assign exactly 8 sprites in the directionSprites array.", this);
    }

    void Update()
    {
        // Read keyboard input (WASD / arrow keys)
        float h = 0f;
        float v = 0f;

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

        moveInput = new Vector2(h, v).normalized;

        // Face the movement direction
        if (moveInput.sqrMagnitude > 0.001f && directionSprites.Length == 8)
            SetDirectionSprite(moveInput);
    }

    void FixedUpdate()
    {
        if (rb != null)
        {
            // Physics movement (collides with colliders properly)
            rb.velocity = moveInput * moveSpeed;
        }
        else
        {
            // Fallback: move the transform directly if there's no Rigidbody2D
            transform.position += (Vector3)(moveInput * moveSpeed * Time.fixedDeltaTime);
        }
    }

    void SetDirectionSprite(Vector2 dir)
    {
        // angle: 0 = +x (right), 90 = +y (up)
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;

        // Map 0..360 degrees into the 8 sprite slots.
        int index = (Mathf.RoundToInt(angle / 45f) + 2) % 8;

        if (spriteRenderer != null)
            spriteRenderer.sprite = directionSprites[index];
    }
}
