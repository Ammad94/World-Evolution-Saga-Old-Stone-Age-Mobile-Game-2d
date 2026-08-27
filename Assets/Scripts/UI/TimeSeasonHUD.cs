using UnityEngine;
using TMPro;

namespace PrehistoricSurvival.UI
{
    /// <summary>
    /// Displays current time of day, day number, and season on the HUD.
    /// </summary>
    public class TimeSeasonHUD : MonoBehaviour
    {
        [Header("References")]
        public TextMeshProUGUI timeText;
        public TextMeshProUGUI dayText;
        public TextMeshProUGUI seasonText;

        [Header("Icons")]
        public UnityEngine.UI.Image seasonIcon;
        public Sprite springIcon;
        public Sprite summerIcon;
        public Sprite autumnIcon;
        public Sprite winterIcon;

        private Survival.SeasonManager _seasonMgr;

        private void Start()
        {
            _seasonMgr = Survival.SeasonManager.Instance;
        }

        private void Update()
        {
            if (_seasonMgr == null) return;

            if (timeText != null)
                timeText.text = _seasonMgr.TimeString;

            if (dayText != null)
                dayText.text = $"Day {_seasonMgr.DayNumber}";

            if (seasonText != null)
                seasonText.text = _seasonMgr.CurrentSeason.ToString();

            if (seasonIcon != null)
            {
                switch (_seasonMgr.CurrentSeason)
                {
                    case Survival.Season.Spring: seasonIcon.sprite = springIcon; break;
                    case Survival.Season.Summer: seasonIcon.sprite = summerIcon; break;
                    case Survival.Season.Autumn: seasonIcon.sprite = autumnIcon; break;
                    case Survival.Season.Winter: seasonIcon.sprite = winterIcon; break;
                }
            }
        }
    }
}
