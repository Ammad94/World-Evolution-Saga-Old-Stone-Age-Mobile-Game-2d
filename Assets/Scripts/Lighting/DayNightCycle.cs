using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.Rendering.Universal;
using PrehistoricSurvival.Core;

namespace PrehistoricSurvival.Lighting
{
    /// <summary>
    /// Controls the day/night cycle by interpolating URP Global Light 2D
    /// color and intensity based on time of day.
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("URP Global Light 2D component.")]
        public Light2D globalLight;

        [Tooltip("Directional shadow caster (rotates to simulate sun position).")]
        public Transform shadowCaster;

        [Header("Colors")]
        public Color dawnColor = new Color(1f, 0.8f, 0.6f);
        public Color dayColor = new Color(1f, 0.95f, 0.85f);
        public Color sunsetColor = new Color(1f, 0.6f, 0.3f);
        public Color nightColor = new Color(0.1f, 0.15f, 0.3f);

        [Header("Intensity")]
        public float dayIntensity = 1f;
        public float nightIntensity = 0.2f;

        [Header("Shadow Rotation")]
        [Tooltip("Speed of shadow rotation (degrees per in-game hour).")]
        public float shadowRotationSpeed = 15f;

        private Survival.SeasonManager _seasonMgr;

        private void Start()
        {
            _seasonMgr = Survival.SeasonManager.Instance;

            if (globalLight == null)
                globalLight = FindFirstObjectByType<Light2D>();
        }

        private void Update()
        {
            if (_seasonMgr == null) return;
            if (globalLight == null) return;

            float time = _seasonMgr.TimeOfDay;

            // Interpolate color based on time of day
            Color targetColor = GetColorForTime(time);
            globalLight.color = Color.Lerp(globalLight.color, targetColor, Time.deltaTime * 2f);

            // Interpolate intensity
            float targetIntensity = GetIntensityForTime(time);
            globalLight.intensity = Mathf.Lerp(globalLight.intensity, targetIntensity, Time.deltaTime * 2f);

            // Rotate shadow caster to simulate sun position
            if (shadowCaster != null)
            {
                float sunAngle = time * 360f - 90f; // 0.25 = sunrise (east), 0.75 = sunset (west)
                shadowCaster.rotation = Quaternion.Euler(0f, 0f, sunAngle);
            }

            // Fire event
            EventManager.TriggerEvent(GameEvents.DayNightChanged, time);
        }

        private Color GetColorForTime(float time)
        {
            // 0.0 = midnight, 0.25 = dawn, 0.5 = noon, 0.75 = dusk
            if (time < 0.2f)
                return nightColor;
            else if (time < 0.3f)
                return Color.Lerp(nightColor, dawnColor, (time - 0.2f) / 0.1f);
            else if (time < 0.4f)
                return Color.Lerp(dawnColor, dayColor, (time - 0.3f) / 0.1f);
            else if (time < 0.6f)
                return dayColor;
            else if (time < 0.7f)
                return Color.Lerp(dayColor, sunsetColor, (time - 0.6f) / 0.1f);
            else if (time < 0.8f)
                return Color.Lerp(sunsetColor, nightColor, (time - 0.7f) / 0.1f);
            else
                return nightColor;
        }

        private float GetIntensityForTime(float time)
        {
            if (time < 0.25f || time > 0.75f)
                return nightIntensity;
            else if (time < 0.35f)
                return Mathf.Lerp(nightIntensity, dayIntensity, (time - 0.25f) / 0.1f);
            else if (time > 0.65f)
                return Mathf.Lerp(dayIntensity, nightIntensity, (time - 0.65f) / 0.1f);
            else
                return dayIntensity;
        }
    }
}
