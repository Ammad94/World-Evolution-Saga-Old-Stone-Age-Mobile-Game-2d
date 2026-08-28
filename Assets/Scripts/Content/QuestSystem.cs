using System;
using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PrehistoricSurvival.Core;
using PrehistoricSurvival.UI;

namespace PrehistoricSurvival.Content
{
    /// <summary>A single quest: description + up to 3 objectives + reward.</summary>
    [Serializable]
    public class Quest
    {
        public string id;
        public string title;
        public string description;
        public string[] objectiveIds;
        public string[] objectiveTexts;
        public int[] objectiveTargets;
        public int[] objectiveProgress;
        public bool done;
        public bool claimed;
        public int knowledgeReward;
        public string[] itemRewards;   // item ids
        public int[] itemAmounts;

        public bool ObjectivesComplete
        {
            get
            {
                for (int i = 0; i < objectiveTargets.Length; i++)
                    if (objectiveProgress[i] < objectiveTargets[i]) return false;
                return true;
            }
        }
    }

    /// <summary>
    /// The saga quest chain: a guided 10-quest arc from first fire to copper age,
    /// plus an on-screen tracker. Objectives advance from existing GameEvents, so
    /// any gameplay system can contribute without coupling.
    /// </summary>
    public class QuestSystem : MonoBehaviour
    {
        public static QuestSystem Instance { get; private set; }

        private readonly List<Quest> _quests = new List<Quest>();
        public IReadOnlyList<Quest> Quests => _quests;

        private QuestTrackerHUD _hud;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildCatalog();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null; // handlers live for the app lifetime
        }

        private void Start()
        {
            Subscribe(GameEvents.ItemCollected, "item");
            Subscribe(GameEvents.ItemCrafted, "craft");
            Subscribe(GameEvents.AnimalKilled, "kill");
            Subscribe(GameEvents.TileDestroyed, "work");
            Subscribe(GameEvents.BuildingPlaced, "build");
            Subscribe("EraAdvanced", "era");
            Subscribe("CampVisited", "visit_camp");
            Subscribe("TradeCompleted", "trade");
            _hud = gameObject.AddComponent<QuestTrackerHUD>();
            if (_quests.Count > 0 && !_quests[0].done) Activate(_quests[0]);
        }

        private void Subscribe(string eventName, string key)
        {
            EventManager.Subscribe(eventName, payload => Progress(key, payload));
        }

        // ------------------------------------------------------------------
        private void BuildCatalog()
        {
            Add("first_steps", "First Steps", "Gather the basics of survival.",
                new[] { "collect_wood", "collect_stone", "collect_fiber" },
                new[] { "Gather 3 wood logs", "Gather 3 stones", "Gather 2 plant fiber" },
                new[] { 3, 3, 2 }, 4, new[] { "berries" }, new[] { 3 });
            Add("hand_axe", "The Hand Axe", "Knock flakes from flint until an edge appears.",
                new[] { "craft_axe" }, new[] { "Craft a stone axe" }, new[] { 1 }, 5,
                new[] { "stone_axe" }, new[] { 1 });
            Add("fire_brand", "A Capture of Fire", "Fire cooks, warms and holds the dark at bay.",
                new[] { "craft_campfire", "cook_meat" },
                new[] { "Build a campfire", "Cook meat on it" }, new[] { 1, 1 }, 6,
                new[] { "cooked_meat" }, new[] { 2 });
            Add("first_hunt", "First Hunt", "Bring down prey and claim its meat.",
                new[] { "kill_animal", "collect_meat" },
                new[] { "Kill any animal", "Collect raw meat" }, new[] { 1, 2 }, 8,
                new[] { "healing_salve" }, new[] { 1 });
            Add("shelter", "Walls of Hide and Pole", "A tent turns wilderness into home.",
                new[] { "place_tent" }, new[] { "Place a tent" }, new[] { 1 }, 8,
                new[] { "fiber" }, new[] { 6 });
            Add("predator_beware", "Predator Beware", "The cave lion hunts the hunter. Answer it.",
                new[] { "kill_predator" }, new[] { "Kill a predator (sabertooth, lion, wolf, bear or hyena)" },
                new[] { 1 }, 12, new[] { "fur_cloak" }, new[] { 1 });
            Add("mammoth_bones", "Thunder of the North", "A mammoth feeds the tribe for a season.",
                new[] { "kill_mammoth" }, new[] { "Kill a woolly mammoth" }, new[] { 1 }, 16,
                new[] { "bone" }, new[] { 5 });
            Add("new_stone", "The New Stone", "Master bone and obsidian work.",
                new[] { "craft_bone_spear", "craft_obsidian_knife" },
                new[] { "Craft a bone spear", "Craft an obsidian knife" }, new[] { 1, 1 }, 14,
                new[] { "obsidian" }, new[] { 3 });
            Add("the_gathering", "The Gathering", "Trade with the wandering tribe.",
                new[] { "visit_camp", "trade_once" },
                new[] { "Visit a tribe camp", "Complete a trade" }, new[] { 1, 1 }, 14,
                new[] { "copper_ore" }, new[] { 2 });
            Add("copper_dawn", "Dawn of Copper", "From stone to metal — a new age begins.",
                new[] { "era_copper" }, new[] { "Advance the tribe to the Copper Age" }, new[] { 1 }, 24,
                new[] { "copper_amulet" }, new[] { 1 });
        }

        private void Add(string id, string title, string desc, string[] objIds, string[] objTexts,
            int[] targets, int knowledgeReward, string[] itemRewards, int[] itemAmounts)
        {
            _quests.Add(new Quest
            {
                id = id,
                title = title,
                description = desc,
                objectiveIds = objIds,
                objectiveTexts = objTexts,
                objectiveTargets = targets,
                objectiveProgress = new int[targets.Length],
                knowledgeReward = knowledgeReward,
                itemRewards = itemRewards,
                itemAmounts = itemAmounts,
            });
        }

        // ------------------------------------------------------------------
        public Quest Active { get; private set; }

        private void Activate(Quest q)
        {
            if (q == null || q.done) return;
            Active = q;
            ShowToast($"NEW QUEST — {q.title}", q.description, 4.5f);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUiSound("quest_offer", 0.8f);
            _hud?.Refresh();
        }

        /// <summary>Route an event key + payload into the active quest's objectives.</summary>
        private void Progress(string key, object payload)
        {
            if (Active == null || Active.done) return;

            bool anyProgress = false;
            for (int i = 0; i < Active.objectiveIds.Length; i++)
            {
                if (Active.objectiveProgress[i] >= Active.objectiveTargets[i]) continue;
                if (!Matches(Active.objectiveIds[i], key, payload)) continue;
                Active.objectiveProgress[i]++;
                anyProgress = true;
            }

            if (anyProgress)
            {
                _hud?.Refresh();
                if (Active.ObjectivesComplete) Complete(Active);
                else if (AudioManager.Instance != null) AudioManager.Instance.PlayUiSound("ui_waypoint_set", 0.4f);
            }
        }

        /// <summary>Objective-id grammar: collect_&lt;item&gt;, craft_&lt;item&gt;, kill_animal,
        /// kill_predator, kill_mammoth, place_&lt;building&gt;, era_&lt;n&gt;, visit_camp, trade.</summary>
        private static bool Matches(string objId, string key, object payload)
        {
            switch (key)
            {
                case "item":
                {
                    string itemId = (payload as ItemEventPayload)?.Item?.itemId ?? "";
                    if (objId == "collect_any") return true;
                    if (objId == "collect_meat") return itemId.Contains("meat");
                    return objId == "collect_" + itemId;
                }
                case "craft":
                {
                    string recipeId = (payload as Crafting.Recipe)?.recipeId ?? "";
                    return objId == "craft_" + recipeId || objId == "craft_any";
                }
                case "kill":
                {
                    var ai = payload as PrehistoricSurvival.AI.AnimalAI;
                    string name = (ai != null ? ai.animalName : "").ToLowerInvariant();
                    if (objId == "kill_animal") return true;
                    if (objId == "kill_mammoth") return name.Contains("mammoth") || name.Contains("rhino");
                    if (objId == "kill_predator")
                        return name.Contains("sabertooth") || name.Contains("lion") || name.Contains("wolf")
                            || name.Contains("bear") || name.Contains("hyena");
                    return false;
                }
                case "build":
                {
                    var go = payload as GameObject;
                    var marker = go != null ? go.GetComponent<Environment.BuildingMarker>() : null;
                    string buildingId = marker != null ? marker.buildingId : (go != null ? go.name.ToLowerInvariant() : "");
                    return objId == "place_" + buildingId || objId == "place_any";
                }
                case "era":
                    return objId == "era_" + Convert.ToInt32(payload);
                case "visit_camp":
                    return objId == "visit_camp";
                case "trade":
                    return objId == "trade_once" || objId == "trade";
                default:
                    return false;
            }
        }

        private void Complete(Quest q)
        {
            q.done = true;
            q.claimed = true;
            Debug.Log($"[QuestSystem] Complete: {q.title}");
            ShowToast($"QUEST COMPLETE — {q.title}", "The tribe grows stronger.", 4f);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUiSound("quest_complete", 0.9f);
            if (Feedback.GameFeel.Instance != null) Feedback.GameFeel.Shake(0.25f);

            if (EraProgression.Instance != null) EraProgression.Instance.Learn(q.knowledgeReward);

            var inv = InventorySystem.Instance;
            if (inv != null)
            {
                for (int i = 0; i < q.itemRewards.Length; i++)
                {
                    var so = Resources.Load<ItemDataSO>("Items/" + q.itemRewards[i]);
                    if (so != null) inv.AddItem(so.data, q.itemAmounts[i]);
                }
            }
            // Activate next quest in the chain.
            int idx = _quests.IndexOf(q);
            for (int i = idx + 1; i < _quests.Count; i++)
            {
                if (!_quests[i].done) { Activate(_quests[i]); break; }
            }
        }

        private void ShowToast(string title, string body, float seconds)
        {
            _hud?.ShowToast(title, body);
        }

        /// <summary>Restore saved progress (called by SaveSystem).</summary>
        public void Restore(List<SaveSystem.SavedQuest> saved)
        {
            if (saved == null) return;
            foreach (var s in saved)
            {
                var q = _quests.Find(x => x.id == s.id);
                if (q == null) continue;
                q.done = s.done;
                for (int i = 0; i < q.objectiveProgress.Length && i < s.progress.Length; i++)
                    q.objectiveProgress[i] = s.progress[i];
            }
            foreach (var q in _quests)
            {
                if (!q.done) { Activate(q); break; }
            }
        }

        public List<SaveSystem.SavedQuest> Snapshot()
        {
            var list = new List<SaveSystem.SavedQuest>();
            foreach (var q in _quests)
                list.Add(new SaveSystem.SavedQuest
                {
                    id = q.id,
                    done = q.done,
                    progress = (int[])q.objectiveProgress.Clone(),
                });
            return list;
        }
    }

    /// <summary>Top-left tracker: active objective + progress + toast area.</summary>
    public class QuestTrackerHUD : MonoBehaviour
    {
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _objective;
        private GameObject _toast;
        private TextMeshProUGUI _toastTitle;
        private TextMeshProUGUI _toastBody;
        private float _toastLeft;

        private void Awake()
        {
            var canvasGO = new GameObject("QuestCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var tracker = new GameObject("Tracker");
            tracker.transform.SetParent(canvasGO.transform, false);
            var trt = tracker.AddComponent<RectTransform>();
            UIFactory.Anchor(trt, new Vector2(0f, 1f), new Vector2(430, 110), new Vector2(24, -24));
            var bg = UIFactory.Panel(tracker.transform, "BG", new Color(0.1f, 0.08f, 0.06f, 0.72f));
            UIFactory.Stretch(bg);
            bg.sprite = UITheme.Tooltip;
            bg.type = Image.Type.Sliced;

            _title = UIFactory.Text(tracker.transform, "QT_Title", "", 24, Vector2.zero, new Vector2(400, 34),
                new Color(0.95f, 0.87f, 0.62f));
            UIFactory.Anchor(_title.rectTransform, new Vector2(0.5f, 0.78f), new Vector2(400, 34));
            _objective = UIFactory.Text(tracker.transform, "QT_Obj", "", 20, Vector2.zero, new Vector2(400, 56),
                new Color(0.88f, 0.84f, 0.74f));
            UIFactory.Anchor(_objective.rectTransform, new Vector2(0.5f, 0.36f), new Vector2(400, 56));

            _toast = new GameObject("Toast");
            _toast.transform.SetParent(canvasGO.transform, false);
            var rt = _toast.AddComponent<RectTransform>();
            UIFactory.Anchor(rt, new Vector2(0.5f, 0.93f), new Vector2(860, 150));
            var tbg = UIFactory.Panel(_toast.transform, "BG", new Color(0.12f, 0.09f, 0.06f, 0.85f));
            UIFactory.Stretch(tbg);
            tbg.sprite = UITheme.Banner;
            tbg.type = Image.Type.Sliced;
            _toastTitle = UIFactory.Text(_toast.transform, "T", "", 34, Vector2.zero, new Vector2(820, 46),
                new Color(0.97f, 0.9f, 0.66f));
            UIFactory.Anchor(_toastTitle.rectTransform, new Vector2(0.5f, 0.72f), new Vector2(820, 46));
            _toastBody = UIFactory.Text(_toast.transform, "B", "", 24, Vector2.zero, new Vector2(820, 60),
                new Color(0.88f, 0.83f, 0.7f));
            UIFactory.Anchor(_toastBody.rectTransform, new Vector2(0.5f, 0.34f), new Vector2(820, 60));
            _toast.SetActive(false);
        }

        public void Refresh()
        {
            var q = QuestSystem.Instance != null ? QuestSystem.Instance.Active : null;
            if (q == null) { _title.text = ""; _objective.text = ""; return; }
            _title.text = q.title;
            string txt = "";
            for (int i = 0; i < q.objectiveTexts.Length; i++)
            {
                txt += $"{(q.objectiveProgress[i] >= q.objectiveTargets[i] ? "✔ " : "• ")}" +
                       $"{q.objectiveTexts[i]} ({q.objectiveProgress[i]}/{q.objectiveTargets[i]})";
                if (i < q.objectiveTexts.Length - 1) txt += "\n";
            }
            _objective.text = txt;
        }

        public void ShowToast(string title, string body)
        {
            _toastTitle.text = title;
            _toastBody.text = body;
            _toastLeft = 4f;
            _toast.SetActive(true);
            Feedback.UITween.Show(_toast);
        }

        private void Update()
        {
            if (!_toast.activeSelf) return;
            _toastLeft -= Time.unscaledDeltaTime;
            if (_toastLeft <= 0f) Feedback.UITween.Hide(_toast, 0.3f);
        }
    }
}
