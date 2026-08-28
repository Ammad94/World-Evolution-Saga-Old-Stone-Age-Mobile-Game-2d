using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.Rendering.Universal;

namespace PrehistoricSurvival.Lighting
{
    /// <summary>
    /// Flickering light source for torches, campfires, and stone fat lamps.
    /// Uses URP Point Light 2D with random intensity variation.
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public class TorchLight : MonoBehaviour
    {
        public enum LightType { Torch, Campfire, StoneFatLamp }

        [Header("Light Type")]
        public LightType type = LightType.Torch;

        [Header("Flicker Settings")]
        [Tooltip("Base intensity of the light.")]
        public float baseIntensity = 1f;
        [Tooltip("Maximum random variation (±).")]
        public float flickerAmount = 0.2f;
        [Tooltip("How fast the flicker changes.")]
        public float flickerSpeed = 10f;

        [Header("Range")]
        [Tooltip("Base point light radius.")]
        public float baseRadius = 5f;
        [Tooltip("Radius variation.")]
        public float radiusVariation = 0.5f;

        [Header("Fuel System")]
        [Tooltip("Whether this light consumes fuel over time.")]
        public bool usesFuel = true;
        [Tooltip("Seconds of burn time.")]
        public float maxFuel = 600f; // 10 minutes

        private Light2D _light;
        private float _fuel;
        private float _flickerTimer;
        private bool _isLit = true;

        private void Awake()
        {
            _light = GetComponent<Light2D>();
            _fuel = maxFuel;
        }

        private void Start()
        {
            if (_light != null)
            {
                _light.intensity = baseIntensity;
                _light.pointLightOuterRadius = baseRadius;
            }
        }

        private void Update()
        {
            if (!_isLit || _light == null) return;

            // Fuel consumption
            if (usesFuel)
            {
                _fuel -= Time.deltaTime;
                if (_fuel <= 0f)
                {
                    Extinguish();
                    return;
                }

                // Dim as fuel runs low
                float fuelFraction = _fuel / maxFuel;
                if (fuelFraction < 0.2f)
                    baseIntensity = Mathf.Lerp(0.3f, 1f, fuelFraction / 0.2f);
            }

            // Flicker effect
            _flickerTimer += Time.deltaTime * flickerSpeed;
            float flicker = Mathf.PerlinNoise(_flickerTimer, 0f) * 2f - 1f;

            _light.intensity = baseIntensity + flicker * flickerAmount;
            _light.pointLightOuterRadius = baseRadius + flicker * radiusVariation;
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>Light the torch/fire.</summary>
        public void Ignite()
        {
            _isLit = true;
            if (_light != null) _light.enabled = true;
        }

        /// <summary>Extinguish the light.</summary>
        public void Extinguish()
        {
            _isLit = false;
            if (_light != null) _light.enabled = false;
        }

        /// <summary>Refuel the light.</summary>
        public void AddFuel(float amount)
        {
            _fuel = Mathf.Min(_fuel + amount, maxFuel);
        }

        /// <summary>Is the light currently burning?</summary>
        public bool IsLit => _isLit;

        /// <summary>Remaining fuel fraction (0..1).</summary>
        public float FuelFraction => _fuel / maxFuel;
    }
}
