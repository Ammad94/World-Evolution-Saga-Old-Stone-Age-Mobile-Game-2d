using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace PrehistoricSurvival.AI
{
    /// <summary>
    /// Finite State Machine AI for prehistoric animals.
    /// States: Idle, Patrol, Chase, Attack, Flee, Dead.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class AnimalAI : MonoBehaviour
    {
        public enum AIState { Idle, Patrol, Chase, Attack, Flee, Dead }
        public enum AggressionLevel { Passive, Neutral, Aggressive }

        [Header("Animal Properties")]
        public string animalName = "Woolly Mammoth";
        public float maxHealth = 200f;
        public float currentHealth;
        public float damage = 20f;
        public float moveSpeed = 3f;
        public float runSpeed = 6f;

        [Header("AI Behavior")]
        public AggressionLevel aggression = AggressionLevel.Neutral;
        [Tooltip("Distance at which the animal detects the player.")]
        public float detectionRange = 15f;
        [Tooltip("Distance at which the animal attacks.")]
        public float attackRange = 2f;
        [Tooltip("Distance at which the animal gives up chasing.")]
        public float leashRange = 30f;
        [Tooltip("Time between attacks (seconds).")]
        public float attackCooldown = 2f;

        [Header("Flee Behavior")]
        [Tooltip("Health percentage below which the animal flees.")]
        [Range(0f, 1f)]
        public float fleeThreshold = 0.2f;
        [Tooltip("Passive animals always flee when player is near.")]
        public bool alwaysFleeIfPassive = true;

        [Header("Patrol")]
        public float patrolRadius = 10f;
        public float patrolWaitTime = 3f;

        [Header("Visual")]
        public SpriteRenderer spriteRenderer;

        // --- State ---
        private AIState _state = AIState.Idle;
        private Rigidbody2D _rb;
        private Transform _player;
        private Vector3 _spawnPos;
        private Vector3 _patrolTarget;
        private float _stateTimer;
        private float _attackTimer;
        private bool _isDead;

        public AIState CurrentState => _state;
        public float HealthPercent => currentHealth / maxHealth;

        // Events
        public event Action<AnimalAI> OnDeath;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            currentHealth = maxHealth;
        }

        private void Start()
        {
            _spawnPos = transform.position;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _player = player.transform;
            SetState(AIState.Patrol);
        }

        private void Update()
        {
            if (_isDead) return;

            _stateTimer += Time.deltaTime;
            _attackTimer -= Time.deltaTime;

            switch (_state)
            {
                case AIState.Idle:    UpdateIdle(); break;
                case AIState.Patrol:  UpdatePatrol(); break;
                case AIState.Chase:   UpdateChase(); break;
                case AIState.Attack:  UpdateAttack(); break;
                case AIState.Flee:    UpdateFlee(); break;
            }
        }

        // ------------------------------------------------------------------
        // State Machine
        // ------------------------------------------------------------------
        private void SetState(AIState newState)
        {
            _state = newState;
            _stateTimer = 0f;

            switch (newState)
            {
                case AIState.Idle:
                    _rb.linearVelocity = Vector2.zero;
                    break;
                case AIState.Patrol:
                    _patrolTarget = _spawnPos + new Vector3(
                        Random.Range(-patrolRadius, patrolRadius),
                        0,
                        Random.Range(-patrolRadius, patrolRadius)
                    );
                    break;
            }
        }

        // ------------------------------------------------------------------
        // State Updates
        // ------------------------------------------------------------------
        private void UpdateIdle()
        {
            if (_stateTimer > patrolWaitTime)
                SetState(AIState.Patrol);

            if (CheckPlayerNearby())
                ReactToPlayer();
        }

        private void UpdatePatrol()
        {
            Vector3 dir = (_patrolTarget - transform.position);
            if (dir.magnitude < 1f)
            {
                SetState(AIState.Idle);
                return;
            }

            MoveToward(_patrolTarget, moveSpeed);

            if (CheckPlayerNearby())
                ReactToPlayer();
        }

        private void UpdateChase()
        {
            if (_player == null) { SetState(AIState.Patrol); return; }

            float dist = Vector3.Distance(transform.position, _player.position);

            // Give up if too far
            if (dist > leashRange)
            {
                SetState(AIState.Patrol);
                return;
            }

            // Attack if close enough
            if (dist <= attackRange)
            {
                SetState(AIState.Attack);
                return;
            }

            MoveToward(_player.position, runSpeed);
        }

        private void UpdateAttack()
        {
            if (_player == null) { SetState(AIState.Patrol); return; }

            float dist = Vector3.Distance(transform.position, _player.position);

            // Player moved out of range
            if (dist > attackRange * 1.5f)
            {
                SetState(AIState.Chase);
                return;
            }

            // Attack on cooldown
            if (_attackTimer <= 0f)
            {
                PerformAttack();
                _attackTimer = attackCooldown;
            }

            // Check flee
            if (HealthPercent <= fleeThreshold)
                SetState(AIState.Flee);
        }

        private void UpdateFlee()
        {
            if (_player == null) { SetState(AIState.Patrol); return; }

            Vector3 fleeDir = (transform.position - _player.position).normalized;
            Vector3 fleeTarget = transform.position + fleeDir * 20f;
            MoveToward(fleeTarget, runSpeed);

            // Stop fleeing if far enough
            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist > leashRange)
                SetState(AIState.Patrol);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------
        private bool CheckPlayerNearby()
        {
            if (_player == null) return false;
            return Vector3.Distance(transform.position, _player.position) <= detectionRange;
        }

        private void ReactToPlayer()
        {
            if (_player == null) return;

            // Passive animals flee
            if (aggression == AggressionLevel.Passive)
            {
                if (alwaysFleeIfPassive)
                    SetState(AIState.Flee);
                return;
            }

            // Aggressive animals chase
            if (aggression == AggressionLevel.Aggressive)
            {
                SetState(AIState.Chase);
                return;
            }

            // Neutral: only chase if player is very close
            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist < detectionRange * 0.5f)
                SetState(AIState.Chase);
        }

        private void MoveToward(Vector3 target, float speed)
        {
            Vector2 dir = ((Vector2)target - (Vector2)transform.position).normalized;
            _rb.linearVelocity = dir * speed;

            // Flip sprite based on direction
            if (spriteRenderer != null && dir.x != 0f)
                spriteRenderer.flipX = dir.x < 0f;
        }

        private void PerformAttack()
        {
            if (_player == null) return;

            // Apply damage to player's SurvivalStats
            var stats = _player.GetComponent<Survival.SurvivalStats>();
            if (stats != null)
                stats.TakeDamage(damage);

            Debug.Log($"[{animalName}] attacked player for {damage} damage.");
        }

        // ------------------------------------------------------------------
        // Damage & Death
        // ------------------------------------------------------------------
        public void TakeDamage(float amount)
        {
            if (_isDead) return;

            currentHealth -= amount;
            Debug.Log($"[{animalName}] took {amount} damage. HP: {currentHealth}/{maxHealth}");

            // Aggro on hit
            if (_state == AIState.Patrol || _state == AIState.Idle)
            {
                if (aggression != AggressionLevel.Passive)
                    SetState(AIState.Chase);
                else
                    SetState(AIState.Flee);
            }

            // Flee if low health
            if (HealthPercent <= fleeThreshold && _state != AIState.Flee)
                SetState(AIState.Flee);

            if (currentHealth <= 0f)
                Die();
        }

        private void Die()
        {
            _isDead = true;
            _state = AIState.Dead;
            _rb.linearVelocity = Vector2.zero;

            // Drop loot
            var dropper = GetComponent<LootDropper>();
            if (dropper != null) dropper.DropLoot();

            OnDeath?.Invoke(this);
            Core.EventManager.TriggerEvent(Core.GameEvents.AnimalKilled, this);

            // Disable collider and fade out
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            // Destroy after delay
            Destroy(gameObject, 10f);
        }
    }
}
