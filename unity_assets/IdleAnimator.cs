using UnityEngine;

/// <summary>
/// Plays the idle animation (breathing + hair/fur swaying in a light breeze).
///
/// The `frames` array is FLATTENED, direction-major:
///   [dir0_frame0, dir0_frame1, ..., dir0_frameN-1,
///    dir1_frame0, dir1_frame1, ... ]
/// i.e. for each direction there are `frameCount` consecutive frames.
///
/// It reads the current facing direction from PlayerController3D and swaps
/// the sprite every 1/fps seconds to loop the idle animation.
/// </summary>
public class IdleAnimator : MonoBehaviour
{
    [Header("Frames")]
    [Tooltip("Flattened idle frames, direction-major (see class comment).")]
    public Sprite[] frames;

    [Tooltip("Number of directions (must match the controller, e.g. 8 or 16).")]
    public int directionCount = 16;

    [Tooltip("Number of animation frames per direction.")]
    public int frameCount = 3;

    [Tooltip("Playback speed (frames per second).")]
    public float fps = 4f;

    [Tooltip("Randomize start phase so multiple characters don't breathe in sync.")]
    public bool randomStartPhase = true;

    private SpriteRenderer sr;
    private PlayerController3D pc;
    private float startOffset;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        pc = GetComponent<PlayerController3D>();
        if (randomStartPhase) startOffset = Random.value * 1000f;
    }

    void LateUpdate()
    {
        if (sr == null || pc == null) return;
        if (frames == null || frames.Length < directionCount * frameCount) return;

        int dir = pc.CurrentDirectionIndex;
        if (dir < 0 || dir >= directionCount) return;

        // loop the animation frames for this direction
        float t = (Time.time + startOffset) * fps;
        int f = Mathf.FloorToInt(t) % frameCount;

        int index = dir * frameCount + f;
        if (index < frames.Length)
            sr.sprite = frames[index];
    }
}
