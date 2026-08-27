using System;
using UnityEngine;
using PrehistoricSurvival.Core;

namespace PrehistoricSurvival.Survival
{
    public enum Season { Spring, Summer, Autumn, Winter }

    /// <summary>
    /// Manages the 4-season cycle, day/night time progression, and fires
    /// events when seasons or days change.
    /// </summary>
    public class SeasonManager : MonoBehaviour
    {
        public static SeasonManager Instance { get; private set; }

        [Header("Time Settings")]
        [Tooltip("Real-world seconds per in-game day.")]
        public float dayDuration = 600f; // 10 minutes

        [Tooltip("Starting time of day (0 = midnight, 0.5 = noon).")]
        [Range(0f, 1f)]
        public float startTimeOfDay = 0.25f; // 6 AM

        [Header("Season Settings")]
        [Tooltip("Number of in-game days per season.")]
        public int daysPerSeason = 7;

        [Header("Particle Systems")]
        [Tooltip("Autumn falling leaves.")]
        public ParticleSystem autumnLeaves;
        [Tooltip("Winter snowfall.")]
        public ParticleSystem winterSnow;
        [Tooltip("Spring light rain.")]
        public ParticleSystem springRain;

        [Header("Visual Tints")]
        public Color autumnTint = new Color(1f, 0.7f, 0.3f);
        public Color summerHeatTint = new Color(1f, 0.95f, 0.85f);
        public Color winterTint = new Color(0.9f, 0.95f, 1f);
        public Color springTint = Color.white;

        // --- State ---
        private float _timeOfDay;
        private int _dayNumber;
        private Season _currentSeason;

        public float TimeOfDay
        {
            get => _timeOfDay;
            set => _timeOfDay = Mathf.Repeat(value, 1f);
        }
        public int DayNumber
        {
            get => _dayNumber;
            set => _dayNumber = value;
        }
        public Season CurrentSeason => _currentSeason;

        // Events
        public event Action<Season> OnSeasonChanged;
        public event Action<int> OnDayChanged;
        public event Action<float> OnTimeChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            _timeOfDay = startTimeOfDay;
            _dayNumber = 1;
            _currentSeason = Season.Spring;
            UpdateSeasonVisuals();
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

            float prevDay = _timeOfDay;
            _timeOfDay += Time.deltaTime / dayDuration;

            if (_timeOfDay >= 1f)
            {
                _timeOfDay -= 1f;
                _dayNumber++;
                OnDayChanged?.Invoke(_dayNumber);

                // Check season change
                if (_dayNumber % daysPerSeason == 0)
                {
                    _currentSeason = (Season)(((int)_currentSeason + 1) % 4);
                    OnSeasonChanged?.Invoke(_currentSeason);
                    UpdateSeasonVisuals();
                    EventManager.TriggerEvent(GameEvents.SeasonChanged, _currentSeason);
                }
            }

            OnTimeChanged?.Invoke(_timeOfDay);
        }

        // ------------------------------------------------------------------
        // Season Visuals
        // ------------------------------------------------------------------
        private void UpdateSeasonVisuals()
        {
            // Particle systems
            SetParticleActive(autumnLeaves, _currentSeason == Season.Autumn);
            SetParticleActive(winterSnow, _currentSeason == Season.Winter);
            SetParticleActive(springRain, _currentSeason == Season.Spring);

            // Reload chunks to apply new tints
            if (World.ChunkManager.Instance != null)
                World.ChunkManager.Instance.ForceReloadAll();

            // Notify footprint system about snow
            var footprints = FindObjectOfType<Player.FootprintSystem>();
            if (footprints != null)
                footprints.SetSnowMode(_currentSeason == Season.Winter);
        }

        private void SetParticleActive(ParticleSystem ps, bool active)
        {
            if (ps == null) return;
            if (active && !ps.isPlaying) ps.Play();
            else if (!active && ps.isPlaying) ps.Stop();
        }

        /// <summary>Force-set the current season (for save/load or debug).</summary>
        public void SetSeason(Season season)
        {
            _currentSeason = season;
            UpdateSeasonVisuals();
        }

        // ------------------------------------------------------------------
        // Season Modifiers
        // ------------------------------------------------------------------

        /// <summary>Get the thirst drain multiplier for the current season.</summary>
        public float ThirstDrainMultiplier =>
            _currentSeason == Season.Summer ? 2f : 1f;

        /// <summary>Get the energy drain multiplier for the current season.</summary>
        public float EnergyDrainMultiplier =>
            _currentSeason == Season.Summer ? 2f : 1f;

        /// <summary>Get the movement speed multiplier for the current season.</summary>
        public float MovementSpeedMultiplier =>
            _currentSeason == Season.Winter ? 0.6f : 1f;

        /// <summary>Is it currently nighttime?</summary>
        public bool IsNight => _timeOfDay < 0.25f || _timeOfDay > 0.75f;

        /// <summary>Get current hour (0-23).</summary>
        public int CurrentHour => Mathf.FloorToInt(_timeOfDay * 24f);

        /// <summary>Get formatted time string (HH:MM).</summary>
        public string TimeString
        {
            get
            {
                int h = CurrentHour;
                int m = Mathf.FloorToInt((_timeOfDay * 24f - h) * 60f);
                return $"{h:D2}:{m:D2}";
            }
        }
    }
}
