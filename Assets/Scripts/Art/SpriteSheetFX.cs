using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PrehistoricSurvival.Art
{
    /// <summary>
    /// Pooled one-shot sprite-sheet VFX (slash, hitflash, dust, sparks, blood,
    /// splash, ring, puff, leaf, fire...). Sheets are single-row PNGs under
    /// Resources/Sprites/VFX/&lt;name&gt; and are sliced at runtime, so no import
    /// settings are required. Usage: FX.Spawn("slash", position).
    /// </summary>
    public class SpriteSheetFX : MonoBehaviour
    {
        private static SpriteSheetFX _instance;
        public static SpriteSheetFX Instance => _instance;

        private const int POOL_SIZE = 28;
        private readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        private readonly Dictionary<string, Sprite[]> _cache = new Dictionary<string, Sprite[]>();
        private readonly List<Coroutine> _live = new List<Coroutine>();

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("VFXPool");
            _instance = go.AddComponent<SpriteSheetFX>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            for (int i = 0; i < POOL_SIZE; i++)
            {
                var go = new GameObject("fx");
                go.SetActive(false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 900;
                _pool.Add(sr);
            }
        }

        /// <summary>Play a named one-shot effect at a world position.</summary>
        public static void Spawn(string name, Vector3 pos, float scale = 1f, float fps = 16f, Color? tint = null)
        {
            if (_instance == null) Ensure();
            _instance.PlayInternal(name, pos, scale, fps, tint ?? Color.white);
        }

        private void PlayInternal(string name, Vector3 pos, float scale, float fps, Color tint)
        {
            var frames = GetFrames(name);
            if (frames == null || frames.Length == 0) return;

            SpriteRenderer sr = null;
            for (int i = 0; i < _pool.Count; i++)
            {
                if (!_pool[i].gameObject.activeSelf) { sr = _pool[i]; break; }
            }
            if (sr == null) sr = _pool[0]; // steal oldest slot
            sr.transform.position = pos;
            sr.transform.localScale = Vector3.one * scale;
            sr.sprite = frames[0];
            sr.color = tint;
            sr.gameObject.SetActive(true);
            _live.Add(StartCoroutine(Animate(sr, frames, fps)));
        }

        private IEnumerator Animate(SpriteRenderer sr, Sprite[] frames, float fps)
        {
            float wait = 1f / Mathf.Max(1f, fps);
            for (int i = 0; i < frames.Length; i++)
            {
                if (sr == null) yield break;
                sr.sprite = frames[i];
                yield return new WaitForSeconds(wait);
            }
            if (sr != null) sr.gameObject.SetActive(false);
        }

        private Sprite[] GetFrames(string name)
        {
            if (_cache.TryGetValue(name, out var cached) && cached != null && cached.Length > 0) return cached;

            // Prefer pre-sliced sprites if the importer produced them.
            var sliced = Resources.LoadAll<Sprite>("Sprites/VFX/" + name);
            if (sliced != null && sliced.Length > 1)
            {
                System.Array.Sort(sliced, (a, b) => string.CompareOrdinal(a.name, b.name));
                _cache[name] = sliced;
                return sliced;
            }

            // Fallback: slice the row texture at runtime.
            var tex = Resources.Load<Texture2D>("Sprites/VFX/" + name);
            if (tex == null) return null;
            int frames = Mathf.Max(1, tex.width / tex.height);
            var sprites = new Sprite[frames];
            for (int i = 0; i < frames; i++)
            {
                sprites[i] = Sprite.Create(tex, new Rect(i * tex.height, 0, tex.height, tex.height),
                    new Vector2(0.5f, 0.5f), 100f);
            }
            _cache[name] = sprites;
            return sprites;
        }
    }

    /// <summary>Short helper alias.</summary>
    public static class FX
    {
        public static void Spawn(string name, Vector3 pos, float scale = 1f, float fps = 16f, Color? tint = null)
            => SpriteSheetFX.Spawn(name, pos, scale, fps, tint);

        public static void Hit(Vector3 pos, bool heavy = false)
        {
            Spawn("hitflash", pos, heavy ? 1.4f : 1f, 20f);
            if (heavy) Spawn("blood", pos, 1f, 14f, new Color(1f, 0.35f, 0.3f, 0.9f));
        }
        public static void Chop(Vector3 pos) => Spawn("spark", pos, 0.8f, 18f, new Color(1f, 0.85f, 0.5f, 1f));
        public static void Mine(Vector3 pos) => Spawn("spark", pos, 1f, 20f);
        public static void Dust(Vector3 pos) => Spawn("dust", pos, 1f, 12f);
        public static void Splash(Vector3 pos) => Spawn("splash", pos, 1.2f, 14f);
    }
}
