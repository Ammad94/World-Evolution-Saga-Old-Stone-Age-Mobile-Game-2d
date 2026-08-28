using UnityEngine;
using PrehistoricSurvival.Core;

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
        public PrehistoricSurvival.Core.DamageType damageType = Core.DamageType.Blunt;
        public int comboStep { get; private set; }
        public float comboWindow = 0.85f;
        private float _comboTimer;

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
            _comboTimer -= Time.deltaTime;
            if (_comboTimer <= 0f) comboStep = 0;

            // Attack on left click or touch
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
                TryAttack();
        }

        private void TryAttack()
        {
            if (_attackTimer > 0f) return;

            _attackTimer = attackCooldown;
            comboStep = comboStep >= 3 ? 1 : comboStep + 1;
            _comboTimer = comboWindow;
            var equipment = GetComponent<PrehistoricSurvival.Core.CombatEquipment>();
            if (equipment != null && !equipment.UseWeapon()) return;
            bool heavySwing = equipment != null && equipment.weaponDamageMultiplier > 1.05f;
            Core.AudioManager.Instance?.Play($"effort_{Random.Range(0, 2)}", 0.4f);

            // Full-body swing animation + arc VFX + swoosh
            var move = GetComponent<PrehistoricSurvival.Player.PlayerController>();
            float angle = move != null ? Mathf.Atan2(move.MoveDirection.y, move.MoveDirection.x) * Mathf.Rad2Deg : 0f;
            GetComponent<PrehistoricSurvival.Art.PlayerActionAnimator>()?.PlayAttack(angle);
            Vector3 swingPos = transform.position + new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0f) * 1.2f;
            PrehistoricSurvival.Art.FX.Spawn(heavySwing ? "slash" : "slash", swingPos, heavySwing ? 1.3f : 1f, 18f);

            // Play sound
            var clipName = heavySwing ? $"swing_heavy_{Random.Range(0, 3)}" : $"swing_{Random.Range(0, 3)}";
            var swoosh = Core.AudioManager.Clip("sfx/" + clipName);
            if (swoosh != null) _audio.PlayOneShot(swoosh, 0.8f);
            else if (swingSound != null) _audio.PlayOneShot(swingSound);

            // Detect animals in range
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                transform.position, attackRange, _animalLayer);

            float totalDamage = (baseDamage + weaponDamageBonus) * (1f + (comboStep - 1) * 0.15f);
            if (equipment != null) totalDamage *= equipment.weaponDamageMultiplier;

            foreach (var hit in hits)
            {
                // Check angle
                Vector2 dir = (hit.transform.position - transform.position).normalized;
                Vector2 facing = transform.right;
                float targetAngle = Vector2.Angle(facing, dir);

                if (targetAngle <= attackArc * 0.5f)
                {
                    var ai = hit.GetComponent<AI.AnimalAI>();
                    if (ai != null)
                    {
                        ai.TakeDamage(totalDamage);
                        var fleshHit = Core.AudioManager.Clip($"sfx/hit_flesh_{Random.Range(0, 3)}");
                        if (fleshHit != null) _audio.PlayOneShot(fleshHit, 0.9f);
                        else if (hitSound != null) _audio.PlayOneShot(hitSound);
                        PrehistoricSurvival.Feedback.DamageNumber.Damage(hit.transform.position, totalDamage, ai.HealthPercent < 0.2f);
                        PrehistoricSurvival.Feedback.GameFeel.Impact(hit.transform.position, ai.HealthPercent < 0.2f);
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
