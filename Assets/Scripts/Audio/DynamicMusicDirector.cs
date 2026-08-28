using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PrehistoricSurvival.AI;
using PrehistoricSurvival.Survival;
using PrehistoricSurvival.Core;
using PrehistoricSurvival.Content;

namespace PrehistoricSurvival.Audio
{
    /// <summary>
    /// Adaptive music: blends between exploration / tension / combat / stalk layers
    /// (plus winter and tribe-camp variants) based on the live game state.
    /// Layers are full tracks under Resources/Audio/music that crossfade smoothly.
    /// </summary>
    [DefaultExecutionOrder(-350)]
    public class DynamicMusicDirector : MonoBehaviour
    {
        public static DynamicMusicDirector Instance { get; private set; }

        [Header("Layer tracks (Resources paths under Audio/)")]
        public string explore = "music/explore_serene";
        public string tension = "music/explore_tension";
        public string combat = "music/combat_battle";
        public string stalk = "music/hunt_stalk";
        public string winter = "music/season_winter";
        public string tribe = "music/tribe_dawn";

        [Header("Behaviour")]
        public float reevaluateEvery = 1.2f;
        public float combatDetectRange = 20f;
        public float threatDetectRange = 30f;
        public float campMusicRange = 22f;
        public float fadeTime = 2.5f;

        public string CurrentLayer { get; private set; } = "menu";

        private float _timer;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Called by menus / cutscenes to force a layer.</summary>
        public void ForceLayer(string layer)
        {
            CurrentLayer = layer;
            ApplyLayer(fadeTime);
        }

        private void Update()
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer < reevaluateEvery) return;
            _timer = 0f;
            Evaluate();
        }

        private void Evaluate()
        {
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            Vector3 pos = player.transform.position;

            bool combatNear = false, threatNear = false;
            IEnumerable<GameObject> animals = AnimalSpawner.Instance != null
                ? (IEnumerable<GameObject>)AnimalSpawner.Instance.GetAliveAnimals()
                : System.Array.ConvertAll(FindObjectsByType<AnimalAI>(FindObjectsSortMode.None), a => a.gameObject);
            foreach (var go in animals)
            {
                var ai = go != null ? go.GetComponent<AnimalAI>() : null;
                if (ai == null || ai.CurrentState == AnimalAI.AIState.Dead) continue;
                float d = Vector3.SqrMagnitude(ai.transform.position - pos);
                if (d < combatDetectRange * combatDetectRange &&
                    (ai.CurrentState == AnimalAI.AIState.Chase || ai.CurrentState == AnimalAI.AIState.Attack))
                    combatNear = true;
                else if (d < threatDetectRange * threatDetectRange && ai.aggression == AnimalAI.AggressionLevel.Aggressive)
                    threatNear = true;
                if (combatNear) break;
            }

            string target;
            if (combatNear) target = "combat";
            else if (threatNear) target = "tension";
            else
            {
                var season = SeasonManager.Instance;
                bool isWinter = season != null && season.CurrentSeason == Season.Winter;
                float campDist = TribeCampSystem.Instance != null
                    ? TribeCampSystem.Instance.NearestCampDistance(pos) : float.MaxValue;
                if (campDist < campMusicRange) target = "tribe";
                else target = isWinter ? "winter" : "explore";
            }

            if (target != CurrentLayer)
            {
                CurrentLayer = target;
                ApplyLayer(fadeTime);
            }
        }

        private void ApplyLayer(float fade)
        {
            string path = CurrentLayer switch
            {
                "combat" => combat,
                "tension" => tension,
                "stalk" => stalk,
                "winter" => winter,
                "tribe" => tribe,
                "menu" => "music/menu_theme",
                _ => explore,
            };
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayMusic(path, fade);
        }
    }
}
