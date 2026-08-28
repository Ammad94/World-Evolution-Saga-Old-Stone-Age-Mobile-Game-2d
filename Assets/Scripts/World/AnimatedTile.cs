using UnityEngine;
using UnityEngine.Tilemaps;

namespace PrehistoricSurvival.World
{
    /// <summary>
    /// A runtime tile that cycles through sprite frames — used for the animated
    /// water surfaces (ocean swell, lake ripple, river current). The tilemap
    /// animator handles per-tile phase from the animation start time, so the
    /// whole ocean shimmers instead of pulsing in unison.
    /// </summary>
    public class AnimatedTile : TileBase
    {
        public Sprite[] frames;
        [Tooltip("Seconds each frame stays on screen.")]
        public float frameInterval = 0.55f;

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            if (frames == null || frames.Length == 0) return;
            tileData.sprite = frames[0];
            tileData.color = UnityEngine.Color.white;
            // TileFlags has no "LockedColor" member — the flag that keeps the tile's own
            // colour (and therefore its water tint) from being overridden by brush/tilemap
            // tinting is TileFlags.LockColor.
            tileData.flags = TileFlags.LockColor;
        }

        public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData tileAnimationData)
        {
            if (frames == null || frames.Length < 2) return false;
            tileAnimationData.animatedSprites = frames;
            tileAnimationData.animationSpeed = 1f / Mathf.Max(0.05f, frameInterval);
            // Stagger neighbouring tiles so the water reads as rolling, not blinking.
            tileAnimationData.animationStartTime = ((position.x * 0.37f + position.y * 0.11f) % 1f) * frameInterval * frames.Length;
            return true;
        }

        /// <summary>Create a runtime animated tile (no asset needed).</summary>
        public static AnimatedTile Create(Sprite[] frames, float frameInterval = 0.55f)
        {
            if (frames == null) return null;
            var valid = new System.Collections.Generic.List<Sprite>(frames.Length);
            foreach (var f in frames) if (f != null) valid.Add(f);
            if (valid.Count < 2) return null;
            var tile = ScriptableObject.CreateInstance<AnimatedTile>();
            tile.frames = valid.ToArray();
            tile.frameInterval = frameInterval;
            return tile;
        }
    }
}
