using UnityEngine;

/// <summary>
/// Third-person style camera that keeps the target on screen.
/// - Frame-rate independent smoothing (same feel at 30, 60, 144 fps)
/// - Optional "snap" mode that locks the camera dead-center on the target
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Tooltip("The object to follow (drag the Player here).")]
    public Transform target;

    [Tooltip("Camera offset from the target. Z must stay NEGATIVE (e.g. -10) so the camera looks at the scene.")]
    public Vector3 offset = new Vector3(0f, 0f, -10f);

    [Tooltip("Follow speed. Higher = snappier, lower = floatier. 10 is a good default.")]
    public float smoothSpeed = 10f;

    [Tooltip("If ON, the camera snaps instantly to the target every frame (character always dead-center).")]
    public bool snapToTarget = false;

    void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 desired = target.position + offset;

        if (snapToTarget)
        {
            transform.position = desired;
        }
        else
        {
            // Frame-rate independent smoothing
            float t = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, t);
        }
    }
}
