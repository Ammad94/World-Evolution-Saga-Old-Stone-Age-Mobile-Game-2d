using UnityEngine;

namespace WorldEvolution
{
    /// <summary>
    /// Global time-of-day + weather state for the Stone Age scene. Read
    /// every frame by <see cref="BillboardCharacter"/> (and any future
    /// surface that wants to react to the same state) so the entire
    /// scene shares one consistent look.
    ///
    /// Designed to be easy to extend later:
    ///  - add a new <see cref="WeatherKind"/> value (e.g. Fog)
    ///  - add a matching public property
    ///  - drive it from a weather system / cutscene / save game
    ///  - the billboard character picks it up automatically because
    ///    BillboardCharacter.LateUpdate reads every property every
    ///    frame and pushes it into the material.
    ///
    /// The instance is created on demand via
    /// <see cref="EnsureExists"/> so any code that runs before the
    /// first scene load still has a valid singleton to query. This
    /// also makes it usable from editor-only code without manual
    /// setup.
    /// </summary>
    [DisallowMultipleComponent]
    public class TimeOfDayWeather : MonoBehaviour
    {
        public static TimeOfDayWeather Instance { get; private set; }

        /// <summary>
        /// Returns the existing instance, creating an in-memory one
        /// (not saved with the scene) if none exists. Use this from
        /// Awake / OnEnable / runtime code so the singleton is always
        /// available.
        /// </summary>
        public static TimeOfDayWeather EnsureExists()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<TimeOfDayWeather>();
            if (existing != null) { Instance = existing; return Instance; }
            var go = new GameObject("TimeOfDayWeather (auto)");
            Instance = go.AddComponent<TimeOfDayWeather>();
            return Instance;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public enum WeatherKind
        {
            Clear,
            Overcast,
            Rain,
            Storm,
            Fog,
        }

        [Header("Time of day (0 = midnight, 0.5 = noon, 1 = next midnight)")]
        [Range(0f, 1f)] public float timeOfDay = 0.30f;

        [Tooltip("Days per real-time second. 0 = frozen (manual edit).")]
        public float daySpeed = 0.005f;

        [Header("Weather")]
        public WeatherKind weather = WeatherKind.Clear;

        [Tooltip("0 = bone dry, 1 = soaking wet. Driven by the weather " +
                 "(Rain = 0.7, Storm = 1.0) and the character's wetness " +
                 "settings; you can also override by hand.")]
        [Range(0f, 1f)] public float Wetness = 0f;

        [Tooltip("Strength of the wet-shine highlight on the cloth + hair.")]
        [Range(0f, 0.5f)] public float WetShine = 0.18f;

        [Header("Derived (read-only — computed from the above)")]
        [Range(0f, 2f)]  public float Daylight = 1f;
        public Color AmbientTint = Color.white;
        [Range(0f, 1f)]  public float Darkness = 0f;

        void Update()
        {
            if (daySpeed > 0f)
            {
                timeOfDay = (timeOfDay + daySpeed * Time.deltaTime) % 1f;
            }
            Recompute();
        }

        /// <summary>
        /// Pushes the current time-of-day + weather into the public
        /// "derived" properties (<see cref="Daylight"/>,
        /// <see cref="AmbientTint"/>, <see cref="Wetness"/>,
        /// <see cref="Darkness"/>). Idempotent — call this after you
        /// edit <see cref="timeOfDay"/> or <see cref="weather"/> by
        /// hand and the new state will show up immediately.
        /// </summary>
        public void Recompute()
        {
            // Convert time-of-day into a sun-elevation curve.
            // 0.0 = midnight, 0.25 = sunrise, 0.5 = noon, 0.75 = sunset.
            // daylight peaks at noon, dips to 0 at midnight, with a soft
            // shoulder at sunrise/sunset so dusk and dawn look natural.
            float tod = Mathf.Clamp01(timeOfDay);
            // sunElev in -1..1: 1 at noon, -1 at midnight, 0 at dawn/dusk
            float sunElev = Mathf.Sin(Mathf.PI * tod);
            // 0.18 = moon brightness floor; 0.97 amplitude so noon peaks at 1.15
            Daylight = Mathf.Clamp(0.18f + 0.97f * sunElev, 0f, 2f);

            // Tint: cool blue at night, warm white at noon, pinkish at dusk.
            // We map the day into 4 phases: night -> dawn -> day -> dusk
            // and lerp between them in order.
            Color night = new Color(0.18f, 0.24f, 0.42f, 1f);  // cold blue
            Color dawn  = new Color(0.85f, 0.55f, 0.40f, 1f);  // pinkish orange
            Color day   = new Color(1.00f, 0.97f, 0.92f, 1f);  // warm white
            Color dusk  = new Color(0.70f, 0.45f, 0.45f, 1f);  // muted pink-red

            // 0.00 - 0.20: night -> dawn
            // 0.20 - 0.50: dawn -> day
            // 0.50 - 0.70: day -> dusk
            // 0.70 - 1.00: dusk -> night
            Color tod2;
            if (tod < 0.20f)
                tod2 = Color.Lerp(night, dawn, Mathf.InverseLerp(0.00f, 0.20f, tod));
            else if (tod < 0.50f)
                tod2 = Color.Lerp(dawn,  day,  Mathf.InverseLerp(0.20f, 0.50f, tod));
            else if (tod < 0.70f)
                tod2 = Color.Lerp(day,   dusk, Mathf.InverseLerp(0.50f, 0.70f, tod));
            else
                tod2 = Color.Lerp(dusk,  night, Mathf.InverseLerp(0.70f, 1.00f, tod));
            AmbientTint = tod2;

            // Wetness + darkness from weather.
            switch (weather)
            {
                case WeatherKind.Clear:    Wetness = 0f;  Darkness = 0f; break;
                case WeatherKind.Overcast: Wetness = 0.05f; Darkness = 0.05f; break;
                case WeatherKind.Rain:     Wetness = 0.7f;  Darkness = 0.15f; break;
                case WeatherKind.Storm:    Wetness = 1.0f;  Darkness = 0.30f; break;
                case WeatherKind.Fog:      Wetness = 0.2f;  Darkness = 0.10f; break;
            }
        }

        // ---- convenience API for other systems -----------------------

        /// <summary>Force the weather to a specific kind, with optional
        /// smoothing (so cinematics can blend rain in gradually).</summary>
        public void SetWeather(WeatherKind w, bool instant = true)
        {
            weather = w;
            if (instant) Recompute();
        }

        /// <summary>Freeze the day-night cycle at a given time. daySpeed
        /// is set to 0 so Update() does not advance it.</summary>
        public void SetTimeOfDay(float t)
        {
            timeOfDay = Mathf.Repeat(t, 1f);
            daySpeed = 0f;
            Recompute();
        }
    }
}
