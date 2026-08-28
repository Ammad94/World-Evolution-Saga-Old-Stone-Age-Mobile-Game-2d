using UnityEngine;
using PrehistoricSurvival.Core;
using PrehistoricSurvival.Survival;

namespace PrehistoricSurvival.Audio
{
    /// <summary>Weather-driven ambience layer + thunder one-shots during storms.</summary>
    public class WeatherAudio : MonoBehaviour
    {
        private static WeatherAudio _instance;
        public static WeatherAudio Instance => _instance;

        private AudioSource _weatherSource;
        private WeatherType _current = WeatherType.Clear;
        private float _thunderTimer;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            _weatherSource = gameObject.AddComponent<AudioSource>();
            _weatherSource.loop = true;
            _weatherSource.playOnAwake = false;
            _weatherSource.volume = 0f;
            EventManager.Subscribe(GameEvents.WeatherChanged, OnWeatherChanged);
        }

        private void OnDestroy()
        {
            EventManager.Unsubscribe(GameEvents.WeatherChanged, OnWeatherChanged);
            if (_instance == this) _instance = null;
        }

        private void OnWeatherChanged(object payload)
        {
            if (payload is WeatherType wt) SetWeather(wt);
        }

        private void SetWeather(WeatherType wt)
        {
            _current = wt;
            if (AudioManager.Instance == null) return;
            string clipPath = wt switch
            {
                WeatherType.Rain => "sfx/rain_loop",
                WeatherType.Storm => "sfx/rain_heavy_loop",
                WeatherType.Snow => "sfx/wind_gust",
                WeatherType.Fog => "sfx/wind_loop",
                _ => null,
            };
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeTo(AudioManager.Clip(clipPath)));
        }

        private Coroutine _fadeRoutine;

        private System.Collections.IEnumerator FadeTo(AudioClip clip)
        {
            float start = _weatherSource.volume;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 2.5f)
            { _weatherSource.volume = Mathf.Lerp(start, 0f, t); yield return null; }
            _weatherSource.clip = clip;
            if (clip != null)
            {
                _weatherSource.Play();
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 2.5f)
                { _weatherSource.volume = Mathf.Lerp(0f, 0.55f, t); yield return null; }
            }
            _weatherSource.volume = clip != null ? 0.55f : 0f;
        }

        private void Update()
        {
            if (_current != WeatherType.Storm) return;
            _thunderTimer -= Time.unscaledDeltaTime;
            if (_thunderTimer <= 0f && AudioManager.Instance != null)
            {
                _thunderTimer = Random.Range(9f, 22f);
                AudioManager.Instance.Play("thunder_" + Random.Range(0, 3), 0.8f);
            }
        }
    }

}
