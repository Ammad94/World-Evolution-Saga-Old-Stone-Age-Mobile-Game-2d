using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace PrehistoricSurvival.Feedback
{
    /// <summary>Rising, fading world-space damage numbers (pooled).</summary>
    public class DamageNumber : MonoBehaviour
    {
        private static DamageNumber _instance;
        private const int POOL = 14;
        private readonly List<TextMeshPro> _pool = new List<TextMeshPro>();

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("DamageNumbers");
            _instance = go.AddComponent<DamageNumber>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            for (int i = 0; i < POOL; i++)
            {
                var go = new GameObject("dmg");
                go.SetActive(false);
                var tmp = go.AddComponent<TextMeshPro>();
                tmp.fontSize = 3.2f;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.sortingOrder = 950;
                var rt = tmp.rectTransform;
                rt.sizeDelta = new Vector2(4f, 1.2f);
                _pool.Add(tmp);
            }
        }

        /// <summary>Show a floating number/text at a world position.</summary>
        public static void Show(Vector3 pos, string text, Color color)
        {
            if (_instance == null) Ensure();
            _instance.PlayInternal(pos, text, color);
        }

        public static void Damage(Vector3 pos, float amount, bool heavy = false)
        {
            Show(pos + Vector3.up * 0.6f, Mathf.RoundToInt(amount).ToString(),
                heavy ? new Color(1f, 0.55f, 0.25f) : new Color(1f, 0.92f, 0.75f));
        }

        public static void Heal(Vector3 pos, float amount)
            => Show(pos + Vector3.up * 0.6f, "+" + Mathf.RoundToInt(amount), new Color(0.55f, 1f, 0.6f));

        private void PlayInternal(Vector3 pos, string text, Color color)
        {
            TextMeshPro tmp = null;
            for (int i = 0; i < _pool.Count; i++)
                if (!_pool[i].gameObject.activeSelf) { tmp = _pool[i]; break; }
            if (tmp == null) tmp = _pool[0];
            tmp.transform.position = pos;
            tmp.text = text;
            tmp.color = color;
            tmp.gameObject.SetActive(true);
            StartCoroutine(Animate(tmp));
        }

        private IEnumerator Animate(TextMeshPro tmp)
        {
            Vector3 start = tmp.transform.position;
            float t = 0f;
            while (t < 0.8f)
            {
                t += Time.unscaledDeltaTime;
                float k = t / 0.8f;
                tmp.transform.position = start + Vector3.up * (k * 1.4f);
                var c = tmp.color;
                c.a = 1f - Mathf.Pow(k, 2f);
                tmp.color = c;
                yield return null;
            }
            tmp.gameObject.SetActive(false);
        }
    }
}
