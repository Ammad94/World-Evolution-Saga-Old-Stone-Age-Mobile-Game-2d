using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PrehistoricSurvival.Feedback
{
    /// <summary>
    /// Tiny tween library for UI: pops, fades and slides with ease-out curves,
    /// plus a button feel component (hover/press scale + click sound) that the
    /// UIFactory attaches to every button automatically.
    /// </summary>
    public static class UITween
    {
        public static IEnumerator PopRoutine(RectTransform rt, float duration = 0.22f, float overshoot = 1.12f)
        {
            if (rt == null) yield break;
            Vector3 target = rt.localScale;
            float t = 0f;
            rt.localScale = target * 0.75f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = t / duration;
                // back-out ease
                float e = 1f + (overshoot - 1f) * Mathf.Sin(k * Mathf.PI) + k * (1f - k) * 0.5f;
                if (rt != null) rt.localScale = Vector3.LerpUnclamped(target * 0.75f, target, Mathf.Clamp01(e));
                yield return null;
            }
            if (rt != null) rt.localScale = target;
        }

        public static IEnumerator FadeRoutine(CanvasGroup cg, float target, float duration)
        {
            if (cg == null) yield break;
            float start = cg.alpha;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                if (cg == null) yield break;
                cg.alpha = Mathf.Lerp(start, target, t / duration);
                cg.interactable = cg.alpha > 0.6f;
                cg.blocksRaycasts = cg.alpha > 0.6f;
                yield return null;
            }
            if (cg != null)
            {
                cg.alpha = target;
                cg.interactable = target > 0.6f;
                cg.blocksRaycasts = target > 0.6f;
            }
        }

        public static IEnumerator SlideRoutine(RectTransform rt, Vector2 fromOffset, float duration)
        {
            if (rt == null) yield break;
            Vector2 target = rt.anchoredPosition;
            rt.anchoredPosition = target + fromOffset;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Pow(1f - t / duration, 3f); // cubic out
                if (rt == null) yield break;
                rt.anchoredPosition = Vector2.LerpUnclamped(target + fromOffset, target, k);
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = target;
        }

        /// <summary>GetComponent-or-Add, Unity-safe. Never use '??' with UnityEngine.Object:
        /// GetComponent returns a "fake null" wrapper for missing components that the
        /// null-coalescing operator treats as a valid reference (MissingComponentException).</summary>
        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        /// <summary>Convenience: pop animation on a RectTransform.</summary>
        public static void Pop(RectTransform rt, float duration = 0.22f, float overshoot = 1.12f)
        {
            if (rt == null) return;
            GetOrAdd<TweenHost>(rt.gameObject).Coroutine(PopRoutine(rt, duration, overshoot));
        }

        public static void Pop(GameObject panel, float duration = 0.22f, float overshoot = 1.12f)
        {
            if (panel == null) return;
            Pop(panel.GetComponent<RectTransform>(), duration, overshoot);
        }

        /// <summary>Convenience: show a panel with fade + pop; returns the coroutine host.</summary>
        public static void Show(GameObject panel, bool animate = true)
        {
            if (panel == null) return;
            panel.SetActive(true);
            if (!animate) return;
            var cg = GetOrAdd<CanvasGroup>(panel);
            cg.alpha = 0f;
            var host = GetOrAdd<TweenHost>(panel);
            host.Coroutine(FadeRoutine(cg, 1f, 0.18f));
            host.Coroutine(PopRoutine(panel.GetComponent<RectTransform>()));
        }

        public static void Hide(GameObject panel, float duration = 0.15f)
        {
            if (panel == null || !panel.activeSelf) return;
            var cg = GetOrAdd<CanvasGroup>(panel);
            var host = GetOrAdd<TweenHost>(panel);
            host.StartCoroutine(HideRoutine(panel, cg, duration));
        }

        private static IEnumerator HideRoutine(GameObject panel, CanvasGroup cg, float duration)
        {
            yield return FadeRoutine(cg, 0f, duration);
            if (panel != null) panel.SetActive(false);
            if (cg != null) cg.alpha = 1f;
        }
    }

    /// <summary>Invisible MonoBehaviour used to host UI coroutines on panels.</summary>
    public class TweenHost : MonoBehaviour
    {
        public void Coroutine(IEnumerator routine) => StartCoroutine(routine);
    }

    /// <summary>Button feel: scale on hover/press + click sound. Attached by UIFactory.</summary>
    public class UIButtonFX : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler,
        UnityEngine.EventSystems.IPointerExitHandler, UnityEngine.EventSystems.IPointerDownHandler,
        UnityEngine.EventSystems.IPointerUpHandler
    {
        public float hoverScale = 1.045f;
        public float pressScale = 0.96f;

        private RectTransform _rt;
        private Vector3 _base;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            _base = _rt.localScale;
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData _)
        {
            _rt.localScale = _base * hoverScale;
            if (Core.AudioManager.Instance != null) Core.AudioManager.Instance.PlayHover();
        }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData _)
        {
            _rt.localScale = _base;
        }

        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData _)
        {
            _rt.localScale = _base * pressScale;
        }

        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData _)
        {
            _rt.localScale = _base * hoverScale;
            if (Core.AudioManager.Instance != null) Core.AudioManager.Instance.PlayUI();
        }
    }
}
