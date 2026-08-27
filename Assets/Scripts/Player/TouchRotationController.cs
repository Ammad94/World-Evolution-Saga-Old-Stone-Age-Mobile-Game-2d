using UnityEngine;

namespace PrehistoricSurvival.Player
{
    /// <summary>
    /// Detects touch swipes on the right half of the screen and exposes a
    /// rotation delta that can be used to rotate the player or camera.
    /// </summary>
    public class TouchRotationController : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Minimum swipe distance (pixels) to register rotation.")]
        public float minSwipeDistance = 10f;

        [Tooltip("Sensitivity multiplier for rotation delta.")]
        public float sensitivity = 0.25f;

        [Tooltip("Only respond to touches on the right half of the screen.")]
        public bool rightHalfOnly = true;

        private int _rotationPointerId = -1;
        private Vector2 _lastTouchPos;
        private Vector2 _rotationDelta;

        /// <summary>
        /// The accumulated rotation delta since last read (x = yaw, y = pitch).
        /// Read and reset each frame from the consumer.
        /// </summary>
        public Vector2 RotationDelta
        {
            get
            {
                var d = _rotationDelta;
                _rotationDelta = Vector2.zero;
                return d;
            }
        }

        private void Update()
        {
            HandleTouchRotation();
        }

        private void HandleTouchRotation()
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);

                // Filter to right half
                if (rightHalfOnly && touch.position.x < Screen.width * 0.5f) continue;

                if (_rotationPointerId == -1 && touch.phase == TouchPhase.Began)
                {
                    _rotationPointerId = touch.fingerId;
                    _lastTouchPos = touch.position;
                }

                if (touch.fingerId == _rotationPointerId)
                {
                    if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                    {
                        Vector2 delta = touch.position - _lastTouchPos;
                        if (delta.magnitude > minSwipeDistance)
                        {
                            _rotationDelta += delta * sensitivity;
                        }
                        _lastTouchPos = touch.position;
                    }
                    else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        _rotationPointerId = -1;
                    }
                }
            }

            // Fallback for mouse (PC testing)
            if (Input.touchCount == 0 && Input.GetMouseButton(1))
            {
                Vector2 delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 100f;
                _rotationDelta += delta * sensitivity;
            }
        }
    }
}
