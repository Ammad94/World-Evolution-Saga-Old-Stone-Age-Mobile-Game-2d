using UnityEngine;

namespace PrehistoricSurvival.Player
{
    /// <summary>
    /// Handles 8-directional movement with sprite swapping based on input angle.
    /// Supports both keyboard/mouse and mobile joystick input.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Base movement speed (units/sec).")]
        public float baseSpeed = 5f;
        [Tooltip("Acceleration when starting to move.")]
        public float acceleration = 20f;
        [Tooltip("Deceleration when stopping.")]
        public float deceleration = 25f;

        [Header("Sprites – one array per direction (N, NE, E, SE, S, SW, W, NW)")]
        [Tooltip("Animation frames for each of the 8 directions. Length must match.")]
        public Sprite[] spritesNorth;
        public Sprite[] spritesNorthEast;
        public Sprite[] spritesEast;
        public Sprite[] spritesSouthEast;
        public Sprite[] spritesSouth;
        public Sprite[] spritesSouthWest;
        public Sprite[] spritesWest;
        public Sprite[] spritesNorthWest;

        [Header("Animation")]
        [Tooltip("Frames per second for walk cycle.")]
        public float animFrameRate = 10f;
        [Tooltip("Small visual stride motion used even when a directional set has one imported frame.")]
        public float strideBob = 0.035f;
        public float strideSquash = 0.02f;

        [Header("Input")]
        [Tooltip("Reference to the on-screen joystick (optional).")]
        public MobileJoystick joystick;

        // Internal
        private Rigidbody2D _rb;
        private SpriteRenderer _sr;
        private Vector2 _moveInput;
        private Vector2 _velocity;
        private float _animTimer;
        private int _frameIndex;
        private float _currentSpeed;
        private bool _isMoving;
        private WeightCarrySystem _weightSystem;
        private Water.SwimmingSystem _swimming;
        private Vector3 _baseScale;
        private Vector3 _baseLocalPosition;

        // Exposed state
        public bool IsMoving => _isMoving;
        /// <summary>When true (action one-shots), the walk animation pauses.</summary>
        public bool AnimationLocked { get; set; }
        public Vector2 MoveDirection => _moveInput.normalized;
        public float CurrentSpeed => _currentSpeed;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _sr = GetComponentInChildren<SpriteRenderer>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _weightSystem = GetComponent<WeightCarrySystem>();
            _swimming = GetComponent<Water.SwimmingSystem>();
            _baseScale = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;
            _baseLocalPosition = transform.localPosition;
        }

        private void Update()
        {
            GatherInput();
            Animate();
        }

        private void FixedUpdate()
        {
            Move();
        }

        // ------------------------------------------------------------------
        // Input
        // ------------------------------------------------------------------
        private void GatherInput()
        {
            // Keyboard / WASD
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            _moveInput = new Vector2(h, v);

            // Override with joystick if present and active
            if (joystick != null && joystick.IsActive)
                _moveInput = joystick.Direction;
        }

        // ------------------------------------------------------------------
        // Movement
        // ------------------------------------------------------------------
        private void Move()
        {
            // Effective speed considering carried weight
            float effectiveSpeed = baseSpeed;
            if (_weightSystem != null)
                effectiveSpeed *= (1f - _weightSystem.SpeedPenalty);
            if (_swimming != null)
                effectiveSpeed *= _swimming.GetSpeedModifier();
            var season = Survival.SeasonManager.Instance;
            if (season != null)
                effectiveSpeed *= season.MovementSpeedMultiplier;

            Vector2 target = _moveInput.normalized * effectiveSpeed;

            if (target.sqrMagnitude > 0.01f)
            {
                _velocity = Vector2.MoveTowards(_velocity, target, acceleration * Time.fixedDeltaTime);
                _isMoving = true;
            }
            else
            {
                _velocity = Vector2.MoveTowards(_velocity, Vector2.zero, deceleration * Time.fixedDeltaTime);
                _isMoving = _velocity.sqrMagnitude > 0.01f;
            }

            _currentSpeed = _velocity.magnitude;
            _rb.linearVelocity = _velocity;

            // Update dynamic sorting order for pseudo-3D depth
            _sr.sortingOrder = World.ChunkManager.SortingOrderFor(transform.position.y);
        }

        // ------------------------------------------------------------------
        // 8-Direction Sprite Animation
        // ------------------------------------------------------------------
        private void Animate()
        {
            if (AnimationLocked) return;
            if (!_isMoving)
            {
                _animTimer = 0f; _frameIndex = 0;
                transform.localPosition = Vector3.Lerp(transform.localPosition, _baseLocalPosition, Time.deltaTime * 10f);
                transform.localScale = Vector3.Lerp(transform.localScale, _baseScale, Time.deltaTime * 10f);
                return;
            }

            // Determine direction index from movement angle. In the GTA-style 2.5D
            // chase view the camera always hangs behind the player's back, so the
            // character is always seen from behind (the "south"/back sprite set).
            float angle = CameraFollow.Chase3D
                ? 270f
                : Mathf.Atan2(_moveInput.y, _moveInput.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            Sprite[] frames = GetFramesForAngle(angle);
            if (frames == null || frames.Length == 0) return;

            _animTimer += Time.deltaTime;
            if (_animTimer >= 1f / animFrameRate)
            {
                _animTimer = 0f;
                _frameIndex = (_frameIndex + 1) % frames.Length;
            }

            _sr.sprite = frames[_frameIndex];
            float phase = (_frameIndex / (float)Mathf.Max(1, frames.Length)) * Mathf.PI * 2f;
            transform.localPosition = _baseLocalPosition + Vector3.up * (Mathf.Sin(phase) * strideBob);
            transform.localScale = _baseScale * (1f + Mathf.Sin(phase) * strideSquash);
        }

        private Sprite[] GetFramesForAngle(float angle)
        {
            // 8 sectors of 45° each, centered on the cardinal/intercardinal directions
            //  E  = 0°,  NE = 45°,  N = 90°,  NW = 135°
            //  W  = 180°, SW = 225°, S = 270°, SE = 315°
            if (angle >= 337.5f || angle < 22.5f) return spritesEast;
            if (angle < 67.5f) return spritesNorthEast;
            if (angle < 112.5f) return spritesNorth;
            if (angle < 157.5f) return spritesNorthWest;
            if (angle < 202.5f) return spritesWest;
            if (angle < 247.5f) return spritesSouthWest;
            if (angle < 292.5f) return spritesSouth;
            return spritesSouthEast;
        }
    }
}
