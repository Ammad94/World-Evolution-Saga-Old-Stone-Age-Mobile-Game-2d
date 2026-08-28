using UnityEngine;

namespace PrehistoricSurvival.Content
{
    /// <summary>Simple frame animation for NPC prefabs (south walk cycle).</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class NpcWalker : MonoBehaviour
    {
        public Sprite[] walkSouth = new Sprite[0];
        public float framesPerSecond = 6f;

        private SpriteRenderer _sr;
        private Vector3 _lastPos;
        private float _timer;
        private int _frame;

        private void Awake()
        {
            _sr = GetComponentInChildren<SpriteRenderer>();
            _lastPos = transform.position;
        }

        private void Update()
        {
            bool moving = (transform.position - _lastPos).sqrMagnitude > 0.0004f;
            _lastPos = transform.position;
            if (!moving || walkSouth == null || walkSouth.Length == 0) { _frame = 0; return; }

            _timer += Time.deltaTime;
            if (_timer >= 1f / Mathf.Max(1f, framesPerSecond))
            {
                _timer = 0f;
                _frame = (_frame + 1) % walkSouth.Length;
            }
            if (walkSouth[_frame] != null) _sr.sprite = walkSouth[_frame];
        }
    }
}
