using System.Collections;
using TMPro;
using System.Collections.Generic;
using UnityEngine;
using PrehistoricSurvival.Core;
using PrehistoricSurvival.World;
using PrehistoricSurvival.AI;
using PrehistoricSurvival.Lighting;

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

            var npcPrefab = LoadPrefabSafe("Prefabs/NPC/Villager");
            var elderPrefab = LoadPrefabSafe("Prefabs/NPC/Elder");
            var tentPrefab = LoadPrefabSafe("Prefabs/Structures/Tent");
            var firePrefab = LoadPrefabSafe("Prefabs/Structures/Campfire");

            // Placement runs in up to three phases. Phase 0 uses the strict biome
            // rules in the configured distance band; if the spawn region is all
            // mountains/coast the strict pass can fail every sample, so later
            // phases widen the ring and accept any dry land. Without this the
            // system logged "0 camps placed" on mountainous seeds.
            int placed = 0, phase = 0;
            while (placed < campCount && phase < 3)
            {
                float rMin = minCampDistance;
                float rMax = maxCampDistance * (phase == 0 ? 1f : phase == 1 ? 3f : 6f);
                bool strict = phase == 0;

                int attempts = 0;
                while (placed < campCount && attempts < 250)
                {
                    attempts++;
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    float dist = Random.Range(rMin, rMax);
                    Vector3 candidate = origin + new Vector3(Mathf.Cos(ang) * dist, Mathf.Sin(ang) * dist, 0f);

                    var sample = map.SampleWorld(candidate);
                    if (sample.isWater || sample.isRiver) continue;
                    if (strict)
                    {
                        if (sample.biome == BiomeType.Ocean || sample.biome == BiomeType.ShallowWater) continue;
                        if (sample.biome == BiomeType.Mountain || sample.biome == BiomeType.SnowPeak) continue;
                    }
                    if (NearestCampDistance(candidate) < 60f) continue;

                    PlaceOneCamp(candidate, firePrefab, tentPrefab, npcPrefab, elderPrefab);
                    placed++;
                    yield return null;
                }

                if (placed < campCount)
                {
                    var atSpawn = map.SampleWorld(origin);
                    Debug.LogWarning(
                        $"[TribeCampSystem] {placed}/{campCount} camps placed in phase {phase} — spawn area is mostly " +
                        $"{atSpawn.biome}. {(phase < 2 ? "Widening the search..." : "Keeping what we have.")}");
                    phase++;
                }
            }

            Debug.Log($"[TribeCampSystem] {placed} camps placed.");
        }

        /// <summary>
        /// Loads a prefab from Resources but rejects assets that carry missing-script
        /// references (left over from renamed/deleted scripts or editor-only components).
        /// Missing scripts instantiate as broken Behaviours, so a clean runtime
        /// placeholder is used instead.
        /// </summary>
        private static GameObject LoadPrefabSafe(string resourcePath)
        {
            GameObject prefab;
            try { prefab = Resources.Load<GameObject>(resourcePath); }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TribeCampSystem] Could not load '{resourcePath}': {e.Message}");
                return null;
            }
            if (prefab == null) return null;

            foreach (var c in prefab.GetComponentsInChildren<Component>(true))
            {
                if (c == null)
                {
                    Debug.LogWarning(
                        $"[TribeCampSystem] '{resourcePath}' has a missing script reference — using a runtime " +
                        $"placeholder instead. Re-run 'PrehistoricSurvival → Create Prefabs Only' to regenerate clean prefabs.");
                    return null;
                }
            }
            return prefab;
        }

        private void PlaceOneCamp(Vector3 candidate, GameObject firePrefab, GameObject tentPrefab,
                                  GameObject npcPrefab, GameObject elderPrefab)
        {
            // Campfire: prefab or runtime fallback (light + crackle + crafting station).
            if (firePrefab != null) Instantiate(firePrefab, candidate + Vector3.up * 0.5f, Quaternion.identity);
            else MakePlaceholderCampfire(candidate + Vector3.up * 0.5f);

            if (tentPrefab != null) Instantiate(tentPrefab, candidate + new Vector3(1.8f, 1.2f, 0f), Quaternion.identity);
            else MakePlaceholderTent(candidate + new Vector3(1.8f, 1.2f, 0f));

            for (int v = 0; v < villagersPerCamp; v++)
            {
                var go = npcPrefab != null
                    ? Instantiate(npcPrefab, candidate + (Vector3)(Random.insideUnitCircle * 1.6f), Quaternion.identity)
                    : MakePlaceholderNpc(candidate + (Vector3)(Random.insideUnitCircle * 1.6f), false);
                go.AddComponent<CampNPC>();
                _npcs.Add(go.transform);
            }

            // The elder runs the trade post — always spawn one (placeholder if no prefab).
            var elder = elderPrefab != null
                ? Instantiate(elderPrefab, candidate + Vector3.down * 1.2f, Quaternion.identity)
                : MakePlaceholderNpc(candidate + Vector3.down * 1.2f, true);
            elder.AddComponent<CampNPC>();
            elder.AddComponent<CampTrader>();
            _npcs.Add(elder.transform);

            _camps.Add(candidate);
        }

        // --- Runtime placeholders (used when prefabs are missing or broken) ---

        private GameObject MakePlaceholderNpc(Vector3 pos, bool elder)
        {
            var go = new GameObject(elder ? "Elder" : "Villager");
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderHumanSprite(elder);
            sr.sortingOrder = 2;
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.8f, 1.4f);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            return go;
        }

        private void MakePlaceholderCampfire(Vector3 pos)
        {
            var go = new GameObject("Campfire");
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderCampfireSprite();
            sr.sortingOrder = 1;
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(2f, 2f);

            var light = go.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            light.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
            light.color = new Color(1f, 0.75f, 0.4f);
            light.intensity = 1.2f;
            light.pointLightInnerRadius = 2f;
            light.pointLightOuterRadius = 8f;

            var torch = go.AddComponent<TorchLight>();
            torch.type = TorchLight.LightType.Campfire;
            torch.baseIntensity = 1.2f;
            torch.baseRadius = 8f;
            torch.usesFuel = false; // camp fires should not burn out

            var clip = Resources.Load<AudioClip>("Audio/sfx/campfire_loop");
            if (clip != null)
            {
                var src = go.AddComponent<AudioSource>();
                src.clip = clip;
                src.loop = true;
                src.playOnAwake = true;
                src.volume = 0.5f;
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 4f;
                src.maxDistance = 16f;
            }

            go.AddComponent<PrehistoricSurvival.Crafting.CraftingStationTrigger>();
        }

        private void MakePlaceholderTent(Vector3 pos)
        {
            var go = new GameObject("Tent");
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderTentSprite();
            sr.sortingOrder = 0;
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(2f, 2f);
        }

        // --- Procedural pixel sprites so placeholders are always visible
        //     (Assets/Sprites is not a Resources folder, so Resources.Load<Sprite>
        //     on those paths always returned null and NPCs were invisible). ---

        private static Sprite _humanSprite, _elderSprite, _campfireSprite, _tentSprite;

        private static Sprite PlaceholderHumanSprite(bool elder)
        {
            var cache = elder ? _elderSprite : _humanSprite;
            if (cache != null) return cache;

            const int w = 8, h = 16;
            var skin = new Color(0.85f, 0.65f, 0.48f);
            var body = elder ? new Color(0.60f, 0.48f, 0.36f) : new Color(0.45f, 0.33f, 0.24f);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[w * h];
            System.Action<int, int, Color> set = (x, y, c) => px[y * w + x] = c;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    set(x, y, Color.clear);
            for (int y = 12; y < 16; y++) for (int x = 2; x < 6; x++) set(x, y, skin); // head
            for (int y = 5; y < 12; y++) for (int x = 1; x < 7; x++) set(x, y, body);  // torso
            for (int y = 0; y < 5; y++) { set(2, y, body); set(3, y, body); set(4, y, body); set(5, y, body); } // legs
            if (elder) for (int y = 1; y < 13; y++) set(7, y, new Color(0.35f, 0.25f, 0.15f)); // staff
            tex.SetPixels32(px);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 12f); // ~0.66 x 1.33 units
            if (elder) _elderSprite = sprite; else _humanSprite = sprite;
            return sprite;
        }

        private static Sprite PlaceholderCampfireSprite()
        {
            if (_campfireSprite != null) return _campfireSprite;

            const int w = 12, h = 12;
            var log = new Color(0.36f, 0.26f, 0.16f);
            var flame = new Color(1f, 0.62f, 0.15f);
            var flameCore = new Color(1f, 0.9f, 0.45f);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[w * h];
            System.Action<int, int, Color> set = (x, y, c) => px[y * w + x] = c;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    set(x, y, Color.clear);
            for (int x = 1; x < 11; x++) { set(x, 2, log); set(x, 3, log); }           // logs
            for (int y = 4; y < 11; y++) { set(5, y, flame); set(6, y, flame); }       // flame
            for (int y = 6; y < 10; y++) set(4, y, flame);
            for (int y = 6; y < 9; y++) set(7, y, flame);
            for (int y = 7; y < 10; y++) { set(5, y, flameCore); set(6, y, flameCore); } // core
            tex.SetPixels32(px);
            tex.Apply();

            _campfireSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 8f); // 1.5 x 1.5 units
            return _campfireSprite;
        }

        private static Sprite PlaceholderTentSprite()
        {
            if (_tentSprite != null) return _tentSprite;

            const int w = 16, h = 12;
            var hide = new Color(0.62f, 0.45f, 0.28f);
            var dark = new Color(0.42f, 0.30f, 0.18f);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[w * h];
            System.Action<int, int, Color> set = (x, y, c) => px[y * w + x] = c;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    set(x, y, Color.clear);
            for (int y = 0; y < h; y++)                       // triangle
            {
                int half = (y * (w / 2 - 1)) / (h - 1);
                for (int x = w / 2 - half; x <= w / 2 + half; x++)
                    set(x, y, y < 2 ? dark : hide);
            }
            for (int y = 3; y < 6; y++) set(w / 2, y, dark);   // entrance
            tex.SetPixels32(px);
            tex.Apply();

            _tentSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 8f); // 2 x 1.5 units
            return _tentSprite;
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
            _target = _home + (Vector3)(Random.insideUnitCircle * wanderRadius);
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
