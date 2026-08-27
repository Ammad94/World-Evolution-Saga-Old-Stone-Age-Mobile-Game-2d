using UnityEngine;
using TMPro;

namespace PrehistoricSurvival.UI
{
    /// <summary>
    /// Floating tooltip UI that appears near interactive objects.
    /// Shows object name, yields, and status.
    /// </summary>
    public class TooltipUI : MonoBehaviour
    {
        [Header("References")]
        public RectTransform panel;
        public TextMeshProUGUI text;

        [Header("Settings")]
        [Tooltip("Offset from the world object in screen space.")]
        public Vector2 offset = new Vector2(0f, 50f);

        [Tooltip("How quickly the tooltip follows the target.")]
        public float followSpeed = 10f;

        private Vector3 _worldTarget;
        private bool _visible;

        private void Start()
        {
            if (panel != null) panel.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_visible || panel == null) return;

            // Convert world position to screen position
            Vector3 screenPos = Camera.main.WorldToScreenPoint(_worldTarget);
            screenPos += (Vector3)offset;

            // Smooth follow
            panel.position = Vector3.Lerp(panel.position, screenPos, followSpeed * Time.deltaTime);
        }

        /// <summary>Show the tooltip with the given text at a world position.</summary>
        public void Show(string tooltipText, Vector3 worldPos)
        {
            if (panel == null || text == null) return;

            _worldTarget = worldPos;
            text.text = tooltipText;
            panel.gameObject.SetActive(true);
            _visible = true;
        }

        /// <summary>Hide the tooltip.</summary>
        public void Hide()
        {
            if (panel != null) panel.gameObject.SetActive(false);
            _visible = false;
        }
    }
}
