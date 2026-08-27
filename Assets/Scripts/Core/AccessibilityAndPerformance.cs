using UnityEngine;

namespace PrehistoricSurvival.Core
{
    /// <summary>Central user preferences for accessibility, haptics and mobile quality.</summary>
    public class AccessibilityAndPerformance : MonoBehaviour
    {
        public static AccessibilityAndPerformance Instance { get; private set; }
        public bool haptics = true;
        public bool reducedMotion;
        public bool highContrast;
        public int qualityLevel = 1;
        public bool batterySaver;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
            haptics = PlayerPrefs.GetInt("access_haptics", 1) == 1;
            reducedMotion = PlayerPrefs.GetInt("access_reduced_motion", 0) == 1;
            highContrast = PlayerPrefs.GetInt("access_high_contrast", 0) == 1;
            batterySaver = PlayerPrefs.GetInt("battery_saver", 0) == 1;
            qualityLevel = Mathf.Clamp(PlayerPrefs.GetInt("quality_level", 1), 0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(qualityLevel, true);
            Application.targetFrameRate = batterySaver ? 30 : 60;
        }

        public void SetHaptics(bool enabled) { haptics = enabled; PlayerPrefs.SetInt("access_haptics", enabled ? 1 : 0); }
        public void SetReducedMotion(bool enabled) { reducedMotion = enabled; PlayerPrefs.SetInt("access_reduced_motion", enabled ? 1 : 0); }
        public void SetHighContrast(bool enabled) { highContrast = enabled; PlayerPrefs.SetInt("access_high_contrast", enabled ? 1 : 0); }
        public void SetBatterySaver(bool enabled) { batterySaver = enabled; Application.targetFrameRate = enabled ? 30 : 60; PlayerPrefs.SetInt("battery_saver", enabled ? 1 : 0); }
        public static void Vibrate() { if (Instance != null && Instance.haptics && !Instance.reducedMotion) Handheld.Vibrate(); }
    }
}
