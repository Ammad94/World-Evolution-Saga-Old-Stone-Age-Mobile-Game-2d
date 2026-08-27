using UnityEngine;
using PrehistoricSurvival.Core;

namespace PrehistoricSurvival.Survival
{
    /// <summary>Temperature exposure layer for clothing, weather and shelter gameplay.</summary>
    public class TemperatureSystem : MonoBehaviour
    {
        [Range(-30f, 50f)] public float bodyTemperature = 37f;
        public float comfortMinimum = 35.5f;
        public float comfortMaximum = 38.5f;
        public float exposureRate = 0.08f;
        public float shelterWarmth;
        public float clothingWarmth;
        public bool IsHypothermic => bodyTemperature < comfortMinimum;
        public bool IsOverheated => bodyTemperature > comfortMaximum;
        public float Comfort01 => Mathf.Clamp01(1f - Mathf.Abs(bodyTemperature - 37f) / 4f);

        private SurvivalStats _stats;
        private float _tick;

        private void Start() { _stats = GetComponent<SurvivalStats>(); }
        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;
            _tick += Time.deltaTime; if (_tick < 1f) return; _tick = 0f;
            var weather = WeatherController.Instance;
            float target = 22f;
            if (weather != null)
            {
                switch (weather.CurrentWeather) { case WeatherType.Snow: target = -8f; break; case WeatherType.Rain: target = 12f; break; case WeatherType.Storm: target = 5f; break; }
            }
            bodyTemperature = Mathf.MoveTowards(bodyTemperature, 37f + (target - 22f) * 0.08f + clothingWarmth + shelterWarmth, exposureRate);
            if (_stats != null && (IsHypothermic || IsOverheated)) _stats.Health -= (1f - Comfort01) * 0.25f;
        }
    }
}
