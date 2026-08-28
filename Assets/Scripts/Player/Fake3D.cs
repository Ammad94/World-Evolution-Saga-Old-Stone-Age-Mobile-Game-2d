using UnityEngine;

namespace PrehistoricSurvival.Player
{
    /// <summary>
    /// Runtime helpers that turn the flat 2D sprite world into a 2.5D ("fake 3D")
    /// view, to be used with <see cref="CameraFollow"/> in GTAChase mode:
    ///
    /// - <see cref="Ensure"/> migrates a SpriteRenderer onto a "Visual" child
    ///   (so billboard rotation never tilts physics colliders) and attaches a
    ///   <see cref="BillboardSprite"/> that keeps the sprite facing the camera,
    ///   making characters, animals, trees and buildings "stand up" in the
    ///   tilted view instead of lying flat on the ground like stickers.
    /// - <see cref="GroundPoint"/> converts a screen point to the world XY plane
    ///   via a ray-plane hit, which is required once the camera is pitched
    ///   (Camera.ScreenToWorldPoint no longer lands on the ground).
    /// </summary>
    public static class Fake3D
    {
        /// <summary>
        /// Idempotent: billboard the first SpriteRenderer found on the object.
        /// Safe to call on any spawned creature / prop / structure.
        /// </summary>
        public static void Ensure(GameObject go)
        {
            if (go == null) return;
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr == null) return;
            if (go.GetComponentInChildren<BillboardSprite>(true) != null) return;

            // Move the renderer under a "Visual" child so the billboard rotation
            // never affects the root transform (colliders, rigidbody, shadow caster).
            if (sr.transform == go.transform)
            {
                var visual = new GameObject("Visual");
                visual.transform.SetParent(go.transform, false);
                sr.transform.SetParent(visual.transform, true); // keeps world scale/rotation
            }

            if (sr.GetComponent<BillboardSprite>() == null)
                sr.gameObject.AddComponent<BillboardSprite>();
        }

        /// <summary>
        /// Screen point → world point on the XY ground plane.
        /// Works with a pitched (2.5D) camera; returns false when no camera exists.
        /// </summary>
        public static bool GroundPoint(Vector3 screenPos, out Vector3 worldPos)
        {
            worldPos = Vector3.zero;
            var cam = Camera.main;
            if (cam == null) return false;

            var ray = cam.ScreenPointToRay(screenPos);
            var ground = new Plane(Vector3.forward, Vector3.zero);
            if (!ground.Raycast(ray, out float distance)) return false;
            worldPos = ray.GetPoint(distance);
            return true;
        }
    }

    /// <summary>
    /// Keeps a sprite facing the camera each frame so it "stands up" in the
    /// tilted 2.5D view (the classic billboard trick used by GTA 1/2 style games).
    /// Attached to the sprite's own transform — never to a physics body.
    /// </summary>
    public class BillboardSprite : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 toCam = cam.transform.position - transform.position;
            if (toCam.sqrMagnitude < 0.0001f) return;

            // Degenerate when the camera is directly overhead — keep last rotation.
            if (Mathf.Abs(toCam.x) + Mathf.Abs(toCam.y) < 0.01f) return;

            transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
        }
    }
}
