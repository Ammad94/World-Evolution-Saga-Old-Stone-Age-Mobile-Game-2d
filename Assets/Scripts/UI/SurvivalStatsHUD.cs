using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PrehistoricSurvival.UI
{
    /// <summary>
    /// Updates survival stat bars (Health, Hunger, Thirst, Energy, Stamina) on the HUD.
    /// </summary>
    public class SurvivalStatsHUD : MonoBehaviour
    {
        [Header("Sliders")]
        public Slider healthBar;
        public Slider hungerBar;
        public Slider thirstBar;
        public Slider energyBar;
        public Slider staminaBar;

        [Header("Labels")]
        public TextMeshProUGUI healthText;
        public TextMeshProUGUI hungerText;
        public TextMeshProUGUI thirstText;
        public TextMeshProUGUI energyText;
        public TextMeshProUGUI staminaText;

        [Header("Color Coding")]
        public Image healthFill;
        public Image hungerFill;
        public Image thirstFill;
        public Image energyFill;
        public Image staminaFill;

        public Color normalColor = Color.green;
        public Color warningColor = Color.yellow;
        public Color criticalColor = Color.red;

        private Survival.SurvivalStats _stats;

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                _stats = player.GetComponent<Survival.SurvivalStats>();
        }

        private void Update()
        {
            if (_stats == null) return;

            UpdateBar(healthBar, healthText, healthFill, _stats.Health);
            UpdateBar(hungerBar, hungerText, hungerFill, _stats.Hunger);
            UpdateBar(thirstBar, thirstText, thirstFill, _stats.Thirst);
            UpdateBar(energyBar, energyText, energyFill, _stats.Energy);
            UpdateBar(staminaBar, staminaText, staminaFill, _stats.Stamina);
        }

        private void UpdateBar(Slider slider, TextMeshProUGUI text, Image fill, float value)
        {
            if (slider != null)
                slider.value = value / 100f;

            if (text != null)
                text.text = $"{value:F0}%";

            if (fill != null)
            {
                if (value > 50f)
                    fill.color = normalColor;
                else if (value > 20f)
                    fill.color = warningColor;
                else
                    fill.color = criticalColor;
            }
        }
    }
}
