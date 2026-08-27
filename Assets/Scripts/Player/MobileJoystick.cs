using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PrehistoricSurvival.Player
{
    /// <summary>
    /// Dynamic 360° virtual joystick for mobile touch input.
    ///
    /// This component sits on a full-screen (or left-half) invisible touch area so it
    /// always receives touches; the visible ring + knob fade in wherever the finger
    /// lands and fade out on release. It also works with the mouse in the editor.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class MobileJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Settings")]
        [Tooltip("Maximum distance the knob can travel from the centre (screen pixels, reference resolution).")]
        public float maxRadius = 140f;

        [Tooltip("Only respond to touches on the left half of the screen (right half is camera / interaction).")]
        public bool leftHalfOnly = true;

        [Header("Visuals")]
        [Tooltip("Ring that appears under the finger.")]
        public RectTransform background;
        [Tooltip("Knob that follows the finger.")]
        public RectTransform knob;
        [Tooltip("Group used to fade the joystick in and out.")]
        public CanvasGroup visuals;

        [Header("Dead Zone")]
        [Range(0f, 0.5f)] public float deadZone = 0.12f;

        [Header("Fade")]
        public float fadeSpeed = 8f;

        private Vector2 _inputVector;
        private int _pointerId = -100;
        private bool _active;
        private Canvas _canvas;
        private float _targetAlpha;

        /// <summary>Normalised movement direction (-1..1 on both axes).</summary>
        public Vector2 Direction => _inputVector;
        /// <summary>True while a finger is controlling the stick.</summary>
        public bool IsActive => _active;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            if (background == null) background = transform.Find("Ring") as RectTransform;
            if (visuals == null && background != null)
            {
                visuals = background.GetComponent<CanvasGroup>();
                if (visuals == null) visuals = background.gameObject.AddComponent<CanvasGroup>();
            }
            if (visuals != null) visuals.alpha = 0f;

            // Make sure the touch area actually receives raycasts.
            var image = GetComponent<Image>();
            if (image == null)
            {
                image = gameObject.AddComponent<Image>();
                image.color = new Color(0f, 0f, 0f, 0f);
            }
            image.raycastTarget = true;
        }

        private void Update()
        {
            if (visuals == null) return;
            visuals.alpha = Mathf.MoveTowards(visuals.alpha, _targetAlpha, fadeSpeed * Time.unscaledDeltaTime);
        }

        // ------------------------------------------------------------------
        public void OnPointerDown(PointerEventData eventData)
        {
            if (leftHalfOnly && eventData.position.x > Screen.width * 0.5f) return;

            _pointerId = eventData.pointerId;
            _active = true;
            _targetAlpha = 1f;

            if (background != null)
            {
                background.position = ScreenToCanvas(eventData);
                if (knob != null) knob.anchoredPosition = Vector2.zero;
            }
            _inputVector = Vector2.zero;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId || background == null) return;

            Vector2 origin = RectTransformUtility.WorldToScreenPoint(UICamera, background.position);
            Vector2 delta = eventData.position - origin;

            float scale = _canvas != null ? _canvas.scaleFactor : 1f;
            float radiusPixels = maxRadius * scale;

            if (delta.magnitude > radiusPixels)
                delta = delta.normalized * radiusPixels;

            if (knob != null) knob.anchoredPosition = delta / Mathf.Max(0.0001f, scale);

            Vector2 normalized = delta / radiusPixels;
            _inputVector = normalized.magnitude < deadZone ? Vector2.zero : normalized;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            Release();
        }

        private void OnDisable() => Release();

        private void Release()
        {
            _pointerId = -100;
            _active = false;
            _inputVector = Vector2.zero;
            _targetAlpha = 0f;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
        }

        private Camera UICamera =>
            _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

        private Vector3 ScreenToCanvas(PointerEventData eventData)
        {
            if (_canvas == null) return eventData.position;
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)_canvas.transform, eventData.position, UICamera, out Vector3 world);
            return world;
        }
    }
}
