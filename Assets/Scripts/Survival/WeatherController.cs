using System;
using UnityEngine;
using PrehistoricSurvival.Core;

namespace PrehistoricSurvival.Survival
{
    public enum WeatherType { Clear, Cloudy, Rain, Storm, Snow, Fog }

    /// <summary>
    /// Controls dynamic weather based on season and biome.
    /// Activates particle effects and modifies environment properties.
    /// </summary>
    public class WeatherController : MonoBehaviour
    {
        public static WeatherController Instance { get; private set; }

        [Header("Weather Systems")]
        public ParticleSystem rainSystem;
        public ParticleSystem stormSystem;
        public ParticleSystem snowSystem;
        public ParticleSystem fogSystem;

        [Header("Audio")]
        public AudioClip rainAmbience;
        public AudioClip stormAmbience;
        public AudioClip windAmbience;

        [Header("Settings")]
        [Tooltip("How often weather can change (seconds).")]
        public float weatherChangeInterval = 300f;

        [Tooltip("Probability of bad weather (0-1).")]
        public float badWeatherChance = 0.3f;

        private WeatherType _currentWeather = WeatherType.Clear;
        private float _weatherTimer;
        private AudioSource _audioSource;

        public WeatherType CurrentWeather => _currentWeather;

        public event Action<WeatherType> OnWeatherChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.loop = true;
        }

        private void Start()
        {
            SetWeather(WeatherType.Clear);

            // Listen for season changes
            if (SeasonManager.Instance != null)
                SeasonManager.Instance.OnSeasonChanged += OnSeasonChanged;
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

            _weatherTimer += Time.deltaTime;
            if (_weatherTimer >= weatherChangeInterval)
            {
                _weatherTimer = 0f;
                RandomizeWeather();
            }
        }

        private void OnSeasonChanged(Season season)
        {
            RandomizeWeather();
        }

        // ------------------------------------------------------------------
        // Weather Logic
        // ------------------------------------------------------------------
        private void RandomizeWeather()
        {
            if (Random.value > badWeatherChance)
            {
                SetWeather(WeatherType.Clear);
                return;
            }

            var seasonMgr = SeasonManager.Instance;
            if (seasonMgr == null) return;

            switch (seasonMgr.CurrentSeason)
            {
                case Season.Spring:
                    SetWeather(Random.value < 0.5f ? WeatherType.Rain : WeatherType.Cloudy);
                    break;
                case Season.Summer:
                    SetWeather(Random.value < 0.3f ? WeatherType.Storm : WeatherType.Clear);
                    break;
                case Season.Autumn:
                    SetWeather(Random.value < 0.4f ? WeatherType.Fog : WeatherType.Cloudy);
                    break;
                case Season.Winter:
                    SetWeather(Random.value < 0.6f ? WeatherType.Snow : WeatherType.Cloudy);
                    break;
            }
        }

        public void SetWeather(WeatherType weather)
        {
            _currentWeather = weather;

            // Stop all particles first
            StopAllWeatherParticles();

            // Activate appropriate system
            switch (weather)
            {
                case WeatherType.Clear:
                case WeatherType.Cloudy:
                    break;
                case WeatherType.Rain:
                    if (rainSystem != null) rainSystem.Play();
                    PlayAmbience(rainAmbience);
                    break;
                case WeatherType.Storm:
                    if (stormSystem != null) stormSystem.Play();
                    if (rainSystem != null) rainSystem.Play();
                    PlayAmbience(stormAmbience);
                    break;
                case WeatherType.Snow:
                    if (snowSystem != null) snowSystem.Play();
                    PlayAmbience(windAmbience);
                    break;
                case WeatherType.Fog:
                    if (fogSystem != null) fogSystem.Play();
                    break;
            }

            OnWeatherChanged?.Invoke(weather);
            EventManager.TriggerEvent(GameEvents.WeatherChanged, weather);
        }

        private void StopAllWeatherParticles()
        {
            if (rainSystem != null) rainSystem.Stop();
            if (stormSystem != null) stormSystem.Stop();
            if (snowSystem != null) snowSystem.Stop();
            if (fogSystem != null) fogSystem.Stop();
        }

        private void PlayAmbience(AudioClip clip)
        {
            if (clip == null || _audioSource == null) return;
            if (_audioSource.clip == clip && _audioSource.isPlaying) return;
            _audioSource.clip = clip;
            _audioSource.Play();
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        /// <summary>Is visibility reduced (fog or storm)?</summary>
        public bool IsLowVisibility =>
            _currentWeather == WeatherType.Fog || _currentWeather == WeatherType.Storm;

        /// <summary>Is the player getting wet?</summary>
        public bool IsWet =>
            _currentWeather == WeatherType.Rain || _currentWeather == WeatherType.Storm;
    }
}
