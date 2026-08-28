using System.Collections;
using UnityEngine;
using PrehistoricSurvival.Core;
using PrehistoricSurvival.Player;

namespace PrehistoricSurvival.Art
{
    /// <summary>
    /// Plays one-shot full-body animations (attack / gather / swim / climb / hit / die)
    /// over the player's walk cycle. Frame sets are provided by the editor setup for
    /// each of the 8 directions. While a one-shot plays, the walk animator is locked.
    /// </summary>
    public class PlayerActionAnimator : MonoBehaviour
    {
        public static PlayerActionAnimator Instance { get; private set; }

        [Header("Frame sets — indexed N, NE, E, SE, S, SW, W, NW")]
        public Sprite[][] attack = new Sprite[8][];
        public Sprite[][] gather = new Sprite[8][];
        public Sprite[][] swim = new Sprite[8][];
        public Sprite[][] climb = new Sprite[8][];
        public Sprite[][] hit = new Sprite[8][];
        public Sprite[] die = new Sprite[3];

        private SpriteRenderer _sr;
        private PrehistoricSurvival.Player.PlayerController _controller;
        private Coroutine _playing;
        private bool _swimming;

        /// <summary>Direction order used by every array: N, NE, E, SE, S, SW, W, NW.</summary>
        public static int DirFromAngle(float angleDeg)
        {
            if (angleDeg < 0f) angleDeg += 360f;
            // convert compass (E=0, CCW) to index order N,NE,E,SE,S,SW,W,NW
            if (angleDeg >= 337.5f || angleDeg < 22.5f) return 2; // E
            if (angleDeg < 67.5f) return 1;  // NE
            if (angleDeg < 112.5f) return 0; // N
            if (angleDeg < 157.5f) return 7; // NW
            if (angleDeg < 202.5f) return 6; // W
            if (angleDeg < 247.5f) return 5; // SW
            if (angleDeg < 292.5f) return 4; // S
            return 3; // SE
        }

        private void Awake()
        {
            Instance = this;
            _sr = GetComponentInChildren<SpriteRenderer>();
            _controller = GetComponent<PrehistoricSurvival.Player.PlayerController>();
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void OnEnable()
        {
            EventManager.Subscribe(GameEvents.PlayerEnteredWater, OnWaterEnter);
            EventManager.Subscribe(GameEvents.PlayerExitedWater, OnWaterExit);
        }

        private void OnDisable()
        {
            EventManager.Unsubscribe(GameEvents.PlayerEnteredWater, OnWaterEnter);
            EventManager.Unsubscribe(GameEvents.PlayerExitedWater, OnWaterExit);
        }

        private void OnWaterEnter(object _) { _swimming = true; PlayLoop("swim"); }
        private void OnWaterExit(object _) { _swimming = false; Release(); }

        private int CurrentDir()
        {
            // GTA chase: the camera is always behind the player, so every action
            // (attack / gather / swim / hit) plays in the back-view set.
            if (CameraFollow.Chase3D) return 4; // S
            var dir = _controller != null ? _controller.MoveDirection : Vector2.right;
            if (dir.sqrMagnitude < 0.001f) return 4; // S default
            return DirFromAngle(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }

        /// <summary>Play a one-shot action (attack/gather/hit) in the given or current direction.</summary>
        public void PlayOnce(Sprite[][] set, float fps = 12f, int dir = -1, bool lockWalk = true)
        {
            if (set == null || die == null) return;
            if (_playing != null) StopCoroutine(_playing);
            _playing = StartCoroutine(PlayOnceRoutine(set, fps, dir < 0 ? CurrentDir() : dir, lockWalk, permanent: false));
        }

        public void PlayAttack(float moveAngleDeg)
        {
            if (attack == null) return;
            if (_playing != null) StopCoroutine(_playing);
            // GTA chase: the camera sits behind the back, so swing in the back-view set.
            _playing = StartCoroutine(PlayOnceRoutine(attack, 14f, DirFromAngle(CameraFollow.Chase3D ? 270f : moveAngleDeg), true, false));
        }

        public void PlayHit()
        {
            if (hit == null) return;
            if (_playing != null) StopCoroutine(_playing);
            _playing = StartCoroutine(PlayOnceRoutine(hit, 10f, CurrentDir(), true, false));
        }

        public void PlayDie()
        {
            if (die == null || die.Length == 0) return;
            if (_playing != null) StopCoroutine(_playing);
            _playing = StartCoroutine(PlayOnceRoutine(null, 6f, 4, true, permanent: true));
        }

        public void PlayLoop(string action)
        {
            var set = action == "swim" ? swim : action == "climb" ? climb : null;
            if (set == null) return;
            if (_playing != null) StopCoroutine(_playing);
            _playing = StartCoroutine(LoopRoutine(set, 8f, CurrentDir()));
        }

        /// <summary>Static convenience for other systems: play 'gather' on the player.</summary>
        public static void TriggerGather() { if (Instance != null) Instance.PlayOnce(Instance.gather, 11f); }
        public static void TriggerHit() { if (Instance != null) Instance.PlayHit(); }
        public static void TriggerDie() { if (Instance != null) Instance.PlayDie(); }

        public void Release()
        {
            if (_playing != null) { StopCoroutine(_playing); _playing = null; }
            if (_controller != null) _controller.AnimationLocked = false;
        }

        private IEnumerator PlayOnceRoutine(Sprite[][] set, float fps, int dir, bool lockWalk, bool permanent)
        {
            if (lockWalk && _controller != null) _controller.AnimationLocked = true;
            float wait = 1f / Mathf.Max(1f, fps);

            if (permanent) // death: die frames then hold last
            {
                for (int i = 0; i < die.Length; i++)
                {
                    if (die[i] != null) _sr.sprite = die[i];
                    yield return new WaitForSeconds(wait * 2f);
                }
                if (die[die.Length - 1] != null) _sr.sprite = die[die.Length - 1];
                yield break;
            }

            var frames = set != null && dir >= 0 && dir < set.Length ? set[dir] : null;
            if (frames == null || frames.Length == 0)
            {
                if (lockWalk && _controller != null) _controller.AnimationLocked = false;
                _playing = null;
                yield break;
            }
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] != null) _sr.sprite = frames[i];
                yield return new WaitForSeconds(wait);
            }
            if (lockWalk && _controller != null) _controller.AnimationLocked = false;
            _playing = null;
        }

        private IEnumerator LoopRoutine(Sprite[][] set, float fps, int dir)
        {
            if (_controller != null) _controller.AnimationLocked = true;
            float wait = 1f / Mathf.Max(1f, fps);
            var frames = set != null && dir >= 0 && dir < set.Length ? set[dir] : null;
            if (frames == null || frames.Length == 0)
            {
                if (_controller != null) _controller.AnimationLocked = false;
                _playing = null;
                yield break;
            }
            int f = 0;
            while (_swimming)
            {
                if (frames[f] != null) _sr.sprite = frames[f];
                f = (f + 1) % frames.Length;
                yield return new WaitForSeconds(wait);
            }
            if (_controller != null) _controller.AnimationLocked = false;
            _playing = null;
        }
    }
}
