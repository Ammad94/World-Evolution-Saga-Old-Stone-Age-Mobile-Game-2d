using System.Collections.Generic;
using UnityEngine;

namespace PrehistoricSurvival.World
{
    /// <summary>
    /// One central update loop that sways every registered foliage sprite
    /// (trees, bushes, grass tufts) in the breeze. A single component updates a
    /// rotating slice of the registered props each frame, so even with thousands
    /// of plants loaded the CPU cost stays at a fraction of a millisecond —
    /// per-instance MonoBehaviour updates would be far too heavy on mobile.
    /// </summary>
    public class WindSystem : MonoBehaviour
    {
        public static WindSystem Instance { get; private set; }

        [Tooltip("Maximum props animated per frame (higher = smoother, more CPU).")]
        public int perFrameBudget = 350;
        [Tooltip("Global sway strength in degrees at full wind.")]
        public float maxSwayDegrees = 1.8f;
        [Tooltip("Sway oscillation speed.")]
        public float swaySpeed = 1.4f;

        private struct SwayProp
        {
            public Transform transform;
            public float amplitude;   // degrees (bushes/grass sway more than trees)
            public float phase;
        }

        private readonly List<SwayProp> _props = new List<SwayProp>();
        private int _cursor;
        private float _wind;          // slow global gust curve 0..1

        public static WindSystem EnsureExists()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<WindSystem>();
            if (existing != null) { Instance = existing; return existing; }
            var go = new GameObject("WindSystem");
            var ws = go.AddComponent<WindSystem>();
            Instance = ws;
            return ws;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Register a foliage prop. amplitude in degrees.</summary>
        public void Register(Transform t, float amplitudeDegrees)
        {
            if (t == null) return;
            if (_props.Count > 6000) return; // hard cap for very weak devices
            _props.Add(new SwayProp
            {
                transform = t,
                amplitude = amplitudeDegrees,
                phase = Random.value * 20f
            });
        }

        private void Update()
        {
            if (_props.Count == 0) return;

            // Slow gusts: the whole forest breathes together.
            _wind = Mathf.PerlinNoise(Time.time * 0.22f, 17.31f);

            int processed = 0;
            int start = _cursor;
            while (processed < _props.Count && processed < perFrameBudget)
            {
                int idx = (start + processed) % _props.Count;
                var p = _props[idx];
                var t = p.transform;
                if (t == null)
                {
                    // Prop destroyed with its chunk — lazily drop it.
                    _props.RemoveAt(idx);
                    if (idx < _cursor) _cursor--;
                    continue;
                }
                float sway = Mathf.Sin(Time.time * swaySpeed + p.phase) * p.amplitude * (0.25f + _wind);
                t.localRotation = Quaternion.Euler(0f, 0f, sway);
                processed++;
            }
            _cursor = _props.Count == 0 ? 0 : (_cursor + processed) % _props.Count;
        }
    }
}
