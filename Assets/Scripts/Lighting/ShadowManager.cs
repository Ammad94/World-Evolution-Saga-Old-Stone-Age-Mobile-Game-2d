using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PrehistoricSurvival.Lighting
{
    /// <summary>
    /// Manages 2D shadow casting for environmental objects.
    /// Attaches ShadowCaster2D components and updates shadow properties.
    /// </summary>
    public class ShadowManager : MonoBehaviour
    {
        public static ShadowManager Instance { get; private set; }

        [Header("Settings")]
        [Tooltip("Default shadow color.")]
        public Color shadowColor = new Color(0f, 0f, 0f, 0.5f);

        [Tooltip("Shadow self-shadowing enabled.")]
        public bool selfShadows = true;

        [Header("Auto-Setup")]
        [Tooltip("Automatically add ShadowCaster2D to tagged objects on start.")]
        public bool autoSetup = true;

        [Tooltip("Tags that should receive shadow casters.")]
        public string[] shadowCasterTags = { "Tree", "Rock", "Building" };

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (autoSetup)
                SetupShadowCasters();
        }

        /// <summary>Find all tagged objects and add ShadowCaster2D components.</summary>
        private void SetupShadowCasters()
        {
            foreach (string tag in shadowCasterTags)
            {
                if (string.IsNullOrEmpty(tag)) continue;

                GameObject[] objects;
                try
                {
                    // FindGameObjectsWithTag throws when the tag is not defined
                    // in Project Settings, so guard against broken setups.
                    objects = GameObject.FindGameObjectsWithTag(tag);
                }
                catch (UnityException)
                {
                    Debug.LogWarning($"[ShadowManager] Tag '{tag}' is not defined in Project Settings → Tags & Layers. Skipping it (add the tag to silence this).");
                    continue;
                }

                foreach (var obj in objects)
                {
                    AddShadowCaster(obj);
                }
            }
        }

        /// <summary>Add a ShadowCaster2D to an object if it doesn't have one.</summary>
        public void AddShadowCaster(GameObject obj)
        {
            if (obj == null) return;

            var caster = obj.GetComponent<ShadowCaster2D>();
            if (caster != null) return; // Already has one

            // Require a SpriteRenderer or PolygonCollider2D
            var sr = obj.GetComponent<SpriteRenderer>();
            var col = obj.GetComponent<PolygonCollider2D>();

            if (sr == null && col == null)
            {
                Debug.LogWarning($"[ShadowManager] {obj.name} has no SpriteRenderer or PolygonCollider2D.");
                return;
            }

            caster = obj.AddComponent<ShadowCaster2D>();
            caster.selfShadows = selfShadows;
        }

        /// <summary>Remove ShadowCaster2D from an object.</summary>
        public void RemoveShadowCaster(GameObject obj)
        {
            if (obj == null) return;
            var caster = obj.GetComponent<ShadowCaster2D>();
            if (caster != null) Destroy(caster);
        }

        /// <summary>Update shadow color for all shadow casters.</summary>
        public void SetShadowColor(Color color)
        {
            shadowColor = color;
            // Note: URP 2D shadows don't have per-caster color, but this can be used
            // for custom shadow rendering or post-processing effects.
        }
    }
}
