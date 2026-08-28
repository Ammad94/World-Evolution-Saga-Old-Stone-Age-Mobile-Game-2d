using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PrehistoricSurvival.Core;

namespace PrehistoricSurvival.UI
{
    /// <summary>First-run tutorial overlay and accessible settings controls.</summary>
    public class OnboardingAndAccessibilityUI : MonoBehaviour
    {
        private GameObject _tutorial;
        private int _page;
        private readonly string[] _tips = { "Move with the left joystick. Explore the world and watch your hunger and thirst.", "Use the action button near trees, rocks and wildlife. Open BAG to inspect your supplies.", "CRAFT tools and food, then use BUILD to place a shelter or campfire.", "Weather changes your temperature. Find shelter, wear warm clothing, and avoid predators." };
        public void Build(Canvas canvas)
        {
            if (PlayerPrefs.GetInt("tutorial_seen", 0) == 1 || canvas == null) return;
            var bg = UIFactory.Panel(canvas.transform, "TutorialOverlay", new Color(.04f,.03f,.02f,.96f)); _tutorial = bg.gameObject; UIFactory.Stretch(bg);
            UIFactory.Text(bg.transform, "Title", "SURVIVAL GUIDE", 56, new Vector2(.5f,.75f), new Vector2(1000,80), UIFactory.Parchment);
            var body = UIFactory.Text(bg.transform, "Tip", _tips[0], 30, new Vector2(.5f,.52f), new Vector2(1100,180), Color.white); body.textWrappingMode = TextWrappingModes.Normal;
            var next = UIFactory.Button(bg.transform, "Next", "NEXT", new Vector2(.5f,.28f), new Vector2(300,75), () => Next(body), UIFactory.Ember, 28);
            UIFactory.Button(bg.transform, "Skip", "SKIP", new Vector2(.5f,.16f), new Vector2(220,60), Finish, UIFactory.Bark, 22);
        }
        private void Next(TextMeshProUGUI body) { _page++; if (_page >= _tips.Length) { Finish(); return; } body.text = _tips[_page]; }
        private void Finish() { PlayerPrefs.SetInt("tutorial_seen", 1); PlayerPrefs.Save(); if (_tutorial != null) _tutorial.SetActive(false); }
    }
}
