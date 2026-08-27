using UnityEngine;
using PrehistoricSurvival.Core;

namespace PrehistoricSurvival.Water
{
    /// <summary>
    /// Craftable log raft that the player can mount to navigate water.
    /// Provides faster movement and prevents stamina drain while in water.
    /// </summary>
    public class RaftController : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Movement speed while on raft.")]
        public float raftSpeed = 4f;

        [Tooltip("How quickly the raft accelerates/decelerates.")]
        public float acceleration = 5f;

        [Header("Visual")]
        [Tooltip("Raft sprite renderer.")]
        public SpriteRenderer raftSprite;

        [Tooltip("Player mount point (child transform).")]
        public Transform mountPoint;

        [Header("Audio")]
        public AudioClip paddleSound;

        private Rigidbody2D _rb;
        private AudioSource _audio;
        private Transform _player;
        private bool _isMounted;
        private Vector2 _input;
        private Vector2 _velocity;

        public bool IsMounted => _isMounted;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _audio = GetComponent<AudioSource>();
            if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
        }

        // ------------------------------------------------------------------
        // Mount / Dismount
        // ------------------------------------------------------------------

        /// <summary>Mount the player onto this raft.</summary>
        public void Mount(Transform player)
        {
            if (_isMounted || player == null) return;

            _player = player;
            _isMounted = true;

            // Parent player to mount point
            player.SetParent(mountPoint != null ? mountPoint : transform);
            player.localPosition = Vector3.zero;

            // Disable player controller
            var controller = player.GetComponent<Player.PlayerController>();
            if (controller != null) controller.enabled = false;

            Debug.Log("[RaftController] Player mounted raft.");
        }

        /// <summary>Dismount the player from the raft.</summary>
        public void Dismount()
        {
            if (!_isMounted || _player == null) return;

            _player.SetParent(null);
            _player.position = transform.position + Vector3.right * 2f;

            // Re-enable player controller
            var controller = _player.GetComponent<Player.PlayerController>();
            if (controller != null) controller.enabled = true;

            _isMounted = false;
            _player = null;
            _rb.linearVelocity = Vector2.zero;

            Debug.Log("[RaftController] Player dismounted raft.");
        }

        private void Update()
        {
            if (!_isMounted || _player == null) return;

            // Gather input
            _input.x = Input.GetAxisRaw("Horizontal");
            _input.y = Input.GetAxisRaw("Vertical");

            // Also accept joystick if available
            var joystick = _player.GetComponentInChildren<Player.MobileJoystick>();
            if (joystick != null && joystick.IsActive)
                _input = joystick.Direction;
        }

        private void FixedUpdate()
        {
            if (!_isMounted) return;

            Vector2 target = _input.normalized * raftSpeed;
            _velocity = Vector2.MoveTowards(_velocity, target, acceleration * Time.fixedDeltaTime);
            _rb.linearVelocity = _velocity;

            // Rotate raft to face movement direction
            if (_velocity.sqrMagnitude > 0.01f)
            {
                float angle = Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg - 90f;
                _rb.rotation = Mathf.LerpAngle(_rb.rotation, angle, 5f * Time.fixedDeltaTime);
            }
        }

        // ------------------------------------------------------------------
        // Interaction
        // ------------------------------------------------------------------
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (_isMounted) return;

            // Auto-mount when player walks into raft
            Mount(other.transform);
        }
    }
}
