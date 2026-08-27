using UnityEngine;

namespace PrehistoricSurvival.Player
{
    /// <summary>
    /// Simple melee weapon/attack system for the player.
    /// Detects animals in range and applies damage.
    /// </summary>
    public class WeaponSystem : MonoBehaviour
    {
        [Header("Attack Settings")]
        [Tooltip("Attack range (units).")]
        public float attackRange = 2f;

        [Tooltip("Base damage per attack.")]
        public float baseDamage = 10f;

        [Tooltip("Time between attacks (seconds).")]
        public float attackCooldown = 1f;

        [Tooltip("Attack arc angle (degrees).")]
        public float attackArc = 90f;

        [Header("Weapon Bonus")]
        [Tooltip("Extra damage from equipped weapon.")]
        public float weaponDamageBonus;

        [Header("Visual")]
        [Tooltip("Attack swing animation overlay.")]
        public SpriteRenderer swingOverlay;
        public Sprite swingSprite;

        [Header("Audio")]
        public AudioClip swingSound;
        public AudioClip hitSound;

        private AudioSource _audio;
        private float _attackTimer;
        private LayerMask _animalLayer;

        private void Start()
        {
            _audio = GetComponent<AudioSource>();
            if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
            _animalLayer = LayerMask.GetMask("Animal");
        }

        private void Update()
        {
            _attackTimer -= Time.deltaTime;

            // Attack on left click or touch
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
                TryAttack();
        }

        private void TryAttack()
        {
            if (_attackTimer > 0f) return;

            _attackTimer = attackCooldown;

            // Show swing visual
            if (swingOverlay != null && swingSprite != null)
            {
                swingOverlay.sprite = swingSprite;
                swingOverlay.enabled = true;
                Invoke(nameof(HideSwing), 0.3f);
            }

            // Play sound
            if (swingSound != null) _audio.PlayOneShot(swingSound);

            // Detect animals in range
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                transform.position, attackRange, _animalLayer);

            float totalDamage = baseDamage + weaponDamageBonus;

            foreach (var hit in hits)
            {
                // Check angle
                Vector2 dir = (hit.transform.position - transform.position).normalized;
                Vector2 facing = transform.right;
                float angle = Vector2.Angle(facing, dir);

                if (angle <= attackArc * 0.5f)
                {
                    var ai = hit.GetComponent<AI.AnimalAI>();
                    if (ai != null)
                    {
                        ai.TakeDamage(totalDamage);
                        if (hitSound != null) _audio.PlayOneShot(hitSound);
                        if (CombatFeedback.Instance != null) CombatFeedback.Instance.Impact(ai.HealthPercent < 0.2f);
                        EventManager.TriggerEvent(GameEvents.AnimalHit, ai);
                    }
                }
            }
        }

        private void HideSwing()
        {
            if (swingOverlay != null) swingOverlay.enabled = false;
        }

        /// <summary>Set weapon damage bonus (called when equipping weapons).</summary>
        public void SetWeaponBonus(float bonus)
        {
            weaponDamageBonus = bonus;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
