using UnityEngine;

namespace PrehistoricSurvival.Traversal
{
    /// <summary>
    /// Allows the player to climb cliffs and trees using trigger colliders tagged "Climbable".
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class ClimbingSystem : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Climbing speed (units/sec).")]
        public float climbSpeed = 2f;

        [Tooltip("Stamina drain while climbing (per second).")]
        public float staminaDrain = 1f;

        [Header("Visual")]
        [Tooltip("Sprite to show while climbing.")]
        public Sprite climbingSprite;

        private Rigidbody2D _rb;
        private SpriteRenderer _sr;
        private Sprite _originalSprite;
        private Survival.SurvivalStats _stats;
        private Player.PlayerController _playerController;
        private bool _isClimbing;
        private Collider2D _currentClimbable;

        public bool IsClimbing => _isClimbing;

        private void Start()
        {
            _rb = GetComponent<Rigidbody2D>();
            _sr = GetComponentInChildren<SpriteRenderer>();
            _stats = GetComponent<Survival.SurvivalStats>();
            _playerController = GetComponent<Player.PlayerController>();
            if (_sr != null) _originalSprite = _sr.sprite;
        }

        private void Update()
        {
            if (_isClimbing)
                HandleClimbing();
        }

        private void HandleClimbing()
        {
            // Vertical input for climbing
            float v = Input.GetAxisRaw("Vertical");

            // Climbing movement
            Vector2 moveDir = new Vector2(0f, v);
            _rb.linearVelocity = moveDir * climbSpeed;

            // Drain stamina
            if (_stats != null && v != 0f)
                _stats.Stamina -= staminaDrain * Time.deltaTime;

            // Stop climbing if no stamina
            if (_stats != null && _stats.Stamina <= 0f)
                StopClimbing();

            // Exit climbing on horizontal input
            float h = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(h) > 0.5f)
                StopClimbing();
        }

        // ------------------------------------------------------------------
        // Trigger Detection
        // ------------------------------------------------------------------
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Climbable")) return;
            if (_isClimbing) return;

            _currentClimbable = other;
            StartClimbing();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other != _currentClimbable) return;
            StopClimbing();
        }

        private void StartClimbing()
        {
            _isClimbing = true;

            // Disable normal movement
            if (_playerController != null)
                _playerController.enabled = false;

            // Switch to climbing sprite
            if (_sr != null && climbingSprite != null)
                _sr.sprite = climbingSprite;

            _rb.gravityScale = 0f;
        }

        private void StopClimbing()
        {
            _isClimbing = false;
            _currentClimbable = null;

            // Re-enable normal movement
            if (_playerController != null)
                _playerController.enabled = true;

            // Restore sprite
            if (_sr != null)
                _sr.sprite = _originalSprite;

            _rb.linearVelocity = Vector2.zero;
        }
    }
}
