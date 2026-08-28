using UnityEngine;
using UnityEngine.UI;

namespace PrehistoricSurvival.UI
{
    /// <summary>
    /// Themed UI skin: 9-sliced sprites generated into Resources/Sprites/UI/Skin.
    /// UIFactory applies these automatically when present; systems can also
    /// reference them directly (UITheme.Parchment, UITheme.Slot, ...).
    /// </summary>
    public static class UITheme
    {
        private static Sprite _panelDark, _parchment, _button, _buttonPressed, _slot,
            _barFrame, _barFill, _knob, _tooltip, _banner, _dialogue, _divider, _checkOn, _checkOff;

        public static Sprite PanelDark => Get(ref _panelDark, "panel_dark");
        public static Sprite Parchment => Get(ref _parchment, "panel_parchment");
        public static Sprite Button => Get(ref _button, "button");
        public static Sprite ButtonPressed => Get(ref _buttonPressed, "button_pressed");
        public static Sprite Slot => Get(ref _slot, "slot");
        public static Sprite BarFrame => Get(ref _barFrame, "bar_frame");
        public static Sprite BarFill => Get(ref _barFill, "bar_fill");
        public static Sprite Knob => Get(ref _knob, "knob");
        public static Sprite Tooltip => Get(ref _tooltip, "tooltip");
        public static Sprite Banner => Get(ref _banner, "banner");
        public static Sprite Dialogue => Get(ref _dialogue, "panel_dialogue");
        public static Sprite Divider => Get(ref _divider, "divider");
        public static Sprite CheckboxOn => Get(ref _checkOn, "checkbox_on");
        public static Sprite CheckboxOff => Get(ref _checkOff, "checkbox_off");

        public static bool Ready => PanelDark != null;

        private static Sprite Get(ref Sprite field, string name)
        {
            if (field == null)
                field = Resources.Load<Sprite>("Sprites/UI/Skin/" + name);
            return field;
        }

        /// <summary>Apply the dark panel look to an Image (no-op when skin missing).</summary>
        public static void ApplyPanel(Image img, SkinPanel panel)
        {
            if (img == null) return;
            Sprite s;
            switch (panel)
            {
                case SkinPanel.Parchment: s = Parchment; break;
                case SkinPanel.Tooltip: s = Tooltip; break;
                case SkinPanel.Dialogue: s = Dialogue; break;
                default: s = PanelDark; break;
            }
            if (s == null) return;
            img.sprite = s;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
        }

        public enum SkinPanel { Dark, Parchment, Tooltip, Dialogue }
    }
}
