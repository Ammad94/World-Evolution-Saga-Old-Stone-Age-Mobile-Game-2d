using UnityEngine;
using PrehistoricSurvival.Core;

namespace PrehistoricSurvival.Environment
{
    /// <summary>
    /// Attached to trees, bushes, and other vegetation. Handles interaction
    /// (harvesting wood, fruit, berries) and regrowth timers.
    /// </summary>
    public class VegetationInteraction : MonoBehaviour
    {
        public enum VegetationType
        {
            TimberTree,
            FruitTree,
            BerryBush,
            Vine
        }

        [Header("Vegetation Info")]
        public VegetationType type;
        public string speciesName = "Oak Tree";

        [Header("Yields")]
        public ItemData woodDrop;
        public int woodYield = 3;
        public ItemData fruitDrop;
        public int fruitYield = 5;

        [Header("Regrowth")]
        [Tooltip("Days until fruit/berries regrow after harvest.")]
        public int regrowDays = 7;

        [Header("Interaction")]
        [Tooltip("Time to chop/harvest (seconds).")]
        public float harvestTime = 3f;

        [Header("Visual")]
        [Tooltip("Sprite to show when depleted.")]
        public Sprite depletedSprite;

        private SpriteRenderer _sr;
        private Sprite _originalSprite;
        private bool _isDepleted;
        private float _regrowTimer;

        // Tooltip data
        public string TooltipText
        {
            get
            {
                string txt = $"<b>{speciesName}</b>\n";
                if (type == VegetationType.TimberTree)
                    txt += $"Wood Yield: {woodYield}";
                else if (type == VegetationType.FruitTree)
                    txt += $"Fruit: {(_isDepleted ? 0 : fruitYield)}\nRegrow: {regrowDays} days";
                else if (type == VegetationType.BerryBush || type == VegetationType.Vine)
                    txt += $"Berries: {(_isDepleted ? 0 : fruitYield)}\nRegrow: {regrowDays} days";
                return txt;
            }
        }

        public bool IsDepleted => _isDepleted;

        private void Awake()
        {
            _sr = GetComponentInChildren<SpriteRenderer>();
            if (_sr != null) _originalSprite = _sr.sprite;
        }

        private void Update()
        {
            // Regrowth timer
            if (_isDepleted)
            {
                _regrowTimer -= Time.deltaTime;
                if (_regrowTimer <= 0f)
                    Regrow();
            }
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>Harvest this vegetation. Returns true if successful.</summary>
        public bool Harvest()
        {
            if (_isDepleted) return false;

            var inv = InventorySystem.Instance;
            if (inv == null) return false;

            switch (type)
            {
                case VegetationType.TimberTree:
                    inv.AddItem(woodDrop, woodYield);
                    Deplete();
                    break;

                case VegetationType.FruitTree:
                case VegetationType.BerryBush:
                case VegetationType.Vine:
                    inv.AddItem(fruitDrop, fruitYield);
                    Deplete();
                    break;
            }

            return true;
        }

        private void Deplete()
        {
            _isDepleted = true;
            if (_sr != null && depletedSprite != null)
                _sr.sprite = depletedSprite;

            // Convert regrow days to real seconds (assuming 1 day = 5 minutes for demo)
            _regrowTimer = regrowDays * 300f;
        }

        private void Regrow()
        {
            _isDepleted = false;
            if (_sr != null)
                _sr.sprite = _originalSprite;
        }

        // ------------------------------------------------------------------
        // Trigger-based proximity detection
        // ------------------------------------------------------------------
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            // Show tooltip via UI system
            var tooltipUI = FindFirstObjectByType<UI.TooltipUI>();
            if (tooltipUI != null)
                tooltipUI.Show(TooltipText, transform.position);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            var tooltipUI = FindFirstObjectByType<UI.TooltipUI>();
            if (tooltipUI != null)
                tooltipUI.Hide();
        }
    }
}
