using System.Collections;
using UnityEngine;
using PrehistoricSurvival.Core;

namespace PrehistoricSurvival.Feedback
{
    /// <summary>
    /// Central game-feel facade: hit-stop, camera shake trauma, sprite flashes and
    /// combined impact presets. Pure code — the cheapest, highest-leverage polish.
    /// All effects respect the reduced-motion accessibility setting.
    /// </summary>
    public class GameFeel : MonoBehaviour
    {
        private static GameFeel _instance;
        public static GameFeel Instance => _instance;

        [Header("Tuning")]
        public float hitStopDuration = 0.045f;
        public float shakeDecay = 1.6f;
        public float maxShakeOffset = 0.35f;

        private float _trauma;
        private bool _inHitStop;

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("GameFeel");
            _instance = go.AddComponent<GameFeel>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }

        private static bool MotionAllowed
            => AccessibilityAndPerformance.Instance == null || !AccessibilityAndPerformance.Instance.reducedMotion;

        // ------------------------------------------------------------------
        /// <summary>Brief freeze-frame on impact (does not stack).</summary>
        public static void HitStop(float duration = 0f)
        {
            if (_instance == null || !MotionAllowed) return;
            if (_instance._inHitStop) return;
            _instance.StartCoroutine(_instance.HitStopRoutine(duration > 0f ? duration : _instance.hitStopDuration));
        }

        private IEnumerator HitStopRoutine(float duration)
        {
            _inHitStop = true;
            float old = Time.timeScale;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = old;
            _inHitStop = false;
        }

        /// <summary>Add camera shake trauma (0..1). Squared falloff applied by the camera.</summary>
        public static void Shake(float amount)
        {
            if (_instance == null || !MotionAllowed) return;
            _instance._trauma = Mathf.Clamp01(_instance._trauma + amount);
        }

        /// <summary>Tint-flash a sprite (damage flash, pickup blip...).</summary>
        public static void Flash(SpriteRenderer sr, Color color, float duration = 0.12f)
        {
            if (sr == null) return;
            _instance?.StartCoroutine(FlashRoutine(sr, color, duration));
        }

        private static IEnumerator FlashRoutine(SpriteRenderer sr, Color color, float duration)
        {
            Color original = sr.color;
            sr.color = color;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (sr != null) sr.color = original;
        }

        /// <summary>Small scale pulse on any transform (UI pop, creature roar...).</summary>
        public static void Pulse(Transform target, float strength = 0.15f, float duration = 0.18f)
        {
            if (target == null || !MotionAllowed) return;
            _instance?.StartCoroutine(PulseRoutine(target, strength, duration));
        }

        private static IEnumerator PulseRoutine(Transform target, float strength, float duration)
        {
            Vector3 original = target.localScale;
            float t = 0f;
            while (t < duration && target != null)
            {
                t += Time.unscaledDeltaTime;
                float k = 1f + Mathf.Sin(t / duration * Mathf.PI) * strength;
                target.localScale = original * k;
                yield return null;
            }
            if (target != null) target.localScale = original;
        }

        // ------------------------------------------------------------------
        /// <summary>Full melee impact preset: vfx + hit-stop + shake + haptics.</summary>
        public static void Impact(Vector3 pos, bool heavy)
        {
            Art.FX.Hit(pos, heavy);
            HitStop(heavy ? hitStopDuration * 1.6f : hitStopDuration);
            Shake(heavy ? 0.5f : 0.3f);
            if (AccessibilityAndPerformance.Instance != null && AccessibilityAndPerformance.Instance.haptics && MotionAllowed)
                Handheld.Vibrate();
        }

        private void Update()
        {
            if (_trauma <= 0f) return;
            _trauma = Mathf.Max(0f, _trauma - shakeDecay * Time.unscaledDeltaTime);

            var cam = Camera.main;
            if (cam == null) return;
            var follow = cam.GetComponent<PrehistoricSurvival.Player.CameraFollow>();
            if (follow != null)
            {
                follow.shakeOffset = _trauma * _trauma * maxShakeOffset *
                    new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f);
                if (_trauma <= 0f) follow.shakeOffset = Vector3.zero;
            }
        }
    }
}
