using UnityEngine;
using PrehistoricSurvival.Art;

namespace PrehistoricSurvival.Player
{
    /// <summary>
    /// Runtime helpers that turn the flat 2D sprite world into a 2.5D / 3D billboarded
    /// view (as in https://www.youtube.com/watch?v=_LRZcmX_xw0), to be used with
    /// <see cref="CameraFollow"/>:
    ///
    /// - <see cref="Ensure"/> migrates a SpriteRenderer onto a "Visual" child
    ///   (so billboard rotation never tilts physics colliders or triggers) and attaches a
    ///   <see cref="BillboardSprite"/> that keeps the sprite facing the camera,
    ///   making characters, animals, trees, and buildings "stand up" in the
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
        public static void Ensure(GameObject go, bool staticBillboard = true)
        {
            if (go == null) return;
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr == null) return;

            // Check if already billboarded
            var existing = go.GetComponentInChildren<Billboard>(true);
            if (existing != null)
            {
                existing.useStaticBillboard = staticBillboard;
                return;
            }

            // Move the renderer under a "Visual" child so the billboard rotation
            // never affects the root transform (colliders, rigidbody, physics).
            Transform visualTransform = sr.transform;
            if (sr.transform == go.transform)
            {
                var visual = new GameObject("Visual");
                visual.transform.SetParent(go.transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                // Move SpriteRenderer to child
                var newSr = visual.AddComponent<SpriteRenderer>();
                CopySpriteRenderer(sr, newSr);
                Object.Destroy(sr);

                visualTransform = visual.transform;
            }

            var billboard = visualTransform.GetComponent<BillboardSprite>();
            if (billboard == null)
            {
                billboard = visualTransform.gameObject.AddComponent<BillboardSprite>();
            }

            billboard.useStaticBillboard = staticBillboard;
        }

        private static void CopySpriteRenderer(SpriteRenderer src, SpriteRenderer dst)
        {
            dst.sprite = src.sprite;
            dst.color = src.color;
            dst.flipX = src.flipX;
            dst.flipY = src.flipY;
            dst.sortingLayerID = src.sortingLayerID;
            dst.sortingOrder = src.sortingOrder;
            dst.sharedMaterial = src.sharedMaterial;
        }

        /// <summary>
        /// Screen point → world point on the XY ground plane (Z = 0).
        /// Works with both Perspective and Orthographic pitched 2.5D cameras.
        /// </summary>
        public static bool GroundPoint(Vector3 screenPos, out Vector3 worldPos)
        {
            worldPos = Vector3.zero;
            var cam = Camera.main;
            if (cam == null)
            {
                var follow = CameraFollow.Instance;
                if (follow != null) cam = follow.GetComponent<Camera>();
            }
            if (cam == null) return false;

            var ray = cam.ScreenPointToRay(screenPos);
            var ground = new Plane(Vector3.forward, Vector3.zero); // XY plane at Z=0
            if (!ground.Raycast(ray, out float distance)) return false;

            worldPos = ray.GetPoint(distance);
            worldPos.z = 0f;
            return true;
        }
    }

    /// <summary>
    /// Keeps a sprite facing the camera each frame so it "stands up" in the
    /// tilted 2.5D / 3D view (the classic billboard technique).
    /// Inherits from <see cref="PrehistoricSurvival.Art.Billboard"/>.
    /// </summary>
    public class BillboardSprite : PrehistoricSurvival.Art.Billboard
    {
    }
}
