using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrehistoricSurvival.Core
{
    public enum DamageType { Blunt, Slash, Pierce, Fire, Cold }

    [Serializable] public class ArmorProfile { public float blunt, slash, pierce, fire, cold; public float Reduce(DamageType type) { switch (type) { case DamageType.Blunt: return blunt; case DamageType.Slash: return slash; case DamageType.Pierce: return pierce; case DamageType.Fire: return fire; default: return cold; } } }

    /// <summary>Damage resolution and equipment durability foundation for weapons and clothing.</summary>
    public class CombatEquipment : MonoBehaviour
    {
        public static CombatEquipment Instance { get; private set; }
        public ArmorProfile armor = new ArmorProfile();
        [Range(0f, 100f)] public float weaponDurability = 100f;
        public float weaponDamageMultiplier = 1f;
        private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }
        public float ResolveDamage(float amount, DamageType type) { return Mathf.Max(0f, amount * (1f - Mathf.Clamp01(armor.Reduce(type) / 100f))); }
        public bool UseWeapon(float amount = 1f) { weaponDurability = Mathf.Max(0f, weaponDurability - amount); return weaponDurability > 0f; }
        public void RepairWeapon(float amount) { weaponDurability = Mathf.Min(100f, weaponDurability + amount); }
    }

    [Serializable] public class QuestObjective { public string id, description; public int required, progress; public bool Complete => progress >= required; }
    [Serializable] public class QuestDefinition { public string id, title, description; public int reward; public QuestObjective[] objectives; public bool Complete { get { if (objectives == null || objectives.Length == 0) return false; foreach (var o in objectives) if (!o.Complete) return false; return true; } } }

    /// <summary>Data-driven objective progress that survives scene changes.</summary>
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }
        public List<QuestDefinition> quests = new List<QuestDefinition>();
        public event Action<QuestDefinition> OnQuestCompleted;
        private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; DontDestroyOnLoad(gameObject); }
        public void Track(string objectiveId, int amount = 1)
        {
            foreach (var q in quests)
            {
                if (q == null || q.objectives == null) continue;
                foreach (var o in q.objectives)
                {
                    if (o == null || o.id != objectiveId || o.Complete) continue;
                    o.progress = Mathf.Min(o.required, o.progress + amount);
                    if (q.Complete) OnQuestCompleted?.Invoke(q);
                }
            }
        }
        public QuestDefinition Get(string id) { return quests.Find(q => q.id == id); }
    }

    [Serializable] public class TradeOffer { public ItemData give, receive; public int giveAmount = 1, receiveAmount = 1; }
    /// <summary>Simple safe trading API for future tribe vendors and stations.</summary>
    public class TradingSystem : MonoBehaviour
    {
        public static TradingSystem Instance { get; private set; }
        public List<TradeOffer> offers = new List<TradeOffer>();
        private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }
        public bool Trade(TradeOffer offer) { var inv = InventorySystem.Instance; if (offer == null || inv == null || !inv.HasItem(offer.give.itemId, offer.giveAmount) || !inv.CanAdd(offer.receive, offer.receiveAmount)) return false; inv.RemoveItem(offer.give, offer.giveAmount); inv.AddItem(offer.receive, offer.receiveAmount); return true; }
    }

    [Serializable] public class TrackMark { public Vector3 position; public string species; public float age; }
    /// <summary>Stores recent animal signs for a tracking UI and compass integration.</summary>
    public class AnimalTrackingSystem : MonoBehaviour
    {
        public static AnimalTrackingSystem Instance { get; private set; }
        public float markLifetime = 90f; public List<TrackMark> marks = new List<TrackMark>();
        private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }
        private void Update() { for (int i = marks.Count - 1; i >= 0; i--) { marks[i].age += Time.deltaTime; if (marks[i].age > markLifetime) marks.RemoveAt(i); } }
        public void AddMark(Vector3 position, string species) { marks.Add(new TrackMark { position = position, species = species }); if (marks.Count > 100) marks.RemoveAt(0); }
        public TrackMark Nearest(string species, Vector3 origin) { TrackMark best = null; float distance = float.MaxValue; foreach (var m in marks) if ((string.IsNullOrEmpty(species) || m.species == species) && Vector3.Distance(origin, m.position) < distance) { best = m; distance = Vector3.Distance(origin, m.position); } return best; }
    }
}
