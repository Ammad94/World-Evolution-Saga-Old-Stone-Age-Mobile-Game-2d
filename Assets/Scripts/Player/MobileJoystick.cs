using UnityEngine;
using UnityEngine.EventSystems;

namespace PrehistoricSurvival.Player
{
    /// <summary>
    /// Dynamic 360° virtual joystick for mobile touch input.
    /// Appears wherever the user first touches on the left half of the screen.
    /// </summary>
    public class MobileJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Settings")]
        [Tooltip("Maximum distance the knob can travel from center (in screen pixels).")]
        public float maxRadius = 100f;

        [Tooltip("Only respond to touches on the left half of the screen.")]
        public bool leftHalfOnly = true;

        [Tooltip("Visual knob (child of this object).")]
        public RectTransform knob;

        [Tooltip("Background circle (this RectTransform).")]
        public RectTransform background;

        [Header("Dead Zone")]
        [Range(0f, 0.5f)]
        public float deadZone = 0.1f;

        // State
        private Vector2 _inputVector;
        private int _pointerId = -1;
        private bool _active;

        public Vector2 Direction => _inputVector;
        public bool IsActive => _active;

        private void Start()
        {
            if (background == null) background = GetComponent<RectTransform>();
            gameObject.SetActive(false); // hidden until touched
        }

        // ------------------------------------------------------------------
        // Pointer Events
        // ------------------------------------------------------------------
        public void OnPointerDown(PointerEventData eventData)
        {
            if (leftHalfOnly && eventData.position.x > Screen.width * 0.5f) return;

            _pointerId = eventData.pointerId;
            _active = true;
            gameObject.SetActive(true);

            // Move joystick background to touch position
            background.position = eventData.position;
            knob.anchoredPosition = Vector2.zero;
            _inputVector = Vector2.zero;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;

            Vector2 delta = eventData.position - (Vector2)background.position;
            float distance = delta.magnitude;

            if (distance > maxRadius)
                delta = delta.normalized * maxRadius;

            knob.anchoredPosition = delta;

            // Normalize and apply dead zone
            Vector2 normalized = delta / maxRadius;
            if (normalized.magnitude < deadZone)
                _inputVector = Vector2.zero;
            else
                _inputVector = normalized;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;

            _pointerId = -1;
            _active = false;
            _inputVector = Vector2.zero;
            knob.anchoredPosition = Vector2.zero;
            gameObject.SetActive(false);
        }
    }
}
