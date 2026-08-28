using System.Collections;
using UnityEngine;
using PrehistoricSurvival.Core;

namespace PrehistoricSurvival.Player
{
    /// <summary>Impact feedback shared by melee combat: haptics, short hit-stop and camera shake hook.</summary>
    public class CombatFeedback : MonoBehaviour
    {
        public static CombatFeedback Instance { get; private set; }
        public float hitStopDuration = 0.045f;
        public float shakeStrength = 0.05f;
        private Camera _camera;
        private Vector3 _cameraOrigin;

        private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; _camera = Camera.main; }
        public void Impact(bool heavy = false)
        {
            AccessibilityAndPerformance.Vibrate();
            // Route through the shared GameFeel system (hit-stop + camera trauma).
            PrehistoricSurvival.Feedback.GameFeel.HitStop(heavy ? hitStopDuration * 1.5f : hitStopDuration);
            PrehistoricSurvival.Feedback.GameFeel.Shake(heavy ? 0.55f : 0.3f);
        }
        private IEnumerator HitStop(float duration) { float old = Time.timeScale; Time.timeScale = 0f; yield return new WaitForSecondsRealtime(duration); Time.timeScale = old; }
        private IEnumerator Shake(float amount)
        {
            if (_camera == null) yield break; _cameraOrigin = _camera.transform.localPosition; float t = 0f;
            while (t < .12f) { t += Time.unscaledDeltaTime; _camera.transform.localPosition = _cameraOrigin + (Vector3)Random.insideUnitCircle * amount * (1f - t / .12f); yield return null; }
            _camera.transform.localPosition = _cameraOrigin;
        }
    }
}
