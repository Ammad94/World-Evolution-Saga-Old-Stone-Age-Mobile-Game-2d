using System;
using UnityEngine;
using PrehistoricSurvival.Core;

namespace PrehistoricSurvival.Survival
{
    /// <summary>
    /// Tracks player survival stats: Health, Hunger, Thirst, Energy, Stamina.
    /// Stats drain over time and can be restored by consuming items.
    /// </summary>
    public class SurvivalStats : MonoBehaviour
    {
        [Header("Stats (0-100)")]
        [SerializeField] private float _health = 100f;
        [SerializeField] private float _hunger = 100f;
        [SerializeField] private float _thirst = 100f;
        [SerializeField] private float _energy = 100f;
        [SerializeField] private float _stamina = 100f;

        [Header("Drain Rates (per second)")]
        public float hungerDrain = 0.15f;
        public float thirstDrain = 0.25f;
        public float energyDrain = 0.1f;
        public float staminaDrainRunning = 1.5f;
        public float staminaRegenRate = 2f;

        [Header("Penalties")]
        [Tooltip("Health drain when hunger is zero.")]
        public float starvationDamage = 0.5f;
        [Tooltip("Health drain when thirst is zero.")]
        public float dehydrationDamage = 0.8f;

        [Header("Thresholds")]
        public float lowStatThreshold = 20f;

        // Events
        public event Action<string, float> OnStatChanged;
        public event Action OnPlayerDeath;

        // Properties
        public float Health { get => _health; set { _health = Mathf.Clamp(value, 0f, 100f); OnStatChanged?.Invoke("Health", _health); } }
        public float Hunger { get => _hunger; set { _hunger = Mathf.Clamp(value, 0f, 100f); OnStatChanged?.Invoke("Hunger", _hunger); } }
        public float Thirst { get => _thirst; set { _thirst = Mathf.Clamp(value, 0f, 100f); OnStatChanged?.Invoke("Thirst", _thirst); } }
        public float Energy { get => _energy; set { _energy = Mathf.Clamp(value, 0f, 100f); OnStatChanged?.Invoke("Energy", _energy); } }
        public float Stamina { get => _stamina; set { _stamina = Mathf.Clamp(value, 0f, 100f); OnStatChanged?.Invoke("Stamina", _stamina); } }

        public bool IsAlive => _health > 0f;
        public bool IsStarving => _hunger <= 0f;
        public bool IsDehydrated => _thirst <= 0f;
        public bool IsExhausted => _energy <= 0f;

        private Player.PlayerController _player;
        private SeasonManager _seasonMgr;
        private bool _isRunning;

        private void Start()
        {
            _player = GetComponent<Player.PlayerController>();
            _seasonMgr = SeasonManager.Instance;
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;
            if (!IsAlive) return;

            float dt = Time.deltaTime;

            // Season modifiers
            float thirstMult = _seasonMgr != null ? _seasonMgr.ThirstDrainMultiplier : 1f;
            float energyMult = _seasonMgr != null ? _seasonMgr.EnergyDrainMultiplier : 1f;

            // Drain stats
            Hunger -= hungerDrain * dt;
            Thirst -= thirstDrain * thirstMult * dt;
            Energy -= energyDrain * energyMult * dt;

            // Stamina: drain when moving, regen when idle
            if (_player != null && _player.IsMoving)
            {
                _isRunning = Input.GetKey(KeyCode.LeftShift);
                if (_isRunning)
                    Stamina -= staminaDrainRunning * dt;
            }
            else
            {
                Stamina += staminaRegenRate * dt;
            }

            // Penalties
            if (IsStarving)
                Health -= starvationDamage * dt;
            if (IsDehydrated)
                Health -= dehydrationDamage * dt;

            // Death check
            if (!IsAlive)
            {
                OnPlayerDeath?.Invoke();
                EventManager.TriggerEvent(GameEvents.PlayerDied);
                if (GameManager.Instance != null)
                    GameManager.Instance.TriggerGameOver();
            }
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>Restore stats from a consumable.</summary>
        public void Consume(float hunger, float thirst, float health, float energy)
        {
            Hunger += hunger;
            Thirst += thirst;
            Health += health;
            Energy += energy;
        }

        /// <summary>Apply damage to health.</summary>
        public void TakeDamage(float amount)
        {
            Health -= amount;
        }

        /// <summary>Heal health.</summary>
        public void Heal(float amount)
        {
            Health += amount;
        }

        /// <summary>Can the player sprint (has enough stamina)?</summary>
        public bool CanSprint()
        {
            return _stamina > 5f;
        }
    }
}
