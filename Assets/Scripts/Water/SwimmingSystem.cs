using UnityEngine;
using PrehistoricSurvival.Core;

namespace PrehistoricSurvival.Water
{
    /// <summary>
    /// Handles swimming mechanics when the player enters water.
    /// Reduces speed, drains stamina faster, hides lower body, plays ripple effects.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class SwimmingSystem : MonoBehaviour
    {
        [Header("Swimming Settings")]
        [Tooltip("Movement speed while swimming (fraction of normal).")]
        [Range(0f, 1f)]
        public float swimSpeedMultiplier = 0.4f;

        [Tooltip("Stamina drain multiplier while swimming.")]
        public float staminaDrainMultiplier = 2f;

        [Header("Visual")]
        [Tooltip("Lower body sprite renderer (hidden while swimming).")]
        public SpriteRenderer lowerBodyRenderer;

        [Tooltip("Water ripple particle system.")]
        public ParticleSystem rippleParticles;

        [Tooltip("Water surface Y level for visual clipping.")]
        public float waterSurfaceY = 0f;

        [Header("Audio")]
        public AudioClip splashSound;
        public AudioClip swimmingLoop;

        private Rigidbody2D _rb;
        private AudioSource _audio;
        private Survival.SurvivalStats _stats;
        private Player.FootprintSystem _footprints;
        private bool _isSwimming;
        private float _originalSpeed;

        public bool IsSwimming => _isSwimming;

        private void Start()
        {
            _rb = GetComponent<Rigidbody2D>();
            _audio = GetComponent<AudioSource>();
            if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
            _stats = GetComponent<Survival.SurvivalStats>();
            _footprints = GetComponent<Player.FootprintSystem>();
        }

        // ------------------------------------------------------------------
        // Trigger Detection
        // ------------------------------------------------------------------
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Water"))
                EnterWater();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Water"))
                ExitWater();
        }

        // ------------------------------------------------------------------
        // Swimming Logic
        // ------------------------------------------------------------------
        private void EnterWater()
        {
            if (_isSwimming) return;
            _isSwimming = true;

            // Hide lower body
            if (lowerBodyRenderer != null)
                lowerBodyRenderer.enabled = false;

            // Start ripple particles
            if (rippleParticles != null)
                rippleParticles.Play();

            // Play splash
            if (splashSound != null)
                _audio.PlayOneShot(splashSound);

            // Swimming loop
            if (swimmingLoop != null)
            {
                _audio.clip = swimmingLoop;
                _audio.loop = true;
                _audio.Play();
            }

            // Disable footprints
            if (_footprints != null)
                _footprints.SetInWater(true);

            EventManager.TriggerEvent(GameEvents.PlayerEnteredWater);
        }

        private void ExitWater()
        {
            if (!_isSwimming) return;
            _isSwimming = false;

            // Restore lower body
            if (lowerBodyRenderer != null)
                lowerBodyRenderer.enabled = true;

            // Stop ripple particles
            if (rippleParticles != null)
                rippleParticles.Stop();

            // Stop swimming audio
            if (_audio != null && _audio.isPlaying)
                _audio.Stop();

            // Re-enable footprints
            if (_footprints != null)
                _footprints.SetInWater(false);

            EventManager.TriggerEvent(GameEvents.PlayerExitedWater);
        }

        /// <summary>Called by PlayerController to get speed modifier.</summary>
        public float GetSpeedModifier()
        {
            return _isSwimming ? swimSpeedMultiplier : 1f;
        }

        private void Update()
        {
            // The planet itself decides where water is, so swimming works in every
            // ocean, lake and river of the streamed world (no hand placed triggers).
            var map = World.WorldMap.Instance;
            if (map != null)
            {
                bool inWater = map.IsWater(transform.position);
                if (inWater && !_isSwimming) EnterWater();
                else if (!inWater && _isSwimming) ExitWater();
            }

            if (!_isSwimming) return;

            // Extra stamina drain
            if (_stats != null)
            {
                float extraDrain = (staminaDrainMultiplier - 1f) * 1.5f * Time.deltaTime;
                _stats.Stamina -= extraDrain;
            }
        }
    }
}
