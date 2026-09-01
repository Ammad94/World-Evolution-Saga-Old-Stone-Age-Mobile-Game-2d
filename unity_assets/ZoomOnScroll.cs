using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Optional: mouse scroll-wheel zoom for an orthographic camera.
/// Lets you dial in the perfect "camera distance" while playing,
/// then copy the final Size value into the camera's inspector.
/// </summary>
public class ZoomOnScroll : MonoBehaviour
{
    [Tooltip("How much each scroll tick zooms in/out.")]
    public float zoomStep = 0.5f;

    [Tooltip("Limits (in orthographic Size units).")]
    public float minSize = 2f;
    public float maxSize = 15f;

    private Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        if (cam == null || !cam.orthographic) return;

        float scroll = 0f;
#if ENABLE_INPUT_SYSTEM
        Mouse m = Mouse.current;
        if (m != null) scroll = m.scroll.ReadValue().y;
#else
        scroll = Input.GetAxis("Mouse ScrollWheel");
#endif
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - scroll * zoomStep, minSize, maxSize);
        }
    }
}
