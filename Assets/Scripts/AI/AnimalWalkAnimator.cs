using System.Collections;
using UnityEngine;
using PrehistoricSurvival.Core;

namespace PrehistoricSurvival.AI
{
    /// <summary>
    /// Eight-direction animal walk cycle with stride bobbing, plus multi-frame
    /// attack and death one-shots. Frame arrays are filled by the editor setup;
    /// single-frame arrays still work (stride bob keeps them alive).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class AnimalWalkAnimator : MonoBehaviour
    {
        public Sprite[] north, northEast, east, southEast, south, southWest, west, northWest;
        [Header("One-shots (side view, mirrored automatically)")]
        public Sprite[] attackFrames = new Sprite[3];
        public Sprite[] deathFrames = new Sprite[3];
        public float framesPerSecond = 8f;
        public float strideBob = 0.035f;
        public float strideSquash = 0.025f;

        private SpriteRenderer _renderer;
        private Rigidbody2D _body;
        private Vector3 _baseScale;
        private Vector3 _basePosition;
        private float _timer;
        private int _frame;
        private bool _locked;
        private AnimalAI _ai;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _body = GetComponent<Rigidbody2D>();
            _ai = GetComponent<AnimalAI>();
            _baseScale = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;
            _basePosition = transform.localPosition;
        }

        private void OnEnable()
        {
            if (_ai != null) _ai.OnDeath += PlayDeath;
        }

        private void OnDisable()
        {
            if (_ai != null) _ai.OnDeath -= PlayDeath;
        }

        private void Update()
        {
            if (_locked) return;
            Vector2 velocity = _body.linearVelocity;
            bool moving = velocity.sqrMagnitude > 0.03f;
            Sprite[] frames = DirectionFrames(velocity.sqrMagnitude > 0.001f ? Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg : 270f);
            if (frames != null && frames.Length > 0 && moving)
            {
                _timer += Time.deltaTime;
                if (_timer >= 1f / Mathf.Max(1f, framesPerSecond)) { _timer = 0f; _frame = (_frame + 1) % frames.Length; }
                if (frames[_frame] != null) _renderer.sprite = frames[_frame];
                float phase = (_frame / (float)Mathf.Max(1, frames.Length)) * Mathf.PI * 2f;
                transform.localPosition = _basePosition + Vector3.up * (Mathf.Sin(phase) * strideBob);
                transform.localScale = _baseScale * (1f + Mathf.Sin(phase) * strideSquash);
            }
            else
            {
                _timer = 0f; _frame = 0; transform.localPosition = Vector3.Lerp(transform.localPosition, _basePosition, Time.deltaTime * 10f); transform.localScale = Vector3.Lerp(transform.localScale, _baseScale, Time.deltaTime * 10f);
            }
        }

        private Sprite[] DirectionFrames(float angle)
        {
            if (angle < 0f) angle += 360f;
            if (angle >= 337.5f || angle < 22.5f) return east;
            if (angle < 67.5f) return northEast;
            if (angle < 112.5f) return north;
            if (angle < 157.5f) return northWest;
            if (angle < 202.5f) return west;
            if (angle < 247.5f) return southWest;
            if (angle < 292.5f) return south;
            return southEast;
        }

        // ------------------------------------------------------------------
        // One-shots
        // ------------------------------------------------------------------
        /// <summary>Play the attack lunge (faces the player when possible).</summary>
        public void PlayAttack()
        {
            if (attackFrames == null || attackFrames.Length == 0) return;
            StartCoroutine(PlayFrames(attackFrames, 12f, mirrorToFacing: true, permanent: false));
        }

        /// <summary>Play the death animation (locked; ends lying on the ground).</summary>
        public void PlayDeath(AnimalAI _ = null)
        {
            if (deathFrames == null || deathFrames.Length == 0) return;
            StopAllCoroutines();
            _locked = false;
            StartCoroutine(PlayFrames(deathFrames, 7f, mirrorToFacing: false, permanent: true));
        }

        private IEnumerator PlayFrames(Sprite[] frames, float fps, bool mirrorToFacing, bool permanent)
        {
            _locked = true;
            bool mirror = false;
            if (mirrorToFacing)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) mirror = player.transform.position.x < transform.position.x;
            }
            float wait = 1f / Mathf.Max(1f, fps);
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] != null)
                {
                    _renderer.sprite = frames[i];
                    _renderer.flipX = mirror;
                }
                yield return new WaitForSeconds(wait);
            }
            if (permanent)
            {
                // Hold the lying pose and fade out over the corpse timer.
                float t = 0f;
                Color c = _renderer.color;
                while (t < 6f)
                {
                    t += Time.deltaTime;
                    if (t > 3.5f) _renderer.color = new Color(c.r, c.g, c.b, Mathf.InverseLerp(6f, 3.5f, t));
                    yield return null;
                }
            }
            else _locked = false;
        }
    }
}
