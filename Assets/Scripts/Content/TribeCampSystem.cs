using System.Collections;
using TMPro;
using System.Collections.Generic;
using UnityEngine;
using PrehistoricSurvival.Core;
using PrehistoricSurvival.World;
using PrehistoricSurvival.AI;

namespace PrehistoricSurvival.Content
{
    /// <summary>
    /// Places 2-3 friendly tribe camps near the spawn point on walkable land,
    /// populates them with villagers and an elder, and tracks friendship earned
    /// through trading. Trading happens at the camp fire via the trade UI.
    /// </summary>
    public class TribeCampSystem : MonoBehaviour
    {
        public static TribeCampSystem Instance { get; private set; }

        public int campCount = 3;
        public float minCampDistance = 40f;
        public float maxCampDistance = 130f;
        public int villagersPerCamp = 2;

        private readonly List<Vector3> _camps = new List<Vector3>();
        private readonly List<Transform> _npcs = new List<Transform>();
        public float Friendship { get; private set; }

        public event System.Action OnFriendshipChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start()
        {
            StartCoroutine(PlaceCamps());
        }

        /// <summary>Distance to the nearest camp (MaxValue when none).</summary>
        public float NearestCampDistance(Vector3 pos)
        {
            float best = float.MaxValue;
            foreach (var c in _camps)
                best = Mathf.Min(best, Vector2.Distance(new Vector2(c.x, c.y), new Vector2(pos.x, pos.y)));
            return best;
        }

        public bool NearCamp(Vector3 pos, float range = 6f) => NearestCampDistance(pos) <= range;

        public void AddFriendship(float amount)
        {
            Friendship += amount;
            OnFriendshipChanged?.Invoke();
        }

        /// <summary>Restore friendship from a save without firing events.</summary>
        public void RestoreFriendship(float value) { Friendship = value; }

        // ------------------------------------------------------------------
        private IEnumerator PlaceCamps()
        {
            yield return null; // let the world initialise
            var map = WorldMap.Instance;
            if (map == null) yield break;

            var player = GameObject.FindGameObjectWithTag("Player");
            Vector3 origin = player != null ? player.transform.position : Vector3.zero;
            var npcPrefab = Resources.Load<GameObject>("Prefabs/NPC/Villager");
            var elderPrefab = Resources.Load<GameObject>("Prefabs/NPC/Elder");
            var tentPrefab = Resources.Load<GameObject>("Prefabs/Structures/Tent");
            var firePrefab = Resources.Load<GameObject>("Prefabs/Structures/Campfire");

            int placed = 0, attempts = 0;
            while (placed < campCount && attempts < 400)
            {
                attempts++;
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float dist = Random.Range(minCampDistance, maxCampDistance);
                Vector3 candidate = origin + new Vector3(Mathf.Cos(ang) * dist, Mathf.Sin(ang) * dist, 0f);

                var sample = map.SampleWorld(candidate);
                if (sample.isWater || sample.isRiver) continue;
                if (sample.biome == BiomeType.Ocean || sample.biome == BiomeType.ShallowWater) continue;
                if (sample.biome == BiomeType.Mountain || sample.biome == BiomeType.SnowPeak) continue;
                if (NearestCampDistance(candidate) < 60f) continue;

                // Camp: tent + campfire + NPCs
                if (firePrefab != null) Instantiate(firePrefab, candidate + Vector3.up * 0.5f, Quaternion.identity);
                if (tentPrefab != null) Instantiate(tentPrefab, candidate + new Vector3(1.8f, 1.2f, 0f), Quaternion.identity);
                for (int v = 0; v < villagersPerCamp; v++)
                {
                    var go = npcPrefab != null ? Instantiate(npcPrefab, candidate + Random.insideUnitCircle * 1.6f, Quaternion.identity)
                                               : MakePlaceholderNpc(candidate);
                    go.AddComponent<CampNPC>();
                    _npcs.Add(go.transform);
                }
                if (elderPrefab != null)
                {
                    var elder = Instantiate(elderPrefab, candidate + Vector3.down * 1.2f, Quaternion.identity);
                    elder.AddComponent<CampNPC>();
                    var trader = elder.AddComponent<CampTrader>();
                    _npcs.Add(elder.transform);
                }
                _camps.Add(candidate);
                placed++;
                yield return null;
            }
            Debug.Log($"[TribeCampSystem] {placed} camps placed.");
        }

        private GameObject MakePlaceholderNpc(Vector3 pos)
        {
            var go = new GameObject("Villager");
            go.tag = "Untagged";
            var sr = go.AddComponent<SpriteRenderer>();
            var sprite = Resources.Load<Sprite>("Sprites/NPC/Villager/south/villager_south_0");
            sr.sprite = sprite;
            go.transform.position = pos;
            go.AddComponent<BoxCollider2D>().isTrigger = true;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            return go;
        }
    }

    /// <summary>Wanders near its camp; shows the trade hint when the player is close.</summary>
    public class CampNPC : MonoBehaviour
    {
        public float wanderRadius = 3.5f;
        public float wanderEvery = 4f;

        private Vector3 _home;
        private Vector3 _target;
        private float _timer;
        private SpriteRenderer _sr;
        private Transform _player;
        private GameObject _hint;
        private static GameObject _tradeUi;

        private void Start()
        {
            _home = transform.position;
            _sr = GetComponent<SpriteRenderer>();
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            PickTarget();
        }

        private void PickTarget()
        {
            _target = _home + Random.insideUnitCircle * wanderRadius;
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

            _timer -= Time.deltaTime;
            if (_timer <= 0f) { _timer = wanderEvery * Random.Range(0.7f, 1.4f); PickTarget(); }

            Vector3 delta = _target - transform.position;
            if (delta.sqrMagnitude > 0.04f)
            {
                transform.position += delta.normalized * (1.1f * Time.deltaTime);
                if (_sr != null)
                {
                    int dir = PrehistoricSurvival.Art.PlayerActionAnimator.DirFromAngle(
                        Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                    _sr.flipX = dir == 6 || dir == 5 || dir == 7; // W-ish
                }
            }

            // Player proximity: hint bubble + quest visit event.
            if (_player != null)
            {
                float d = Vector2.Distance(transform.position, _player.position);
                if (d < 3.5f && _hint == null)
                {
                    _hint = new GameObject("hint");
                    _hint.transform.SetParent(transform, false);
                    _hint.transform.localPosition = Vector3.up * 1.4f;
                    var tmp = _hint.AddComponent<TextMeshPro>();
                    tmp.text = "TRADE";
                    tmp.fontSize = 2.2f;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.color = new Color(0.98f, 0.9f, 0.65f);
                    var rt = tmp.rectTransform;
                    rt.sizeDelta = new Vector2(3f, 1f);
                    EventManager.TriggerEvent("CampVisited");
                }
                else if (d >= 5f && _hint != null)
                {
                    Destroy(_hint);
                }
            }
        }
    }

    /// <summary>The elder: walking up to him opens the trade panel.</summary>
    public class CampTrader : MonoBehaviour
    {
        private Transform _player;
        private float _cooldown;

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        private void Update()
        {
            _cooldown -= Time.deltaTime;
            if (_player == null || _cooldown > 0f) return;
            if (Vector2.Distance(transform.position, _player.position) < 3.2f)
            {
                _cooldown = 4f;
                TradeUI.Open();
            }
        }
    }
}
